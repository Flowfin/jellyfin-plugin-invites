# Configuration reference, fixture

Every setting has a row and every row has a setting, so the two older legs are
quiet on this pair. What is wrong is one cell: `AllowRemoteAccess` has no
initialiser in the type beside this file, so a fresh install has it off, and the
Default column here says it is on.

That direction is the one worth catching. A reference that overstates what a
default grants is read by an operator as permission already given, and they
never open the page to take it away.

| Setting              | What it does                     | Default | Bounds     |
| -------------------- | -------------------------------- | ------- | ---------- |
| `PublicBaseAddress`  | The address links are built from | unset   | a URL      |
| `MaximumUseCount`    | How many accounts one link may   | 1       | 1 to 10    |
| `AllowRemoteAccess`  | Whether the account plays away   | true    | true/false |
