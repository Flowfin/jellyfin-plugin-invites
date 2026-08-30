# Manual checks before a release

Two things this plugin has to be right about cannot be checked by the suite, for
reasons written down in `docs/tests-not-written.md`: whether the setup page
actually renders in a browser, and whether the packaged plugin loads into a real
Jellyfin server. Both are replaced by a person doing them once per release.

A manual check with no place to record it is a manual check nobody can tell was
skipped. This file is that place. It is append-only in practice: a release gets
a section, the section gets filled in, and a later release never edits an
earlier one.

## What is checked

### The setup page renders

Follow a minted invitation link in an ordinary browser against a server running
the packaged build. Confirm the setup page appears with its fields, that
submitting it produces an account, and that a refused invitation shows the
refusal rather than a blank page or a stack trace.

Run it against a server with no web client installed. That is not an unusual
server chosen to be strict about it; it is the condition the page's whole shape
was picked for. #74 chose an embedded page over a redirect into the client
precisely so the flow survives a server that has no client, and its last clause
asks for the page to render on one. A run against a server that has the client
answers a different question and cannot be read as that clause.

The `Notes` cell says which it was. A run that could only be made against a
server carrying the web client is still worth recording, and it says so rather
than being filed as the check the clause asks for.

Half of that condition is executed now and the half left is the one a person is
here for. `.github/workflows/e2e-no-web-client.yaml` starts a published server
with the client turned off, confirms it is gone before asking for anything else,
and compares the served page against the tracked file byte for byte. So whether
the page ARRIVES on such a server is answered on every pull request, and what
this run is still for is whether a browser renders what arrives. A run that
finds the page missing or altered would be finding something that job already
refuses; a run that finds it unreadable is finding what nothing else can.

This covers what the route-level tests in #107 cannot see. Anything the route
tests do cover is not repeated here, because a manual step that duplicates an
automated one is a step people learn to skip.

### The packaged plugin loads

For each supported server line, install the packaged artefact into a clean
Jellyfin server, restart it, and confirm the plugin appears in the dashboard
with its configuration page openable. Which lines are supported is item 1 in
#11, and it is answered: one line, 10.11, decided on #97. The table below keeps
the line as a column to fill in anyway, because what a run was made against is a
fact of that run and not of this page, and a record that inherits the line from
a sentence written above it stops being readable the day the answer moves.

### The plugin does its job on that server

Loading is the cheapest half of the question and it is not the one an operator
cares about. A plugin that appears in the dashboard and then mints nothing has
passed the check above and failed the release. So the run goes on: mint one
invitation, redeem it, and read the resulting account's policy back against the
template the invitation carried.

The policy is read field by field rather than glanced at. THIS PARAGRAPH SAID THE
TEMPLATE NAMES SEVEN GRANTS AND IT NAMES SIXTEEN PROPERTIES. Eight permissions
arrived on that type under #64 and each one is a field of the server's policy
somebody doing this check has to look at:

    grep -cE '^    public .* \{ get; \}' Jellyfin.Plugin.Invites/Accounts/AccountTemplate.cs
    16

Fifteen of those sixteen properties are written to the account's policy by the
routine that applies a template. The one that is not is `MayManage`, which the
server carries no single field for and which #62 owns.

The fields the template does not name are supposed to be exactly what the server
set when it created the user, so a run that only confirms the account exists has
not looked at the half where a mistake hides. #69 is where that table is written
and it is what this row reads against, so this page does not carry a second copy
of it. That table exists now:

    grep -cE '^        policy\.' Jellyfin.Plugin.Invites/Accounts/AccountTemplateApplication.cs
    15

What it does not remove is this row. The routine is asserted against a policy a
test hands it, and this check is the one that reads a policy off a running
server, which nothing in this repository does.

Two of those three steps cannot be run today, and this is the form rather than
the run, so they are here waiting for the release rather than left out of it:

    $ git grep -nE '\[Http(Get|Post)' -- Jellyfin.Plugin.Invites/Controllers/RedeemController.cs
    Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:59:    [HttpGet("{code}")]

The redemption route serves a page and there is no post behind it, which is #74,
so nothing is redeemed and no account is created. A row for a step that cannot
be run says so in `Notes` and stays in the table. Taking it out until the code
lands is how a release gets cut without it.

## How a run is recorded

Copy the block below, fill every cell, and append it under `## Runs`. A cell
that cannot be filled says why rather than being left blank, and a check that
was not done says it was not done. A row admitting a check was skipped stays an
admission.

```
### <version>, <commit sha>

Archive: <file name>
Checksum: <md5 of the archive that was installed>
Server: <the exact server version the run was made against>

| Check | Server line | Result | Notes |
| --- | --- | --- | --- |
| Setup page renders | | | |
| Packaged plugin loads | | | |
| One invitation minted | | | |
| That invitation redeemed | | | |
| Account policy matches its template, field by field | | | |
```

The server version is a separate line from the line column, and both are wanted.
The column says which supported line the row answers for and the line above says
which build of it was actually running, because a plugin that loads on 10.11.11
and not on 10.11.4 fails on a line this page would otherwise record as passing.

The checksum is above the table rather than a column in it because every check
in it is run against one archive, and a value repeated per row is a value two
rows can disagree about. It is the archive's `md5` because that is the digest a
Jellyfin catalogue serves as the plugin checksum, so an operator comparing what
they installed against what this record covers compares the same kind of value:

    git grep -n 'md5sum' -- .github/workflows/publish.yaml
    .github/workflows/publish.yaml:514:          md5sum "${zip}" > "${zip%.zip}.md5"

Compute it from the archive that was actually installed rather than copying it
out of a release. The run these checks belong to is the dry run, which creates no
release, and `docs/RELEASING.md` is where that order is set out.

## Runs

None. Nothing has been released, and no packaged build exists to check:

```
$ gh release list --repo Flowfin/jellyfin-plugin-invites
```

The release process now names these checks as a step of cutting a release, in
`docs/RELEASING.md`, so what this section is waiting on is a release rather than
a procedure. That is #155. An empty section here is the honest state rather than
a missing file, and it stops being empty the first time somebody pushes a tag.
