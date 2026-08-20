# Plugin catalogue readiness

Checked on 2026-08-07 against version 0.1.0.0, which is what the packaging
metadata declares at the commit this record lands on:

    git grep -n '^version:' -- build.yaml
    build.yaml:4:version: "0.1.0.0"

A pass recorded against one version says nothing about a later one, so this
record is re-run when the version moves rather than re-read.

A second pass is at the end of this file. It was made because the tree moved and
not because the version did, and it changes the verdict on seven of the items
below. Read it before taking any verdict in this record as current.

## There is no published checklist to work through

Issue #121 asks for the current checklist to be read from the current
documentation rather than from memory. It was read, and what was found is that
no checklist is published. Three places were looked at on the date above.

The plugin template repository, which is where a plugin author is sent to
start:

    gh api repos/jellyfin/jellyfin-plugin-template/contents/README.md --jq .content | base64 -d

It carries build and debugging instructions and one requirement about
licensing, quoted under that item below. It states no submission criteria and
no acceptance bar.

The plugin page on the documentation site,
<https://jellyfin.org/docs/general/server/plugins/>. It covers installing a
plugin from the built-in catalogue and adding a third-party repository. It
states no criteria, no submission process and no approval workflow.

The publishing post the template README links to for creating a repository,
<https://jellyfin.org/posts/plugin-updates/>. It is the nearest thing to a
requirements document and it says plainly that there is not one:

> We don't require any specific method for hosting these files, as that would
> go against the ideals of the project.

That absence is the finding and it is not softened anywhere below. The items in
this record are not a bar anybody else set. They are the things those three
sources require or assume, together with the field set the published catalogue
manifest carries today, and they are derived rather than quoted. A later
sentence claiming this repository passed an official checklist would be
claiming something that does not exist.

## Where the field set comes from

A server reads a manifest, and for this plugin that manifest is generated from
`build.yaml` by the packager rather than written by hand. The field names below
are the ones on entries in the published catalogue manifest, read on the date
above from

    https://repo.jellyfin.org/files/plugin/manifest.json

which answers with a redirect to a mirror,
<https://fra1.mirror.jellyfin.org/files/plugin/manifest.json>. An entry there
carries `guid`, `name`, `description`, `overview`, `owner`, `category` and
`versions`, and about half of them also carry `imageUrl`. An element of
`versions` carries `version`, `changelog`, `targetAbi`, `sourceUrl`,
`checksum` and `timestamp`. The publishing post shows the same shape without
`imageUrl`.

## The items

### The identifier is this plugin's own

Fails. The identifier is still the template's, and it is in three places rather
than the two the issue that owns it names:

    git grep -n 'eb5d7894-8eef-4b36-aa6f-5d124e828ce1'
    Jellyfin.Plugin.Template/Configuration/configPage.html:23:                pluginUniqueId: 'eb5d7894-8eef-4b36-aa6f-5d124e828ce1'
    Jellyfin.Plugin.Template/Plugin.cs:32:    public override Guid Id => Guid.Parse("eb5d7894-8eef-4b36-aa6f-5d124e828ce1");
    build.yaml:3:guid: "eb5d7894-8eef-4b36-aa6f-5d124e828ce1"

The third copy is the one the configuration page hands to the dashboard when it
loads and saves this plugin's configuration, so it has to move with the other
two or the page reads somebody else's settings. Issue #3 owns the change and
the count in its done-condition is wrong against the tree; that is written into
the issue rather than only here.

The publishing post is explicit about why this one matters:

> Please note that the GUID must be unique (both in the manifest and the plugin
> itself) if you want to avoid conflicts with other plugins.

### The display name is this plugin's own

Fails. The name is the template's in the packaging metadata and in the plugin
class:

    git grep -n '^name:' -- build.yaml
    build.yaml:2:name: "Template"

Issue #3 owns the display name and issue #8 owns the packaging metadata field.

### The overview and the description say what the plugin does

Fails. Both are the template's filler:

    git grep -n '^overview:' -- build.yaml
    build.yaml:7:overview: "Short description about your plugin"

The description below it is the template's two lines of filler. Issue #8 owns
both fields.

