# Releasing

A release is published by pushing a tag. Nothing is created by hand.

## The tag

The tag has the form `X.Y.Z-stable` or `X.Y.Z.W-stable`, for example `1.4.0-stable`
or `0.1.0.0-stable`. The numeric part is the plugin version that Jellyfin installs,
and it must be exactly the `version` in `build.yaml`, written the same way, with the
same number of parts. The `-stable` suffix lives only in the tag and in the release
name.

A tag of the form `X.Y.Z-rcN` or `X.Y.Z.W-rcN` is a dry run rather than a release.
The numeric part means the same thing and is checked the same way; the `-rcN` suffix
is what tells the run to stop after the build.

## The dry run

Pushing an `-rcN` tag runs the metadata gate and the build and stops. Nothing is
signed, no release is created, and the archive is left as a build artifact with the
thirty day retention every other artifact here has.

```
git tag 1.4.0-rc1 <commit>
git push origin 1.4.0-rc1
```

Everything that can refuse a release refuses a dry run in the same words, because it
is the same gate and the same build reading the same `build.yaml`. What a dry run
does not exercise is the two jobs it stops before: the provenance statement and the
release itself, including the refusal to touch a release that already exists. Those
run for the first time on the first `-stable` tag, and a dry run says nothing about
them.

Use it after step 1 below and before step 4, when the version and the changelog have
moved and nobody has yet found out whether the packaging tool is happy with them. Its
archive is also what the manual checks are run against, which is step 3 and the other
reason the dry run comes first. The cost of the answer arriving late is a spent tag: a
`-stable` tag that fails the gate cannot be reused, and the fix is a new version
rather than a new tag.

The suffix is what the run reads, in one place, and the two jobs that reach outside
the run are conditioned on what it read:

    $ git grep -n "needs.gate.outputs.publish" -- .github/workflows/publish.yaml
    .github/workflows/publish.yaml:450:    if: needs.gate.outputs.publish == 'true'
    .github/workflows/publish.yaml:481:    if: needs.gate.outputs.publish == 'true'

Both lines moved down by thirty-six under #119, which put the manifest entry's
generation into the build job above them, and up by one under #394, which took
the gate's own reader of `build.yaml` out of the file. Neither condition changed
and neither job gained or lost one; what moved is where they sit in the file.

## The manual checks

Two things this plugin has to be right about are checked by a person rather than
by the suite: that the setup page renders in a browser, and that the packaged
plugin loads into a clean Jellyfin server. `docs/tests-not-written.md` is where
the suite refuses them and `docs/manual-checks.md` is the form a run is recorded
on. They are a step of this procedure rather than a good habit beside it, and a
release cut without them is a release nobody has installed.

Run them against the dry run's archive, which is what the `-rcN` tag leaves as a
build artifact, and record the run before the `-stable` tag is pushed. That order
is the whole point: a check made after publication finds a broken package that
people can already install.

The record names the checksum of the archive it was installed from, which is the
cell `docs/manual-checks.md` asks for above the table. Two things about it are
worth stating rather than leaving to be worked out. The `-stable` run builds the
archive again from the same commit, so the record covers the artefact it was run
against and nothing here claims the two are byte-identical. And the `.md5` a
catalogue serves is produced by the release job, so the value in the record is
computed from the downloaded archive rather than copied from a release that does
not exist yet.

Which server lines the second check is run on is item 1 in #11, and it is
answered: one line, 10.11, decided on #97. So the check is run once rather than
per line of a set nobody has enumerated, and the record still says which line it
was, because a record that leaves the line to be inferred from the date is a
record nobody can read back. `build.yaml` declares `targetAbi` and no field
names a ceiling; what the plugin does when it meets a line it was not built for
is #97 rather than something this page decides.

A check that could not be run says so on the record, with the reason. A row
admitting a check was skipped stays an admission and is not rewritten later into
a result.

Nothing enforces any of this, and the sentence is stronger than it sounds. No job
reads `docs/manual-checks.md`:

    $ git grep -n 'manual-checks' -- .github/ | grep -vE ':[0-9]+:[[:space:]]*#' ; echo "exit=$?"
    exit=1

