# The redemption flow

This is the whole path from a person following an invitation link to that person
holding an account, written before any of it was built. Every state and every
transition below is a promise held by an issue, and the issue number is where
the promise is kept.

THIS PARAGRAPH SAID NOTHING IN THIS DOCUMENT IS IMPLEMENTED, AND FOUR OF THE
ROUTINES THE STATES BELOW NAME ARE IN THE TREE. The decision that judges a
presented code, the limiter that bounds attempts at it, the rules a password is
refused by, and the routine that creates the account in the one safe order:

    git grep -ln 'public static class RedemptionDecision\|public sealed class AttemptLimiter\|public static class PasswordRules\|public static class AccountCreation' origin/master -- 'Jellyfin.Plugin.Invites/*.cs'
    origin/master:Jellyfin.Plugin.Invites/Accounts/AccountCreation.cs
    origin/master:Jellyfin.Plugin.Invites/Redemption/AttemptLimiter.cs
    origin/master:Jellyfin.Plugin.Invites/Redemption/RedemptionDecision.cs
    origin/master:Jellyfin.Plugin.Invites/Setup/PasswordRules.cs

THAT PARAGRAPH THEN SAID NONE OF THEM HAS A CALLER, BECAUSE THE POST THIS FLOW
TURNS ON DOES NOT EXIST. It exists, and three of the four have a caller:

    git grep -nE '\[Http(Get|Post)' -- Jellyfin.Plugin.Invites/Controllers/RedeemController.cs
    Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:148:    [HttpGet("{code}")]
    Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:210:    [HttpPost("{code}")]

The post asks the limiter, asks the decision and calls the creation routine, and
it reaches the password rules too, through the judgement it makes about the
answers before it looks at any code:

    git grep -n 'PasswordRules.WhyRefused(submission.Password)' -- Jellyfin.Plugin.Invites/Controllers/SetupAnswers.cs
    Jellyfin.Plugin.Invites/Controllers/SetupAnswers.cs:117:        if (PasswordRules.WhyRefused(submission.Password) is not null)

THIS PARAGRAPH SAID THE FOURTH IS REACHED BY NOTHING. All four have a caller.

So this document is no longer a page of promises, and the states below are worth
reading with that in mind. What a reader should not take from the landing is that
the flow is walked end to end. Which transitions act and which are still promises
is written at the branch table rather than counted here, and three of the states
below name work no route does: `Done` is #79 and is served by nothing, so a
finished redemption ends at the server's own not-found page. The anti-forgery
token in `Form` and `Posted` was named here as one of them and is not one any
more; #78 landed it, and `AntiForgeryTests` drives both states. The `Validated` state is reached: the post
judges the answers it was sent before it judges the code, and the one answer it
cannot fully judge is the username, whose shape it refuses against the server's
own expression and whose collision with an existing account it cannot see. That
half is #67's, and branch 3 of the table below is the branch it leaves standing.

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

Four things this flow touches were open when it was drawn and all four are
answered in #11. The table below is what each answer does to the flow, and it
replaces the list of open questions that stood here.

| Question | What the answer is | What it does to this flow |
| --- | --- | --- |
| Single use or multi use, and which is the default | Redeemable once, item 2 | `Consuming` still decrements a count and the count still exists, and the ordinary invitation is spent by its first redemption |
| Who sets the password, and whether to defer to a sign-in provider | The invited person sets it, and on a server running single sign-on the flow defers to the identity provider, item 4 | `Credential` stays the invited person choosing a password. The sign-on server keeps its own terminal state, which is a second flow rather than a branch inside this one, and is #66 |
| Whether the setup form collects a contact address | None is collected, item 9 | `Form` gains no field and the record gains no row. It changes no transition, and it is not a gap for a later change to fill |
| Whether an invited account expires with its invitation | It does not, and where an operator asks for a lapse the account is deactivated rather than deleted, off by default, item 3 | Nothing in this flow. It is a scheduled task acting on an account this flow already created, and it is #68 |

The rest of the document is written against the password path, which is what the
plan assumes everywhere else. Where the sign-in-provider flow would change a
cell, the cell says so.

## The routes

