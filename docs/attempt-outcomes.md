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

There is no redemption path to produce an outcome and no trail to append to. The
command this rested on asked whether the tree has any route at all, and it
stopped answering the question once the administrator routes and the setup page
landed, so it is corrected rather than dropped. What holds the claim up is that
the routine deciding a redemption has no caller:

    git grep -n 'Decide(' -- 'Jellyfin.Plugin.Invites/*.cs' ':!*RedemptionDecision.cs'
    exit=1

THIS PARAGRAPH SAID NOTHING HERE IS ENFORCED AND ONE PART OF IT NOW IS. Four of
the nine names below are members of the type the decision routine returns, and
`AttemptOutcomeSetTests.EveryVerdictTheDecisionReachesHasARowOnThisPage` reads
the table on this page and requires each of the four to have a row spelled the
same way. A refusal added to that type without a row added here is refused
rather than noticed, which is the failure the paragraph under the table
describes. What is still held by nothing is the rest: `Accepted`, the four
refusals no type carries yet, the bound, the drop entry, and the
one-entry-per-attempt property, none of which has an implementation to judge.

`docs/personal-data.md` already names this set as what the outcome field holds,
so the set is written here rather than left as a reference to a list nobody had
made.

## The set

| Outcome                | What it means                                                                                                     | Produced by |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------- | ----------- |
| `Accepted`             | The code was honoured and an account was created.                                                                 | #74         |
| `NoSuchInvitation`     | The presented code matched no record. The entry carries no invitation identifier, because there is none to carry. | #74         |
| `Expired`              | The record was found and its expiry had passed at the single clock reading this redemption took.                  | #51, #74    |
| `Spent`                | The record was found and had no uses left.                                                                        | #55, #74    |
| `Revoked`              | The record was found and the operator had revoked it.                                                             | #54, #74    |
| `RefusedByRateLimit`   | The attempt was refused before or at the lookup because a limit was reached.                                      | #31         |
| `RefusedByCeiling`     | The redemption was refused because a ceiling on what the plugin may create was reached.                           | #33         |
| `RefusedByAntiForgery` | The submission failed the cross-site check.                                                                       | #78         |
| `RefusedByValidation`  | The answers on the form did not validate on the server.                                                           | #75, #76    |

The last four are the reason this is written down now. Each is introduced by a
different issue, and an issue that adds a refusal without adding its member here
produces an attempt with no entry, which fails the one-entry-per-attempt property
quietly rather than loudly. Adding a refusal means adding a member.

The names are the shape rather than the spelling. Whichever type carries them,
the set is closed and the field is never free text, which is what lets the
personal-data inventory say what the field holds without knowing what happened.

## The column pointed at finished work, and this is where it points now

`Produced by` named #56 on five rows and #52 on one, and both are closed. A
reader following the column to find where an outcome lands arrived at work that
was done, which is this page's own subject happening on this page. The tracker
is not a thing this tree can re-run, so the reading is quoted rather than
checked by anything here:

    gh issue view 56 --repo Flowfin/jellyfin-plugin-invites --json state --jq .state
    CLOSED
    gh issue view 52 --repo Flowfin/jellyfin-plugin-invites --json state --jq .state
    CLOSED

What #56 built is in the tree and is not what the column was about. It put the
whole redemption decision in one routine, and that routine reaches five states:

    git grep -nE '^    (NoSuchInvitation|Revoked|Expired|Spent|Honoured)' -- Jellyfin.Plugin.Invites/Redemption/RedemptionOutcome.cs
    Jellyfin.Plugin.Invites/Redemption/RedemptionOutcome.cs:32:    NoSuchInvitation = 0,
    Jellyfin.Plugin.Invites/Redemption/RedemptionOutcome.cs:37:    Revoked = 1,
    Jellyfin.Plugin.Invites/Redemption/RedemptionOutcome.cs:42:    Expired = 2,
    Jellyfin.Plugin.Invites/Redemption/RedemptionOutcome.cs:47:    Spent = 3,
    Jellyfin.Plugin.Invites/Redemption/RedemptionOutcome.cs:52:    Honoured = 4,

What no issue has built is the caller that turns one of those into an entry, and
that is the post on the redemption route, #74. So the column names it on the
five rows a decision reaches, and #51, #54 and #55 stay beside it because each
of those still owns the rule its row is about.

`Accepted` and `Honoured` are not two spellings of one state, and moving the
column does not make them one. The decision's `Honoured` says the invitation may
produce an account; this page's `Accepted` says one was created. A redemption
that was honoured and then failed to create an account is the difference between
them, and it is a state the trail has to be able to record. That is why the
assertion above requires a row for the four refusals the decision reaches and
not for the fifth member.

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

**The failure bound is one thousand entries.** This paragraph said the number was
not chosen here and that it belonged with the limits in #31, on the ground that a
bound below the limiter's threshold leaves the trail unable to show the limiter
working. Those limits are chosen now, so the ground has moved and the number
follows from them:

    git grep -n 'Per source address, twenty attempts an hour' origin/master -- docs/rate-limit.md
    origin/master:docs/rate-limit.md:133:**Per source address, twenty attempts an hour. Across all sources, ten attempts

    $ awk 'BEGIN{ bound=1000; perAddress=20; global=10;
        printf "bound / per-address limit an hour = %d sources at their ceiling, held whole\n", bound/perAddress;
        printf "bound / global limit a second     = %d seconds of history at saturation\n", bound/global; }'
    bound / per-address limit an hour = 50 sources at their ceiling, held whole
    bound / global limit a second     = 100 seconds of history at saturation

Fifty times the per-address threshold is the figure that matters. One source
running all the way to its ceiling fills a fiftieth of the trail, so it is
visible whole and it cannot push the rest out, which is the constraint this
paragraph used to state without a number to satisfy it.

The second row is the honest limit of the whole idea rather than an argument for
a larger bound. At the global ceiling the trail holds a hundred seconds, and no
bound makes a trail a record of sustained saturation. Raising it to ten thousand
buys a thousand seconds and costs ten times the file for a case the entry below
already covers.

When failures are dropped, the trail says so with the count. A trail that
silently forgot is worse than one that admits it did, and the admission costs one
entry.

What an entry costs in bytes is not claimed. There is no entry type, so there is
nothing to measure, and the bound above is a count rather than a size.

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

Retention is a parameter with no value, and decision 8 in #11 is not what fills
it. That decision has an answer now, ninety days, and it is about how long a
spent or expired invitation record is kept.
[docs/personal-data.md](personal-data.md#retention) holds it, and says in place
that the answer does not set the trail's bound, which is a separate quantity
nothing has chosen. So this page still has no number and the reason has moved:
it is not that the decision is open, it is that the decision was about the other
parameter.
