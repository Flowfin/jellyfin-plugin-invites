# Security policy

This plugin mints invitation links that create Jellyfin accounts. A defect here
can hand somebody an account on a server they were never invited to, so security
reports are wanted and are not a nuisance.

## Reporting a vulnerability

Use GitHub's private vulnerability reporting on this repository:

<https://github.com/Flowfin/jellyfin-plugin-invites/security/advisories/new>

It is enabled, which anyone can check:

```
gh api repos/Flowfin/jellyfin-plugin-invites/private-vulnerability-reporting
{"enabled":true}
```

The name in both is the one this repository answers to today, read back rather
than copied:

```
gh repo view --json nameWithOwner --jq .nameWithOwner
Flowfin/jellyfin-plugin-invites
```

An older path still reaches here through a rename redirect, so a link written
against it works until somebody else claims that name. A reporter following one
would be checking whatever the old path resolves to on the day they run it,
which is not a check of this repository's setting.

That route is private until an advisory is published, and it is the only route
this document promises. Please do not open a public issue for something that
lets somebody in, and please do not send it to a personal address; a public
issue is a working exploit handed to every reader of this repository, and a
personal address is a route nobody else can pick up.

If the report concerns Jellyfin itself rather than this plugin, it belongs to
the Jellyfin project's own policy and not here.

## What to expect

This is a small project without a staffed security team, so the numbers below
are what one maintainer can actually hold to rather than what reads well.

- An acknowledgement within seven days that the report was received and read.
- An assessment within thirty days: whether it is accepted, what it is thought
  to affect, and either a fix or a stated reason for not fixing.
- Credit in the advisory unless you ask otherwise.

If seven days pass with no acknowledgement, the report has probably not been
seen. Say so in a public issue without describing the vulnerability, and that
is the escalation.

## What is in scope

The code in this repository, its packaging metadata, and its workflows. A
report that the plugin grants more than the invitation it came from, that a
spent or revoked invitation is honoured, that an invitation code is guessable
or enumerable, or that a code or secret appears in a log, is in scope even if
you cannot demonstrate a full exploit.

Out of scope: the Jellyfin server itself, a server whose operator configured it
to be open, and reports produced by a scanner with nothing behind them.

## What this plugin defends

Seven properties, each with the reason it exists and what holds it today. They
are stated no more strongly than what can be run, so each one names the tests
that hold it, and a property nothing holds yet says so in the same place rather
than being left out where its absence reads as silence.

A name in `PascalCase` below is a test in `Jellyfin.Plugin.Invites.Tests`,
reached by:

```
dotnet test --configuration Release
```

Nothing in this repository redeems an invitation or creates an account yet, so
several of these are held at the routine that decides and not on any path a
stranger can reach. Where that is the difference, it is written out.

### Codes are minted from a cryptographic source at a calculated entropy

The code is the whole credential, so its size is read off a calculation rather
than picked because a number of characters looks long.
[docs/code-entropy.md](docs/code-entropy.md) requires 128 bits and derives that
from the live invitation count, the guess rate an attacker can sustain and a
stated margin. A code is twenty-six characters over a thirty-two character
alphabet, which is 130 bits, all of it random: no mint time, no operator, no
prefix, no checksum.

`InvitationCodeTests.AMintedCodeIsTwentySixCharactersFromTheAlphabet` holds the
length and the alphabet, `EveryCharacterOfTheAlphabetIsMinted` holds that the
draw reaches all thirty-two rather than a subset, and
`TwoCodesMintedInTheSameMillisecondDiffer` holds that a code is not derived from
the moment it was minted.

No test holds that the source is cryptographic, and none can. A good source and
a bad one are indistinguishable from their output at any sample size a suite can
take. What refuses a bad one is the `weak-random` rule in
`.github/lint/invariants.sh`, and it matches a spelling rather than a dataflow,
so the same source reached through a helper two files away passes it. Neither of
the two things a reader would expect to stand behind it does. The analyzer
rule for an insecure random source is turned down to information in
`jellyfin.ruleset`, so the mistake written into the minting routine builds with
no warning and no error, and the dataflow query that would follow it was run
against that same mistake in that same routine and reported nothing, which is
measured in issue #16.

### A code is stored only as a hash of itself

