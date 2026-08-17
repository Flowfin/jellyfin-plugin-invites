# The API

Somebody will call these routes from a script. A plugin that mints invitations
is a plugin an operator wants to drive from whatever already runs their server,
and that happens whether or not anything is written down. The only choice is
whether they are working from this page or from a browser's network tab.

So the routes were fixed here before they existed, rather than described
afterwards. Four of the seven are served now:

    $ git grep -lE 'ControllerBase' -- 'Jellyfin.Plugin.Invites/*.cs'
    Jellyfin.Plugin.Invites/Controllers/InvitesController.cs

The four administrator operations landed under #82. The three redemption routes
are #74 and #75 and are still what this page is built against rather than a
description of anything. Which is which is not read off this paragraph: the
register at the end of the page is the list, and the suite holds it against the
assembly.

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
reading before anybody calls this route. #74 landed the page and nothing else:
the same bytes are served for every code, no invitation is looked up, and the
response carries no anti-forgery token. So the refusal half is undelivered, and
the route discloses nothing about a code because it does not read one. The
lookup by code that both halves need does not exist in this plugin; #75 and #77
own the refusal and #78 owns the token.

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
| username | form | yes | The name the account will have. Refused if the server would reject it or if it collides, which is #67 |
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

Four operations and no more, from #82. Every one of them requires an
administrator, and #83 is where that is asserted against the assembly rather
than promised here.

### `POST /Invites`

Mints one invitation and returns the code exactly once.

| Parameter | In | Required | What it is |
| --- | --- | --- | --- |
| template | body | yes | Which grant the invitation carries. #61 copies the template into the invitation rather than referencing it by name |
| validity | body | no | How long the link lasts. Bounded by the maximum in [docs/expiry-rules.md](expiry-rules.md), and an invitation with no expiry at all is refused |
| uses | body | no | How many accounts the invitation is good for. Refused at zero and above the ceiling, which is #52 and #33 |

The response carries the code. It is the only response in this API that ever
does, it carries it once, and no later call to any route returns it again. #85
owns that property on the operator's side and this page owes it the same
sentence: a code that is not copied at this moment is gone, and the repair is a
new invitation rather than a lookup.

### `GET /Invites`

Lists invitations.

Returns records without codes and without hashes. Each carries the non-secret
invitation identifier, the state, the uses remaining, the expiry and what the
invitation created, which are the fields
[docs/personal-data.md](personal-data.md) already holds.

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
Writing a schema before the record type exists would fix a serialisation of
fields #38 has not defined, and #38 is open with two of its fields waiting on
decisions in #11.

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

- `POST /redeem/{code}`
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
being true without the sentence moving. Five of the seven headings above are
served by the assembly now, and the two that are not are the two the section
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
