# Personal data this plugin holds

An invitation is a record that a particular operator invited a particular
person at a particular time, and that a particular account came out of it. That
is personal data about two people before a single optional field is added, and
it is worth writing down before the record type exists rather than after.

Nothing below is implemented. The record is #38, the attempt trail is #43, and
the store they both live in is #39. This document is what those three are built
against: a field that is not in the inventory does not go in the record, and a
field in the inventory carries the reason it is there.

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

## The three that failed

The operator's label first. #38 leaves it open and #82 has minting accept one.
It is the field most likely to end up holding a person's full name and mail
address, because that is the obvious thing to type into a box that says what
this link is for. The plugin works without it: an invitation is already
identifiable by its identifier, who minted it and when. The recommendation is
that it is not stored. If it is kept anyway, it is kept as a field the operator
is told is stored in clear and shown in the administrator view, and it is
covered by the same retention as the record.

Then a contact address for the invited person. Decision 9 in #11 is whether the
guided setup collects one. This inventory is the argument for collecting none:
the plugin's job ends when an account exists, account recovery is the server's
job, and an address collected here is the only field that would make this
plugin hold contact data about a person the server does not already hold.
Written as a parameter rather than a refusal, because it is the maintainer's
call and not this document's.

Last, the address a redemption came from. This one is worth separating with
care,
because seeing a value and holding it are different things. Rate limiting and
lockout in #31 need the source address while the request is being decided, and
need nothing of it afterwards. Storing it in the trail is what turns a counter
into a record of where a person was. The recommendation is that #31 keeps it in
memory for as long as its window and no longer, and that the trail does not
carry it. #43 allows the field if this inventory does, and this inventory does
not.

## Retention

Two named parameters are what the record and the trail are built against. One
of them has its answer and the other does not, and the difference is worth
seeing at a glance rather than being read out of a paragraph.

`record-retention` is ninety days. It is how long a spent, expired or revoked
invitation record is kept after it stops being usable, counted from the moment
it stops being usable rather than from when it was minted. Long enough that an
operator meeting an account they do not recognise can still find where it came
from, and short enough that what is left behind is not an indefinite register
of who was invited. Decision 8 in #11 is where the number was chosen.

This number is a decision rather than a measurement, so it carries the issue
that decided it instead of a command. Nothing in this tree can be run to
produce it, and nothing here should be read as having measured it.

The sweep that applies it is #59. Shortening the period later deletes records on
the next sweep rather than stopping new ones from being kept, which is the right
way round and is not reversible.

`trail-bound` is what bounds the attempt trail, and it has no number. Decision 8
asks how long spent and expired invitation records are kept, which is the
parameter above, so the answer to it does not set this one.
`docs/attempt-outcomes.md` reads decision 8 as also covering how long trail
entries are kept beyond the bound. That reading is not settled here, and either
way the bound itself is a separate quantity that nothing has chosen.

#43 requires the trail be bounded and says why: an endpoint a stranger can
hammer, writing an unbounded trail, is a disk-filling attack that uses the
operator's own record keeping as the weapon. The bound is a count or an age, and
it names what is dropped first. Until it is chosen, the trail has a requirement
and no number.

## What deletes anything

Nothing today. There is no store, so there is nothing holding a value that
could be deleted:

```
$ git ls-files --with-tree=origin/master -- '*.cs' ':!.github/lint/fixtures'
Jellyfin.Plugin.Invites.Tests/ClockSeamTests.cs
Jellyfin.Plugin.Invites.Tests/PluginPagesTests.cs
Jellyfin.Plugin.Invites.Tests/Stubs.cs
Jellyfin.Plugin.Invites/Configuration/PluginConfiguration.cs
Jellyfin.Plugin.Invites/Plugin.cs
Jellyfin.Plugin.Invites/Time/IClock.cs
Jellyfin.Plugin.Invites/Time/SystemClock.cs
$ git grep -lE 'Invitation|Redeem' origin/master -- '*.cs' ':!.github/lint/fixtures'
origin/master:Jellyfin.Plugin.Invites/Plugin.cs
$ git grep -nE 'Invitation|Redeem' origin/master -- 'Jellyfin.Plugin.Invites/Plugin.cs'
origin/master:Jellyfin.Plugin.Invites/Plugin.cs:29:    public override string Name => "Account Invitations";
```

Seven source files, and the one occurrence of the word invitation in any of
them is the display name the dashboard shows. No redemption path, no store and
no record type. The fixtures are excluded because they hold their violations on
purpose.

Three deleters are planned and no others. The retention sweep in #59 removes
records the retention rule allows and never touches an account. Revocation in
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
is the `secret-in-a-log-call` rule in `.github/lint/invariants.sh`.

## What this document does not settle

The record type does not exist, so no field has been removed from it. That
clause of #34 is met by construction today and has to be checked again when #38
lands, against this file.

The clause asking that a documentation page carry the same inventory has no
page to land in. Every issue in M11 was read for one and none of the seven
names an inventory or personal data as its content:

```
$ for n in 110 111 112 113 114 115 116; do
    gh api repos/iderex/jellyfin-plugin-invites/issues/$n --jq .body \
      | grep -ciE 'personal data|inventory'
  done
0
0
0
0
0
0
0
```

So the page that would carry this is not owned by anything open. That is a gap
in the plan rather than a step that was skipped, and it is the reason #34 stays
open with this file landed.