Somebody who reads the data directory or a backup should learn nothing they can
present at the redemption page. The record carries the hash and no code, and
that is refused rather than remembered:
`InvitationRecordTests.ARecordWithoutAKeyedHashIsRefused` refuses a record with
nothing to compare against, and `NoMemberOfTheRecordHandsBackSomethingShapedLikeACode`
mints a code and asserts that no member of the type hands back anything
canonicalisation would accept as one.
`InvitationModelTests.AMintedCodeIsNotRecoverableFromTheStore` writes a record
and reads the file back, asserting the longest run of code alphabet in it is
shorter than a code.

The key is not held. There is no implementation of `IInvitationCodeHash` in the
plugin, and the suite supplies its own, so what those tests hold is that the
code does not reach the disk and not that the stored form is keyed. The secret a
keyed hash would use is generated, stored and rotated already, in `HashSecret`,
and nothing hashes with it. Until an implementation lands, treat this property
as the code being absent from the store rather than as the store being useless
to somebody who has it.

### The four ways a redemption fails are one answer

A response that separates "no such invitation" from "expired" from "already
used" from "revoked" hands somebody guessing an oracle, and turns a search
nobody can afford into one they can. The reason the invitation was refused is
the operator's to see, in their own view, and not the caller's.

`RedemptionDecisionTests.RubbishAndAnUnmintedCodeAreTheSameVerdict` holds that a
code that was never minted, a string that is not a code, an empty one and none
at all are one outcome carrying nothing.
`ARecordThatIsBothRevokedAndExpiredAndSpentReadsAsRevoked` and
`AnExpiredRecordWithUsesLeftIsExpiredRatherThanSpent` hold that the order the
refusals are read in is fixed rather than incidental, and
`RedemptionDecisionTableTests.EveryReachableCombinationAnswersItsRow` holds the
whole table rather than the cases somebody thought of.

The other half of this is duration. The stored hash is compared with
`CryptographicOperations.FixedTimeEquals`, every record is compared rather than
the loop returning on the first match, and
`RedemptionDecisionTests.TheAnswerDoesNotDependOnWhereTheRecordSits` moves the
matching record through every position and requires the same answer. The
`secret-compared-with-equality` and `secret-compared-by-sequence` rules refuse
the two spellings that would take it back. No timing was measured, and this is a
claim about which branches the routine takes rather than about a clock.

Byte-identical responses are not held, because there is no response. Nothing in
this repository serves a route. What a person is shown is decided in
[docs/refusal-response.md](docs/refusal-response.md), and the assertion that the
four are identical on the wire waits for the route that writes them.

### Redemption is rate limited

It is not. Nothing in this repository limits or locks out anything, and there is
no endpoint to limit. This property is on the list because the entropy
calculation quotes a figure that assumes a limiter, and reading the code length
as sufficient on its own would be reading the unthrottled row of that page, not
the throttled one. Both rows are on it, and the unthrottled one is what holds
today.

### An invitation expires, at an instant the plugin does not argue with

Expiry is judged against one absolute instant, read through the one clock seam
the plugin has, so the boundary is a comparison somebody can point at rather
than a behaviour that depends on when the code ran.
`RedemptionDecisionTests.TheExpiryBoundaryIsExclusive` asserts the answer one
tick before the boundary, at it, and one tick after, which is the difference
between "expires at" and "expires after" and the only place it shows up before a
support thread.

`ClockJumpTests.AClockSteppingBackwardsAcrossTheExpiryMakesTheInvitationUsableAgain`
holds the accepted cost of a clock that goes backwards,
`AClockSteppingBackwardsRevivesNeitherARevokedNorASpentInvitation` bounds that
cost to expiry alone, and `AJumpPastSeveralExpiriesRefusesEveryOneOfThem` holds
a jump forward across several at once.
[docs/expiry-rules.md](docs/expiry-rules.md) is where the backwards jump is
argued rather than defended.

`clock-read-outside-the-seam` refuses a machine clock read anywhere but the one
file, so a later change cannot quietly acquire a second opinion about the time.

### An invitation can be revoked, and revoking it twice costs nothing

