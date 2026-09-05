# Configuration reference

One row per setting: what it does, its default, its bounds, what happens at each
bound, and what breaks if it is set badly. The reader this is for has met a
setting and does not know what it means, so the row answers that and nothing
else. How the settings are used in sequence belongs in
[the operator guide](operator-guide.md), which #111 owns.

THIS PARAGRAPH SAID THAT GUIDE IS NOT WRITTEN, and it has been in the tree since
the twenty-sixth:

    git log --diff-filter=A -1 --format='%h %ad %s' --date=short -- docs/operator-guide.md
    0d2a271 2026-08-26 Write the operator guide, from installing to revoking, for #111

The link was missing in one direction only, which is how it survived being read.
That guide names this file three times, so a reader walking from the guide
arrives here; a reader walking the other way was told the place they were being
sent to does not exist, and stopped.

    git grep -c 'configuration.md' -- docs/operator-guide.md
    docs/operator-guide.md:3

What it cost is this file's own reader. Somebody who has met a setting, found its
row, and then wants to know what to do with it in order is exactly the person the
sentence was written for, and it sent them nowhere.

A setting that reaches the configuration type without a row here fails a check,
so this file cannot quietly fall behind the type:

    bash .github/lint/configuration-reference.sh check

The rows are checked against the type rather than generated from it. Four of the
six columns are sentences somebody has to write, and a file generated whole is
one nobody reviews the prose of. What the check reads is the property names on
both sides, and it refuses either set holding a name the other does not.

It reads one cell as well. The Default column states a fact the type also
states, so a row whose default disagrees with the initialiser beside it is
refused. That is the drift with the sharpest edge in this file: a reader who
meets a wrong sentence about what breaks argues with it, and a reader who meets
a wrong default believes it and configures against it.

What it will not judge is a setting with no initialiser whose declared type has
no unambiguous language default. A bool with no initialiser is false and an
integer is zero, and those are compared; anything else is null, and whether a
row should write that as unset, none or empty is a writing decision the check
has no business taking. It names each such setting on every run, so a green mark
is never read as every default having been compared.

The Default cell carries the value and nothing else. A unit or a qualification
belongs in Bounds, because the moment the check starts deciding that "7 days"
and "7" are the same value, that column is prose again.

Nothing else a row says is read. Whether the sentence about what breaks is true
is what the review is for.

## The settings

| Setting | What it does | Default | Bounds | At the bound | If it is set badly |
| ------- | ------------ | ------- | ------ | ------------ | ------------------ |
| `PublicBaseUrl` | The address invitation links are built from, as a stranger outside the network reaches this server. It is what the mint response writes its link against, and it is read from here and never from the request | Empty | An absolute `http` or `https` address, with an optional path prefix and no query or fragment | Empty mints as usual and returns the refusal in place of the link, naming this setting | Every link points somewhere the invited person cannot reach, or reaches a server that is not this one. Nothing is minted wrongly and no account is affected, because the address is used only to write the link down |
| `Templates` | The named account templates an operator mints against. Each entry carries a label, the libraries the account may see, the ten permissions #64 decided and the three ceilings, and it is the value an invitation copies at minting rather than a name it looks up later | `[]` | A list. Every label is non-blank, unpadded and unique ignoring case, every library identifier is non-zero and named once, and every ceiling is absent or at least zero | Empty is a fresh install: no template exists, and once the mint copies a grant out of this list nothing can be minted until an operator writes one down | The list is refused whole when the plugin loads, with the position of the entry and the rule it missed named and no label quoted. Nothing is corrected, nothing is dropped and no account is affected, because a template is read only where a grant is copied |
| `RecordRetentionDays` | How many days a record that has stopped being usable is kept before the nightly sweep removes it. A record still worth redeeming is never removed, whatever this says | 90 | A whole number of days from 1 to 3650 | At 1 a record is removed the day after it stops being usable, which is the shortest trace this plugin will keep; at 3650 it is kept for ten years, which is where an indefinite register of who was invited starts | Too short and the answer to where an account came from is gone before an operator asks it; too long and the plugin holds a list of who was invited for longer than the reason it kept one. Outside the range the sweep refuses to run at all and removes nothing, naming this setting when the plugin loads |
| `RedemptionAttemptsPerAddressInAnHour` | How many presented codes one source address may have judged in an hour. Fetching the setup page is not one and a refused request is not one | 20 | A whole number from 1 to 20, the upper end being the constant `AttemptLimiter.PerAddressCeiling` compiles | At 1 one address judges one code an hour; at 20 it is the compiled limit and the setting changes nothing. A value above 20 is refused rather than accepted, because the entropy argument rests on that number | Set too low, people behind one shared address or one reverse proxy refuse each other. It cannot be set too high: the maximum is the ceiling, and an out-of-range value refuses every attempt rather than falling back to 20 |
| `RedemptionAttemptsPerSecond` | How many presented codes all sources together may have judged in a second | 10 | A whole number from 1 to 10, the upper end being the constant `AttemptLimiter.GlobalCeiling` compiles | At 1 the whole server judges one code a second; at 10 it is the compiled limit and the setting changes nothing. A value above 10 is refused, for the reason the row above gives | The same shape as the row above, one limit along. It bounds everybody at once, so it is the one that keeps meaning something when an attacker has many addresses |