THE COMMAND CHANGED AND THE CLAIM DID NOT. It was a bare `git grep` over
`.github/`, and it stopped being a probe for this sentence the moment a workflow
mentioned the page in a comment: `e2e-no-web-client.yaml` names it to say which
half of that check it takes over, reads nothing, and made the paste exit 0. The
claim was still true and the command had stopped testing it, which is the shape
this repository has already been caught by twice. The filter drops a comment line
in both the YAML and the shell under it, so a job that actually read the file
would still be found. It was the pasted-status check that found this, on the
change that caused it.

So no gate compares that file against the tag, and a `-stable` tag pushed with it
untouched publishes exactly as if both checks had been run and passed. This
section is the whole control. No issue holds a mechanism for it today, which is
said here rather than left to be discovered by somebody looking for the check
that refuses a release with no record.

## Cutting a release

The four steps below are done by a person. Everything after the tag is pushed is
the `Publish Release` workflow, and no step of it waits for a hand.

1. On the release branch, move three things together in one change and merge it:
   `version` in `build.yaml`, the `changelog` key in the same file, and the entry
   for that version in `CHANGELOG.md`. The two changelog texts carry the same
   words, because one is what a catalogue shows an operator deciding whether to
   upgrade and the other is what somebody reads in the repository.
   `CHANGELOG.md` holds the rule for what an entry has to say, including the class
   of change every entry states whether or not the answer is nothing.
2. Check that the commit you want to release is on that branch.
3. Run the manual checks above against that commit's dry-run archive and append
   the filled-in record to `docs/manual-checks.md`.
4. Push the tag for that commit:

    ```
    git tag 1.4.0-stable <commit>
    git push origin 1.4.0-stable
    ```

The `Publish Release` workflow takes it from there.

Only part of step 1 is held by anything but the person doing it. The run fails a
tag whose numeric part disagrees with `version`, and it fails a `build.yaml`
whose `changelog` key is absent or empty, for a reason that is about the
packaging tool rather than about release notes:

    $ git grep -n 'changelog' -- .github/workflows/publish.yaml
    .github/workflows/publish.yaml:142:          # directory, and the two already disagreed: asked for changelog, this
    .github/workflows/publish.yaml:157:          # changelog is in this list because the packaging tool reads build_cfg
    .github/workflows/publish.yaml:158:          # ['changelog'] without a default and dies with a Python KeyError when it is
    .github/workflows/publish.yaml:167:          for key in name guid version targetAbi framework owner overview description category changelog; do

The command returned three lines until #394 and returns four, and the key list
is one name shorter. That issue gave `build.yaml` one reader, so the gate asks
`.github/lint/manifest.sh` for each of those keys instead of grepping for them:
the first line above is a remark about that reader, and `artifacts` left the
list because it is a sequence rather than a scalar and is asked for on a line of
its own below. What the gate refuses is the same key list and is slightly wider
on each of them, which is why the sentence above now says absent or empty: the
reader answers the same way for both, where the grep it replaced could tell them
apart.

Nothing reads what that key says, compares it against `CHANGELOG.md`, or notices
that neither text moved with the version. A release whose notes still describe the
previous version passes every check in this repository, so the agreement of those
three edits is a thing a person keeps.

Push one tag at a time and wait for its run to finish. GitHub keeps at most one
queued run per concurrency group, and although the group here is keyed on the tag,
serialising them by hand is what keeps the release order readable.

## What the run produces

The workflow builds the plugin from the tagged commit, creates the GitHub release
for the tag, and attaches four files:

- the plugin archive
- the packaging metadata written beside it, `<archive>.zip.meta.json`
- one `.md5` file, the checksum of the archive
- one `.sha256` file for the same archive

The `.md5` is the value a Jellyfin catalog serves as the plugin checksum. There is
exactly one per release so that no generator can pair a checksum with the wrong
file. Both the archive and the metadata are checked for existence by name before the
release job runs, so a release with three of the four files is not a state this route
can reach.