Three routes carry the flow. The names below are fixed rather than provisional,
and [docs/api.md](api.md) is where each one has its heading, its parameters and
its responses:

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
| 8 | Form posted twice | `Posted` | The first post reaches `Done`. The second is refused by the second read under the lock, which finds the use already recorded. The anti-forgery token is not what refuses it, and the row said it was: the token is not spent on use | Nothing beyond the account the first post created | #52, #56 |
| 9 | Post with no valid anti-forgery token | `Posted` | The bad request this route already answers a post it read nothing out of with, under the same five headers. Not the single refusal, which the row said and which `docs/refusal-response.md` names this case as being outside of | Nothing, and no use is consumed | #78 |
| 10 | The store is unreachable at any point | any | The single refusal | Nothing, or the unwind below if `Created` was reached | #40 |

Branches 5 and 6 share one unwind, and it is the only compensating action in the
flow. The account created at `Created` is disabled and has been since it was
created, so the unwind is a delete of an account that has never been usable and
that nobody but this routine has ever seen. It runs in the same routine, it is
recorded against the invitation's non-secret identifier, and if the delete
itself fails the account stays disabled, which is the state this flow refuses to
leave: not an account with no password, but an account nobody can sign into.

THAT PARAGRAPH SAID IT IS A CLAIM ABOUT A ROUTINE THAT DOES NOT EXIST. The
routine exists and it does none of the three things the paragraph promises. It
landed under #398:

    git log --oneline --diff-filter=A -1 origin/master -- Jellyfin.Plugin.Invites/Accounts/AccountCreation.cs
    2b5b156 Create the account an invitation redeems into, for #398

It does not unwind, and it says so on itself rather than leaving a reader to
find out:

    git grep -n 'Nothing is undone' d0cebf36fdb3c6cc1f26b697de0a09bcf93d8334 -- Jellyfin.Plugin.Invites/Accounts/AccountCreation.cs
    d0cebf36fdb3c6cc1f26b697de0a09bcf93d8334:Jellyfin.Plugin.Invites/Accounts/AccountCreation.cs:63:/// <b>What a failure part-way leaves.</b> Nothing is undone. A refusal from the

It does not disable the account either. The seam it writes through declares
three acts and none of them is a delete or a disable:

    git grep -nE '^    Task' d0cebf36fdb3c6cc1f26b697de0a09bcf93d8334 -- Jellyfin.Plugin.Invites/Accounts/IServerAccountWrites.cs
    d0cebf36fdb3c6cc1f26b697de0a09bcf93d8334:Jellyfin.Plugin.Invites/Accounts/IServerAccountWrites.cs:53:    Task<Guid> CreateAccountAsync(string username);
    d0cebf36fdb3c6cc1f26b697de0a09bcf93d8334:Jellyfin.Plugin.Invites/Accounts/IServerAccountWrites.cs:61:    Task SetCredentialAsync(Guid account, string password);
    d0cebf36fdb3c6cc1f26b697de0a09bcf93d8334:Jellyfin.Plugin.Invites/Accounts/IServerAccountWrites.cs:75:    Task ApplyTemplateAsync(Guid account, AccountTemplate template);

THE DELETE THIS UNWIND NEEDS IS REFUSED RATHER THAN MERELY ABSENT, which is the
part that changes what somebody building these two branches should do. Widening
the seam by a fourth act turns a guard red:

    git grep -n 'public void TheWriteSeamDeclaresOnlyTheThreeActsARedemptionNeeds' d0cebf36fdb3c6cc1f26b697de0a09bcf93d8334 -- Jellyfin.Plugin.Invites.Tests/AccountsAreNeverWrittenTests.cs
    d0cebf36fdb3c6cc1f26b697de0a09bcf93d8334:Jellyfin.Plugin.Invites.Tests/AccountsAreNeverWrittenTests.cs:190:    public void TheWriteSeamDeclaresOnlyTheThreeActsARedemptionNeeds()

Every quotation in this section names a commit rather than `origin/master`, and
it named the branch until #439. A reference against a moving ref is a reference
to whatever that ref holds when the check runs, so the same bytes are green on a
branch and red on the mainline the moment a merge moves a line above the target.
None of these five had gone stale; they were converted because the next change to
either of those two files would have made them stale on the mainline behind a
green pull request, which is what happened twice on two other pages.

That guard is #91's answer read from this side: a plugin that can delete an
account is a larger power than a redemption needs, and it was weighed and
declined rather than not yet built. So the two rows above and this paragraph
describe a compensating action the tree refuses, and what branches 5 and 6
actually leave today is what `AccountCreation` names - an account with no
credential and the server's own default policy, or one with a credential and
that same policy.