### The owner names whoever publishes it

Fails. The owner is `jellyfin`, which is not who publishes this:

    git grep -n '^owner:' -- build.yaml
    build.yaml:12:owner: "jellyfin"

Issue #8 owns it.

### The category is one the catalogue renders

Fails, in the weaker sense that the value is present and is one the catalogue
uses, but it is the template's untouched value rather than one chosen for a
plugin that creates accounts:

    git grep -n '^category:' -- build.yaml
    build.yaml:11:category: "General"

Issue #8 owns it.

### An image

Does not apply. No source read above requires one, and the published manifest
carries `imageUrl` on some entries and not on others, so its absence is not a
defect. This repository has no image and no field for one:

    git grep -n 'imageUrl'
    (no match)

If one is wanted it belongs with the rest of the catalogue metadata, in issue
#8.

### A version, and a scheme behind it

Passes. The version is four parts, it is written in one file, and the assembly
version, file version and package version are all derived from that file rather
than typed a second time:

    git grep -n 'PluginManifestVersion' -- Directory.Build.props

The build refuses rather than falling back when no four part version can be
read from the packaging metadata, which is the `RefuseUnreadablePluginVersion`
target in the same file. What each part means is in `docs/versioning.md`.

### A declared server line

Passes for the declaration and for the compile against it:

    git grep -n '^targetAbi:' -- build.yaml
    build.yaml:5:targetAbi: "10.11.0.0"

The floor build compiles against the line that field names, derived from the
field rather than restated, so a raised claim moves what the floor build
compiles against. What it proves is that every member the plugin calls exists
at the declared floor. It is not evidence that the packaged plugin loads on a
server of that line, which is issue #123 and is a manual step there.

### A changelog

Fails. The changelog is the template's word:

    git grep -n 'changelog' -- build.yaml
    build.yaml:15:changelog: >
    build.yaml:16:  changelog

Issue #124 owns the first entry and the convention behind it.

### A checksum, a source address and a timestamp per version

Fails, because nothing has been published. Those three fields are produced when
a version is packaged and published rather than written into the repository,
and no manifest for this plugin is published anywhere yet. Issue #119 owns
generating the manifest from the built artefact and keeping the checksums equal
to the files that were published.

### The licence permits linking against the server packages

Passes. The template README states the requirement:

> To build a Jellyfin plugin for distribution to others, it must be under the
> GPLv3 or a permissive open-source license that can be linked against the
> GPLv3.

and this repository is under the first of those:

    gh api repos/iderex/jellyfin-plugin-invites --jq .license.spdx_id
    GPL-3.0

### The source is available

Passes. The same README refuses proprietary, source-unavailable or otherwise
hidden plugins for public consumption. This repository is public:

    gh api repos/iderex/jellyfin-plugin-invites --jq .private
    false

### Nothing ships that is not declared

Passes, and this one is checked by a step rather than by reading. The packaging
job publishes the plugin and compares the assemblies in the publish closure
with the artifact list in the packaging metadata, failing when the two
disagree in either direction, and failing closed when either list comes out
empty. The step is `Nothing ships that the artifact list does not name` in
`.github/workflows/package.yaml`. Adding a runtime package reference puts its
assembly in the closure while the declared list stays as it was, which is what
turns it red.

### The framework and the artifact list match what is built

Passes. The framework is `net9.0` in the packaging metadata and in both project
files, and the artifact list names `Jellyfin.Plugin.Invites.dll`, which is the
assembly this repository builds. Issue #2 renamed the project off the template
and moved the artifact list in the same change, because either one moving alone
is what the comparison above reds on.

## What the failing items are waiting on

Five of the seven wait on one thing that is not a piece of work. The identifier,
the display name, the overview and description, the owner and the category are
all held by #3 and #8, and both of those wait on the twelfth entry in #11, which
is where the display name and the identifier are decided. The identifier is
permanent in practice once anybody has installed the plugin, so it is the one
field on this list that cannot be corrected later at the cost of a version bump.

The per-version checksum, source address and timestamp wait on the eleventh
entry in the same place, which decides where the manifest is hosted. #119 cannot
generate a manifest into a location nobody has chosen.

