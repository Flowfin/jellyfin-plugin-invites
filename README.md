> [!NOTE]
>
> **Part of [Flowfin](https://github.com/Flowfin).** It works with any Jellyfin
> server, and with the Flowfin clients.

# Account Invitations

A Jellyfin plugin that lets an operator invite somebody with a link, so that
person sets up their own account instead of the operator creating it by hand and
handing out a password. It is for people who run a server for friends or family
and who would rather not be the one who knows everybody's password.

> [!WARNING]
>
> None of it works yet. What installs today adds one page to the server
> dashboard and there is nothing on that page to set. No invitation can be
> minted, no link can be redeemed, and no account is created by this plugin.
> Everything below describes what is being built and says which part is written
> down and which part is running code.

## The shape of it

An operator mints a link and scopes it in advance: which libraries the account
will see, what it may do, how long the link lasts and how many people it is
good for. They send the link to somebody. That person follows it, answers a
short setup, chooses their own password, and ends up with an account already
scoped the way the operator decided. The operator can revoke the link at any
point before it is used.

The whole path, state by state and branch by branch, is written down in
[docs/redemption-flow.md](docs/redemption-flow.md), including the awkward ones:
a link that expires between the page being shown and the form being posted, a
username somebody else has taken, a password the server refuses. Nothing in that
document is implemented.

## What it deliberately does not do

- It is not open registration. Somebody who reaches the setup page without a
  valid invitation gets an account out of it in no case.
- It never touches an account that already exists. An invitation presented by
  somebody already signed in creates nothing and changes nothing, and there is
  no path by which redeeming one widens an account that is already there.
- It never mints an administrator, whatever the configuration asks for.
- It carries no credential in the link. No password, no temporary password, and
  no token standing in for one.
- It sends no mail. The operator copies a link and hands it over with whatever
  they already use to talk to the person, and there is no mail server to
  configure and no address to collect.

The first four are settled and every issue in the plan is written to keep them
true. The issues that turn each into a refusal in the source are named against
their rows in [docs/threat-model.md](docs/threat-model.md), and none of those
refusals is code yet.

The mail one is narrower than the others and is worth saying exactly. Nothing
sends mail today and nothing in the plan does. Whether sending is ever added is
item 5 in #11, which has no answer, and the issue says that if it is wanted it
is a milestone of its own with a mail or webhook configuration surface behind
it. So read that bullet as what the plugin does rather than as a promise about
what it will never do.

## Supported server line

Jellyfin 10.11.0 and later, on `net9.0`. That is the oldest server the packaging
metadata claims to load on rather than a preference, and it is one value read
from one file:

```
$ git grep -nE '^(targetAbi|framework):' -- build.yaml
build.yaml:6:targetAbi: "10.11.0.0"
build.yaml:7:framework: "net9.0"
```

`Directory.Build.props` derives the floor build from that same line, so the
claim in the manifest is what the plugin is compiled against rather than a
second number somebody keeps in step by hand.

## Installing

There is no published release and no catalogue manifest yet, so there is nothing
to paste into a Jellyfin repository list. Saying so is more useful than an
install section that describes a URL nobody can fetch. `0.1.0.0` is the version
the metadata declares and it has not been tagged; the tag that would publish it
is #155 and the sequence a tag runs through is
[docs/RELEASING.md](docs/RELEASING.md).

To build it from source:

```
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

That produces `Jellyfin.Plugin.Invites/bin/Release/net9.0/Jellyfin.Plugin.Invites.dll`,
which is the file the packaging metadata names. Dropping it into a plugin
directory on a real server is one of the two checks this repository will not
automate, and what a person does instead is in
[docs/manual-checks.md](docs/manual-checks.md).

## Screenshots

None yet. The configuration page has nothing on it to photograph and the setup
page does not exist. They arrive with the pages, in #84 and #74.

## Security

This plugin creates accounts, so a defect in it hands somebody an account on a
server they were never invited to. The posture is that invitation codes are
bearer credentials and are treated as such: minted from a cryptographic source,
stored only as a keyed hash, indistinguishable in failure, expiring, revocable,
and unable to produce an administrator. Every one of those is a decision
recorded in an issue today rather than a line of code, and the document that
says which is which, including what is not defended at all, is
[docs/threat-model.md](docs/threat-model.md). How to report something is
[SECURITY.md](SECURITY.md).

## The documents

Each of these is the one place its subject is settled. They are linked rather
than summarised here, because a readme that restates a decision is a readme that
drifts from it.

| Document | What it settles |
| --- | --- |
| [docs/redemption-flow.md](docs/redemption-flow.md) | Every state and branch from following a link to holding an account |
| [docs/threat-model.md](docs/threat-model.md) | What is defended, how, and what is not defended |
| [docs/personal-data.md](docs/personal-data.md) | Every field held about an invited person, why it exists and what deletes it |
| [docs/expiry-rules.md](docs/expiry-rules.md) | The seven decisions behind what looks like one comparison |
| [docs/attempt-outcomes.md](docs/attempt-outcomes.md) | The fixed set of outcomes a redemption attempt records |
| [docs/setup-never-asks.md](docs/setup-never-asks.md) | What the guided setup may never put a box on the page for |
| [docs/configuration.md](docs/configuration.md) | One row per setting, its default, its bounds and what breaks |
| [docs/limits.md](docs/limits.md) | Behaviour that is correct, surprising, and reported as a bug |
| [docs/disaster-cases.md](docs/disaster-cases.md) | Restore from backup, a cloned server, two servers on one store |
| [docs/migration-from-jfa-go.md](docs/migration-from-jfa-go.md) | Whether this replaces jfa-go, answered in both directions |
| [docs/versioning.md](docs/versioning.md) | Where the version number lives and which part moves when |
| [docs/RELEASING.md](docs/RELEASING.md) | What a tag does and what a person does |
| [docs/tests-not-written.md](docs/tests-not-written.md) | The tests this repository refuses, and what covers each risk instead |
| [docs/manual-checks.md](docs/manual-checks.md) | Where a run of the two unautomatable checks is recorded |
| [CONTRIBUTING.md](CONTRIBUTING.md) | Sign-off, the headless rule, the invariant lint, and what runs on a change |

## Licence

GPLv3, in [LICENSE](LICENSE). A Jellyfin plugin links against the Jellyfin
NuGet packages, which are GPLv3, so the compiled binary is GPLv3 whatever the
source says. [NOTICE.md](NOTICE.md) carries the intended-use notice.
