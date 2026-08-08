# The refusal a person sees

An invitation that is not honoured produces one page. The page is the same page
whether the code was never real, expired last week, was spent this morning or
was revoked a minute ago, and it is the same page whether the person following
the link is the one the operator invited or somebody who guessed.

That is a security property first. An endpoint that answers differently for a
code that exists and a code that does not is an endpoint a stranger can ask
whether a code exists, one guess at a time, and get an answer. It is also, if
the page is written carelessly, a dead end that tells an invited person nothing
and turns into a message to the operator. Both halves are settled here, because
they are settled in the same sentence.

Nothing serves this page today. The page and the post are #74, the decision
behind the refusal is #56, and the route-level comparison is #107. This document
is what those are built against, and it is a decision rather than a description
of behaviour.

## The wording

The page says this and nothing more:

> **This invitation link cannot be used**
>
> The link you followed is no longer usable. This can happen for several
> reasons, and this page cannot tell you which one applies.
>
> If you were expecting to set up an account, contact the person who sent you
> the link. They can issue a new invitation.

Three things are deliberate about it.

It does not say why. Not because the reason is secret from the person, but
because the page cannot tell the person apart from anybody else who has the
link, and any wording that narrows the reason narrows it for both of them
equally.

It does not say whether the code was ever valid. "No such invitation" and "this
invitation was spent" are the two answers a stranger most wants, and a page that
distinguishes them hands over the more useful one for free: a code that was once
real tells an attacker the format is right and the guessing is worth continuing.

It says the one useful thing. The person's route back is the human who sent them
the link, and nothing on the server can help them. A page that says only that
the link is invalid leaves them with nowhere to go, which is how a correct
refusal becomes a support thread.

## The cases it serves

Every case below produces this page, byte for byte identical. The list is one
somebody adds to rather than a paragraph somebody reimplements, and adding a
refusal without adding it here is how the set quietly grows a distinguishable
member.

| Case | Owned by | Trail outcome |
| --- | --- | --- |
| The presented code matched no record | #56 | `NoSuchInvitation` |
| The record's expiry had passed | #51, #56 | `Expired` |
| The record had no uses left | #52, #55, #56 | `Spent` |
| The operator had revoked the record | #54, #56 | `Revoked` |
| A rate limit or lockout refused the attempt | #31 | `RefusedByRateLimit` |
| A ceiling on what the plugin may create refused it | #33 | `RefusedByCeiling` |

The outcome names are the ones in
[docs/attempt-outcomes.md](attempt-outcomes.md) rather than a second vocabulary
for the same states.

The last two are in the set for a reason worth keeping in front of whoever
implements them, because both look like cases that deserve their own message. A
limiter that answers "too many attempts" is an oracle: a stranger hits the limit
deliberately, learns where the boundary is, and learns from the difference which
of their guesses reached a lookup. A ceiling refusal that says the server has
created too many accounts today tells a stranger something true about the server
they had no other way to learn, and it does it while refusing them.

## The two refusals that are not in the set

`RefusedByAntiForgery` and `RefusedByValidation` produce their own responses and
must not be folded into this page.

Both happen after the invitation was already honoured enough to show the person
a form. A password that is too short, a confirmation that does not match or a
username already taken are the person's own answers being wrong, and a page that
responds to those with "this link cannot be used" sends somebody away who was
one field from an account. Neither reveals anything about the code, because by
that point the code has already been accepted.

The username collision does disclose that a username exists on this server. That
is a disclosure with no way around it if the person is to choose their own name,
it is #67's, and it is already written down under what is not defended rather
than hidden here.

## What "identical" covers

Comparing the visible text is not the property. The list below is what the
response is compared on, and it is the list a test asserts rather than a
description of one:

- the status code
- the response body, byte for byte
- the `Content-Type` and the content length
- the security headers the page carries, which are #78's: the content security
  policy, the framing header and the referrer policy
- the presence and value of every other header the plugin sets

Nothing in the response may be interpolated from the case. A body assembled from
a per-case string differs by its length even when the text is identical in a
reader's eye, and a length is the cheapest thing in the response to measure. The
page is one served resource, not a template with a reason in it.

## Timing, and what a test can honestly say about it

#77 asks that the timing not vary with whether the code exists. That clause
cannot be discharged by comparing two recorded responses, and a test that
measures durations on a shared runner measures the runner.

What a test can assert is that the same work is done either way: a well-formed
code is canonicalised, hashed and looked up whether or not it matches, and the
comparison against a stored value is the constant-time one from #29 rather than
an equality that returns on the first differing byte. Those are properties of
the routine in #56 and are assertable without a stopwatch.

What is left after that is a claim rather than a measurement, and it stays
written as one. The plugin does not defend against an attacker who can measure
the difference between a store hit and a store miss through the noise of a media
server, and no test in this repository will say otherwise.

## Where the difference does live

The operator sees what the person cannot. Every case above appends its own
outcome to the attempt trail, which is #43, and the operator's view of it is
#89. An invitation presented eleven times and never redeemed is the row worth
looking at, and it exists precisely because the page in front of the stranger
said nothing.

## One assertion, not four

The byte-for-byte comparison is a done-condition clause in this issue, in #55,
in #31 and in #107. Written four times it is four tests that drift apart, and
the first to be relaxed is the one nobody remembers is load-bearing. The likely
split is one test comparing bodies and another comparing bodies and headers,
both green, while the responses differ by a header only one of them looked at.
That is the oracle all four exist to close, reintroduced by the way they were
tested.

The comparison lives once, at the route level, which is #107, because that is
the only one of the four that sees the response the server actually sends. The
other three add their case to it. This document is where the case list and the
compared-on list are kept, so adding a case is an edit here and a row there
rather than a new test.

## The page is served by this plugin and nothing else

The presentation rules for the setup page apply to this one unchanged: nothing
loaded from another host, no third-party script, no font service, no analytics.
They are in [docs/setup-never-asks.md](setup-never-asks.md) and are not repeated
here.

The page carries no code, no invitation identifier, and nothing that varies
between the cases. It may sit in a browser history on a shared machine, which is
the same reason the completion page in #79 carries nothing either.

## What is settled and what is not

Settled here: the wording, the case list, what identical covers, which two
refusals are outside the set, and where the comparison lives.

Not settled here, and not by anybody yet: the exact security header values,
which are #78's, and the status code, which is #74's to choose when the route
exists. A status code is part of what the responses are compared on, so the
choice binds all six cases at once; it is named here as owed rather than picked
from a document with no route behind it.
