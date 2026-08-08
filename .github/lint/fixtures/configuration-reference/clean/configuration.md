# Configuration reference, fixture

A reference with a row for every setting in the type beside it. It carries the
things a real one carries that a naive row scan would miscount: a header row, a
separator row, a second table that is not the settings table, and a paragraph
mentioning `MaximumUseCount` in backticks outside any table.

| Setting              | What it does                     | Default | Bounds     |
| -------------------- | -------------------------------- | ------- | ---------- |
| `PublicBaseAddress`  | The address links are built from | unset   | a URL      |
| `MaximumUseCount`    | How many accounts one link may   | 1       | 1 to 10    |
| `AllowRemoteAccess`  | Whether the account plays away   | false   | true/false |

The ceiling above is the one an operator raises first, so `MaximumUseCount` gets
a section of its own further down in the file this fixture stands for.

| Not a setting | Something else entirely |
| ------------- | ----------------------- |
| a row         | with no backticks       |
