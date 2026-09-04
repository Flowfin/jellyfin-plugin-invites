# Threat model

This was written before the invitation record was designed, because the fields of
that record are what this model demands rather than the other way round. It was
also written before there was any code to defend, and that is no longer the whole
picture: some of the mitigations below are now lines somebody can read and the
rest are still promises held by an issue. The issue number in each row is where
the promise is kept or was kept, and a row whose issue is still open is a row
this plugin does not yet satisfy. Which rows those are is derived rather than
listed here, because a list in this paragraph is what went stale the last time:

    sed -n '/^| Attack |/,/^$/p' docs/threat-model.md | grep -o '#[0-9]\+' \
      | tr -d '#' | sort -un \
      | while read -r n; do
          gh issue view "$n" --repo Flowfin/jellyfin-plugin-invites --json state --jq .state
        done | sort | uniq -c

At `b734e5b` that answered 10 closed and 25 open. A closed issue means the
mitigation landed under it, not that this file measured it, and
`## Where this model is wrong` is where that difference is stated.

The one thing this plugin does is turn a link an operator sent to somebody into
a Jellyfin account. Everything below is about what else that link can be made to
do.

## The assets

The invitation code. It is a bearer credential for account creation: whoever
holds it, and nothing else, can obtain an account. It is the only asset here
that travels outside the server.

The store of invitations. It holds the state that decides whether a presented
code is honoured, so an attacker who can write it can revive a spent invitation
or extend an expired one, and an attacker who can read it learns who was
invited by whom.

The keyed hash secret. It is what makes the stored form of a code useless to
somebody who has read the store. Its value is entirely in nobody else having it.

The account template. It is the whole answer to what a new account may reach, so
a template an attacker can influence is a permission set an attacker chose.

The accounts the plugin creates. They outlive the invitation, the plugin and the
operator's attention, and they belong to real people who have done nothing
wrong.

## The actors

The operator who mints. Holds administrator rights on the server, is trusted to
decide who is invited, and is not trusted to be careful with a link once it
leaves the dashboard.

The invited person who redeems. Holds a valid code, is trusted with the account
that code was minted for, and is not trusted with anything else on the server.

A stranger who found the link. Holds a valid code that was not meant for them,
through a forwarded message, a chat log, a browser history or a link preview
fetch. Indistinguishable from the invited person by construction.

A stranger who is guessing. Holds no code and can reach the redemption endpoint,
which is unauthenticated because the whole point is that somebody who has no
account can use it.

An existing low-privilege account on the same server. Can already sign in, can
reach whatever the server exposes to an ordinary user, and would like to reach
more.

Anyone who can read the server's disk or its logs. Covers a backup, a support
thread with a log attached, a log collector, a second administrator, and
whoever ends up with a copy of the data directory.

## The grid

One row per attack, in the order the milestone mitigates them. The impact is
what the attacker gets, in one sentence. The issue is where the mitigation
lands.

