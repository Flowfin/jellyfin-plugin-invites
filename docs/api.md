# The API

Somebody will call these routes from a script. A plugin that mints invitations
is a plugin an operator wants to drive from whatever already runs their server,
and that happens whether or not anything is written down. The only choice is
whether they are working from this page or from a browser's network tab.

So the routes were fixed here before they existed, rather than described
afterwards. Most of them answer now:

    $ git grep -lE 'ControllerBase' -- 'Jellyfin.Plugin.Invites/*.cs'
    Jellyfin.Plugin.Invites/Controllers/InvitesController.cs
    Jellyfin.Plugin.Invites/Controllers/RedeemController.cs

This paragraph carried two hand counts and both had gone wrong, in the direction
that reads as less built than the tree is: it said four of seven were served,
against a pasted output naming one controller where the tree holds two. Neither
number is written here now. The register at the end of the page is the list of
what nothing serves, `ApiDocumentTests` holds it against the assembly on every
run, and a count in a sentence is read by nobody.

The administrator operations landed under #82, and the fifth of them, rotation
of the hash secret, under #30. Two of the three redemption routes answer: the
page landed under #74 and the post that receives its form landed after it. The
completion address is #79 and is served by nothing.

THIS SENTENCE SAID THE REDEMPTION ROUTES ARE #74 AND #75, AND THEN THAT ONE OF
THE THREE ANSWERS. The first was written on 2026-08-21, ten days before #71 split
the post out of #74, so it named an issue that had landed no route and left the
post and the completion address attributed to nobody. The second stopped being
true when the post landed. Which of the three answer is held against the assembly
under `## What no controller serves yet` below rather than by this sentence,
which is why the sentence is worth correcting and not worth trusting.

## What is promised

Nothing, before the first release. These paths, parameters and response shapes
may change in any version until `1.0.0`, and a caller written against them today
is written against a plan.

That is the whole promise, and it is stated plainly rather than implied to be
more. [docs/versioning.md](versioning.md) is where the number it refers to
lives. After the first release the promise is worth restating with a real
answer, and this section is where that goes.

## Two prefixes, and why they differ

The administrator routes sit under `/Invites` and the redemption routes under
`/redeem`. That is not an oversight in one of the two.

The administrator routes are called by scripts, beside the server's own API,
where every path is capitalised the same way. The redemption path is different
in kind: it is the visible half of a link that a person is sent, may read off a
screen, and occasionally types. Lower case is what that is for, and matching a
neighbouring convention is worth less than a path somebody can copy without
getting it wrong.

Paths below are relative to the server root. Where the server mounts a plugin's
controllers is the server's business and nothing here has been measured against
one.

## The redemption routes

Three routes, carrying the flow in
[docs/redemption-flow.md](redemption-flow.md). That document names them as
provisional and points here; this is where they stop being provisional.

None of the three requires authentication. That is the design and not an
omission: the person following the link has no account yet, which is the whole
reason the flow exists. Every other route in this plugin requires an
administrator, and #83 is the inventory that holds those two categories against
the assembly.

### `GET /redeem/{code}`

Serves the setup page for a presented code, or the refusal.

| Parameter | In | Required | What it is |
| --- | --- | --- | --- |
| `code` | path | yes | The invitation code as it appears in the link. Never in a query string, for the reason `docs/redemption-flow.md` gives at its route table |

Responds with the setup page as HTML, carrying an anti-forgery token bound to
this request under #78, or with the single refusal below. No JSON shape, because
the caller is a browser following a link. Reading the invitation here writes
nothing.

What answers today is narrower than that paragraph, and the difference is worth
reading before anybody calls this route. THIS PARAGRAPH SAID THE RESPONSE
CARRIES NO ANTI-FORGERY TOKEN. It carries one since #78: the response mints a
value, writes it into the form and into a cookie scoped to this route, and the
post below refuses a submission that does not carry both. What is still narrower
than the paragraph above is the rest of it. No invitation is looked up here and
the same page is served for every code, apart from that token, so the refusal
half is undelivered and the route discloses nothing about a code because it does
not read one. #75 and #77 own the refusal.

