# Mutation testing

Coverage says a line ran. It does not say a test would have noticed if that line
were wrong. For most of this plugin the difference is academic. For the routines
that decide whether an invitation is honoured it is the whole question, because
every mutant that survives there is a rule the suite states somewhere and does
not enforce.

This page is what `stryker-config.json` decides and why. The file is what runs;
this is where the numbers are argued with.

## What is measured

Four things, named in #22: the redemption decision, the expiry comparison, the
use-count arithmetic and the account-template application.

They are addressed as three directories and one file rather than as a list of
routines, because the first three already sit together and the fourth had no code
when the scope was written:

```
$ grep -A7 '"mutate"' stryker-config.json
```

The expiry comparison and the use-count arithmetic are inside
`Redemption/RedemptionDecision.cs`, which is where the greppable rule
`expiry-or-use-count-judged-outside-the-decision` keeps them. The counts
themselves are on `Invitations/Invitation.cs`. The account template is
`Accounts/AccountTemplate.cs`, and the routine that applies it is
`Accounts/AccountTemplateApplication.cs`. THIS PARAGRAPH SAID THAT ROUTINE WAS
NOT WRITTEN, AND THE ARRANGEMENT IT DESCRIBES IS WHAT MADE THE SENTENCE SAFE TO
GO STALE: naming the directory rather than the file is what put the routine in
scope on the day it landed, so the widening nobody remembers to make did not have
to be made.

    git log --diff-filter=A --format='%h %ad %s' --date=short -- Jellyfin.Plugin.Invites/Accounts/AccountTemplateApplication.cs
    048c4b4 2026-08-30 Apply an account template in one routine, for #69

What that costs is worth having here rather than being read off a run: the first
run after it lands mutates a file the runs before it did not, so its score is not
comparable with theirs. #376 carries where the score stood before it and what
that gate has been reporting.

`Codes/InvitationCode.cs` is in scope and is not one of the four. It is there
because the decision's first act is to canonicalise what was presented, so a
mutant in that routine changes what the decision decides.

The file exclusions are named in `stryker-config.json` rather than counted here,
because the count moved once already and this sentence did not.

`Accounts/ServerAccounts.cs` reads account identifiers off the server's user
manager for the consistency report, which is #46 and not this scope. It is
excluded by name rather than by not being reached, so the exclusion is a
decision a reader can argue with. `Accounts/ServerAccountWrites.cs` is the write
side of the same contact and is excluded under #376, which its own section
below argues.

Everything else in the assembly. The store, the startup path, the clock and the
settings class are not mutated. A whole-assembly score is a number nobody acts
on, and the run says how much it left alone rather than leaving that to be
inferred:

```
$ dotnet stryker --config-file stryker-config.json
263   mutants got status Ignored.      Reason: Removed by mutate filter
155   total mutants will be tested
```

## The threshold is 100 and it is not a measured number

The break threshold is 100 per cent. Every mutant the run tests must die.

That number is not read off a score. It is the only threshold that says what the
run is for: a threshold below 100 is a budget for survivors, and a survivor
inside the budget is a rule nobody has to look at. At 95 a run over 155 mutants
can hide seven of them, and which seven is decided by whatever was written last
rather than by anybody.

The cost of the number is that this gate reds on a mutant somebody has to judge
rather than fix, and that is the intended cost. Judging it means either writing
the test that kills it or arguing here that its class does not belong in the
run, and both leave a record.

## The class that is not mutated, and what that costs

The `string` mutator is off.

What it did was replace a string literal with the empty string. Every one of the
seventeen mutants that survived the first run over this scope was the text of an
argument-exception message:

```
$ dotnet stryker --config-file stryker-config.json   # with the mutator on
Killed:   166
Survived:  17
Timeout:    1
The final mutation score is 90.76 %
```

Killing those seventeen means asserting the sentence inside each refusal. Those
sentences are written for whoever is reading a stack trace, they are never shown
to an operator, and a suite that asserts them refuses every improvement to the
wording of a refusal. The message is not what the routine does.

