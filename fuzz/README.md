# The fuzzing corpus

`corpus/` holds the inputs a fuzz run starts from. One file is one input: the
file's text with a single trailing newline removed, because every editor
following this repository's `.editorconfig` writes one. A seed that was about a
trailing newline could therefore not be kept in a file; a leading one survives,
and `leading-newline.txt` is what the corpus carries instead.

The corpus is in the repository rather than in a runner cache. A corpus a
scheduled job keeps to itself is one nobody can read, review or shrink, and it
disappears the first time that cache is cleared.

## What the seeds are for

Each file is a shape the parser has to have an answer for, and the answer is not
written down here: it is what the harness asserts. Roughly, they are the
canonical forms, the ways a code is transcribed by a person (lower case, hyphens
and spaces for grouping, the characters the alphabet leaves out because they are
confusable), the lengths either side of a code, and the things that are not
codes at all — nothing, separators only, a character outside the alphabet, and
characters from outside ASCII that a browser will send without being asked.

`far-too-long.txt` earns its place: it is the seed that found the deliberately
introduced off-by-one the harness was proved against, before a single generated
input had run.

## Running it

The suite runs the harness at a small budget on every test run, so it cannot rot
unnoticed:

```
dotnet test --configuration Release --no-build --filter 'Category=Fuzz'
```

A longer run is the same command with a budget and a seed, which is what
`.github/workflows/fuzz.yaml` does on its schedule:

```
INVITES_FUZZ_ITERATIONS=1000000 INVITES_FUZZ_SEED=987654321 \
  dotnet test --configuration Release --no-build --filter 'Category=Fuzz' \
  --logger 'console;verbosity=detailed'
```

Every failure names the seed and the input that produced it, and a seed replays
the whole run. An unreadable budget is refused rather than replaced by the
default, because a run that spent a thousandth of what it was given reports the
same green as one that spent all of it.

## Growing it

A finding is a new file here, named for the shape rather than for the run that
found it, and kept after the defect is fixed. The harness is
`Jellyfin.Plugin.Invites.Tests/RedemptionFuzzTests.cs`, which says what it
asserts, what it does not, and why it is an in-process generator rather than a
coverage-guided one.