THIS PARAGRAPH SAID THE LOOKUP BY CODE EXISTS AND HAS NO CALLER. It has one: the
post below reaches it through the operation that reads the records, asks for the
verdict and takes the use inside one monitor.

    git grep -n 'var match = Lookup(codeHash.Of(canonical), records);' -- Jellyfin.Plugin.Invites/Redemption/RedemptionDecision.cs
    Jellyfin.Plugin.Invites/Redemption/RedemptionDecision.cs:105:        var match = Lookup(codeHash.Of(canonical), records);

    git grep -n 'Decide(' -- 'Jellyfin.Plugin.Invites/*.cs' ':!*RedemptionDecision.cs' ; echo "exit=$?"
    exit=0

    git grep -n 'RedemptionDecision.Decide' -- 'Jellyfin.Plugin.Invites/*.cs'
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:645:            var verdict = RedemptionDecision.Decide(presented, hash, contents.Invitations, now);

What the paragraph was FOR still holds and is worth keeping in front of whoever
extends this route. It costs more here than in most places, because it is read by
whoever writes against the route it describes, and a reader told the lookup is
missing writes a second one beside it, which is a second authority for what a
match means.
`code-canonicalised-outside-one-function` and
`expiry-or-use-count-judged-outside-the-decision` in `.github/lint/invariants.sh`
refuse two spellings of that shape, and neither refuses the third, which is a
lookup written from scratch.

The response carries `Content-Security-Policy`, `X-Frame-Options`,
`X-Content-Type-Options`, `Cache-Control` and `Referrer-Policy`, and
`SetupPageTests` asserts each of them. The policy names no origin at all beyond
the one hash of the page's own style element, which is derived from the page as
it is served rather than written into a header by hand.

### `POST /redeem/{code}`

Takes the answers, decides, and creates the account.

| Parameter | In | Required | What it is |
| --- | --- | --- | --- |
| `code` | path | yes | The same code the page was served for |
| username | form | yes | The name the account will have. Refused before any use is spent if the server's own expression would reject its shape; a name that COLLIDES with an existing account is not refused here and costs the use, which is #67 |
| password | form | yes | Chosen by the person. Never stored by this plugin and never carried in a link, which is #66 |
| anti-forgery token | form | yes | Validated before anything else happens. A post without a valid one is refused and consumes no use, which is #78 |

Three outcomes. The completion page, reached by redirect to the route below. The
form again, where the username was taken or the password was refused, with the
field marked and nothing echoed back that should not be. Or the single refusal.

The form-again case is the one worth reading carefully, because it is the only
place this API tells a caller something it could have kept quiet about: showing
that a username is taken discloses that it is taken. That is chosen rather than
overlooked, and it is in [docs/limits.md](limits.md) as a disclosure with its
reason.

### `GET /redeem/done`

Shows the completion page.

Takes no parameters and reads no invitation. That is what makes it a route of
its own rather than a state of the first one, and it is the decision this page
was owed: branch 7 of the flow is a person pressing the back button after
finishing, and a completion route that re-read the invitation would meet a
spent one and refuse, turning a finished, correct redemption into an error
message. A route with nothing to look up cannot do that.

It follows that the page says nothing specific to the account that was just
created. Whatever it says, it says to anybody who visits it.

## The administrator routes

Six operations and no more. Every one of them requires an administrator, and
#83 is where that is asserted against the assembly rather than promised here.

It was four, from #82, and the fifth is rotation of the keyed hash secret. #30
is where that was decided and the reason is worth reading before anybody adds a
seventh: the routine that plans a rotation already counts what it invalidates
and already refuses a confirmation made against a store that has moved, and with
no route none of it reached an operator. A mechanism that cannot be reached is an
absent one that looks present. The alternative kept the surface at four and made
rotation an offline edit of a key file, which serves the counter and the refusal
to nobody.

The sixth is the reverse lookup, from an account to the invitations that claim
it, and #89 is where it was asked for. That issue wants both directions of one
view, and only one of them had a route: an operator holding an account and
asking where it came from had to walk every row of the listing by hand. It
stores nothing to make the answer possible, because the claim is already on the
record, which is why it is a route and a shape rather than a change to the
store.

### `POST /Invites`

Mints one invitation and returns the code exactly once.