## The public base address

This is the setting whose misconfiguration produces links that do not work, and
it is the one a reverse proxy makes awkward, so it gets more than a row.

The address is configuration and never the incoming request. Building a link
from the request's host header is the easy way and it is the vulnerability: a
minting call carrying a forged host produces a link pointing at somebody else's
server, and the invited person types their new password into it. A link is also
minted for a person who is not on the other end of any request, so there is no
correct version of deriving one from a request even where the request is honest.
Two rules in `.github/lint/invariants.sh` refuse the two spellings, and
`InvitationLink` takes no request-shaped parameter at all, which is the shape
those rules cannot see.

Set it to what somebody outside the network types to reach this server, scheme
included. A path prefix belongs here where a proxy serves the server under a
subdirectory, and it survives into the link. A trailing slash makes no
difference, and neither does an explicit `:443` on an `https` address.

The fallback when it is not set is a refusal rather than a guess. The plugin
does not read the server's own published address, and that is a decision rather
than an omission: the case this setting exists for is a server behind a proxy,
which is exactly the case where the server's own idea of its address is the
wrong one, and the member a server answers that question with has already been
measured moving between the two lines this plugin loads on. A refusal naming this
setting is a support question with an answer. A link built from the wrong address
is a support question without one.

The refusal is not written to a log for somebody to find later. It comes back in
the mint response, in place of the link, at the moment an operator was expecting
one, which is #50 and is what `docs/api.md` describes under `POST /Invites`.

An address that is not absolute, is not `http` or `https`, or carries a query or
a fragment is refused the same way, because appending a path to any of them
produces something that looks like a link and does not reach the redemption
route.

## The named templates

Decision 6 in #11 keeps several named templates, chosen by name when an
invitation is minted, and this setting is where they are written down. Each
entry is the stored shape of what an invitation minted against it grants: a
label, the libraries by identifier, the ten permissions #64 decided with the
posture that issue gave each one, and the three ceilings.

**What a member left out of an entry is worth.** Closed, except the two #64
opened because they reach nothing beyond the invited person: playing from
outside the network and changing the account's own display preferences start
open, every other permission starts closed, the library list starts empty, and
every ceiling starts absent, which is no ceiling rather than a ceiling of zero.
So an entry that carries nothing but a label is a usable template that grants
no library, and that is a template somebody chose rather than a mistake.

**Two things no entry can say.** There is no member for an account that manages
the server, so no element of the file spells it and an element the server's
reader does not know is dropped rather than read; the grant every entry becomes
has that closed whatever else was written, which is #62's ceiling refused by
shape rather than by a check. And there is no member naming the server's policy
fields a template leaves alone: that list names fields of the server's own
policy, which an operator has no way to read, and a wrong name there would be
refused at the moment the grant is written onto an account that already exists.
A configured template names none, and the field-by-field assertion over a
created account derives what was left alone from what the routine writes.

**Labels are names, not codes.** A label is compared ignoring case, so two
entries whose labels differ only in case are one name written twice and the
list is refused. A label with a space inside it is a name; a label padded with
a space at either end is refused, because nobody types the padding on the mint
form and the entry would name nothing anyone can reach.

