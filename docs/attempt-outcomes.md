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

THIS PARAGRAPH SAID THERE IS NO TRAIL TO APPEND TO, AND THERE IS ONE NOW. The
trail, its entry and its bound are in the tree as
`Jellyfin.Plugin.Invites/Attempts/`, landed under #43.

IT THEN SAID THERE IS NO REDEMPTION PATH TO PRODUCE AN OUTCOME, BECAUSE THE
ROUTINE DECIDING A REDEMPTION HAS NO CALLER. It has one:

    git grep -n 'Decide(' -- 'Jellyfin.Plugin.Invites/*.cs' ':!*RedemptionDecision.cs'
    exit=0

    git grep -n 'RedemptionDecision.Decide' -- 'Jellyfin.Plugin.Invites/*.cs'
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:628:            var verdict = RedemptionDecision.Decide(presented, hash, contents.Invitations, now);

So an outcome is produced on every submission to the redemption post, and nothing
appends to a trail on a running server. Both halves of this page exist and
neither reaches the other, which is the state to read carefully: the gap is one
call rather than two components, and it is #43.

THIS PARAGRAPH SAID NOTHING HERE IS ENFORCED AND MOST OF IT NOW IS. Every name
in the table below is a member of `AttemptOutcome`, and
`AttemptOutcomeSetTests` reads the table on this page and compares the two sets
in both directions, so a member with no row and a row with no member are both
refused. Four of the names are also members of the type the decision routine
returns and `EveryVerdictTheDecisionReachesHasARowOnThisPage` holds those, which
is the narrower comparison that was here before. The bound, the drop order and
the drop entry are held by `AttemptTrailTests` against the implementation rather
than by this page. What is still held by nothing is the one-entry-per-attempt
property, which needs an attempt, and the persistence, which nothing in this
tree writes.

`docs/personal-data.md` already names this set as what the outcome field holds,
so the set is written here rather than left as a reference to a list nobody had
made.

## The set

| Outcome                | What it means                                                                                                     | Produced by |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------- | ----------- |
| `Accepted`             | The code was honoured and an account was created.                                                                 | #43         |
| `NoSuchInvitation`     | The presented code matched no record. The entry carries no invitation identifier, because there is none to carry. | #43         |
| `Expired`              | The record was found and its expiry had passed at the single clock reading this redemption took.                  | #43, #51    |
| `Spent`                | The record was found and had no uses left.                                                                        | #43, #55    |
| `Revoked`              | The record was found and the operator had revoked it.                                                             | #43, #54    |
| `RefusedByRateLimit`   | The attempt was refused before or at the lookup because a limit was reached.                                      | #31         |
| `RefusedByCeiling`     | The redemption was refused because a ceiling on what the plugin may create was reached.                           | #33         |
| `RefusedByAntiForgery` | The submission failed the cross-site check.                                                                       | #78         |
| `RefusedByValidation`  | The answers on the form did not validate on the server.                                                           | #75, #76    |
| `FailuresDropped`      | Failure entries went out of the bound below, and this entry says how many attempts went with them.                | #43         |

`FailuresDropped` is the odd member and it is a member on purpose. It is the
trail's own admission rather than an attempt's outcome, and putting it in the set
is what keeps `docs/personal-data.md`'s sentence exactly true: every entry carries
one value from one closed set, and the outcome field is never free text. The
alternative, an entry whose outcome is absent, would be a second shape of entry
for a reader and for anything that writes one back. `AttemptTrail` is the only
thing that writes it and `AttemptEntry.Of` refuses a caller who tries.

The last four before it are the reason this is written down now. Each is introduced by a
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

What no issue has built is the caller that turns one of those into an entry. So
the column names the issue that would write one on the five rows a decision
reaches, and #51, #54 and #55 stay beside it because each of those still owns the
rule its row is about.

## The column named the post, the post landed, and it appends nothing

THIS SECTION AND FIVE ROWS ABOVE NAMED THE POST ON THE REDEMPTION ROUTE. It
landed, and a cell naming it would send a reader after closed work, which is this
page's subject happening on this page for the third time in a row.

What matters more than the pointer is what the landing did NOT bring. The post
decides a presented code, takes the use, creates the account and records it, and
it appends nothing anywhere:

    git grep -n 'AttemptTrail\|AttemptEntry' -- Jellyfin.Plugin.Invites/Controllers/RedeemController.cs ; echo "exit=$?"
    exit=1

So the caller this column is about is still absent, and the reason changed rather
than the fact: it used to be that no route judged a presented code, and now one
does and does not record what it judged. The cells name #43, which is the issue
that appends an entry and which also still owes where a trail is written and
under which store version. Neither of those is decided anywhere in this tree.

Reading the landing as the column being satisfied is the mistake this section
exists against, and it is easier to make than the last two: the route is there,
the decision is made, and the one act that would fill this table is the one that
is missing.

## The column named the setup page for the post, and it does not now

THIS SECTION AND FIVE ROWS ABOVE NAMED #74 AS THE CALLER THAT TURNS A DECISION
INTO AN ENTRY. The post has not been #74 since 2026-08-31, when #71 split the
act in two: the post that receives the form became #399 and the routine that
creates the account became #398. #74 landed the setup page, and its own
remaining clause is about bytes rendering in a browser, which no entry is
written by.

