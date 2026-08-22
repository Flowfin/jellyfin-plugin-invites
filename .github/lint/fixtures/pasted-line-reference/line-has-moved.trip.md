# A page whose reference has moved

The first reference agrees, and it is here so that a rule firing on the whole
document rather than on the one bad line is caught.

    .github/lint/fixtures/pasted-line-reference/target.txt:2:A line a fixture points at.

The second names the line after it and pastes the line before it, which is what
one inserted line does to every reference below the match.

    .github/lint/fixtures/pasted-line-reference/target.txt:3:A line a fixture points at.

Nothing else on this page carries a line number, so the count the selftest reads
is two and the refusal is the line above.
