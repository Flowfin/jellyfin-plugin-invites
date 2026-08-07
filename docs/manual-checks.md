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

This covers what the route-level tests in #107 cannot see. Anything the route
tests do cover is not repeated here, because a manual step that duplicates an
automated one is a step people learn to skip.

### The packaged plugin loads

For each supported server line, install the packaged artefact into a clean
Jellyfin server, restart it, and confirm the plugin appears in the dashboard
with its configuration page openable. Which lines are supported is item 1 in
#11 and is not answered, so the table below leaves the line as a column to fill
in rather than naming one here.

## How a run is recorded

Copy the block below, fill every cell, and append it under `## Runs`. A cell
that cannot be filled says why rather than being left blank, and a check that
was not done says it was not done. A row admitting a check was skipped stays an
admission.

```
### <version>, <commit sha>

| Check | Server line | Result | Notes |
| --- | --- | --- | --- |
| Setup page renders | | | |
| Packaged plugin loads | | | |
```

## Runs

None. Nothing has been released, and no packaged build exists to check:

```
$ gh release list --repo Flowfin/jellyfin-plugin-invites
```

The release process is #118 and the first release is #155. This section stays
empty until one of them lands, and an empty section here is the honest state
rather than a missing file.
