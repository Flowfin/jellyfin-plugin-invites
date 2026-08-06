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
