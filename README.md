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
> The half an operator drives runs. The half an invited person drives does not.
> What installs today adds one page to the server dashboard, and on it an
> operator sets the public address, mints an invitation, reads the outstanding
> ones, revokes one and rotates the key the codes are stored under. **No link
> can be redeemed and no account is created by this plugin**, so an invitation
> minted today cannot be spent by anybody. Everything below describes what is
> being built and says which part is written down and which part is running
> code, and [docs/operator-guide.md](docs/operator-guide.md) walks the half that
> runs.

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
their rows in [docs/threat-model.md](docs/threat-model.md). One of the four has a
refusal in the source today: the seam this plugin reaches the server's accounts
through declares no member that writes one, and the suite holds it to that rather
than to a comment. The other three wait on the redemption path.

The mail one is narrower than the others and is worth saying exactly, and this
paragraph said the question behind it was open. Item 5 in #11 is answered: this
plugin never sends an invitation itself. A sending path arrives with three
things that do not exist here, a mail or webhook configuration, a contact
address to send to, and an outgoing route off the server, and each of them is a
surface of its own. Item 9 in the same place answers the second of the three in
the same direction, so the guided setup collects no contact address either.

That makes the bullet a decision rather than a gap, which is why it is written
out instead of being left as an absence. An operator who reads nothing about
sending concludes it was forgotten and finds out otherwise after installing. If
sending is ever wanted it is a milestone of its own rather than a setting, and
the answer today is no.

## Supported server line

The 10.11 line, on `net9.0`, and one line rather than two. That is item 1 in
#11, answered on #97, and this section read `10.11.0 and later` before it was.
The number in the manifest is the oldest server of that line the plugin claims
to load on, and it is one value read from one file:

```
$ git grep -nE '^(targetAbi|framework):' -- build.yaml
build.yaml:6:targetAbi: "10.11.0.0"
build.yaml:7:framework: "net9.0"
```

`Directory.Build.props` derives the version the plugin is compiled against from
that same line, so the claim in the manifest is what the plugin is compiled
against rather than a second number somebody keeps in step by hand, and the
assembly a server binds names the floor rather than a newer release of the line.

`targetAbi` is a floor and no field beside it names a ceiling, so a server on a
later line still installs this plugin and the packaging does not refuse it. The
plugin does. It compares the running server against that line when it starts and
answers every one of its own addresses with a refusal naming both versions where
they disagree, which is #97 and is built. So the heading is what this plugin is
built and tested against, and what enforces it is the plugin rather than the
manifest.

## Installing

**There is no published release, so there is nothing here for a Jellyfin
repository list to install.** That is the whole of it and it is not softened
below.

This section said there was no catalogue manifest either, and there is one. It is
the hub catalogue this plugin is distributed from, it answers, and it carries two
entries, neither of them this plugin. Both halves of that are read back rather
than asserted in [docs/distribution.md](docs/distribution.md), which is also
where the address is; this section does not repeat it, because there is nothing
behind it to install.

The two sentences are not the same fact and the difference is what an operator
meets. "No manifest" sends somebody to build from source. A manifest that answers
and does not name this plugin is what somebody who pasted the address would
actually find, and looking for a plugin that is not in a list reads as a failed
install rather than as an absent release. What a Jellyfin server does with that
document is not claimed here: nothing in this repository has put it to one.

`0.1.0.0` is the version the metadata declares and it has not been tagged; the
tag that would publish it is #155 and the sequence a tag runs through is
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

None yet, and what is missing is somebody taking a photograph rather than work.
The configuration page has five sections on it to photograph and nobody has taken
a picture of one. The setup page is served and the post that receives its form
landed, so the form can now be submitted and a screenshot of it would show a
working page rather than a dead one.

THIS PARAGRAPH SAID A SCREENSHOT OF THE SETUP PAGE WOULD SHOW A FORM THAT CANNOT
BE SUBMITTED. What a run of it stops at now is one step further on: the page a
finished redemption is sent to is served by nothing until #79 lands, so somebody
photographing the whole flow gets the form, the account, and then the server's
own not-found page.

## Security

This plugin creates accounts, so a defect in it hands somebody an account on a
server they were never invited to. The posture is that invitation codes are
bearer credentials and are treated as such: minted from a cryptographic source,
stored only as a keyed hash, indistinguishable in failure, expiring, revocable,
and unable to produce an administrator. Four of those six are running code: the
minting, the keyed hash, the expiry comparison and revocation. The two that are
not are indistinguishable failure and the refusal to mint an administrator, and
both wait on the redemption path, which does not exist. The document that says
which is which, including what is not defended at all, is
[docs/threat-model.md](docs/threat-model.md). How to report something is
[SECURITY.md](SECURITY.md).

## The documents

Each of these is the one place its subject is settled. They are linked rather
than summarised here, because a readme that restates a decision is a readme that
drifts from it.

| Document | What it settles |
| --- | --- |
| [docs/operator-guide.md](docs/operator-guide.md) | The walk from installing to revoking, and what to do when somebody says the link does not work |
| [docs/redemption-flow.md](docs/redemption-flow.md) | Every state and branch from following a link to holding an account |
| [docs/api.md](docs/api.md) | Every route, its parameters and its responses, and what the API deliberately does not offer |
| [docs/threat-model.md](docs/threat-model.md) | What is defended, how, and what is not defended |
| [docs/code-entropy.md](docs/code-entropy.md) | How long an invitation code is, and the calculation the length is read off |
| [docs/personal-data.md](docs/personal-data.md) | Every field held about an invited person, why it exists and what deletes it |
| [docs/what-is-held-about-a-person.md](docs/what-is-held-about-a-person.md) | The same inventory for the person it is about: what is held about you, what never is, and what removes it |
| [docs/logging.md](docs/logging.md) | What a log line may carry and what it may never carry, at any level |
| [docs/expiry-rules.md](docs/expiry-rules.md) | The seven decisions behind what looks like one comparison |
| [docs/attempt-outcomes.md](docs/attempt-outcomes.md) | The fixed set of outcomes a redemption attempt records |
| [docs/rate-limit.md](docs/rate-limit.md) | Where the redemption counter lives, and what a restart does to it |
| [docs/setup-never-asks.md](docs/setup-never-asks.md) | What the guided setup may never put a box on the page for |
| [docs/password-rules.md](docs/password-rules.md) | What the guided setup requires of a password, and why the rules are this plugin's rather than the server's |
| [docs/refusal-response.md](docs/refusal-response.md) | The one page every unusable invitation produces, and what identical means |
| [docs/configuration.md](docs/configuration.md) | One row per setting, its default, its bounds and what breaks |
| [docs/limits.md](docs/limits.md) | Behaviour that is correct, surprising, and reported as a bug |
| [docs/what-an-invitation-can-never-do.md](docs/what-an-invitation-can-never-do.md) | Seven sentences an invitation may never break, and what refuses each one |
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
