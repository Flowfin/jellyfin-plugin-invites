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
routines, because the first three already sit together and the fourth has no
code yet:

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

Two exclusions:

`Accounts/ServerAccounts.cs` reads account identifiers off the server's user
manager for the consistency report, which is #46 and not this scope. It is
excluded by name rather than by not being reached, so the exclusion is a
decision a reader can argue with.

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

## What the run does not measure

A mutation score is a statement about the suite, not about the plugin. A routine
whose every mutant dies is one the suite would notice a change to; whether the
rule it enforces is the right rule is what the review and the decision documents
are for.

The scope is three directories and a file. Nothing here says the rest of the
assembly is well tested, and the coverage floors in `docs/coverage-floors.md`
are a different measure over a wider subject rather than a weaker one over this.

Timeouts count as killed. A mutant that hangs the suite is one a test noticed,
and this run has had one, in `Codes/InvitationCode.cs`.

Nothing here has run against a server.