| Attack | Actor | Impact | Mitigation | Issue |
| --- | --- | --- | --- | --- |
| Guess a valid code | A stranger who is guessing | An account on the server, scoped by whatever template the guessed invitation carried. | Codes are minted from a cryptographic source at an entropy chosen from the number of live invitations, the achievable guess rate and a stated margin, with the calculation written down rather than a length that looks long. | #28, #49 |
| Turn guessing into enumeration through distinguishable responses | A stranger who is guessing | Every guess returns whether that code exists, which converts an infeasible search into a feasible one and also discloses which invitations are live. | One indistinguishable response for absent, expired, spent and revoked, asserted byte for byte in a test, with the real reason kept for the operator's own view. | #28, #77 |
| Turn guessing into enumeration through timing | A stranger who is guessing | The same disclosure as above, through the duration of the lookup rather than its body. | The lookup is constant time with respect to whether the code exists, and the stored hash is compared with a fixed-time comparison rather than with the equality operator. | #29 |
| Grind guesses at machine speed | A stranger who is guessing | Enough attempts that even a large code space is reachable, and a log full of failures nobody reads. | Rate limiting and lockout on the redemption endpoint, sized so a human redeeming a link never meets it. | #31 |
| Replay a spent code | A stranger who found the link | A second account from an invitation the operator meant to be used once. | The use count is a field of the record and the only authority for it, the decision routine refuses a record with no uses left, and a spent code is presented with the same indistinguishable response as an absent one. | #52, #55, #56 |
| Use a revoked code that something still honours | A stranger who found the link | An account created after the operator had already decided the link should not work, which is the exact moment revocation exists for. | Revocation takes effect for any redemption not already committed, because the decision routine re-reads the record inside the same lock as the write rather than trusting anything read earlier. | #54, #56 |
| Race two redemptions of a single-use code | A stranger who found the link, racing the invited person | Two accounts from one invitation, the second belonging to whoever else had the link. | One lock covers read, decide and write as one unit, and the store is written to a temporary file in the same directory and renamed over the original so a kill between the two cannot lose it. | #40, #106 |
| Forge a link carrying a better template | A stranger who found the link | An account with permissions the operator never granted, including access to libraries the invitation was not for. | The link carries no template and no permission of any kind. The template is named by the record, resolved from the record at redemption, and the code is the only thing the link carries. | #50, #56 |
| Point the link at another host | The operator who mints, unknowingly | The invited person types a password they chose into a page an attacker controls, having followed a link the operator sent them. | Link construction reads no request header. The base address comes from configuration, and the invariant lint refuses a header-derived link so the shortcut cannot be reintroduced. | #50, #18 |
| Read codes out of the log | Anyone who can read the server's disk or its logs | Every code that was redeemed while that log was being written, which is a working invitation for each one still live. | Nothing logs a code in any form, the hash secret, a chosen password or the full link. An invitation carries a non-secret identifier that logs and the administrator view both name, and the invariant lint refuses a logging call whose argument is a code or a secret. | #32, #18 |
| Read codes out of the store or a backup | Anyone who can read the server's disk or its logs | Nothing directly, if the store holds only keyed hashes. Everything, if it holds codes. | Only a keyed hash of the code is stored, and nothing resembling the code itself. | #29 |
| Read the store and the secret together | Anyone who can read the server's disk or its logs | The ability to test a guess offline, without the rate limit, at the cost of the code's full entropy per guess. | The secret is generated on first use rather than shipped, lives outside the configuration file an operator pastes into a support thread, and has its permissions enforced on write. This narrows who can do it and does not defend against somebody who already holds both. | #30 |
| Mint an invitation without administrator rights | An existing low-privilege account on the same server | An invitation minted by somebody the operator never gave that power to, and an account created from it. | Every administrator route is authorized explicitly, the route list is enumerated so there can be no unlisted one, and the enumeration is what the API document is checked against. | #83, #88 |
| Read minted codes off an administrator route | An existing low-privilege account on the same server | Live codes without having to guess any of them. | No route returns a code after minting. The code appears once, in the response to the mint that created it, and listing returns records without codes or hashes. | #82, #85 |
| Reach something the template did not grant | The invited person who redeems | An account with more than the operator scoped, which is the failure this plugin exists to avoid. | The template is applied in one routine, every permission it sets has an explicit value, an unresolvable library never widens the grant, and the resulting policy is asserted field by field in a test. | #62, #63, #64, #69, #70 |
| Become an administrator through the template | The invited person who redeems | The server. | The creation routine refuses to set the administrator flag whatever the template, the configuration or the request says, and a configuration asking for one is rejected at load. The refusal is inside the routine rather than at its callers, so a future caller that skips validation still meets it. | #62 |
| Widen an account that already exists | An existing low-privilege account on the same server | The invitation becomes a privilege-editing tool for whoever holds it, which is worse than an extra account because it needs no new account at all. | Redemption never modifies an existing account. Presenting an invitation while signed in creates nothing and changes nothing, and reusing an account whose name matches is refused rather than treated as helpful. | #62 |
| Post the redemption form from another site | A stranger who is guessing, through the invited person's browser | An account created with a username and password the attacker chose, from the invited person's address, consuming an invitation meant for someone else. | An anti-forgery token tied to the request that served the page, framing denied by header, and a referrer policy that keeps the code out of an outgoing header. A post without a valid token is refused without consuming a use. | #78 |
| Carry a credential in the link | The operator who mints, unknowingly | A password in a chat log, a browser history and a link preview fetch, for an account that exists on somebody's server. | No minted link carries a password, a temporary password or a token standing in for one. The invited person chooses a password during setup and the plugin sets it through the server's own path without storing, logging or echoing it. | #66 |
| Leave a passwordless account behind | The invited person who redeems, by accident | An account that exists with no credential set, which is an account somebody else may be able to claim. | The password is validated against the server's rules before the account is created, so a refused password never leaves a created account behind. | #76, #72 |
| Revive spent invitations by restoring a backup | The operator who mints, unknowingly | Every invitation redeemed since the backup is live again with its uses restored, and every revocation since is undone. | Not defended. See below. | #46, #96 |
| Copy the data directory to a second machine | The operator who mints, unknowingly | Two servers with the same secret and the same live invitations, where redeeming on one leaves the other still willing. | Not defended. See below. | #96 |
| Point two servers at one shared store | The operator who mints, unknowingly | Two processes writing a file whose atomicity assumptions were single-process, which is silent corruption of the state that decides who gets an account. | Detected and refused at startup with a lock file naming the process and host that holds it. | #96 |
| Undo a revocation by upgrading | The operator who mints, unknowingly | A revoked or spent invitation read as live, because a new version read an old file and treated a missing field as a default. | The store carries a version from the first write, an unknown newer version fails closed naming both versions, and every shipped transition has a migration with a test over a committed fixture. | #42, #92, #93 |
| Learn which usernames exist | A stranger who found the link | Confirmation of which accounts are on the server, one name at a time. | Not defended. See below. | #67, #112 |

