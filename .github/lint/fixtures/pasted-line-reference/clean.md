# A page whose references agree

Both references below name a line of `target.txt` and paste what that line
carries, so the check reads two and refuses neither.

    .github/lint/fixtures/pasted-line-reference/target.txt:2:A line a fixture points at.
    .github/lint/fixtures/pasted-line-reference/target.txt:3:A second line a fixture points at.

The target sits inside this directory rather than in the plugin, so an edit to
the plugin cannot move a line underneath this fixture and turn the clean case
into a failing one for a reason the fixture is not about.