The move above repaired one wrong pointer in this column and installed another
in the same act. It was written on 2026-08-28, three days before the split, so
it was right when it landed and stopped being right without anything on this
page changing - which is the shape a `Produced by` column has and the reason it
is worth a section rather than a quiet substitution.

What it cost the reader this page is for. The last four members of the set exist
so that an issue adding a refusal adds its member here, and the column is how
somebody checks whether the caller for a member has arrived. Sent to #74, they
found an issue that had landed and would have read the caller as built.

`docs/refusal-response.md` carries the same vocabulary in its own `Owned by`
column and was carried over on 2026-09-02, so the two tables that share these
names agree again rather than disagreeing about which issue writes the entry.

Nothing in this tree would have found it. `AttemptOutcomeSetTests` reads this
table for the names in its first column and never for the last one, and
`tracker-claim.sh` judges a present-tense claim that an issue is open or closed,
which "the post is #74" is not. It was found by reading the page against the
tracker.

Writing this section moved the line two pasted references name, one below and
one on [docs/personal-data.md](personal-data.md), from 143 to 178, and the
section written after it moved the same line again, from 178 to 210. Every one of
those is re-made from the command rather than adjusted by the difference, which
is what `pasted-line-reference.sh` refused each change for until they were. Two
sections have now been added above a paste on this page and neither author
noticed the paste; the check did, both times.

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

    git grep -n 'Per source address, twenty attempts an hour' d0cebf36fdb3c6cc1f26b697de0a09bcf93d8334 -- docs/rate-limit.md
    d0cebf36fdb3c6cc1f26b697de0a09bcf93d8334:docs/rate-limit.md:143:**Per source address, twenty attempts an hour. Across all sources, ten attempts

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

**One notice, and its count is cumulative.** A notice appended on every drop and
never removed would be the unbounded thing this bound exists against, arriving
through the door marked honesty: an attacker hammering for a week would leave
thousands of admissions and no failures worth reading. So a drop takes any
earlier notice with it and folds that notice's count into the new one. The trail
therefore holds at most one, and `AttemptTrailTests` asserts that over a run that
drops many times rather than once.

**A dropped entry takes its whole count.** A rate-limiting episode is one entry
standing for many refused requests, so counting a dropped one as a single attempt
would lose the rest and leave the trail claiming it had seen fewer attempts than
it had. The property that makes this checkable is that the sum of what every
entry accounts for equals the number of attempts ever appended, and the trail
keeps that number as the appends happen rather than by reading the entries back,
so the two are independent statements of one quantity.

What an entry costs in bytes is not claimed. Nothing writes a trail to disk, so
there is no encoding to measure, and the bound above is a count rather than a
size.

## What is settled and what is not

The source address is not kept. #43 made the field conditional on the
personal-data inventory allowing it, and the inventory does not: it recommends
that #31 holds an address in memory for as long as its window and no longer, and
that the trail does not carry it. The reasoning is in
[docs/personal-data.md](personal-data.md) under the three fields that failed, and
this page does not restate it.

THIS PARAGRAPH SAID WHETHER A THROTTLED ATTEMPT APPENDS AN ENTRY WAS NOT SETTLED,
AND IT IS SETTLED NOW. It said the two obvious answers were both wrong, and they
are: a limiter that refuses without appending leaves the trail blank during
exactly the event it exists to explain, and one that appends on every refused
request hands the attacker the writing path the bound exists to close. The answer
is neither, and it is the same one on #31 and on #43 rather than one each.

**The trail records the throttling rather than the requests.** One entry when a
source starts being refused against an invitation, carrying the outcome, the time
and how many attempts it covers, and not another until that episode ends. The
number of writes is then bounded by episodes, which the limiter already bounds,
instead of by whatever rate a stranger can send at.

The write happens on the state transition rather than on the event, which is the
shape to keep in view because it is the one a second implementation would drift
from. `AttemptOutcome.RefusedByRateLimit` carries the count and
`AttemptEntry.AttemptsCovered` is where it sits. Where an episode starts and ends
is the limiter's and not the trail's: `AttemptLimiter` is what knows a source, and
the trail deliberately does not, which is the source-address row above read from
the other side.

Retention is a parameter with no value, and decision 8 in #11 is not what fills
it. That decision has an answer now, ninety days, and it is about how long a
spent or expired invitation record is kept.
[docs/personal-data.md](personal-data.md#retention) holds it, and says in place
that the answer does not set the trail's bound.

THIS PARAGRAPH WENT ON TO CALL THE BOUND A QUANTITY NOTHING HAS CHOSEN, AND
THIS PAGE CHOSE IT UNDER `## The bound, in two parts` ABOVE:

    git grep -n 'The failure bound is one thousand entries' -- docs/attempt-outcomes.md
    docs/attempt-outcomes.md:210:**The failure bound is one thousand entries.** This paragraph said the number was

So a reader arriving at this section was told this page has no number for a
thing this page states, which is worse than a stale claim about another
document: the two sentences are sections apart and either one alone reads as
settled. What has no value is retention, meaning how long an entry is kept once
it is inside the bound, and the reason has moved twice rather than once. It is
not that decision 8 is open. It is not that the bound is unchosen. It is that
decision 8 was about the other parameter and nobody has asked this one.
