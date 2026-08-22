# Configuration reference, fixture

Every setting has a row and every row has a setting, so the two older legs are
quiet on this pair. What is wrong is one cell: `MaximumUseCount` is initialised
to 1 in the type beside this file and the Default column here says 10, which is
the top of its own Bounds cell rather than its default.

An operator reading this raises the setting to what they think is the default
and gets nine more accounts per link than they meant to.

| Setting              | What it does                     | Default | Bounds     |
| -------------------- | -------------------------------- | ------- | ---------- |
| `PublicBaseAddress`  | The address links are built from | unset   | a URL      |
| `MaximumUseCount`    | How many accounts one link may   | 10      | 1 to 10    |
| `AllowRemoteAccess`  | Whether the account plays away   | false   | true/false |
