# Personal data this plugin holds

An invitation is a record that a particular operator invited a particular
person at a particular time, and that a particular account came out of it. That
is personal data about two people before a single optional field is added, and
this inventory was written before the record type existed rather than after, so
the record was built against it.

This page said nothing below holds anybody's data yet. Some of it does. The
record is a type, `Jellyfin.Plugin.Invites/Invitations/Invitation.cs` under #38,
the store is `Jellyfin.Plugin.Invites/Storage/InvitationStore.cs` under #39, and
since the administrator routes landed an operator who mints an invitation writes
one:

    $ git grep 'new InvitationStore' origin/master -- 'Jellyfin.Plugin.Invites/*.cs'
    origin/master:Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:            var store = new InvitationStore(directory);
    origin/master:Jellyfin.Plugin.Invites/Storage/InvitationStore.cs:        return new InvitationStore(plugin.DataFolderPath);
    origin/master:Jellyfin.Plugin.Invites/Storage/StoreLoad.cs:                ConsistencyReport.OfALoad(new InvitationStore(directory), accountsTheServerHas));

    $ git grep 'store.Read()\|store.Write(' origin/master -- 'Jellyfin.Plugin.Invites/*.cs'
    origin/master:Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:            var contents = store.Read();
    origin/master:Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:            store.Write(contents.Invitations.Add(minted));
    origin/master:Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:            var contents = store.Read();
    origin/master:Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:            store.Write(contents.Invitations.Replace(found, revoked));
    origin/master:Jellyfin.Plugin.Invites/Storage/ConsistencyReport.cs:        return Of(store.Read().Invitations, accountsTheServerHas);