The changelog under #124 waits on neither and is the one failing item that could
move today.

## What this record does not say

It does not say the plugin is ready for a catalogue. Seven of the items above
fail and each names the issue that owns it. It does not say an official
checklist was passed, because no official checklist was found. It does not
cover whether this plugin should be submitted anywhere, which is a decision
this record has no part in.

## Re-run on 2026-08-16, against the same version

The pass above is recorded against version 0.1.0.0 and its rule is that it is
re-run when the version moves. The version has not moved:

    git grep -n '^version:' -- build.yaml
    build.yaml:5:version: "0.1.0.0"

Seven items above read `Fails`. Six of those failures are no longer true, and
one more item, the image, records a field as absent that is now present. The
rule did not catch any of it, because what moved was the tree rather than the
number the rule watches, so somebody opening this record to find out whether the
plugin is ready for a catalogue is told it still ships the template's
identifier, name, overview, description, owner, category and changelog. That is
what this second pass is for.

The rule wants a second half, that a pass is also re-run when the fields it read
have changed. Adding it changes how this record works rather than re-running it,
and that is not decided here.

Nothing above is edited. It is what was true on 2026-08-07 and the commands it
quotes are the ones that were run then. Two of them name this repository under
an owner it no longer has and answer nothing today, and they are re-run below
under the owner it does have. Fields have also been added to `build.yaml` since,
so the line numbers in the quotations above are not the line numbers a reader
gets now.

The items below are in the order of the items above, and one that has not moved
says so rather than repeating its evidence.

### The identifier is this plugin's own

Passes. The identifier is this plugin's, it is one value, and it is in the three
places that have to agree: the plugin class, the packaging metadata, and the
configuration page that asks the dashboard for this plugin's settings.

    git grep -n '7565756d-8964-49fd-a2c6-f2a878d5001a' -- build.yaml 'Jellyfin.Plugin.Invites/'
    Jellyfin.Plugin.Invites/Configuration/configPage.html:32:                    pluginUniqueId: "7565756d-8964-49fd-a2c6-f2a878d5001a",
    Jellyfin.Plugin.Invites/Plugin.cs:32:    public override Guid Id => Guid.Parse("7565756d-8964-49fd-a2c6-f2a878d5001a");
    build.yaml:3:guid: "7565756d-8964-49fd-a2c6-f2a878d5001a"

The item above quotes two paths under the template's project name, and the
rename took both of them:

    git ls-files 'Jellyfin.Plugin.Template*' | wc -l
    0

A count rather than an exit status, because `git ls-files` answers a pattern it
matches nothing for with a clean exit and says nothing, which reads the same as
a command that was never run.

### The display name is this plugin's own

Passes, in the packaging metadata and in the plugin class, and it is one value
in both:

    git grep -n '^name:' -- build.yaml
    build.yaml:2:name: "Account Invitations"
    git grep -n 'public override string Name' -- Jellyfin.Plugin.Invites/Plugin.cs
    Jellyfin.Plugin.Invites/Plugin.cs:29:    public override string Name => "Account Invitations";

### The overview and the description say what the plugin does

Passes, and both say what the plugin does today rather than what it is planned
to do:

    git grep -n '^overview:' -- build.yaml
    build.yaml:8:overview: "Invitation links that let somebody set up their own account. Under development, nothing of it works yet."

The description is two paragraphs under `description:` at line 9 and the second
of them says that nothing is built yet and that the page the plugin installs has
nothing on it to set. A catalogue entry that claimed working invitations would
be the one field on this list a reader acts on before installing.

### The owner names whoever publishes it

Passes:

    git grep -n '^owner:' -- build.yaml
    build.yaml:14:owner: "Flowfin"

### The category is one the catalogue renders

Passes, and it is now a value chosen for a plugin that creates accounts rather
than the template's untouched one:

    git grep -n '^category:' -- build.yaml
    build.yaml:13:category: "Administration"

### An image