Revocation is the control an operator reaches for at the worst moment, so it has
to work on the first attempt and it has to be safe to repeat.
`RevocationTests.RevokingRecordsTheOperatorAndTheTime` holds the trail,
`RevokingTwiceKeepsTheFirstTimeAndTheFirstOperator` and
`RevokingTwiceHandsBackTheRecordItWasGiven` hold that a second revocation
changes nothing and is visible to a caller as nothing to write, and
`RedemptionDecisionTests.ARevokedInvitationIsRefused` holds that the decision
refuses it.

Two things revocation deliberately does not do, both asserted rather than
described. `RevocationTests.TheAccountsAlreadyCreatedAreStillNamed` holds that
revoking stops future accounts and does not disown past ones, and
`NothingHereCanBeHandedAnAccount` holds that no account manager can be passed
into any of it, so a later change cannot start deleting accounts through this
routine.

Immediacy is not held. A revocation taking effect against a redemption that is
already in flight follows from the lock covering read, decide and write as one
unit, and nothing in this repository holds that lock because nothing redeems.

### No invitation mints an administrator or widens an existing account

This is the ceiling, and it is the property worth a refusal in more than one
place. An invitation that could mint an administrator is the server. An
invitation that could widen an account that already exists is worse, because it
needs no new account at all: a feature that reuses an account whose name matches
turns the link into a privilege-editing tool for whoever holds it.

No test holds either half, because there is no routine that creates an account.
What exists is narrower and is named here so it is not mistaken for the
property. `administrator-flag-set` in `.github/lint/invariants.sh` refuses the
administrator flag being written anywhere in the tree, in either direction, with
no exemption for the file the other policy rules exempt. It matches an
assignment on the line the field is named on, so a write through a local, a
policy built by a helper handed a boolean, or a whole policy object handed to
the server with the flag already set all pass it.

[docs/what-an-invitation-can-never-do.md](docs/what-an-invitation-can-never-do.md)
is where the ceiling is written down line by line, and it marks each line with
whether a test refuses it, a spelling refuses it, or nothing does.

## What a leaked link costs

An invitation link is a bearer credential. Anybody holding it can create an
account, so a link in a forwarded message or a browser history on a shared
machine is worth whatever the invitation behind it was worth. This is the bound
on that, in the same words as
[docs/threat-model.md](docs/threat-model.md), which is where each clause is
placed against the issue that keeps it:

> Somebody who holds a leaked link, and reaches the server before the invited
> person does, gets one account for each use the invitation had left, with
> exactly the template that invitation carried, valid for no longer than the
> invitation had left, listed for the operator as a redemption of that
> invitation, and removable by deleting that account.

Nothing in this repository redeems an invitation yet, so today the sentence is
what this plugin is being built to, and not a report of what it does. A report
that the plugin exceeds this bound once the code exists is a report this policy
wants, and the list above is what to measure it against.

The default validity is seven days, and a spent invitation is spent for good.
The threat model carries the reasoning for both, and both are the shortest
window that still works rather than the longest one an operator would tolerate.

## What the guided setup will never ask

The page that turns an invitation into an account asks for a username, a
password, and a password confirmation. That is the whole form.

It will never ask for a password to another service, a password already used on
this server, a payment detail, a date of birth, a postal address, a legal name,
a security question, anything phrased as optional that the plugin has no field
for, or anything the operator could ask outside the plugin. It loads no script,
font, image or analytics from another host, and it says which server it belongs
to.

A page bearing this plugin's name that asks for any of those is not this
plugin's page, and a report that it does is a report this policy wants. The
reason for each refusal is in
[docs/setup-never-asks.md](docs/setup-never-asks.md), and a field added to the
form needs a row in [docs/personal-data.md](docs/personal-data.md) first.

The page does not exist yet, so this is what it is being built to rather than a
description of something serving today.

## What is not defended

These are the entries the threat model in [docs/threat-model.md](docs/threat-model.md)
marks as undefended, in the same words. That file is where each one is placed
against the attack it belongs to.

Every mitigation the threat model names is a promise held by an open issue
rather than by code, because there is no redemption path in this repository yet.
Read this section as what will still not be defended once those issues land, and
not as a claim that everything else already is.

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

## Supported versions

Nothing is released yet, so there is no supported version and no backport
policy. This section is rewritten at the first release, under milestone M12.
