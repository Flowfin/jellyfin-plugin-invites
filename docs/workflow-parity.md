# Workflow parity with the SSO plugin gate

This is the ledger of what this repository takes from the gate on
`iderex/jellyfin-plugin-sso`, what it leaves, and what it carries that the gate does
not have. One row per workflow file, in both directions, so a workflow can be absent
here on purpose rather than by having been forgotten.

The two listings the rows are taken from, rather than from memory:

```
$ gh api repos/iderex/jellyfin-plugin-sso/contents/.github/workflows --jq '.[].name'
$ git ls-files .github/workflows
```

Adopted means it lands here, and the row names the issue that lands it. Declined
means it does not, with the reason. Deferred means a later milestone decides, and
counts as declined until then. Kept and removed are the two answers for a workflow
this repository has that the gate does not.

An answer is a decision rather than a description of the directory as it stands. A
row saying removed names where the removal is carried out, and until that lands the
file is still there.

A workflow this repository has and this ledger does not name is a blank row rather
than an absence, and it is the failure mode this file has already had: seven of them
accumulated while the second table stood still. Nothing checks that, so it is checked
by hand, and the check is written here rather than remembered:

```
$ git ls-files .github/workflows | sed 's|.*/||' \
    | while read -r f; do grep -qF "\`$f\`" docs/workflow-parity.md || echo "$f"; done
```

Empty output is both tables accounting for the directory. It says nothing about
whether an answer is the right one, which is what a reader is for.

## What the target gate has

| Workflow | Answer | Reason |
| --- | --- | --- |
| `build.yml` | adopted, landed | On the target it is a reusable workflow the others call rather than a check of its own, and #14 landed the same shape here as `plugin-build.yaml`. |
| `codeql.yml` | adopted, landed | #16 landed a CodeQL workflow this repository owns as `scan-codeql.yaml`, replacing the shared call. It runs security-extended rather than the target gate's `+security-and-quality`, which is the same security set without the quality queries. |
| `dco.yml` | adopted, landed | Already in the tree at `e776ccf`. |
| `dependency-review.yml` | adopted, landed | Already in the tree at `e776ccf`. |
| `dotnet.yml` | adopted, landed | The build and test legs landed with #14, and #15 adds the oldest supported server ABI to them. |
| `e2e-login.yml` | declined | That suite drives a browser against a real identity provider, and this plugin's equivalent surface is covered at the route level plus one recorded manual check, because a browser test needs a display and a network and the headless rule refuses both. |
| `fuzz.yml` | adopted | #21 fuzzes the invitation code parser on a schedule. |
| `manifest-freshness.yml` | deferred to M12 | There is no published manifest to keep fresh until a release process exists. |
| `nightly-betas.yml` | deferred to M12 | Nothing is published yet, and how many publishing workflows survive depends on whether one server line is carried or two. |
| `opengrep.yml` | adopted, landed | #18 adopted a greppable invariant lint and seeded it with this plugin's invariants. It landed at `219935b6` as `invariants.yaml` plus `.github/lint/invariants.sh`, and reports `Invariant lint`. #18 stays open on the required-check clause rather than on the file. |
| `pr-hygiene.yml` | adopted, landed | #17 adopted the legs that are decidable by a machine and dropped the ones that need a person to judge. It landed at `9f3245fd` as `pr-hygiene.yaml` plus `.github/lint/pr-hygiene.sh`, and reports `Deterministic pull-request hygiene`. |
| `prettier.yml` | adopted, landed | #19 adopted formatting checks for the non-C# files. It landed at `5c98896b` as `prettier.yaml`, and reports `Non-C# files are formatted`. Its glob is the served-page surface and does not reach markdown, which is written in the file itself. |
| `publish-beta.yml` | deferred to M12 | Same as the other publishing workflows, there is nothing to publish before a release process exists. |
| `publish-failure-alert.yml` | deferred to M12 | It alerts on a publishing failure, so it cannot land before publishing does. |
| `publish-jf12-beta.yml` | deferred to M12 | Whether a second server line is carried at all is an open decision in #11. |
| `publish-jf12-stable.yml` | deferred to M12 | Same as above, and it doubles the publishing surface if the answer is two lines. |
| `publish.yml` | deferred to M12 | The release process is written in M12 and this is the workflow that runs it. |
| `regenerate-manifest.yml` | deferred to M12 | It maintains the manifest a catalogue reads, which does not exist until something is published. |
| `scorecard.yml` | adopted, landed | Already in the tree at `e776ccf`. |
| `stryker-mutation.yml` | adopted | #22 measures the test suite with mutation testing on the redemption decision. |
| `unicode-guard.yml` | adopted, landed | Already in the tree at `e776ccf`. |
| `wiki-lint.yml` | declined | This repository has no wiki and its documentation lives in the tree where the ordinary checks already see it. |
| `zizmor.yml` | adopted, landed | Already in the tree at `e776ccf`. |

## What this repository has

