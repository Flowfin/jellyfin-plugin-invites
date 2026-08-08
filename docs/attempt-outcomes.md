# The attempt outcomes

Every redemption attempt appends exactly one entry to the trail, and the entry's
outcome is one value from the fixed set below. Nothing typed by anyone reaches
that field.

The set exists because the response does not carry the difference. A code that is
absent, expired, spent or revoked produces one response, byte for byte the same
in all four cases, so that the endpoint a stranger can reach is not an oracle
about which codes exist. The operator still has to be able to tell those four
apart, and this trail is the only place where they are told apart. That makes the
set a requirement of the refusals rather than a convenience for the view that
renders them.

Nothing here is enforced. There is no redemption path to produce an outcome and
no trail to append to:

    git grep -nE 'ControllerBase|ApiController|HttpGet|HttpPost' -- '*.cs'
    exit=1

`docs/personal-data.md` already names this set as what the outcome field holds,
so the set is written here rather than left as a reference to a list nobody had
made.

## The set

| Outcome | What it means | Produced by |
| --- | --- | --- |
| `Accepted` | The code was honoured and an account was created. | #56 |
| `NoSuchInvitation` | The presented code matched no record. The entry carries no invitation identifier, because there is none to carry. | #56 |
| `Expired` | The record was found and its expiry had passed at the single clock reading this redemption took. | #51, #56 |
| `Spent` | The record was found and had no uses left. | #52, #55, #56 |
| `Revoked` | The record was found and the operator had revoked it. | #54, #56 |
| `RefusedByRateLimit` | The attempt was refused before or at the lookup because a limit was reached. | #31 |
| `RefusedByCeiling` | The redemption was refused because a ceiling on what the plugin may create was reached. | #33 |
| `RefusedByAntiForgery` | The submission failed the cross-site check. | #78 |
| `RefusedByValidation` | The answers on the form did not validate on the server. | #75, #76 |

The last four are the reason this is written down now. Each is introduced by a
different issue, and an issue that adds a refusal without adding its member here
produces an attempt with no entry, which fails the one-entry-per-attempt property
quietly rather than loudly. Adding a refusal means adding a member.

The names are the shape rather than the spelling. Whichever type carries them,
the set is closed and the field is never free text, which is what lets the
personal-data inventory say what the field holds without knowing what happened.

## What the set is not

It is not the response. The first five outcomes above split into one response for
`Accepted` and one identical response for the other four, and #77 owns that
comparison. A ceiling refusal and a rate-limit refusal produce that same
identical response too, for the same reason: a distinguishable refusal tells a
stranger something true about the server they had no other way to learn.

So the trail is where the operator learns why, and the page is where the person
learns nothing. That asymmetry is the design, and an implementation that makes
the page more helpful has broken it.

## The bound, in two parts

A single ring of entries with the oldest dropped first is the obvious bound and
it is the wrong one here. An unbounded trail on an endpoint a stranger can hammer
is a disk-filling attack; one oldest-first ring on the same endpoint is a
history-erasing attack, where a few thousand failures push out every success the
operator would have looked for and leave a full trail that says nothing.

Successes are kept and are already bounded by other means. Nothing a stranger
does creates a success entry without also creating an account, and the number of
accounts the plugin may create and the number of live invitations are both
ceilings under #33. Bounding successes separately would add no defence and would
lose the one thing the trail exists to answer.

Failures are bounded and dropped oldest first. These are the entries a stranger
can produce for free, and the most recent few hundred are worth more than the
first few thousand, because what an operator wants to see is that something is
being hammered now.

The number is not chosen here. A failure bound below the rate limiter's threshold
means the trail cannot show the limiter working, so the count belongs with the
limits in #31, which take theirs from the entropy calculation in #28. Both are
open.

When failures are dropped, the trail says so with the count. A trail that
silently forgot is worse than one that admits it did, and the admission costs one
entry.

## What is settled and what is not

The source address is not kept. #43 made the field conditional on the
personal-data inventory allowing it, and the inventory does not: it recommends
that #31 holds an address in memory for as long as its window and no longer, and
that the trail does not carry it. The reasoning is in
[docs/personal-data.md](personal-data.md) under the three fields that failed, and
this page does not restate it.

Whether an attempt refused by the rate limiter appends an entry at all is not
settled, and it matters more than it looks. If the limiter refuses without
appending, the trail cannot show the thing an operator most wants to see, which
is that an invitation was hammered. If it appends on every refusal, the limiter
stops bounding the writes the trail's own bound exists to bound. #31 and this
issue need one answer between them rather than two.

Retention is a parameter with no value. How long entries are kept beyond the
bound is decision 8 in #11 and has no answer.
