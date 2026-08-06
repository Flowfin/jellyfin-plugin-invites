# The redemption flow

This is the whole path from a person following an invitation link to that person
holding an account, written before any of it is built. Nothing in this document
is implemented. Every state and every transition below is a promise held by an
issue, and the issue number is where the promise is kept.

It is written now for one reason. The interesting parts of this flow are not the
happy path, they are the eight ways it goes sideways, and a controller written
without them produces a state nobody handles. Two of those states are the ones
that cost something real: an invitation that was valid when the page was served
and gone when the form was posted, and an account that exists with no way to
sign into it.

## What is fixed and what is still open

Three things are not open, and this flow assumes them throughout. An invitation
is minted by an operator and reaches only the people that operator sends it to,
so there is no public registration path into this flow. An invitation can never
create an administrator and never widens an account that already exists. The
link carries neither a credential nor the account template it grants.

Four things this flow touches are not decided, and each one is marked where it
appears rather than answered here.

| Open question | Where it lands in this flow | Decision |
| --- | --- | --- |
| Single use or multi use, and which is the default | `Consuming` decrements a count; whether the default count is one decides whether an invitation is normally spent by its first redemption | #11 item 2 |
| Who sets the password, and whether to defer to a sign-in provider | `Credential` is drawn as the invited person choosing a password. A server running single sign-on wants a different terminal state, and that is a second flow rather than a branch inside this one | #11 item 4, #66 |
| Whether the setup form collects a contact address | Adds a field to `Form` and a row to the record. It changes no transition | #11 item 9, #34 |
| Whether an invited account expires with its invitation | Nothing in this flow. It is a scheduled task acting on an account this flow already created | #11 item 3, #68 |

The rest of the document is written against the planned path, which is the
password path, because that is what the plan assumes everywhere else. Where the
sign-in-provider answer would change a cell, the cell says so.

## The routes

Three routes carry the flow. The names below are provisional, and the API
document under M8 is where they are fixed:

| Route | Purpose |
| --- | --- |
| `GET /redeem/{code}` | Serves the setup page for a presented code, or the refusal |
| `POST /redeem/{code}` | Takes the answers, decides, and creates the account |
| `GET /redeem/done` | Shows the completion page |

The code is in the path rather than in a query string, because a query string
reaches more logs, more analytics and more referrer headers than a path does,
and neither is a place an invitation code should be. That is a smaller claim
than it sounds: the code reaches the server's own request log either way, and
what keeps it out of that log is #32 rather than the route shape.

## The states

| State | What is true here | What is left behind if the flow stops here |
| --- | --- | --- |
| `Start` | A person has followed a link. Nothing has been read | Nothing |
| `Checked` | The code has been looked up and found live | Nothing. No write has happened |
| `Form` | The setup page has been served, with an anti-forgery token bound to this request | Nothing. The token expires unused |
| `Posted` | The answers have arrived and the token has been validated | Nothing |
| `Validated` | The username is free and the password satisfies the rules, both checked before anything is created | Nothing |
| `Locked` | The invitation has been re-read under the store's lock and is still live | Nothing. The lock is released on every exit from this state |
| `Created` | The account exists and is disabled | A disabled account, and only if the flow dies here without unwinding |
| `Credentialed` | The password is set on the account | A disabled account with a credential |
| `Templated` | The account template has been applied | A disabled account, credentialed, with its permissions set |
| `Consumed` | The use has been recorded against the invitation and the account has been enabled | The finished account, which is the point |
| `Done` | The completion page has been shown | The finished account |
| `Refused` | The flow ended without an account | Nothing, in every branch that reaches it |

`Created` through `Consumed` are drawn as one sequence because they are one
transaction from the person's side, and the account is disabled for the whole of
it. That is what makes `Credentialed` safe to fail: an account nobody can sign
into is not left behind, because an account nobody can sign into is exactly what
a disabled account is. Enabling is the last write and it happens in the same
routine that records the use.

## The happy path

    Start -> Checked -> Form -> Posted -> Validated -> Locked
          -> Created -> Credentialed -> Templated -> Consumed -> Done

The second read of the invitation, at `Locked`, is not a repeat of the first. The
first decides whether to serve a page. The second decides whether to create an
account, and it is the only one of the two that anything is written against.

## The branches

One row per way the flow ends other than at `Done`. The response column is what
the person sees. The left-behind column is what exists afterwards that did not
exist before.

| # | Branch | From | Response | Left behind | Issue |
| --- | --- | --- | --- | --- | --- |
| 1 | Code absent, expired, spent or revoked at the first check | `Start` | The single indistinguishable refusal, byte for byte the same in all four cases | Nothing | #28, #55 |
| 2 | Live at the first check, gone by the post | `Locked` | The same single refusal as branch 1, and for the same reason: the person who reached this point cannot be told apart from an attacker who timed it | Nothing. The lock is released and no write has happened | #28, #40, #56 |
| 3 | Username already taken | `Posted` | The form again, with the username field marked and the answers the person already typed still in it | Nothing | #67, #62 |
| 4 | Password refused by the rules | `Posted` | The form again, naming which rule was missed and never echoing the password | Nothing. The rules were checked before anything was created, which is what makes this branch cheap | #76 |
| 5 | Account created, setting the credential failed | `Created` | The single refusal, plus the operator-facing reason recorded against the invitation's non-secret identifier | Nothing, after the unwind below | #66, #32 |
| 6 | Applying the template failed | `Credentialed` | As branch 5 | Nothing, after the unwind below | #64, #69 |
| 7 | Back button after completion | `Done` | The completion page again. The route reads no invitation, so there is nothing to refuse | Nothing beyond the account the completed flow already created | #55 |
| 8 | Form posted twice | `Posted` | The first post reaches `Done`. The second finds the anti-forgery token spent and is refused, and if it were not, the second read under the lock finds the use already recorded and refuses there | Nothing beyond the account the first post created | #78, #52, #56 |
| 9 | Post with no valid anti-forgery token | `Posted` | The single refusal | Nothing, and no use is consumed | #78 |
| 10 | The store is unreachable at any point | any | The single refusal | Nothing, or the unwind below if `Created` was reached | #40 |

Branches 5 and 6 share one unwind, and it is the only compensating action in the
flow. The account created at `Created` is disabled and has been since it was
created, so the unwind is a delete of an account that has never been usable and
that nobody but this routine has ever seen. It runs in the same routine, it is
recorded against the invitation's non-secret identifier, and if the delete
itself fails the account stays disabled, which is the state this flow refuses to
leave: not an account with no password, but an account nobody can sign into.

That is a claim about a routine that does not exist. What makes it checkable
later is that the unwind is in the same routine as the create, which is #56's
whole subject, rather than in a background sweep.

## No branch ends undefined

The eight branches the issue names are rows 1 through 8. Rows 9 and 10 are added
because leaving them out would leave two ways of arriving at an undefined state:
a post that never had a token, which #78 requires be refused without consuming a
use, and a store that is unreachable, which is the state every store operation
here can be in. Every row above names a response and a left-behind, and the
left-behind is `Nothing` in every row that does not reach `Consumed`.

## What this flow does not cover

Sending the link. The operator copies it and sends it themselves, and nothing in
this flow knows how it travelled.

Signing the person in at the end. The flow ends at the completion page and the
person is sent to the login page, because signing somebody in from an
unauthenticated route means minting a session from a bearer credential that has
just been spent, and that is a second decision rather than a convenience. Where
it is wanted it is a transition out of `Done` and it changes nothing above it.

Anything the account does afterwards. Expiry, uninstall and what an operator can
undo are M6 and M9.