**What a fault does.** The whole list is refused, not thinned to the entries
that pass. Handing on the good ones would be the plugin deciding which of an
operator's templates count, and an operator who wrote five and can mint against
four has been corrected without being told, which is the silent fallback the
rule at the top of this page refuses. The refusal is written to the log once
when the plugin loads and names the setting, the position of the entry counted
from one, and the rule it missed. It quotes no label, for the reason
`docs/logging.md` gives: a value reaches a log line only where it is a row in
the inventory, and a setting is not one. Repair the entry, load again, and the
next fault, if there is one, is named the same way.

**What reads this setting.** The load the server makes when it starts reads it
and writes the refusal above. The mint reads it too, and it is the one moment a
name becomes a grant: the name typed on the mint form is looked up in this list,
compared ignoring case, and the grant behind it is copied onto the record as
the invitation is written, which is #61's second clause and the reason editing
an entry afterwards changes the next invitation and none already minted.

THIS PARAGRAPH SAID THE MINT DID NOT YET COPY A GRANT OUT OF THIS LIST, AND IT
DOES. What stood here pasted the mint's signature and said a name that matches
no entry was not refused at minting. Both halves moved with #61:

    git grep -n 'TemplateSettings.Named(_templates.Templates, templateLabel)' -- Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:241:            template = TemplateSettings.Named(_templates.Templates, templateLabel);

A name that matches no entry is refused at minting as a bad request naming this
setting, and a list with a fault in it refuses every mint with a conflict
carrying the sentence above, in both cases before a code is minted and with
nothing written.

THIS PARAGRAPH SAID WHAT AN ENTRY CARRIES REACHES NO ACCOUNT BECAUSE NOTHING
REDEEMS. Something redeems. The post on the redemption route takes the grant off
the record and hands it to the routine that creates the account, and it never
reads this list:

    git grep -n 'reserved.Template!' -- Jellyfin.Plugin.Invites/Controllers/RedeemController.cs
    Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:279:                reserved.Template!).ConfigureAwait(false);

That is #61's rule reaching an account for the first time: editing an entry here
changes what the next invitation grants and leaves every live one exactly as it
was minted.

## A fresh install

A server that installs the plugin and never opens the configuration page runs
with an empty public address, which builds no invitation links, and with no
template, which is a grant nobody decided rather than a safe one. The closed
answer for the address is not a safe address, it is no address, and the reason
is in the row above; the closed answer for the templates is none, and the
reason is in the section above.

THIS SECTION NAMED TWO SETTINGS AND THE TYPE HOLDS MORE THAN TWO SINCE #86. The
three numbers arrive with a decided value rather than an empty one, and closed
means something different for each: the retention period is the period somebody
decided rather than either end of its range, and the two rate limits are the
compiled maxima, which is the widest a server can run at and therefore the state
every argument about them was written for. The section below is where that is
argued; the sentence here is that the table naming what each is worth is not this
page.

`Jellyfin.Plugin.Invites.Tests.FreshInstallConfigurationTests` holds the type to
that answer, which is what #87 asks for. It is a table every setting has to be
in, so a setting arriving without a decided fresh-install value reds the suite
rather than shipping whatever the default happened to be.

## The retention period and the two rate limits

These three are settings and the ceilings below are not, which is the decision
taken on #86 on 2026-09-04 rather than an accident of which numbers were easy to
move. A retention policy and how many people sit behind one address are things
installations genuinely differ on. A ceiling is a promise this plugin makes about
what it will not do, and a promise moves by a release rather than by a text field.

**Every one of them has a compiled maximum, and the maximum is what carries the
guarantee.** A configured value is an operator's own restraint and can be relaxed
by whoever holds that account; the compiled maximum cannot be moved without
shipping a new version. That distinction is the whole reason the two rate limits
are lowerable and not raisable:

    git grep -n 'MostAttemptsPerAddressInAnHour\|MostAttemptsPerSecond' Jellyfin.Plugin.Invites/Configuration/NumberSettings.cs

Both name the constant beside them rather than restating its value, so the number
an operator may not exceed is the number `docs/rate-limit.md` and
`docs/code-entropy.md` reason about, and the two cannot drift apart.