What turning the mutator off costs is one mutant of real value, and it is named
here rather than left out: `Invitations/InvitationLink.cs` builds the link's path
from the format `"{0}/{1}/{2}"`, and that literal is the shape of the link rather
than a message. With the mutator on that mutant was killed, so a test does notice
the path changing, and `InvitationLinkTests` still asserts it. What is lost is
the standing proof that it would notice. The measurement above is a one-off in
its place, and re-running it is one edit to `stryker-config.json`.

Nine of the other twelve string mutants that died were on
`string.IsNullOrWhiteSpace` conditions whose behaviour is covered by the
equality and logical mutators, which stay on.

## The file that is not mutated, and what that costs

`Accounts/RequestOperatorIdentity.cs` is excluded, beside `ServerAccounts.cs`
which was excluded for its own reason and `ServerAccountWrites.cs` which the
section below argues.

It is a two-member wrapper over the server's authorization context, and it holds
two mutants no test written against this suite can kill.

The first is the argument of `ConfigureAwait(false)`. Flipped to `true` it
resumes on the captured synchronisation context; a test host captures none, and
the routine has nothing after the await but a field read. So the two spellings
are one behaviour here, and the suite stays green on the flipped spelling for
that reason rather than for want of an assertion:

```
$ sed -i 's/ConfigureAwait(false)/ConfigureAwait(true)/' \
    Jellyfin.Plugin.Invites/Accounts/RequestOperatorIdentity.cs
$ dotnet test Jellyfin.Plugin.Invites.sln --nologo --configuration Release --no-build
Bestanden!   : Fehler:     0, erfolgreich:   636, übersprungen:     8, gesamt:   644
```

The second is the body of `OfAsync`, which the run replaces with a default
return. Killing it means asserting that the routine hands back the identifier the
server named, and the server's `AuthorizationInfo.UserId` has no setter:

```
error CS0200: Für die Eigenschaft oder den Indexer "AuthorizationInfo.UserId"
ist eine Zuweisung nicht möglich. Sie sind schreibgeschützt.
```

Naming an operator means building a user, and the assembly that declares one is
not referenced by the suite. `OperatorIdentityTests` says the same in its own
remarks and asserts what it can, which is that the wrapper reads the field and
invents nothing when the server names nobody. That assertion passes a routine
returning the empty identifier for any reason, so it cannot be the one that kills
this mutant.

A line-level disable was tried on the first of the two before the file was
excluded, and it is worth recording why it was not enough. It removed the boolean
mutant and the block removal underneath it, which the run had reported as
`Ignored` while the boolean stood, arrived as a survivor in its place. One
unkillable mutant became another, in the same file, so the threshold stayed
unreachable and the disable bought nothing.

What excluding the file costs is the standing proof for one guard: the
constructor refuses a null context, that mutant was killed on the run of
2026-08-30, and it is now unmeasured. `OperatorIdentityTests` still asserts it,
so what is lost is the proof that the assertion would notice rather than the
assertion.

## The second seam over the server's user manager is not mutated either

`Accounts/ServerAccountWrites.cs` is excluded, beside the two files above and
for the reason the first of them was excluded, made sharper by what the file is.

It is the write side of this plugin's contact with the server's user table, and
the reason no test can reach three of its four arms is written on the type: a
fake user manager would have to implement `IUserManager`, `ChangePassword` is a
member of it, and that member takes the account entity on the declared ABI floor
and the account identifier on the shipping version. So such a fake compiles
against exactly one end of the line this plugin claims and reds the floor build
at the other. There is no test host in which those arms run.

What that produced when the file first entered the scope, which it did on the
day it landed because the scope names the directory rather than the files, is
twelve survivors in one file:

```
$ dotnet stryker --config-file stryker-config.json --concurrency 4
Survived 15, of which 12 in Accounts/ServerAccountWrites.cs
```

Four of the twelve are the argument of `ConfigureAwait`, which is the class
argued at the excluded file above. The other eight are the constructor body, the
delegating body of the credential arm, and the guards and statements of the arm
that reads a policy and hands it back — every one of them behind a call the
suite cannot make.

