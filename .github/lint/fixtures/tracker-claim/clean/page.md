# A status line, as a fixture

Deliberately small. What this file is for is the comparison, not the reasons.

Three claims, one per state the listing can answer with, so a pair that agreed
because only one of them was being read would not agree here.

Status: neither part exists. #107 is open, and no manual check has been recorded.

Status: the formatter runs on every pull request. #19 is closed and the row above
is what it replaced.

Status: the landing that carried it is in the mainline. #354 is closed, which the
listing spells MERGED, because a merged pull request is closed and a page saying
so is right rather than wrong about a distinction it does not draw.

The fenced block below is evidence of what a command said rather than a sentence
this page asserts, so nothing in it is read as a claim:

```
$ gh issue view 4242 --json state --jq .state
#4242 is open
```

And a past-tense sentence is outside the pattern on purpose: #19 was open on the
day the row above it was written, and the tracker cannot refute that.