Both writes are in `InvitationOperations` and both have a route above them:

    $ git grep '_operations\.Mint(\|_operations\.Revoke(' origin/master -- Jellyfin.Plugin.Invites/Controllers/InvitesController.cs
    origin/master:Jellyfin.Plugin.Invites/Controllers/InvitesController.cs:            var minting = _operations.Mint(
    origin/master:Jellyfin.Plugin.Invites/Controllers/InvitesController.cs:        var revoked = _operations.Revoke(id, revokedBy);

So a server whose operator has minted once holds an invitations file, and the
rows of the record table below that are filled in on that file are the
identifier, the keyed hash, minted by, minted at, expires at, the use count and
the template name, plus revoked at and revoked by once somebody revokes. `Minted
by` and `Revoked by` are personal data about the operator, and they are held
under the `record-retention` parameter named below, which a scheduled sweep
applies.

What is still not written is anything about the invited person. That waits on a
redemption that commits, and the rows it would fill are `Accounts produced` and
the attempt trail, which is #43 and has no type at all.

This document is what all three are built against: a field that is not in the
inventory does not go in the record, and a field in the inventory carries the
reason it is there.

The accounts themselves are not in this inventory. An account created by an
invitation is an ordinary Jellyfin account in the server's own user database,
holding whatever the server holds about any account, and removing this plugin
does not change that. What this plugin adds is the link between that account
and the invitation it came from, and that link is the row worth the most care.

## The test every row had to pass

Would the plugin still do its job without this field. A field that survives has
a reader named in an open issue. A field that fails is not stored, however
useful somebody can imagine it being later.

Three fields failed and are handled below rather than being quietly kept: the
operator's free-text label, a contact address for the invited person, and the
address a redemption arrived from.

## The invitation record

| Field | Why it exists | What deletes it |
| --- | --- | --- |
| Invitation identifier | A non-secret name for one invitation, so a log line and an administrator view can both point at it without either one carrying the code. Required by #32. | The record |
| Keyed hash of the code | Lets a presented code be checked without the code being held. Required by #29. Not data about a person, and listed here so the inventory is the whole record rather than the interesting part of it. | The record |
| Minted by | The operator account answerable for this invitation. This is personal data about the operator, and it is the field that makes #43 answerable at all. | The record |
| Minted at | Fixes the invitation in time, which is what makes a trail readable. | The record |
| Expires at | Read by the expiry comparison in #51. | The record |
| Uses granted, uses remaining | The count #52 makes authoritative. | The record |
| Revoked, revoked at | Revocation is #54, and the time is what tells an operator a restore undid it. | The record |
| Revoked by | The operator account answerable for the revocation, which #54 asks be recorded beside the time. Personal data about the operator on the same footing as minted by, and it exists for the same reason: an invitation that stopped working is answerable to whoever stopped it or to nobody. Only the first revocation is kept, so this is one identifier and not a history. | The record |
| Template name | Which grant this invitation carries, from #61. | The record |
| Accounts produced | The link between an invitation and the accounts it created. This is the most identifying row here, and it is also the one an operator needs when an account they do not recognise appears. | The record |
| Operator label | Failed the test. See below. | Not stored |
| Contact address | Failed the test. See below. | Not stored |

## The attempt trail

| Field | Why it exists | What deletes it |
| --- | --- | --- |
| Invitation identifier, where one matched | Says which invitation an attempt was against. Empty where the presented code matched nothing, because there is nothing to name. | The trail bound |
| Outcome | One value from the fixed set in #43. Not free text, so nothing typed by anyone reaches it. | The trail bound |
| Time | When it happened. | The trail bound |
| Source address | Failed the test. See below. | Not stored |

## The setup form

The guided setup asks three questions, and they belong on this page for the same
reason the two tables above do: a field on a form is a value somebody typed, and
the inventory is where what becomes of it is argued rather than assumed.
[docs/setup-never-asks.md](setup-never-asks.md) decides which questions may be
asked at all. This is what happens to the three that are.

None of the three is stored by this plugin. That is the answer rather than an
omission, and it is why each row below names where the value goes instead of
naming a deleter here.

| Field | Why it exists | What deletes it |
| --- | --- | --- |
| `username` | Becomes the name of the account the server creates, so it is held in the server's own user database on the same footing as an account an operator made by hand. The record points at an account by identifier and never by name, so this plugin keeps no second copy of it. | Deleting the account, on the server |
| `password` | The credential of the account being created. It is handed to the server and held here in no form: not in the store, not in a log line, not in the trail. What reads it in this plugin is the length rule in `Jellyfin.Plugin.Invites/Setup/PasswordRules.cs`, which answers why a password is refused and keeps none of it. | Never held here |
| `confirmation` | Compared with the password so a mistyped credential is caught before an account exists carrying it. It is the one value on the form with no reader outside the request it arrived in. | Never held here |

Nothing takes a submission yet, so no value in this table has reached the plugin
on any server:

    $ git grep -n 'HttpPost' -- Jellyfin.Plugin.Invites/Controllers/RedeemController.cs ; echo "exit=$?"
    exit=1

So the rows are written before the post that fills them, in the same direction
as the record table above, which was written before the record type existed. The
comparison between what the page asks and what this section names is
`Jellyfin.Plugin.Invites.Tests.SetupFormInventoryTests` rather than a reading
somebody repeats, so a fourth question added to the form with no row here reds
the suite, and a row here naming a field the form does not carry reds it in the
other direction.

## The three that failed

The operator's label first. This page said #38 leaves it open and #82 has
minting accept one; both have closed and neither carries the field, which the
last section reads back. It is still the one most likely to end up holding a
person's full name and mail address, because that is the obvious thing to type
into a box that says what this link is for. The plugin works without it: an
invitation is identifiable by its identifier, who minted it and when. The
recommendation is that it is not stored. If it is kept anyway, it is kept in
clear, shown in the administrator view, and covered by the record's retention.

Then a contact address for the invited person. This paragraph wrote it as a
parameter because the call was mine. It has been made, and it is the one this
inventory argued for: decision 9 in #11 is answered and the guided setup
collects none, because the plugin's job ends when an account exists, account
recovery is the server's job, and an address collected here would be the only
field making this plugin hold contact data the server does not already hold.
The last section says what holds it off the record.

Last, the address a redemption came from. This one is worth separating with
care,
because seeing a value and holding it are different things. Rate limiting and
lockout in #31 need the source address while the request is being decided, and
need nothing of it afterwards. Storing it in the trail is what turns a counter
into a record of where a person was. The recommendation was that #31 keeps it in
memory for as long as its window and no longer, and that the trail does not
carry it. #43 allows the field if this inventory does, and this inventory does
not.

The first half is no longer a recommendation. `AttemptLimiter` landed under #31,
it takes the clock and nothing else, and the addresses it is holding go when the
window turns rather than being swept one at a time. A test reads the count back
across a window boundary and a second one reads the type's own members to say
nothing durable is among them. The second half is unchanged and still a
recommendation: nothing writes a trail at all.

## Retention

Two named parameters are what the record and the trail are built against. Both
have an answer now, they were chosen in different places and for different
reasons, and that is worth seeing at a glance rather than being read out of a
paragraph.

`record-retention` is ninety days. It is how long a spent, expired or revoked
invitation record is kept after it stops being usable, counted from the moment
it stops being usable rather than from when it was minted. Long enough that an
operator meeting an account they do not recognise can still find where it came
from, and short enough that what is left behind is not an indefinite register
of who was invited. Decision 8 in #11 is where the number was chosen.

This number is a decision rather than a measurement, so it carries the issue
that decided it instead of a command. Nothing in this tree can be run to
produce it, and nothing here should be read as having measured it.

The sweep that applies it is #59 and it is in the tree: a daily scheduled task
asks `InvitationOperations.Sweep` for the records whose period has run out.
Shortening the period later deletes records on the next sweep rather than
stopping new ones from being kept, which is the right way round and is not
reversible.

One thing about the counting is worth reading before the number is trusted. The
period runs from the moment a record stopped being usable, and a spend is the one
way of stopping that leaves no instant on the record. Such a record is therefore
counted from its expiry, which is always later than the spend, so it is kept
longer than ninety days rather than deleted sooner than the rule allows. The
routine says so in place and a test asserts it; closing the gap is a spent-at
field, which is #52's.

The boundary itself is inclusive, and it is written here rather than only in the
routine because a direction stated in one place is a direction the code is its
own authority for. A record whose period ends exactly at the moment the sweep
reads the clock may be removed; one tick earlier it may not. A sweep on a daily
schedule will practically never land on that instant, so nothing turns on which
way it went, and choosing it anyway is cheaper than leaving it to whoever reads
the routine next. This is a different question from which instant the period
counts from, where every rounding is towards keeping for the reason above, and
the two are worth not reading as one.

`ClockBoundaryTests.TheRetentionBoundaryIsTheDirectionThisPageStates` reads the
sentence above out of this page and asks the routine at both instants, so the
page and the code cannot drift apart without something going red. A page that
stops carrying the sentence fails there rather than passing quietly, which is
the direction to fail in.

`trail-bound` is one thousand failure entries, dropped oldest first. THIS
PARAGRAPH SAID IT HAD NO NUMBER, AND THE NUMBER LANDED ON 2026-08-17. That is
the drift this section is least able to carry: a reader here was told the trail
has a requirement and nothing to satisfy it, while the page that owns the
quantity had already derived one and had been carrying it for a fortnight.

    git log -1 --format='%h %ad %s' --date=short fd0fdfe
    fd0fdfe 2026-08-17 Choose the attempt trail's failure bound, for #43

    git grep -n 'The failure bound is one thousand entries' -- docs/attempt-outcomes.md
    docs/attempt-outcomes.md:128:**The failure bound is one thousand entries.** This paragraph said the number was

It bounds failures and not the whole trail, and that half is the one a summary
here would lose. Successes are kept and are bounded by the ceilings in #33
instead, because nothing a stranger does creates a success entry without also
creating an account. A single oldest-first ring over the whole trail would
satisfy the word bounded and is refused by name on that page as a
history-erasing attack. Where the number comes from, which is the limits chosen
in #31, and what it does not buy, are argued there rather than restated here.

Decision 8 in #11 still does not set it, and the sentence saying so was right.
That decision is `record-retention` above and the two are different quantities.
What is left unchosen is a third thing rather than this one: how long a trail
entry is kept once it is inside the bound, which `docs/attempt-outcomes.md`
names as a parameter with no value.

#43 requires the trail be bounded and says why: an endpoint a stranger can
hammer, writing an unbounded trail, is a disk-filling attack that uses the
operator's own record keeping as the weapon. That requirement is met by a
number on the page that owns it and by nothing that runs. There is no entry
type and nothing appends, so what exists is a decision waiting for an
implementation rather than a defence the plugin has.

## What deletes anything

The retention sweep does, and this paragraph said nothing did. It has said two
different things in turn: first that nothing put a record there, then that
records were put there and nothing took them away. Both have been overtaken, and
the removal is a third writer of the store file rather than a deleter of it:

```
$ git grep -E '\.Write\(' origin/master -- 'Jellyfin.Plugin.Invites/*.cs'
origin/master:Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:            store.Write(contents.Invitations.Add(minted));
origin/master:Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:            store.Write(contents.Invitations.Replace(found, revoked));
origin/master:Jellyfin.Plugin.Invites/Storage/HashSecret.cs:            file.Write(value, 0, value.Length);
origin/master:Jellyfin.Plugin.Invites/Storage/InvitationStore.cs:            writer.Write(json);
origin/master:Jellyfin.Plugin.Invites/Storage/StoreLock.cs:            writer.Write(written);
$ git grep -E '\.Remove\(|\.Delete\(' origin/master -- 'Jellyfin.Plugin.Invites/*.cs'
origin/master:Jellyfin.Plugin.Invites/Storage/StoreLock.cs:                File.Delete(Path);
```

These two commands lost their `-n`, and two of the six numbers they printed were
stale when it came off. The last section says why the numbers went rather than
being corrected. Six writers and three of them are callers: a mint, a revocation
and the retention sweep. The other three are the hash secret writing itself, the
store's own writing member, and the claim on the directory. The one deleter in
the plugin removes the claim file on the way out and reaches no record.

A record is removed by being left out of what the sweep writes back rather than
by a delete, which is why the second command still returns one line and why
reading it alone would say nothing is ever removed. What removes a record is the
third `store.Write` above.

So `record-retention` is a behaviour and no longer only a decision: a record that
stopped being usable more than ninety days ago is gone at the next daily run,
without an operator doing anything. What the sweep never removes is a record that
could still be redeemed, and it reaches no account at all.

The redemption half is unchanged. A `Redeem` controller exists and serves the
page, and no redemption commits:

```
$ git grep -nE '\bRedeem' origin/master -- 'Jellyfin.Plugin.Invites/*.cs'
origin/master:Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:49:public sealed class RedeemController : ControllerBase
origin/master:Jellyfin.Plugin.Invites/Setup/SetupPage.cs:20:/// escaping it, and it is why <see cref="Controllers.RedeemController"/> does
```

One file the plugin writes on every start, named here because it is the write
that happens with no operator action behind it and so appears on a server where
nobody has minted anything. `StoreLock` creates `invitations.lock` beside the
store, holding the server's host name, the process identifier and the moment the
claim was taken, and removes it on the way out. No value from either table above is in it. It is there so that
two servers over one store are refused rather than allowed to corrupt it, which
is `docs/disaster-cases.md`.

The greps are restricted to the plugin's own sources, because the suite declares
a controller of its own to check the route inventory and the lint fixtures hold
their violations on purpose, and neither is code this plugin runs.

Three deleters are named and no others, and one of the three is built. The
retention sweep from #59 removes records the retention rule allows and never
touches an account. Revocation in
#54 does not delete anything, and that is deliberate, because the record of a
revocation is what an operator needs after a restore quietly undoes it, which
is written up in `docs/disaster-cases.md`. Uninstall in #91 removes the
plugin's own state and leaves every account alone, which also means it removes
the only record of which accounts came from invitations, and #91 is where the
export that answers that lives.

## What is never held, in any form

The invitation code, other than as the keyed hash above. The hash secret is
held, and it is a secret rather than personal data, and #30 owns it. The
password an invited person chooses is handed to the server and never stored
here. #32 is the same list applied to log lines, and the greppable half of it
is the `secret-in-a-log-call` and `code-or-link-in-a-log-call` rules in
`.github/lint/invariants.sh`.

## What this document does not settle

The record type exists now, so the clause of #34 asking that a field with no
purpose be removed from it has been checked against this file rather than being
met by an empty tree. Every member of the type is a row of the invitation record
table above, and no row that failed the test is a member. That is not a reading
somebody has to repeat:
`Jellyfin.Plugin.Invites.Tests.InvitationRecordTests` holds the member list
against the rows, so a field added to the record without a row here reds the
suite.

Two rows above each name two things, which is worth knowing before somebody
reads the table and the type side by side and counts. Uses granted and uses
remaining are two members, because neither can be worked out from the other.
Revoked and revoked at are one stored member, `RevokedAt`, and `IsRevoked`
derived from it, so no record can say it is revoked and fail to say when.

THE CLAUSE ASKING THAT A DOCUMENTATION PAGE CARRY THE SAME INVENTORY HAD NO PAGE
TO LAND IN, AND IT HAS ONE. What stood here read every issue in M11 for an owner,
printed the listing that found none, and called the absence a gap in the plan.
The gap was closed from the other end: #409 was opened for the page and
[docs/what-is-held-about-a-person.md](what-is-held-about-a-person.md) is it. The
listing came out rather than being re-run, because what it was evidence of is the
absence it no longer describes.

That page is this inventory addressed to the person it is about rather than to
somebody deciding what may be stored, and same is a property here rather than a
review note.
`Jellyfin.Plugin.Invites.Tests.PersonalDataForAPersonTests` reads the stored
fields off both pages and requires the two sets to be equal, so a row added to
either of the two tables above with no line there reds the suite, and so does a
line there naming a field these tables do not carry. What the comparison rests on
is the last cell of each row: a row whose deleter is `Not stored` records a
decision not to hold something and names no field the other page could have a
line for.

Five of the commands pasted above once printed line numbers and no longer do.
Nine of the numbers they carried had stopped being true: everything the pastes
named inside `InvitationOperations.cs` and `InvitesController.cs` had moved down
those files as they grew, and the pasted output stayed where it was. Nothing
here was going to catch it. `.github/lint/pasted-exit-status.sh` re-runs a
pasted command and judges the exit status it carries, and says in its own header
that it deliberately does not read pasted output, because comparing output means
normalising line numbers and a mismatch there is as often a reflow as a drift.
So the numbers came out rather than being corrected, which takes this page out of
that uncovered population instead of resetting its clock. What those sentences
rest on is which file writes, which route is above it, and which line matched,
and a paste carrying the path and the line says all three without a figure that
goes stale the next time something is inserted higher up.

The operator's label row above said two things about the tracker that stopped
being true. #38 was described as leaving the field open and #82 as having minting
accept one. Both have closed, and neither carries it: no member of the record is
that field, and the mint request carries the template name, the validity and the
use count and nothing an operator would type a person's name into.

    $ git grep -E '^    public [A-Za-z<>?]+ [A-Za-z]+ \{ get' -- Jellyfin.Plugin.Invites/Controllers/MintRequest.cs
    Jellyfin.Plugin.Invites/Controllers/MintRequest.cs:    public string? Template { get; set; }
    Jellyfin.Plugin.Invites/Controllers/MintRequest.cs:    public int? ValidityDays { get; set; }
    Jellyfin.Plugin.Invites/Controllers/MintRequest.cs:    public int? Uses { get; set; }

`Template` is not that field. It is the operator picking which grant to hand out,
and the record keeps it as the template name, which is its own row above.

So the recommendation this page made is what the tree took, and the row stays
because the recommendation is the thing being recorded and because nothing
refuses the field. What holds the absence in the record is
`InvitationRecordTests.EveryPublicMemberOfTheRecordIsARowInThePersonalDataInventory`,
which reads the members of the type against the rows of the table above. It was
seen to bite: putting a member on the record for exactly this field reds that
test and nothing else.

    $ dotnet test Jellyfin.Plugin.Invites.sln --nologo --configuration Release
    ...InvitationRecordTests.EveryPublicMemberOfTheRecordIsARowInThePersonalDataInventory [FAIL]
    Fehler!      : Fehler:     1, erfolgreich:   438, übersprungen:     8, gesamt:   447

Nothing holds the mint request to anything, so a label accepted at the route and
dropped before the record would pass every check in this tree.

Two pastes on this page still ask for line numbers and both are kept. The one
under `## The setup form` prints nothing at all, because what it is evidence of
is an absence and the status it exited is what carries that. The one under
`## What deletes anything` about the redemption half prints two, and both of
them still reproduce.

The contact address row is held off the record by the same guard as the operator
label, and it was proved for this field rather than inferred from that one.
Giving the record a member for it reds one test and no other:

    $ dotnet test Jellyfin.Plugin.Invites.sln --nologo --configuration Release
    ...InvitationRecordTests.EveryPublicMemberOfTheRecordIsARowInThePersonalDataInventory [FAIL]
    Fehler!      : Fehler:     1, erfolgreich:   444, übersprungen:     8, gesamt:   453

The probe was restored from a copy taken beforehand and nothing tracked carries
it. The bound is the same as the label's: the guard reads the record type, so a
value accepted at a route and dropped before the record walks past it.

The paragraph about that row, under `## The three that failed`, was rewritten to
the same number of lines it had. That is not a style: `docs/rate-limit.md` pastes
a line of this page with its number, so a paragraph here that grows breaks a
reference in a file this change is not otherwise touching, which is the defect
`.github/lint/pasted-line-reference.sh` was written for and the one it refused
here. Taking the number out of that paste is the repair, and it belongs to #31,
which owns that page.