What excluding the file costs is the standing proof for the one arm a test does
reach. `SetCredentialOn` takes an `object` for exactly this reason and
`ServerAccountWritesTests` drives both member shapes through it, so that arm is
asserted; what is lost is the proof that those assertions would notice a change.
The arm is also the one that carries the password, which is the reason to be
uncomfortable with the exclusion rather than to leave it unstated.

The routine that calls the seam stays in the scope and is not excluded.
`Accounts/AccountCreation.cs` is where the order of the three acts lives, which
is what #398 is about, and every mutant in it dies except the three arguments of
`ConfigureAwait`. Those carry a line-level disable naming this section rather
than an exclusion, so the statement mutations on the same three lines stay in the
run and are killed by the call trail the suite asserts. That the three are one
behaviour here was probed rather than argued:

```
$ sed -i 's/ConfigureAwait(false)/ConfigureAwait(true)/g'     Jellyfin.Plugin.Invites/Accounts/AccountCreation.cs
$ dotnet build --configuration Release && dotnet test --configuration Release --no-build
Bestanden!   : Fehler: 0, erfolgreich: 695, übersprungen: 8, gesamt: 703
```

## The four survivors, read against their sites and taken out of the run

#376 records that the last honest run of this configuration left four survivors
and that they had not been read against their sites. They have been now, all four
are equivalent mutants, and neither of the two repairs is an exclusion.

**Three of the four were three statements that said nothing.**
`Invitations/LiveCeilingReachedException.cs` carries three constructors an
analyser rule asks an exception type for, and each of them assigned zero to a
property whose own type defaults to zero. A block removal emptied each body and
no test noticed, because there was nothing to notice: `Live` and `Ceiling` are
`int` and read back as zero either way.

The repair is that the six statements are gone and the sentence they were trying
to say is in the type's remarks instead. That removes the three mutants rather
than excluding them, which is the difference between a threshold that is
reachable and one that a page has to keep explaining. `MutationSurvivorTests.TheConstructorsCarriedForTheAnalyserCountNothing`
still holds what the survivors pointed at, which is that those three
constructors report no counts, and it passes unchanged.

**The fourth is a refusal that is duplicated one call down.**
`Invitations/Retention.cs` refuses a null record at the top of `MayBeRemoved`,
and the routine it calls next, `RedemptionDecision.RetentionStartsAt`, refuses
the same argument with the same exception type and the same parameter name. So
a statement mutation that deletes the outer guard changes nothing a caller can
observe, and a test asserting the refusal passes with the guard and without it.

The repair here is a line-level disable carrying its reason, and the reason the
line stays is worth being explicit about: a public boundary that refuses its own
null is not a duplicate of a private one. Whoever edits `RetentionStartsAt` next
is not editing `MayBeRemoved`'s contract, and deleting a guard to reach a score
is the direction this page exists to argue against.

A line-level disable has been tried once before on this tree and bought nothing,
which is recorded above under the excluded file: it removed one unkillable mutant
and a second arrived underneath it. That is why this one is measured rather than
assumed. It bought something here:

```
$ dotnet stryker --config-file stryker-config.json --concurrency 4
Killed 298, Survived 0, Timeout 0
The final mutation score is 100.00 %
$ bash .github/lint/mutation-verdict.sh read StrykerOutput/<run>/reports/mutation-report.json
ok    no mutant timed out (#376): 479 mutants read from ...
```

That is this configuration reaching its own break threshold, on a run whose
verdict the reader above does not refuse. It is one run on one machine at one
concurrency, which is exactly the bound the section below states, and it is not
a claim about what the weekly job will report.

## What the run does not measure

A mutation score is a statement about the suite, not about the plugin. A routine
whose every mutant dies is one the suite would notice a change to; whether the
rule it enforces is the right rule is what the review and the decision documents
are for.

The scope is three directories and a file. Nothing here says the rest of the
assembly is well tested, and the coverage floors in `docs/coverage-floors.md`
are a different measure over a wider subject rather than a weaker one over this.