WHICH OF THE TWO IS WRONG IS NOT DECIDED HERE. Either this flow gives up its
only compensating action and says what the residual is, or the refusal is
revisited with the delete held to the narrowest surface that serves it. The
first is what the tree already does and the second is a change to #91's answer.
The direction of a residual left by a failure part-way is #53's clause. The
routine is written now, and what it does is the first of the two: it leaves the
account where the failure left it, answers the person with the single refusal,
and keeps the use taken. So the flow gives up its compensating action in
practice, this paragraph is what says so, and revisiting #91's answer is still
the other way out rather than a settled one.

What was already right and stays right is where the unwind would live: in the
same routine as the create rather than in a background sweep.

## Which way a death falls

The branches above are failures the route ANSWERS. This is the one it does not:
the process stops between two writes and nobody is answered at all. #53 asks for
the direction of that residual to be stated rather than discovered, and the
direction is chosen rather than incidental.

**The use is written to disk before the server is asked to create anything.** The
reservation reads the records, asks for the verdict and writes the decremented
count, all inside one monitor, and only then does the route call the creation
routine:

    git grep -n 'var reservation = _operations.Reserve(code);' -- Jellyfin.Plugin.Invites/Controllers/RedeemController.cs
    Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:264:        var reservation = _operations.Reserve(code);

That is the first of the two answers #53 offers, writing the intent before the
account exists, and it is chosen for the reason that issue gives: prefer the
direction that loses an invitation over the one that grants an extra account. A
lost invitation is an operator minting a second one. An extra account is a
stranger on the server.

**So a death anywhere after the reservation costs the invitation.** There are two
places it can fall and they leave different things behind.

| Death at | On the server | In the store | What an operator sees |
| --- | --- | --- | --- |
| After the use is written, before the account exists | Nothing | The use is spent and the record claims no account | An invitation that is spent and produced nothing. The remedy is a fresh mint |
| After the account exists, before the record claims it | The account | The use is spent and the record claims no account | The same record, and an account on the server that no invitation names |

The second row is the window this issue is about, and what it does NOT leave is
the failure that window is named for: the record is already spent when the
account comes into existence, so a restart cannot honour the code a second time
and the invitation cannot produce two accounts. That is asserted at the route
rather than argued here, in
`ACrashLosesTheInvitationTests.ARestartAfterTheAccountExistsCannotProduceASecondAccount`.

**The account the second row leaves is not deleted and not disabled**, which is
the compensating action the section above says this flow gives up. What finds it
is the consistency report, which compares the accounts the store claims against
the accounts the server has; an account no invitation names is outside what that
comparison reads, so the operator's route to it is the server's own user list
rather than anything this plugin prints. That gap is real and it is the price of
the direction chosen here.

**What is not covered by any of this.** A machine losing power part-way through
the store's own write is a question about that write rather than about this
order, and it is where the store's atomic replace is argued. Nothing here
measures it, and no process was killed to produce the assertions above: what
they read is the state on disk at the moment the server is written to, and what
a component reading that disk afterwards decides.

## No branch ends undefined

The eight branches the issue names are rows 1 through 8. Rows 9 and 10 are added
because leaving them out would leave two ways of arriving at an undefined state:
a post that never had a token, which #78 requires be refused without consuming a
use, and a store that is unreachable, which is the state every store operation
here can be in. Every row above names a response and a left-behind, and the
left-behind is `Nothing` in every row that does not reach `Consumed`.

TWO OF THOSE ROWS SAID SOMETHING ABOUT THE TOKEN THAT THE TOKEN DOES NOT DO, and
both are corrected in the table rather than underneath it. Row 8 said a second
post finds the token spent. It does not: the token is a value in a cookie and a
value on the form, compared with each other, and nothing marks one as used.
Spending it would need a register of live tokens, which is state this plugin
keeps none of, and it would refuse the person who was sent back to the form by a
refused password and submitted it again. What refuses a second post is the use
count under the lock, which the row already named as the fallback and which is
the whole of it. Row 9 said the refusal is the single indistinguishable one.
`docs/refusal-response.md` names this case as one of exactly two that are outside
that set, and the route follows that page: a post with no good token is answered
out of the request alone, before any code is read, so it is the same bad request
a post missing a field gets and it discloses nothing that answer did not already
disclose.

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