**The retention period is the one that is bounded at both ends and defaults to
the middle.** Its maximum is decided in `NumberSettings` rather than taken from
somewhere else, because there is no arithmetic to take it from: ten years is where
an indefinite register of who was invited starts, which is the thing
`docs/personal-data.md` argues against, and it is reasoned rather than measured
exactly as the ninety days it bounds is.

The bottom of that range is the half worth reading twice. Zero is not a stricter
retention period. It is deletion at the moment a record stops being usable, which
destroys the only link between an account and the invitation that produced it
before anybody could read it, so one day is the floor and the rule keeps rounding
towards keeping.

**An out-of-range value refuses where it would be used, and nothing is
substituted for it.** This is what #86 asks for and the reason is the same for all
three: a silent fallback on a bound is the bound gone, and an operator who typed a
number and quietly got the plugin's own has been corrected without being told. The
two routines refuse in the shape each of them has:

- the sweep reads the period before it opens the store, so a run that meets an
  out-of-range value has read nothing and written nothing, and no record is
  removed until the setting is repaired;
- the redemption limiter refuses the attempt, with the one refusal
  `docs/refusal-response.md` keeps for every case, rather than judging the code
  against the compiled constants.

Both directions are closed rather than convenient: a mistyped number costs
redemptions or costs a sweep, and neither costs a bound.

**Where an operator meets it.** Each of the three has a field on the plugin's own
configuration page, stating its range, and the page refuses a save that would put
one outside it rather than nudging it in - a clamp on a form is the same silent
correction one step earlier than the load. The plugin also reads all three when
the server starts and writes one line naming the setting, the range and the
direction the value went out of it, for the first fault in declaration order. The value that was typed is
never in the line, because `docs/logging.md` admits a value there only where it is
a row in `docs/personal-data.md`, and a server setting is not one.

## The ceilings

**None of these is a setting, and that is the first thing to know about them.**
They are constants in the source, so there is nothing here for an operator to
type and nothing on this page's table to look them up in. They are written down
here anyway, because an operator who meets one meets it as a refused request from
`POST /Invites` with a number in it, and the configuration reference is where
somebody goes to find out where a number came from.

Three numbers act at minting today, and they come from two issues rather than
one. Two of them are #33's ceilings; the validity maximum is #51's and is a
ceiling in the same sense, so it belongs beside them rather than in a section of
its own:

    git grep -n 'public const int UsesCeiling' -- Jellyfin.Plugin.Invites/Invitations/InvitationMint.cs
    Jellyfin.Plugin.Invites/Invitations/InvitationMint.cs:72:    public const int UsesCeiling = 10;

    git grep -n 'public const int LiveCeiling\|public const int MaximumValidityDays' -- Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:67:    public const int MaximumValidityDays = 90;
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:117:    public const int LiveCeiling = 500;

At most ten accounts on one invitation, at most ninety days of validity, and at
most five hundred live invitations in the store at once. The reasoning for each
number is on the constant that carries it rather than restated here, which is
where it stays true when the number moves.

The default validity is a fourth number and is not a ceiling. Seven days, and it
is what a mint that names no validity gets:

    git grep -n 'public static TimeSpan DefaultValidity' -- Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:174:    public static TimeSpan DefaultValidity => TimeSpan.FromDays(7);

The three constants and the default each moved down under #61, by one, one
and ten lines, as the mint gained the template seam above them. None of the
four values changed.

**Nothing is clamped.** A request outside any of them is refused with the limit
and the value in the message, and nothing is written. A use count or a validity
outside its range is a bad request; the live ceiling is a conflict, because what
the caller sent was acceptable and the store's state was not, so the repair is
revoking an invitation rather than editing the request.

**What "live" counts is not what the file holds.** An expired, spent or revoked
record is not live, does not count against five hundred, and stays in the file.
None of the numbers above bounds how large the store grows; retention does, which
is the scheduled sweep rather than a ceiling.

**The third of the three ceilings #33 asks for does not exist.** How many
accounts the plugin may create in a given period is the one that still holds when
the other two are set badly.

THIS PARAGRAPH RESTED ON NOTHING IN THE PLUGIN CREATING AN ACCOUNT, AND
SOMETHING DOES. #398 landed the write seam and the routine that calls it, so the
command that stood here as evidence no longer exits 1:

    git grep -nE 'CreateUserAsync' -- 'Jellyfin.Plugin.Invites/*.cs' ; echo "exit=$?"
    exit=0