## What a leaked link costs

Invitation links leak. They go into chat applications that fetch a preview, into
mail that sits on somebody else's server, into a screenshot, into a browser
history on a shared machine. The design question is not how to stop that, since
a link that cannot be forwarded is a link that cannot be sent. It is what the
leak costs, written as a bound somebody can hold this plugin to.

The bound, and this is the sentence [SECURITY.md](../SECURITY.md) repeats word
for word:

> Somebody who holds a leaked link, and reaches the server before the invited
> person does, gets one account for each use the invitation had left, with
> exactly the template that invitation carried, valid for no longer than the
> invitation had left, listed for the operator as a redemption of that
> invitation, and removable by deleting that account.

Every clause of it is a property some other issue has to make true, and if any
of them is not true the cost is larger than the sentence claims. Each clause
below names where it is kept.

THIS PARAGRAPH SAID NOTHING IN THIS REPOSITORY REDEEMS ANYTHING YET, SO THE
SENTENCE IS THE SPECIFICATION AND NOT A DESCRIPTION. Something redeems, and the
absence the paragraph rested on has gone:

    git grep -n 'Decide(' -- 'Jellyfin.Plugin.Invites/*.cs' ':!*RedemptionDecision.cs'
    exit=0

    git grep -n 'RedemptionDecision.Decide' -- 'Jellyfin.Plugin.Invites/*.cs'
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:628:            var verdict = RedemptionDecision.Decide(presented, hash, contents.Invitations, now);

So the sentence is a description of a live route now, and every clause below is
worth reading as one. Which of them are held by something that runs and which are
still specification is the table below, clause by clause, rather than this
paragraph.

| Clause | What has to hold for it | Issue |
| --- | --- | --- |
| one account for each use the invitation had left | The use count is a field of the record and the only authority for it, the decision routine refuses a record with no uses left, and read, decide and write happen under one lock so two presentations of a one-use invitation cannot both succeed. | #52, #56, #40, #106 |
| with exactly the template that invitation carried | The link carries a code and nothing else, the template is resolved from the record at redemption rather than from anything presented, and the creation routine refuses to set the administrator flag or to touch an account that already exists. | #50, #56, #61, #62, #63, #64 |
| valid for no longer than the invitation had left | Expiry is judged against a clock the plugin reads through one seam, the comparison is on an absolute instant, and an expired record is refused by the same routine that refuses a spent one. | #41, #51, #56, #59 |
| listed for the operator as a redemption of that invitation | Every attempt appends one outcome entry, and the administrator view renders which invitation produced which account, including when the account has since gone. | #43, #45, #89 |
| and removable by deleting that account | Deleting the account is an ordinary server action the plugin does not resist, the record renders the account as gone rather than failing, and the operator has a route that shows what the plugin did to an account and undoes it. | #45, #94, #95 |

Two clauses carry a qualification a reader should not have to derive.

The first clause is one account only where the invitation is single use. A
multi-use invitation is worth as many accounts as it has uses left, to whoever
holds the link, and that is the price of the feature rather than a defect in it.
This paragraph said the common case was undecided. Decision 2 in #11 is
answered: an invitation is redeemable once, so single use is the case the rest
of this file is written against and a count above one is the operator asking for
a wider blast radius deliberately.

The fourth clause says listed rather than noticed. The operator is told what
happened when they look, and nothing here alerts them.

### The default validity, and the reason for the number

Seven days, as the default an operator may change within the ceilings #33 sets,
enforced by the expiry rules in #51 and defaulted and validated in #86.

The number is the exposure window, so the argument is what each direction costs
when it is wrong. Too short costs the invited person a link that died before
they opened it and the operator one more mint, which is a minute of somebody's
evening and is fully recoverable. Too long costs a live account-creation
credential sitting in a mailbox, a chat backup and a link preview cache for as
long as the number says, and that is not recoverable by anybody who has stopped
thinking about it. The costs are not symmetric, so the number belongs at the
short end of what still works.

What still works is one full week. A link sent on any weekday survives the
weekend on either side of it, which covers the person who reads mail at work and
sets up a media account at home. Twenty-four hours does not survive somebody
being away for two days and turns the ordinary case into re-minting. Thirty days
buys the same working case as seven and pays for it with four more weeks of
exposure, which is the direction that does not recover.

