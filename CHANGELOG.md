# Changelog

One entry per version, newest first. The entry here and the `changelog` key in
`build.yaml` carry the same text, because the key is what a catalogue shows an
operator deciding whether to upgrade and this file is what somebody reads in the
repository. Two texts for one version would let the catalogue and the repository
say different things about the same bytes.

Nothing in this repository refuses an entry that disagrees with the packaging
metadata. The two are kept equal by hand today, and comparing them is the reason
the entry is written as plain lines in both places rather than as a folded string
here and a bullet list there.

`0.1.0.0` has not been published. The tag that would publish it is #155, and the
process a tag runs through is in `docs/RELEASING.md`.

## How an entry is written

An entry says what changed in plain words, for somebody who has the plugin
installed and is deciding whether to upgrade. It is not the commit log and it
does not name pull requests.

Every entry states what the change does to what an invitation can create,
whether or not that is a breaking change in the ordinary sense, and whether or
not the answer is nothing. An invitation creating an account is the whole risk
this plugin carries, so an operator learns about a change to the libraries a
template grants, the permissions it sets, the quotas it bounds, the number of
accounts a link is worth or how long it stays usable from the changelog rather
than from the diff. An entry that would have nothing to say under this rule says
that it has nothing to say.

Which part of the version number moves for which kind of change is in
`docs/versioning.md`, and that file is the authority for it. An entry does not
restate the scheme; it names the version it is for, and the number it names is
the `version` key in `build.yaml` for that release, written the same way and with
the same number of parts.

The identifier is not a version and never changes. It is the key a server files
an installed plugin under, so a new identifier is not an upgrade, it is a second
plugin installed alongside the first with the old one left behind:

```
$ git grep -n '7565756d-8964-49fd-a2c6-f2a878d5001a' -- build.yaml 'Jellyfin.Plugin.Invites/Plugin.cs'
Jellyfin.Plugin.Invites/Plugin.cs:32:    public override Guid Id => Guid.Parse("7565756d-8964-49fd-a2c6-f2a878d5001a");
build.yaml:3:guid: "7565756d-8964-49fd-a2c6-f2a878d5001a"
```

A change to either of those two lines belongs in an entry as loudly as the
changelog can say it, and no such change is planned.

## 0.1.0.0

The first published version. It adds one page to the server dashboard, and on
it an operator sets the public address, writes down the account templates an
invitation is minted against, mints an invitation, reads the outstanding ones,
revokes one and rotates the key the codes are stored under.
A person following an invitation link is served a setup page, and posting it
back creates the account that invitation was scoped for, so a link minted from
this version can be spent by whoever holds it.

It is compiled against 10.11.0, the oldest server of the line the manifest
claims, so a server at the floor of that line loads it rather than refusing the
assembly.

What an invitation can create is exactly the account its template names, and
nothing in this version has been run against a Jellyfin server.