What it matches is three lines, of which the first is a documentation comment
and the other two are the call, which is worth separating because a reader
counting matches here would otherwise read three call sites:

    git grep -nE 'CreateUserAsync' -- 'Jellyfin.Plugin.Invites/*.cs'
    Jellyfin.Plugin.Invites/Accounts/ServerAccountWrites.cs:20:/// <c>CreateUserAsync(name)</c>, <c>GetUserById(identifier)</c>,
    Jellyfin.Plugin.Invites/Accounts/ServerAccountWrites.cs:143:        var created = await _users.CreateUserAsync(username).ConfigureAwait(false);
    Jellyfin.Plugin.Invites/Accounts/ServerAccountWrites.cs:146:            ? throw ServerAccountWriteRefusedException.AnsweredNothingUsable("CreateUserAsync", "an account")

The ceiling is no less absent for that and the gap is wider, not narrower: what
kept it harmless was that the act it would bound could not happen, and the act
exists now.

THIS PARAGRAPH SAID NOTHING CALLS THE ROUTINE FROM A REQUEST, AND SAID THAT
WOULD STOP BEING TRUE ON THE DAY THE POST LANDED. The post landed. A request from
a stranger holding a live link reaches the creation routine, so the rate a
ceiling would bound is no longer zero per hour on a running server, and the only
numbers standing between a leaked link and a run of accounts are the use count on
the record and the redemption limiter's two.

THIS PARAGRAPH SAID NO NUMBER BOUNDS WHAT ARRIVES FROM THE STANDING SET. One
does. Five hundred live invitations at ten uses each is still five thousand
accounts the set can authorise with no further operator action, and #33's third
ceiling bounds how many of them may arrive in a day:

    git grep -n 'public const int AccountsInAWindow' -- Jellyfin.Plugin.Invites/Accounts/CreationCeiling.cs
    Jellyfin.Plugin.Invites/Accounts/CreationCeiling.cs:68:    public const int AccountsInAWindow = 50;

So the five thousand is a hundred days of growth rather than an evening of it,
and the number is reasoned rather than counted: nobody has watched a real server,
and the reasoning is on the constant.

It is not a setting. There is nothing on the configuration type an operator can
move it with, and that is now a decision rather than a thing nobody had got to:
#86 settled on 2026-09-04 that the ceilings stay constants in the first version,
because they are the bounds this plugin promises and a promise moves by a release
rather than by a text field. What that reasoning bought elsewhere is the
arrangement the section above describes - a setting bounded by the constant
beside it rather than replacing it - and it was spent on the retention period and
the two rate limits instead.

**What is owed here and is not written, which is smaller than it was.** #113 asks
that this section say the ceilings are enforced when the configuration loads and
that an out-of-range value refuses the load rather than being clamped. THIS
PARAGRAPH SAID NEITHER HALF COULD BE WRITTEN TRUTHFULLY BECAUSE THERE IS NO
CEILING ON THE CONFIGURATION TYPE AND NO LOAD-TIME COMPARISON TO DESCRIBE. There
is a load-time comparison, and the section above describes it, so what is left
owed is only the part about these three numbers. They are still constants, so
there is nothing about them for a load to compare, and the enforcement they have
is the one the paragraphs above describe: at minting, with the limit and the value
in the refusal, and nothing clamped. That is a smaller claim than the one this
section will carry if any of the three ever becomes a setting, and #86 decided
that none of them does yet.

## What is not in this file yet

The three ceilings as SETTINGS, which is the half the section above cannot write.
What this page holds for them today is where the numbers are and what meeting one
looks like; what it does not hold is a row, because a row is a promise that an
operator can change something and none of the three can. THIS PARAGRAPH ONCE
COVERED EVERY NUMBER IN THE PLUGIN AND NOW COVERS THREE. The retention period and
the two rate limits have rows, a range and a section, and the reason they were
separable from the ceilings is on #86 rather than restated here.

The check refuses a setting with no row. It does not refuse a setting with no
section, because which settings need more than a row is a judgement about what a
reader will trip over rather than a fact about the type, and a check pretending
to make that judgement would turn a red mark into an argument.