| Parameter | In | Required | What it is |
| --- | --- | --- | --- |
| template | body | yes | Which grant the invitation carries, by the name of a configured template, compared ignoring case. The grant behind that name is copied onto the record at this moment, which is #61's rule, and a name matching no configured template is refused as a bad request with nothing written |
| validity | body | no | How long the link lasts. Bounded by the maximum in [docs/expiry-rules.md](expiry-rules.md), and an invitation with no expiry at all is refused |
| uses | body | no | How many accounts the invitation is good for. Refused at zero and above the ceiling, which is #52 and #33 |

A mint that would take the number of live invitations past the ceiling is
refused with a conflict and nothing is written. This page named the use-count
ceiling in the table above and named no other, which read as the whole of what
this route refuses; it is not, and the second of the three ceilings in #33 has
been acting since it landed:

The third acts on the redemption post rather than here, and it refuses nothing an
operator sends: it bounds how many accounts this plugin may create in a day
across every invitation, so an operator who mints a link never meets it and a
person redeeming one meets the single refusal without being told which case it
was.

    git grep -nE 'catch \(LiveCeilingReachedException refused\)' -- Jellyfin.Plugin.Invites/Controllers/InvitesController.cs
    Jellyfin.Plugin.Invites/Controllers/InvitesController.cs:147:        catch (LiveCeilingReachedException refused)

Nothing about such a request is malformed, so it is not a bad request, and the
distinction is the operator's repair rather than a taste in status codes: what
they sent was acceptable and the store's state was not, so what fixes it is
revoking an invitation rather than changing what they asked for. The message
carries the count and the ceiling, because an operator told only that they hit a
limit does not know how far past it they are.

The ceiling counts live invitations and nothing else. A record that has expired,
been spent or been revoked is not live, does not count against it, and stays in
the store until retention removes it, so an operator meeting this refusal on a
server with few outstanding links is reading a bound on what may be redeemed
rather than on how large the file has grown.

The table above names the use-count ceiling and not this one because that one
bounds a parameter of the request. This one bounds the store, belongs to no
parameter, and had no row to go missing from, which is how it came to be absent
from this page.

A configured template list with a fault in it refuses every mint the same way,
with a conflict and nothing written, whatever name was asked for. The list is
judged whole rather than thinned to the entries that pass, for the reason
[docs/configuration.md](configuration.md) gives under `## The named templates`,
and the message is the sentence the load writes when the plugin starts: the
setting, the position of the entry counted from one, and the rule it missed,
with no label quoted. It is a conflict for the reason the ceiling above is one.
What was asked for may be acceptable and the plugin's own configuration is not,
so the repair is on the configuration page rather than in the request. A name
that matches no entry is the other case and stays a bad request, because that
repair is in what was asked.

The response carries the code. It is the only response in this API that ever
does, it carries it once, and no later call to any route returns it again. #85
owns that property on the operator's side and this page owes it the same
sentence: a code that is not copied at this moment is gone, and the repair is a
new invitation rather than a lookup.

It also carries the link, which is the code with the configured address in
front of it and is therefore the same credential under the same rule rather
than a milder one. #50 put it here so that there is one place deciding what a
link looks like: the alternative was the configuration page composing it from
the setting and the code it had just been handed, which is a second such place
in a language the greppable rules do not read. The listing and the two routes
that read one invitation back carry neither the code nor the link.

The address it is written against is `PublicBaseUrl` from
[docs/configuration.md](configuration.md) and nothing else. Nothing about the
request reaches it, which is the whole of #50: a minting call carrying a forged
host would otherwise produce a link pointing at the caller's server, and the
invited person types their new password into it.

Where no address is configured, the response carries the refusal in place of
the link, naming the setting. The invitation is still minted and the code is
still handed over, because the address is only what the link is written
against: getting it wrong affects no record and no account, and a link to the
wrong host is worse than no link.

### `GET /Invites`

Lists invitations.

Returns records without codes and without hashes. Each carries the non-secret
invitation identifier, the state, the uses remaining, the expiry and what the
invitation created, which are the fields
[docs/personal-data.md](personal-data.md) already holds.