Seven days is also short enough that the leak the model cannot defend has
usually already expired by the time a chat log is read by somebody new, and long
enough that the operator is not the one paying for it.

### Whether a spent invitation is spent for good

It is. Once the use count reaches zero the record is never returned to a
redeemable state by any route the plugin offers. There is no un-spend, no raise
the count on a spent invitation, and no reopen. Minting a fresh invitation is
the path, and it produces a fresh code, so the old link stays worthless.

The reason is that an un-spend route is a revocation-undo route wearing another
name. It takes a link that is already loose in a chat log, that the operator has
stopped thinking about, and makes it live again without anybody re-sending it or
deciding to. The blast radius of the feature is every link the invitation ever
had, and the thing it saves is one mint.

#55 holds what a spent code is answered with, which is the indistinguishable
response the grid names above rather than a helpful one. #54 holds revocation
being immediate and idempotent, and #93 holds an upgrade not reading a spent
record as live. The one route that does revive a spent invitation is restoring a
backup, which is undefended and is stated as such below.

## What is not defended

These are stated rather than left as omissions a reader has to notice. They
appear in the same words in [SECURITY.md](../SECURITY.md), and the security
page written under #112 carries them too.

A leaked link within its validity, before the intended person uses it, is an
account for whoever found it. This is what a bearer credential means and no
mitigation in this plugin changes it. What the plugin offers instead is a
smaller window and a smaller blast radius: a validity the operator chooses, a
use count the operator chooses, and revocation that works the moment the
operator reaches for it.

An operator with administrator rights can mint whatever the ceilings allow.
Nothing here defends the server against the person the server already trusts
with it. The ceilings bound what any single invitation can grant, and they are
configuration an administrator can also change.

A restored backup revives spent invitations. The invitations redeemed since the
backup are live again with their uses restored, and the revocations made since
are undone. The plugin cannot prevent this. What it does is compare, on load,
the accounts the store claims to have created against the accounts the server
actually has, and report the disagreement in both directions rather than
reconciling it silently.

A cloned data directory produces two servers that both honour the same live
invitations. Redeeming on one leaves the other still willing, because neither
knows the other exists. The operator guide says to rotate the hash secret after
a clone, and rotation is a revoke-everything operation offered deliberately.

Username availability is disclosed by the setup form. A form that has to tell
somebody their chosen name is taken is a form that tells anybody holding a code
which names exist. The disclosure is bounded by needing a valid code first.

Somebody who can read both the store and the hash secret can test a guess
offline, without meeting the rate limit. The keyed hash and the code's entropy
are what stand between them and a code, and neither is a defence against
somebody who is already reading the data directory.

## Where this model is wrong

This section said that every row above is a claim about code that does not exist
yet. That was true when it was written and is not true now, and nothing in the
tree noticed: the sentence reads exactly as it did on the day it was correct,
which is why it is corrected here in place rather than deleted. Ten of the
thirty-five issues the grid names as where a mitigation lands are closed, and one
of them is the row saying only a keyed hash of the code is stored, which is a
reduction with a caller in the tree:

    git grep -n 'new InvitationCodeHash' -- 'Jellyfin.Plugin.Invites/*.cs'
    exit=0

So the wider claim is withdrawn.

THE NARROWER ONE WAS THAT NOTHING HERE REDEEMS AN INVITATION, SO NO MITIGATION IN
THE GRID HAS BEEN EXERCISED ON A PATH A STRANGER CAN REACH. It rested on the
decision routine having no caller, and it has one:

    git grep -n 'Decide(' -- 'Jellyfin.Plugin.Invites/*.cs' ':!*RedemptionDecision.cs'
    exit=0

    git grep -n 'RedemptionDecision.Decide' -- 'Jellyfin.Plugin.Invites/*.cs'
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:628:            var verdict = RedemptionDecision.Decide(presented, hash, contents.Invitations, now);

What survives is the half about exercise rather than about reach. Several of
these mitigations are on a path a stranger can reach now. None of them has been
exercised there: no server has been run, no request has crossed a socket, and
every assertion behind them is made against a controller instantiated as an
ordinary object in a suite that may open no network connection. A row that is
green in this repository is a row a test drove and not a row an attack met.

A row whose issue is closed is a row where the work landed under an issue that
argued it. It is not a row this file measured, and it is not a row anybody has
seen refuse an attack, because the attacks in the grid arrive through a
redemption and there is none. The value of having written the model first is that
the record shape, the decision routine and the routes are built against it rather
than reconciled with it afterwards. The cost is unchanged: when a mitigation
lands, the issue it landed under is the evidence, and this file is not.