Passes, and the item above records it as not applying because the field was
absent. The field is present, the file it names is in this repository, and the
address answers:

    git grep -n '^imageUrl:' -- build.yaml
    build.yaml:4:imageUrl: "https://raw.githubusercontent.com/Flowfin/jellyfin-plugin-invites/master/img/logo.png"
    git ls-files img/
    img/logo.png
    img/social-preview.png
    curl -s -o /dev/null -w '%{http_code} %{content_type} %{size_download}\n' -L "$(git grep -h '^imageUrl:' -- build.yaml | cut -d'"' -f2)"
    200 image/png 5961

The address names `master` rather than a tag, so it follows the branch and a
later commit that moved or removed that file would change what a published
entry renders without the entry changing.

### A version, and a scheme behind it

Unchanged. Passes for the reason recorded above.

### A declared server line

Unchanged, and the field has moved down one line since:

    git grep -n '^targetAbi:' -- build.yaml
    build.yaml:6:targetAbi: "10.11.0.0"

What it is not evidence of is unchanged too: the floor build proves every member
the plugin calls exists at the declared floor, and not that a packaged plugin
loads on a server of that line.

### A changelog

Passes. It is four sentences about this plugin, and what they say is that
nothing an operator can use is in it yet:

    git grep -n '^changelog:' -- build.yaml
    build.yaml:17:changelog: |

### A checksum, a source address and a timestamp per version

Fails, and it is the one item on this list that still does. Nothing is
published:

    gh api repos/Flowfin/jellyfin-plugin-invites/releases --jq 'length'
    0

A count rather than an empty listing, for the reason under the identifier item:
a query that answered nothing and a query that failed print the same thing.

The three fields are produced by publishing rather than written into the
repository, so this cannot move until something is published.

### The licence permits linking against the server packages

Passes, and the command in the item above names an owner this repository no
longer has. Re-run under the owner it does:

    gh api repos/Flowfin/jellyfin-plugin-invites --jq .license.spdx_id
    GPL-3.0

### The source is available

Passes, with the same correction to the owner:

    gh api repos/Flowfin/jellyfin-plugin-invites --jq .private
    false

### Nothing ships that is not declared

Unchanged. Passes, and it is the one item on this list checked by a step rather
than by reading.

### The framework and the artifact list match what is built

Unchanged. Passes for the reason recorded above.

## What this pass leaves

One failing item, the per-version checksum, source address and timestamp. What
it waits on is unchanged from the paragraph above: where the manifest is hosted
is the eleventh entry in #11, and #119 cannot generate a manifest into a
location nobody has chosen.

The paragraph above headed `What the failing items are waiting on` describes a
state with seven failures in it and is not a description of this pass. Six of
the seven have landed since it was written, and the twelfth entry in #11, which
it names as the thing five of them wait on, has been answered.

This pass says no more about a catalogue than the one above it. No official
checklist was found then and none was looked for again here, so the items remain
derived rather than quoted, and nothing here says the packaged plugin loads on a
server. No server was run for this pass either.

## Read again on 2026-08-20, for one quotation rather than for the items

Nothing on this page is re-judged here. One quotation in the pass above has
stopped reproducing and it is corrected the way that pass corrected the one
before it, by saying so underneath rather than by editing a landed pass.

The identifier item quotes the configuration page at line 32, and the page has
grown since:

    git grep -n '7565756d-8964-49fd-a2c6-f2a878d5001a' -- build.yaml 'Jellyfin.Plugin.Invites/'
    Jellyfin.Plugin.Invites/Configuration/configPage.html:144:                    pluginUniqueId: "7565756d-8964-49fd-a2c6-f2a878d5001a",
    Jellyfin.Plugin.Invites/Plugin.cs:32:    public override Guid Id => Guid.Parse("7565756d-8964-49fd-a2c6-f2a878d5001a");
    build.yaml:3:guid: "7565756d-8964-49fd-a2c6-f2a878d5001a"

The verdict is untouched. It is still one identifier in the three places that
have to agree, and the item passes for the reason it already gave. What moved is
where the first of the three sits in its file, which matters because a reader
who runs that command against the pasted line number and finds something else
has no way to tell a moved line from a plugin reading another plugin's settings.

Only that quotation was read. The line numbers in the first pass are already
disclosed as stale where the second pass says the packaging metadata gained
fields, and no item on this page was re-judged for this reading. No server was
run.