Each row also carries the grant the invitation was minted with, under `grant`.
It is the copy taken at minting rather than the configured template of the same
name, which is #61's rule and is what makes the row answer #94's question months
later: what this plugin applied, not what that name means today. It is `null` on
a record minted before the copy existed, which the store brings forward from its
first version with the grant absent rather than guessing one; such an invitation
creates no account.

`grant` is what was applied and never what the account carries now. Whether an
account still has it is a different fact, this plugin cannot read it, and #94 is
where the difference between the two is settled.

Each account the invitation created is an entry carrying its identifier and what
became of it, rather than an identifier on its own. #45 decided that a record
keeps its pointer at an account somebody has since deleted instead of clearing
it, and a pointer that renders exactly like a live account is the blank that
decision refuses. The three states are that the server has the account, that it
does not, and that it did not answer in a shape this plugin reads. The third is
its own value because reading an unanswered server as an empty set would report
every account this plugin created as deleted.

A route that returned codes here would make the arithmetic in
[docs/code-entropy.md](code-entropy.md) irrelevant, whatever the code length
is, and that page names this surface for exactly that reason.

### `GET /Invites/{id}`

Returns one invitation.

| Parameter | In | Required | What it is |
| --- | --- | --- | --- |
| `id` | path | yes | The non-secret invitation identifier, which is the field #32 requires so that a log line and a view can name the same invitation without either carrying the code |

The same shape as one row of the list, and under the same rule: no code, no
hash.

### `GET /Invites/Accounts/{accountId}`

Returns every invitation that claims to have created one account.

| Parameter | In | Required | What it is |
| --- | --- | --- | --- |
| `accountId` | path | yes | The server's own identifier for the account, as `AccountsProduced` on a listing row carries it |

The reverse of the listing, and #89's second direction. Each entry is the same
shape as one row of the list, under the same rule: no code, no hash.

An account no record claims answers `200` with an empty array rather than `404`.
That is the ordinary case rather than a failure: this plugin puts no mark on an
account, so an account it never created reads exactly like one an operator made
by hand, and on a real server most accounts are the second. It also keeps "this
plugin did not create it" distinguishable from a route that is not there.

More than one entry is possible and is not an error to the caller. Two records
claiming one account is a store disagreeing with itself, and it is the state an
operator would most want to see, so the route reports it rather than picking one
of the two. `docs/disaster-cases.md` is where that state comes from and
`ConsistencyReport` is the other reading of the same data.

### `POST /Invites/{id}/Revoke`

Revokes an invitation.

| Parameter | In | Required | What it is |
| --- | --- | --- | --- |
| `id` | path | yes | The non-secret invitation identifier |

Idempotent. Revoking twice is not an error and does not move the first
timestamp, which is #54. It affects no account the invitation already created,
because an operator stopping a link is not necessarily disowning the people who
already used it.

It is a `POST` to a named operation rather than a `DELETE` of the record, and
the difference is not cosmetic. #82 keeps deleting a record out of the operator's
hands, so a `DELETE` on this path would name an operation this plugin does not
offer, and the first person to try it would learn that from a status code.

### `POST /Invites/HashSecret/Rotate`

Says what rotating the keyed hash secret would cost, and rotates it when the
caller sends that cost back.

| Parameter | In | Required | What it is |
| --- | --- | --- | --- |
| invalidates | body | no | The count from an earlier answer of this route. Omitted, nothing is written and the answer is what a rotation would cost |

Two steps on one route, and the shape is what holds the promise rather than the
wording. The only way to rotate is to send back a number this route gave out, so
nothing can rotate without having put the cost in front of somebody first.

The answer carries the count, the sentence to show an operator, and whether the
secret was rotated. The count is every record the store holds, including the
ones that were already expired, spent or revoked, and the sentence says so:
narrowing it would mean a second routine deciding whether an invitation may be
honoured, which is the one place that judgement lives.

A confirmation naming a count the store no longer holds is refused with a
conflict and nothing is written. Nothing about such a request is malformed, so
it is not a bad request: the store changed between being read and being
confirmed, and the repair is to ask again.

Rotation is a revoke-everything operation. Every stored hash was computed under
the old secret, so no invitation minted before the rotation can be redeemed
again. That is what makes it the answer to a leaked key. It touches no account
and it removes no record, because the trail of what those invitations produced
is retention rather than rotation, and this API offers no route that deletes a
record.

