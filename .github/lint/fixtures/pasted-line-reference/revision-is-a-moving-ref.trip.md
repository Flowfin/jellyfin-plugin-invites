# A page whose reference names a branch rather than a commit

A reference may name a revision, and a branch is not one this check can stand
behind: it resolves to whatever that ref holds at the moment the check runs, so
the same bytes pass on a branch and fail on the mainline as soon as a merge
moves a line above the target. The change that caused it is green and the one
that meets the failure is somebody else's.

    origin/master:.github/lint/fixtures/pasted-line-reference/target.txt:2:A line a fixture points at.

The content pasted above is what the target carries, so a check that only
compared content would pass this and report nothing. What is wrong with it is
the revision, not the line.