| Workflow | Answer | Reason |
| --- | --- | --- |
| `abi-floor.yaml` | kept | This repository's own, landed by #15 at `b80b85b2`. The shipping build compiles against a package from the middle of the server line while `build.yaml` invites people to install on the oldest version of it, and nothing in the ordinary build notices the gap. Reports `Build against the declared ABI floor`. It is a file of its own rather than a leg inside the build, which is what the `dotnet.yml` row above says #15 does to that workflow on the target gate. |
| `build.yaml` | kept | Since #14 it calls a reusable workflow this repository owns rather than a shared template one, and its job id is what the required check context is built from. |
| `configuration-reference.yaml` | kept | This repository's own, landed by #113 at `6e63c3f8`. It holds `docs/configuration.md` to the configuration type in both directions, so a setting without a row and a row without a setting both red. Reports `Every setting has a row`. Nothing on the target gate's list answers to it. |
| `headless.yaml` | kept | This repository's own, landed by #99 at `7c2fb72d`. It runs the suite in a container with no network interface as an unprivileged user, so the headless rule in `CONTRIBUTING.md` is executed rather than asserted. Reports `Suite runs headless and offline`. Nothing on the target gate's list answers to it, and it is the leg that makes declining `e2e-login.yml` a decision rather than an omission. |
| `invariants.yaml` | kept | This repository's answer to the target gate's `opengrep.yml`, landed by #18 at `219935b6`. Six greppable rules, each with a fixture pair the workflow re-proves on every run rather than at review time. Reports `Invariant lint`. |
| `package.yaml` | kept | This repository's own, landed by #20 at `fa2aa6fb`. It packages with JPRM on every pull request and generates a bill of materials, so a packaging mistake is found on the change that caused it rather than at a release. #35 is where the runtime dependency set it records is held empty. |
| `pr-hygiene.yaml` | kept | This repository's answer to the target gate's `pr-hygiene.yml`, landed by #17 at `9f3245fd`. The legs a machine can decide, in two tiers, with the ones needing a person's judgement deliberately absent. Reports `Deterministic pull-request hygiene`. |
| `prettier.yaml` | kept | This repository's answer to the target gate's `prettier.yml`, landed by #19 at `5c98896b`. Check mode only, never write mode, and it bites its own fixture before it scans the tree. Reports `Non-C# files are formatted`. |
| `changelog.yaml` | removed, in #7 | It drafts a release and rewrites the version in `Directory.Build.props` and `build.yaml` with `sed` and `yq`, which puts back the second source of truth #9 removes, and there is nothing to release before M12 writes a release process. |
| `command-dispatch.yaml` | removed, in #7 | It turns an issue comment into a repository dispatch, which is a write surface driven by untrusted input, and it is carried for slash commands nobody here uses. |
| `command-rebase.yaml` | removed, in #7 | It exists only to serve the dispatcher above and has no reason to stay once that goes. |
| `dco.yml` | kept | Adopted from the target gate, landed at `e776ccf`. |
| `dependency-review.yml` | kept | Adopted from the target gate, landed at `e776ccf`. |
| `plugin-build.yaml` | kept | The first-party build leg landed by #14, which is this repository's answer to the target gate's `build.yml`. |
| `plugin-test.yaml` | kept | The first-party test leg landed by #14, which is this repository's answer to the target gate's `dotnet.yml`. |
| `publish.yaml` | deferred to M12 | It publishes on a release, and what it does is decided when the release process it belongs to is written. Its call is pinned by commit and its job carries its own permissions block, under #14, which changes nothing about the release path. |
| `scan-codeql.yaml` | kept | #7 corrected the input so the scan ran against this repository at all, and #16 replaced the shared call with the analysis written out in this file. It is this repository's answer to the target gate's `codeql.yml`. |
| `scorecard.yml` | kept | Adopted from the target gate, landed at `e776ccf`. |
| `sync-labels.yaml` | removed, here | It replaces this repository's labels with a shared list and deletes every label that list does not name, and two labels this plugin's issues lean on are not named there. |
| `test.yaml` | kept | Since #14 it calls the first-party test leg rather than a shared template one. |
| `unicode-guard.yml` | kept | Adopted from the target gate, landed at `e776ccf`. |
| `zizmor.yml` | kept | Adopted from the target gate, landed at `e776ccf`. |

## The label row, in full

The one-sentence reason above is short because the table wants it short. The
measurement behind it is this. The workflow calls a shared one that applies a central
label list with `delete-other-labels: true`:

```
$ gh api -H "Accept: application/vnd.github.raw" \
    repos/jellyfin/jellyfin-meta-plugins/contents/.github/workflows/sync-labels.yaml
        with:
          config-file: https://raw.githubusercontent.com/jellyfin/jellyfin-meta-plugins/master/.github/plugin-repo-labels.yaml
          delete-other-labels: true
```

How often each of this repository's labels is used:

```
$ gh issue list --state all --limit 300 --json number,labels \
    --jq '[.[] | .labels[].name] | group_by(.) | map({label: .[0], count: length}) | sort_by(-.count) | .[] | "\(.count)\t\(.label)"'
38	security
28	enhancement
17	ci
16	tests
12	planning
11	documentation
1	question
```

`security` and `planning` appear in the shared list neither as a label nor as an
alias of one:

```
$ gh api -H "Accept: application/vnd.github.raw" \
    repos/jellyfin/jellyfin-meta-plugins/contents/.github/plugin-repo-labels.yaml | grep -nE "security|enhancement|planning"
11:    - major-enhancement
40:    - enhancement
```

So a run deletes them, and with them 50 label attachments across the issues that
carry them. `enhancement` survives but not under its own name: it is listed as an
alias of `feature`, so the 28 issues carrying it end up carrying `feature` instead,
and anything that filters on `enhancement` stops matching.

The workflow runs on a monthly schedule and nothing here would say it had happened.