The response carries no secret. There is no field it could be expressed in.

## One refusal, not four

Every unusable invitation produces the same response. Absent, expired, spent and
revoked are byte for byte identical to the caller, and
[docs/refusal-response.md](refusal-response.md) is where that is settled and
what the page says.

That constrains this document as much as it constrains the code. An API
reference listing four error responses would hand back, in prose, exactly the
oracle the code is careful not to give: a caller reading it would learn that
four states exist and that a real code can be told from an invented one. So
there is one refusal documented here and the reason it is one, and no table of
error cases beside it.

The difference does live somewhere. It lives in the attempt trail, whose fixed
set of outcomes is [docs/attempt-outcomes.md](attempt-outcomes.md), and it
reaches the operator through the administrator surface rather than the caller
through a response.

## What this API deliberately does not offer

No route returns a code after minting. The one response that carries a code is
the mint, once.

No route creates an account without a valid invitation. There is no registration
path here and there is no administrator route that creates an account either,
because an operator who wants to create one has the server's own user editor.

No route modifies an account that already exists. An invitation presented by
somebody already signed in creates nothing and changes nothing, which is #62,
and it is why there is no undo button anywhere in this API. #94 explains what
replaces one.

No route deletes an invitation record. Revocation is the operator's control, and
removal is retention rather than a button.

All four are promises made elsewhere and repeated here because the absence of a
route is invisible to somebody reading a list of routes. Each names the issue
that holds it, so this page is where a reader finds them and not where they are
decided.

## What is not settled here

The response bodies are named by their fields rather than given as a schema.
The reason written here was that the record type did not exist: #38 had not
defined its fields and was open with two of them waiting on decisions in #11.
That reason has expired in both halves. #38 is closed, `Invitation` carries its
fields, and every numbered decision in #11 has an answer.

What stands in its place is the promise at the top of this page rather than a
gap. Nothing about these shapes is promised before `1.0.0`, so a schema written
here would be a serialisation a caller reads as a commitment and this page is
free to move in the next version. Naming the fields says what a response holds
without saying it will keep holding it that way, which is the smaller claim and
the true one. That changes on the release the promise section is written to be
rewritten on.

The status codes are not listed. They follow from the outcomes above, and
committing to numbers before there is a controller means committing to what a
framework does rather than to what this plugin decides.

The rate limit from #31 applies to the redemption routes and its refusal is a
member of the outcome set, but what a caller sees when it fires is that issue's
to fix, not this one's.

## What no controller serves yet

Every heading above is a decision rather than something that answers. Which
ones is the list below, and it is read by the suite rather than by a person.
`ApiDocumentTests` requires it to hold exactly the routes this page names and
the plugin assembly does not register, so a route that starts answering while
its line is still here reds, and so does a heading with neither a controller nor
a line behind it.

- `GET /redeem/done`

A line leaves in the change that lands its route, and the last one to leave
empties the section rather than deleting it. This is not the heading list said
twice: a heading says the route is decided, a line here says nothing serves it,
and the day those two stop agreeing is the day this page starts lying about the
plugin.

## What is not claimed

Nothing here has been measured against a running server. That much is unchanged
and it is the sentence worth keeping.

The reason given for it was that none of these routes exists, and that stopped
being true without the sentence moving. Six of the seven headings above are
served by the assembly now, and the one that is not is the one the section
above lists, which the suite holds rather than a reader. So the claim is
narrowed rather than dropped: what has not been measured is this page against a
server, and what is now source rather than intention is whatever the register
above does not name. A sentence saying every heading is a decision about what
will be built was true when it was written and is not true now.

What holds this page against the source is `ApiDocumentTests`, and it reads
routes rather than files: the controllers are discovered through the same
feature provider the server uses to decide which types become endpoints, and
the method and template come off the action's own attributes. It bites against
a controller the suite declares for the purpose, carrying a route no heading
here names.

What no check reads is whether a documented parameter, response or refusal is
the one the route implements. A heading whose body has gone wrong passes every
route in this repository, and the agreement between the two halves of a section
is held by whoever writes it.
