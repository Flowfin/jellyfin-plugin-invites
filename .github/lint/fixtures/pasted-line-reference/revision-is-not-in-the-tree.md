# A page whose reference names a revision this clone does not carry

A reference may name a revision, and then it is read at that revision rather
than at this commit. Where the revision is not in the clone the check has read
nothing, and it says so and counts it rather than passing the reference for
being unreadable.

    0000000000000000000000000000000000000000:.github/lint/fixtures/pasted-line-reference/target.txt:2:A line a fixture points at.

The content pasted above is the line that revision would have carried if it
existed, so a check that silently fell back to the working tree would pass this
and report nothing.