Timeouts count as killed, and that is the sentence on this page to be careful
with. A mutant that hangs the suite is one a test noticed, and this scope has
had such a mutant, in `Codes/InvitationCode.cs`. What the same sentence also
does is turn a run that was too slow into a run that passed, because nothing in
the report separates a mutant that hung the suite from one whose test host was
starved of a core.

That is measured rather than supposed. Two runs of this configuration over one
tree, one after the other on one machine:

```
$ dotnet stryker --config-file stryker-config.json
Killed 206, Timeout 85, Survived 0
The final mutation score is 100.00 %

$ dotnet stryker --config-file stryker-config.json --concurrency 4
Killed 287, Timeout 0, Survived 4
The final mutation score is 98.63 %
```

Stryker spawns one test host per core unless it is told otherwise, which was
sixteen on that machine; the second run was given four. The first met the break
threshold and the second did not. The four the second reports are the ones the
section above reads against their sites, and among the eighty-five the first
called timeouts were three block removals that emptied a constructor whose
properties already defaulted to the values it assigned. Nothing there can hang
anything, so those three were a statement about the run and not about the
mutants. Those three sites no longer exist, which is that section's repair
rather than anything about this measurement: the two runs quoted here are the
runs that were made, at the tree of the day they were made.

What follows for a reader of a green run is that the score is not the whole
verdict, and the timeout count is the number to read beside it. A run reporting
more timeouts than the handful this scope has genuinely produced has measured
less than its score says, in whichever direction the score moved.

## A timeout is not a kill, and this gate refuses a run that carries one

That is the decision this page left open, and the measurement above is what
takes it. The score stays what the tool computes, timeouts counted inside it.
What changes is that the score is no longer the whole verdict:
`.github/lint/mutation-verdict.sh` reads the run's own JSON report and refuses a
run carrying any timeout, and `stryker-mutation.yaml` runs it after the tool
under `if: always()`, so a run that already reds on the break threshold still
owes this reading.

Against the two runs above it refuses the first and passes the second. The pair
that produced two verdicts on one tree produces one.

The rule cannot be proved against anything tracked, because what it reads is
written by a run rather than committed. Fixtures stand in for that, and the
selftest is a step of the workflow rather than something run once at review
time:

```
$ bash .github/lint/mutation-verdict.sh selftest
bites timed-out (#376): .github/lint/fixtures/mutation-verdict/timed-out.trip.json is refused
bites no-mutants (#376): .github/lint/fixtures/mutation-verdict/no-mutants.trip.json is refused
passes clean (#376): .github/lint/fixtures/mutation-verdict/clean.json is read and not refused
bites absent (#376): a report that does not exist is refused
```

The second of the four is the near-miss worth naming rather than counting. A
reader that has stopped matching - one character wrong in how it breaks the
report up, or a report whose shape has moved - finds no mutant, therefore finds
no timeout, and reports the same silence as a run with nothing wrong. That
fixture is a report whose mutants call their outcome something other than
`status`, and it is refused rather than passed. The fourth is the same failure
in its cheapest form: a report that was never written.

What the rule costs is what the break threshold of 100 already costs, pointed at
a second class. A mutant that genuinely hangs the suite - this scope has had
one, in `Codes/InvitationCode.cs` - now reds this gate and has to be judged
rather than counted as a kill. Judging it means killing it faster, arguing its
class out on this page beside the `string` mutator, or raising the tool's own
timeout so a run can tell a hang from a starved host. All three leave a record.
Scoring it as a kill leaves none.

What the rule does not do is make the run reproducible, and reading a green run
as reproducible would be reading it as more than it says. A machine that times
out everything and one that times out nothing measure different sets either way;
this refuses both rather than reconciling them. Pinning a concurrency in
`stryker-config.json` is the other repair #376 names and it is not taken here:
it fixes the gate to one machine shape, and a runner and the machine the two
runs above were made on do not have the same number of cores, so a number chosen
for one is a guess about the other. That half is still open on #376.

Nothing here has run against a server.