The run also signs a build provenance statement for the archive, in a separate job
that downloads the archive and runs no build tooling. A downloaded archive can be
checked against it:

```
gh attestation verify <archive>.zip --repo <owner>/<repository>
```

Nothing here writes a plugin catalog. A GitHub release is the whole output. If this
repository previously published through the Jellyfin meta plugins workflow, that path
is gone and no catalog is fed until a manifest generator is added.

## What fails the run

- The tag ends in neither `-stable` nor `-rcN`, or the workflow was started from
  something other than a tag.
- The numeric part of the tag differs from `version` in `build.yaml`.
- `build.yaml` is missing a required field, or `version`, `targetAbi`, `framework`
  or `guid` has the wrong shape.
- `framework` in `build.yaml` names a target the plugin project is not built for.
- A packaging manifest that shadows `build.yaml` is present, such as `jprm.yaml` or
  `meta.yaml`.
- `build.yaml` declares an `image` file that is not in the repository.
- The tagged commit is not contained in a release branch, or the tag was moved after
  the run started.
- There is no `packages.lock.json` next to the plugin project, so the release build
  cannot restore against a reviewed dependency graph. Create one with
  `dotnet restore <project> -p:RestorePackagesWithLockFile=true` and commit it.
- The version stamped into the assembly is not the version in `build.yaml`.
- The build produced no archive, or more than one, or no packaging metadata.
- A release already exists for the tag.

All of these fail before anything is published.

## What the run notes without failing

The packaging tool warns when `build.yaml` declares neither `image` nor `imageUrl`.
The plugin then shows without a logo in a catalog. That is a warning on every run
until a logo exists, and it is not a reason to hold a release.

## Re-running

A release that exists is not touched again. The release job asks whether a release
exists for the tag before it writes anything and stops if one does, and the upload
step is configured not to replace an asset of the same name. Replacing the bytes of a
version people have already installed is the failure this prevents, and it is worth
more than the convenience of a re-run.

So: if a release went out with the wrong contents, fix the problem, raise the version
in `build.yaml`, write the entry for the new version in both places as step 1 above
asks, and push a new tag.

If a run failed **before** the release was created, the tag is still clean. Fix the
cause and re-run the workflow from the Actions page, or delete and re-push the tag.

If a run failed **after** the release was created but before every asset was attached,
the release is incomplete and a re-run will refuse it. What is possible then depends
on the repository settings below. Without immutable releases you can delete the
incomplete release, delete the tag, and push it again. With immutable releases you
cannot, and the version has to be raised.

## Who may release

Whoever can push a `-stable` tag matching the patterns above. Nothing else stands in
the way. An `-rcN` tag is not a release and does not need the same answer, since the
run it starts creates nothing. The workflow declares no environment, so no reviewer is
asked between the push and the release:

    $ git grep -c 'environment:' -- .github/workflows/publish.yaml; echo "exit=$?"
    exit=1

and this repository carries one ruleset, which is about branches:

    $ gh api repos/Flowfin/jellyfin-plugin-invites/rulesets --jq '.[]|[.name,.target]|@tsv'
    gate	branch

The list below asks for a rule restricting who may push `*-stable` tags. That rule
is not in place. Until it is, the people who may release are the people who may
push, and the sentence above is the whole answer rather than a summary of a
control.

The artefact is authenticated rather than the person. The run signs a build
provenance statement for the archive, and `gh attestation verify` above is how
somebody who downloaded it checks that it came from this repository's workflow.
That says nothing about who pushed the tag.

## Repository settings this expects

- Default workflow permissions set to read only.
- A rule that restricts who may push `*-stable` tags.
- The `ABI floor build` check required on the release branches.
- Immutable releases, if the repository wants the guarantee that a published release
  can never be edited or deleted at all. The workflow does not depend on it: the
  refusal to touch an existing release is enforced in the release job. Turning it on
  removes the only recovery path for an incomplete release, so try it on one
  repository and cut a release there before turning it on everywhere.
