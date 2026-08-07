# Contributing

## Sign your work

Every commit carries a `Signed-off-by` line matching its author. That line is
an assertion of the Developer Certificate of Origin, whose text is in
[DCO](DCO) at the root of this repository. Read it before you sign something
with it.

```
git commit -s
```

This is enforced rather than requested. `.github/workflows/dco.yml` walks every
non-merge commit in a pull request and reds the check on any commit whose
sign-off does not match its author, so an unsigned commit blocks the merge. To
fix a branch that already has some:

```
git rebase --signoff master
```

## Every change starts as an issue

An issue says what is wrong, what the evidence is, and what "done" means. If
the evidence is a number, it carries the command that produced it. Direct
pushes to `master` are refused; the change arrives as a pull request.

One topic per pull request. A change carrying two unrelated topics gets a
description of one of them.

## Build and test

```
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

`--locked-mode` fails rather than resolving a package version the committed
lock file does not name, so adding or moving a dependency has to arrive as a
diff in `packages.lock.json` that somebody reads. Regenerate it deliberately
with `dotnet restore` when that is what you meant.

Warnings are errors, and the analyzers are on by default in the project file.
A clean build means the analyzers passed, not that they were quiet.

## The headless rule

The suite runs on a machine with no display, no server and nobody watching. A
test may not:

- open a window or need a display
- ask for elevated rights, and a test that would need them is skipped with the
  skip disclosed rather than worked around
- write anywhere except a temporary directory it creates and removes
- read or write the machine's certificate stores
- open a network connection
- launch an external binary or expect one to be installed
- sleep on a real clock to wait for something, because four behaviours here are
  clock-driven and a suite that sleeps gets slower until people stop running it

If a behaviour cannot be tested inside that, it is not tested silently. Say so,
and say what covers it instead.

The rule is executed rather than trusted. `.github/workflows/headless.yaml` runs
the suite inside a container with no network interface, as an unprivileged user,
so a test that reaches out fails there rather than on the next machine.

The tests this rule refuses are listed in `docs/tests-not-written.md`, each with
the clause that refuses it and what covers the same risk instead. Read it before
adding a test that needs a browser, a server, a certificate or a sleep, because
the answer is probably already there and says what to write instead. Two of the
replacements are a person doing something once per release, and
`docs/manual-checks.md` is where a run of those is recorded.

## The invariant lint

Some of this plugin's rules are shapes that must never appear in the source: a
non-cryptographic random source, a stored secret compared with `==`, a code or a
secret handed to a log call, a user policy written outside the routine that
applies an account template, a link built from what a request says the host is.
Each of those is decided in prose by an issue and refused by a grep, because an
invariant nobody can grep for is one that comes back.

```
bash .github/lint/invariants.sh check .
bash .github/lint/invariants.sh selftest
```

`check` scans the tree and fails on a match. `selftest` fails unless every rule
still matches its tripping fixture and matches nothing in its clean one. The
selftest runs first on every workflow run rather than once at review time: most
of these rules cannot fire against the source yet, because the code they are
about is not written, and a rule that has quietly stopped matching the shape it
names would go green forever.

To add one:

- add a row to `RULES` in `.github/lint/invariants.sh`. The fields are separated
  by `@` because every pattern contains an alternation: the id, the issue that
  decided the rule, the pattern, and the lines the rule exempts.
- add a line to `explain()` saying what goes wrong when the shape appears. A
  failure that points at a line without saying why is one people route around.
- add `.github/lint/fixtures/<id>.trip.cs` holding the violation and
  `<id>.clean.cs` holding the same code with the violation taken out.
- run the selftest and watch the new rule report `bites`.

The rules match spellings, not meanings. `new Random()` written in the code path
is caught, and the same source reached through a helper two files away is not.
A green run says that none of those shapes appear literally, which is a smaller
claim than the invariants being upheld, and the dataflow queries in the CodeQL
workflow are the other half of that ground.

## The checks

What runs on a pull request is a file in `.github/workflows`, and the set is
printed rather than listed here, since a list in a document drifts against the
directory it describes:

```
git ls-files .github/workflows
```

Which of them a merge cannot pass without is a repository setting rather than a
file in the tree, so it is read rather than remembered:

```
gh api repos/iderex/jellyfin-plugin-invites/rulesets/20465179 \
  --jq '[.rules[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context]'
```

Several have a local route, and running those before you push is cheaper than
reading a red square afterwards:

- the build and the suite, with the three commands above
- the oldest server this plugin claims to load on:
  `dotnet build Jellyfin.Plugin.Template.sln -p:BuildAgainstAbiFloor=true`
- the invariant lint, with the two commands above
- the pull-request hygiene legs: `bash .github/lint/pr-hygiene.sh selftest`
- the sign-off, by committing with `git commit -s` in the first place

The rest have no local route worth writing down. The CodeQL analysis needs a
database and the CodeQL CLI, the headless run needs a container runtime and the
pinned image the workflow names, and the dependency review, the packaging job
and the workflow audit each need something that only exists on a runner. None of
those is a reason to push without running the ones that do.

## A change to the redemption decision

One routine decides whether an invitation is honoured, and the suite holds that
decision as a table of cases rather than as repeated test methods. Neither
exists yet: they are #56 and #102. Once they do, a change to that routine
arrives with the rows that cover it, because a branch nobody added a row for is
a rule the suite does not enforce.

## No guard without proof that it bites

A test or a check that has never been seen to fail proves nothing. When you add
one, break the thing it guards, watch it go red, put the change back, and write
what you did into the pull request. Pick the one-character mistake somebody will
actually make rather than a break that could not have happened.

## Claims carry the command that produced them

A number in an issue or a pull request body comes with the command that printed
it, run against what the reader will have rather than your working tree. Where
something was not checked, the body says it was not checked. A line admitting
that a leg was not run stays an admission; it does not get rewritten later into
a tick.

## What not to put in the tree

No attribution of authorship to a tool, no generated-by markers, and nothing
naming who or what produced a change. English in tracked files.

## Reporting a vulnerability

Not here. [SECURITY.md](SECURITY.md) has the private route and what to expect
from it.
