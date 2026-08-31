# Where this plugin is distributed from

A Jellyfin server does not discover a plugin. Somebody pastes an address into
the server's plugin catalogue, the server fetches a manifest from it, and what
that document says is the whole of what the operator can install. So the address
is a fact this repository has to hold in one place, and the document behind it is
a thing that can be wrong in ways no run of this repository would notice.

This page is that one place. It carries the address, what the address serves
today, and it is where `.github/workflows/manifest-freshness.yaml` reads the
address from rather than carrying a second copy that goes stale the first time it
moves.

## The address

    https://flowfin.dev/manifest.json

That is the hub catalogue, decided for this plugin on #155 on 2026-08-27
together with #119's hosting half: distribution is that catalogue rather than the
official Jellyfin one, and the reason the official one is declined is that it is
not filled by submitting to it. `docs/catalogue-checklist.md` is where that
reading lives and is not repeated here.

Generation stays in this repository and hosting does not.
`.github/lint/manifest.sh` writes the entry from `build.yaml` and from the
archive that was published; where that entry then travels is the hub's route and
is not built in this tree.

## What that address carries for this plugin today: nothing

Read back rather than asserted, on 2026-08-31:

    curl -sS -o manifest.json -w '%{http_code}\n' https://flowfin.dev/manifest.json
    200

    jq -r '.[] | [.name, .guid] | @tsv' manifest.json
    Requests	0f9c9107-b31b-459e-81fa-6d35dac25e79
    Playback Statistics	29e90267-52ee-4bec-b4fb-870b8f5ddc53

The identity this plugin would be keyed on is not among them:

    git grep -n '^guid:' -- build.yaml
    build.yaml:3:guid: "7565756d-8964-49fd-a2c6-f2a878d5001a"

**So there is nothing to install from that address, and there is nothing an
operator can paste anywhere to get this plugin.** That is not a gap in the
address; it is that no release exists to be offered. The only tag this repository
carries is the dry run from `0.1.0.0-rc1`, which publishes nothing by design, and
`docs/RELEASING.md` is where the two acts that would change it are written. The
sentences in `README.md` and `docs/operator-guide.md` saying there is no
published release and no URL to paste are unchanged by this page and stay true.

## What reads this page, and what it decides

`.github/workflows/manifest-freshness.yaml` greps the address out of the section
above, fetches it, lists the releases this repository has, and hands both files to
`.github/lint/manifest-freshness.sh`. That script compares the newest release
against the newest version the entry offers at the server line `build.yaml`
names, and refuses a disagreement in either direction.

Today both sides are empty, and the check says so rather than saying nothing:

    bash .github/lint/manifest-freshness.sh check <fetched-manifest> <release-list>
    ok    no -stable release exists and the manifest carries no entry for 7565756d-8964-49fd-a2c6-f2a878d5001a. Nothing is published and nothing is offered, so there is nothing here to disagree.

A green run of that check therefore means "these two agree", which today is a
statement about two empty sets. It becomes a statement about an install route on
the day a release exists, and the refusals it makes are proven against fixtures on
every run in the meantime, because a reader nobody has watched saying no is a
reader that might say yes to everything.

## What this page does not say

It does not say the hub serves an entry that installs. Nothing here downloaded an
archive, hashed one, or put a package to a server; whether a package a manifest
offers loads is #123's recorded manual check and is a person at a machine.

It does not say the freshness run reaches anybody. A red scheduled run sits in a
run list on this repository and raises nothing an operator or a maintainer sees.
Building a notifier is not proposed here and no issue is opened for it by this
page.

It does not say how the entry gets from a release into that document. The route
from this repository's generated entry to the hub's manifest is the hub's, this
tree holds no part of it, and #119's third clause is about the generation rather
than the delivery.
