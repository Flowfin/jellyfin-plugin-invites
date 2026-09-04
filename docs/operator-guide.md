# The operator guide

This is the sequence, from installing the plugin to revoking an invitation, for
the person who runs the server. The reference material lives in
[docs/configuration.md](configuration.md) and [docs/api.md](api.md); this page is
the walk and the four questions that come up afterwards.

It describes what the plugin does today rather than what it is being built to do.
Where a step of the sequence has no screen behind it yet, the step says so and
names what is missing, because a guide that walks you into a page that does not
exist costs more than the paragraph it saved.

## What is not walkable yet, said before you start rather than after

One step of the seven has nothing to point at.

THIS SECTION SAID TWO STEPS HAD NOTHING TO POINT AT, AND ONE OF THEM HAS A
SCREEN NOW. Defining an account template is done under Settings on the plugin's
own page since #435: each template is a block of fields with the rules it has
to satisfy stated above it, a template is added, edited or removed there, and
the list is saved with the Save button the address uses. Step 3 below walks it.
What the mint form takes is the label of such an entry; a name that matches none
is refused, and the grant behind a name that matches is copied onto the
invitation at that moment, so editing the entry afterwards changes the next
invitation and not the ones already sent.

**Watching an invitation get redeemed.** The redemption address serves the setup
page and nothing posts back to it, so nobody can complete a setup and no account
is created by this plugin at all. Whatever you mint today cannot be spent.

So the useful half of this guide is everything an operator does on their own
side: install, address, mint, send, look, revoke. The other person's half does
not run.

## Before you start

The plugin is built for the 10.11 server line, and for that line only. It reads
the running server's version when it starts and compares it against the line it
was built for; where they disagree, every one of its own addresses answers a
refusal naming both versions and nothing else happens. Install the build for the
line your server runs.

**There is no published release, so there is nothing to install through the
server's plugin catalogue.** This paragraph also said there is no repository URL,
and there is one: the hub catalogue this plugin is distributed from answers, and
it carries two entries, neither of them this plugin. Both are read back in
[distribution.md](distribution.md), which is where the address is written down.
It is worth the distinction, because a list that loads and does not contain
Account Invitations sends you looking for a fault in your install rather than at
a release that has not happened. What exists is a build from source, and the
commands are in [the readme](../README.md#installing). Dropping the resulting
file into a plugin directory on a real server is one of the checks this
repository does not automate, and what a person does instead is
[docs/manual-checks.md](manual-checks.md).

## Step 1: install the plugin

After the server restarts, the plugin adds one page to the dashboard, under the
plugin list, called Account Invitations. Everything below happens on that page
except where it says otherwise.

If the page loads and every action on it answers a refusal naming two version
numbers, that is the server-line check above and not a fault in the install. The
repair is the build for the line you are on.

## Step 2: set the public address

The Settings section at the foot of the page has one field, Public address. Set
it to what somebody outside your network types to reach this server, scheme
included, and save.

This is the setting that produces links which do not work, so it is worth being
exact. Invitation links are built from this value and never from the incoming
request, because a request can claim to be any host and a link minted from a
forged one points the invited person's new password at somebody else's server. A
link is also minted for a person who is not on the other end of any request, so
there is no honest way to derive it from one.

**Behind a reverse proxy.** This is the case the setting exists for, and the case
where the server's own idea of its address is the wrong one. Write the address
the proxy publishes, not the address the server listens on. If the proxy serves
Jellyfin under a subdirectory, the path prefix belongs in this value and survives
into the link. A trailing slash makes no difference, and neither does an explicit
`:443` on an `https` address.

**If you leave it empty.** Minting still works and the mint answers a refusal
naming this setting in place of the link. That is deliberate: an empty address
refuses rather than guessing, and a refusal at the moment you expected a link is
a support question with an answer, where a link built from the wrong address is
one without. Nothing is minted wrongly and no account is affected either way,
because the address is used only to write the link down.

An address that is not absolute, is not `http` or `https`, or carries a query or
a fragment is refused for the same reason: appending a path to any of them
produces something that looks like a link and reaches nothing.

The full row for this setting, with its bounds and what breaks when it is set
badly, is in [docs/configuration.md](configuration.md).

## Step 3: decide what the invitation grants

Under Settings on the plugin's page, below the public address, is the list of
account templates. Press Add a template, give it a label, tick the libraries the
account may see, open the permissions it needs and set a ceiling where you want
one, then Save. The libraries are the server's own, read when the page opens, so
a library is ticked by name rather than typed by identifier; a library the
setting names that the server no longer has is shown as such and stays ticked
until you untick it. Removing a template is a button on its block, and it too
takes effect at Save.

The rules a template has to satisfy are written above the list, and the page
judges none of them: a save that breaks one is accepted by the page and refused
when the plugin loads, with the position of the entry and the rule it missed in
the server log, and nothing can be minted until it is repaired. The shape of
what is saved is in [docs/configuration.md](configuration.md) under `## The
named templates`.

What the mint form asks for is the label of one of those templates, and what
the record keeps is that label and, beside it, a copy of the grant the label
stood for when you minted: the libraries, the permissions and the ceilings.

The copy is the part to know about before you edit a template. Changing an entry
changes what the next invitation minted against it grants and leaves every
invitation already sent exactly as it was, so a link you handed out last week
is worth what the template said last week. To take a grant back from somebody
who has not redeemed yet, revoke the invitation rather than editing the template.

A name that matches no entry is refused at minting, compared ignoring case, and
nothing is written. A template list with a fault in one entry refuses every
mint, with the same sentence the log carries from the last start, until the
entry is repaired.

**The empty template case, which is the one to get wrong on purpose once rather
than by accident later.** A template that grants no libraries produces an account
that signs in and sees nothing. The plugin's own rule is that a template holds a
resolved list of libraries rather than a grant-everything flag. An entry with an
empty library list is a usable template rather than a fault, because it is a
choice somebody wrote down, and today the mint copies it as written; whether a
mint against it should be refused with a message instead is #63's and is not
built. What is built is the copy, so the account such an invitation would create
is decided by the entry as it stood at minting rather than by whatever the entry
says on the day somebody redeems.

**An access schedule is not something an invitation grants, and that is not the
same as not being able to have one.** If you want an invited account limited to
certain hours, a child's account that works after school and not at two in the
morning, the server does that on the account itself rather than through this
plugin. The field is `AccessSchedules` on the account's policy, which this plugin
never writes and deliberately does not carry on a template: a schedule needs its
own value type, its own bounds and its own refusals, and the server already does
the whole job on an account that exists. So mint and send the invitation as
usual, and set the schedule on the account once the person has one.

What is not claimed here is where the server puts that field on screen. The name
is read off the server's own type, and nobody has opened the account page on a
running server to say which menu it sits under, so look for it where the server
lets you edit an account rather than for the spelling above.

## Step 4: mint the first invitation

The Invite somebody form at the top of the page takes three things.

**Template.** Required. Blank is refused, and so is a name no configured
template carries. The name is matched ignoring case, and the grant behind it is
copied onto the invitation as you mint.

**Valid for, in days.** Optional. Left empty, the invitation lasts seven days.
The largest an operator may set is ninety. Zero, a negative number and an
invitation with no expiry at all are each refused. The clock starts at minting
rather than at first use, because what expiry bounds is how long the link has
been loose in the world.

**Accounts it is good for.** Optional, and one when left empty. The most an
invitation may carry is ten. Ten covers a household and a few guests; above that
the link is an open registration page with extra steps, and this plugin declines
to be one.

Press Mint an invitation. The code appears in a section of its own.

**Copy it now.** That is the only time the code is shown. What the plugin stores
is a keyed hash of it, so no page and no route in this plugin will hand it back
afterwards, and the repair for a code nobody copied is a new invitation rather
than a lookup. The field is selectable and copyable with the keyboard on its own;
the button beside it is a convenience.

**Two refusals you may meet here.** A request outside one of the ceilings above
comes back as a bad request, with the message naming which ceiling you met. A
server that already holds five hundred live invitations comes back as a conflict
instead: everything you sent was acceptable and the store's state was not, so the
repair is revoking an invitation rather than changing what you asked for. Live
means unexpired, unspent and unrevoked; expired and spent records stay in the
file and do not count against that number.

If the plugin has no data directory, minting answers that the store is
unavailable rather than pretending to have written something.

## Step 5: send it

You send it. The plugin has no mail configuration, collects no contact address
and has no outgoing route off the server, and that is a decision rather than a
gap: sending arrives with three surfaces that do not exist here, and each of them
is its own security question. Hand the link over with whatever you already use to
talk to the person.

Treat the link as a bearer credential while it is in flight, because it is one.
Anybody holding it can spend a use of it, and there is nothing in it tying it to
the person you meant.

## Step 6: watch it get redeemed

Not yet. The redemption address answers with the setup page and there is no post
behind it, so following the link shows a person a form that cannot be submitted.
Nothing is read, decided, spent or created by a request to it, and the same page
comes back for a code that was never minted as for one you minted a minute ago.

When the post lands, this becomes the step where you refresh the table and see a
use spent and an account listed against the invitation. Today the table answers
the first half of that and the accounts column stays at zero.

## Step 7: revoke one

Every row of the Outstanding invitations table carries a Revoke button, and it
asks before it acts. What it says is what it does: it stops accounts that have
not been created yet and leaves the accounts it already created alone.

Revocation is one-way. There is no un-revoke, and the repair for revoking the
wrong row is to mint a new invitation and send that instead.

## The table, column by column

The Outstanding invitations table shows the records as they stand. It judges none
of them: whether an invitation may still be honoured is decided in one place at
the moment a code is presented, and a second answer worked out on a page would be
a second answer.

| Column | What it is |
| --- | --- |
| Invitation | The record's identifier. Not the code, and not derived from it |
| Template | The name you typed when you minted it |
| Minted | When it was created |
| Expires | The instant it stops being redeemable |
| Uses left | Uses remaining, of uses granted |
| Revoked | When it was revoked, or blank |
| Accounts | How many accounts it produced, and how many of those the server no longer has |

An expired or spent invitation is still in this table. Expiry is not deletion:
the record stays until the retention sweep removes it. That sweep runs daily on
the server's own scheduler and takes records that stopped being usable more than
ninety days ago, so an invitation that stops working stays visible here for a
quarter and then goes. Nothing it removes is an account.

## The four questions that come up afterwards

### Somebody says the link does not work

Do not read their screen as a diagnosis. Every unusable invitation produces one
page with one wording, deliberately: a code that was never minted, one that has
expired, one that has been spent and one that has been revoked are
indistinguishable to whoever presents it. That is what stops the address telling
a stranger which codes exist. The wording itself is
[docs/refusal-response.md](refusal-response.md).

The side that does distinguish them is yours. Find the row in the table and read
Expires, Uses left and Revoked. If there is no row, the code was never minted on
this server, or its record has been removed, or the person mistyped it.

Two causes are worth checking before you conclude the invitation is at fault: the
public address, if the link does not resolve at all from outside your network,
and the server line, if every address of the plugin is answering a refusal naming
two versions.

### Whether an invitation was used

The Uses left column, read as remaining of granted. An invitation minted for
three with two left has been redeemed once. The Accounts column is the same fact
from the other side, and it is the one to trust when somebody deleted an account
afterwards, because it says how many of the accounts it produced the server still
has.

### Which invitation an account came from

The plugin answers this, and not from a screen yet. The administrator route
`GET /Invites/Accounts/{accountId}` hands back the invitations that created a
given account, and `GET /Invites` hands back every record with the accounts it
produced. Both are in [docs/api.md](api.md), and both require an administrator.

The dashboard page shows the invitation-to-account direction in its Accounts
column and does not yet show the reverse one, so answering this question today
means asking the route rather than reading the page.

### A code is lost

Mint a new invitation and revoke the old one. There is no lookup that recovers a
code, because what is stored is a hash and not the code, and a plugin that could
show you a code again would be one that could show it to anybody who reached the
same route.

Revoke the old one rather than leaving it. A code nobody can find is still a code
somebody may hold.

## Upgrading the server

The plugin is built for one server line and refuses to work on another. Upgrading
the server across a line, without replacing the plugin, leaves every one of the
plugin's addresses answering a refusal that names both versions. Nothing is
deleted, nothing is corrupted and the store is not touched: the start-up load
declines before it claims anything.

**What that means for invitations you have already sent.** They keep expiring.
Expiry is a comparison against an absolute instant, and that instant does not
care whether the plugin is running, so an invitation with two days left on the
evening you start an upgrade has two days left however long the upgrade takes. An
invitation that expires while the plugin cannot load is expired, and there is no
way to give the time back.

**So, before an upgrade across a server line**, read the table and decide what to
do with what is outstanding. There are three honest answers and which one fits
depends on the link rather than on the plugin.

- Wait, if the invitations are short and you can finish the upgrade before they
  expire. Nothing is required of you.
- Revoke and re-mint afterwards, if you cannot. Re-minting produces a new code,
  which means telling the person again; there is no way to extend an invitation
  that has already been minted.
- Do nothing and accept the expiry, where the invitation was going to lapse
  anyway.

**During the upgrade itself** the plugin creates nothing and honours nothing, so
there is no window in which a half-upgraded server does something surprising with
an invitation. The refusal is the whole behaviour.

**After the upgrade**, install the build for the new line. The store is read as
it was left: every record you could see before is there, with the same expiry
instants, and the ones that lapsed in the meantime read as expired because the
comparison says so rather than because anything marked them.

Upgrading the server within its line, which is the ordinary case, changes none of
this and needs nothing from you.

## Copying the server to another machine

Copying a data directory onto a second machine copies the plugin's store and the
key its codes are stored under. That is the ordinary way to move house, to build
a staging server, or to try an upgrade without risk, and none of those intentions
changes what the copy takes.

**The plugin does not detect it.** A copied data directory is indistinguishable
from the one it was copied from, from inside. Nothing on the plugin's page will
tell you a second machine exists, and there is no state anywhere that says one
did.

**What the copy took is real today.** Both machines now hold the same records and
the same key. Nothing can be redeemed on either of them, because redemption does
not run at all yet and the step at the top of this page says so, but the key is
copied now and the invitations are duplicated now. On the day redemption runs, an
invitation spent on one machine is still worth an account on the other.

**Decide which machine keeps the identity, then rotate the key on it.** The
control is under "The key the codes are stored under" on the plugin page, and the
route behind it is `POST /Invites/HashSecret/Rotate` in
[docs/api.md](api.md). It reads the count of what rotating would invalidate and
puts that number in front of you before anything is written.

**Rotating costs every invitation you have already sent.** Every stored
invitation is a hash computed under the old key, so no invitation minted before
the rotation can be redeemed again, the ones nobody has touched included. That is
what makes rotation the answer here rather than something to press to tidy up:
the copy took the key, and the only way to make a copied key worthless is to stop
using it. Re-mint what you still want outstanding afterwards, which means telling
those people a new code.

Rotating touches no account and removes no record. It stops future redemptions of
old codes and says nothing about accounts already created, and the table keeps
every row it had.

Deleting the second machine afterwards does not put things back. The key left the
first machine at the moment the directory was copied, and where it went after
that is not something the first machine can see. Rotate anyway.

## Disabling the plugin

Disabling is not the same question as upgrading, and both halves of it are
settled now. The second was honestly unknown until a job put it to a server.

**Expiry keeps running.** An invitation with a day left, on a plugin disabled for
a week, is expired when the plugin comes back. Expiry is about how long the link
has been loose in the world rather than about the plugin's uptime, and a plugin
that credited its own downtime back would silently extend the exposure of every
link you had already sent.

**The redemption address keeps answering until the server is restarted.** A
plugin's routes are discovered from its assembly when the server builds its route
table, and nothing takes an address back out of a table that is already built.
That was an argument until it was put to a server.
`.github/workflows/e2e-plugin-disabled.yaml` installs the packaged plugin on a
published 10.11.11 image, disables it through the server's own route, and reads
the address twice. With the server still running it answers 200, exactly as it did
before the disable. After a restart it answers 404.

**So do not treat disabling the plugin as a way of turning redemption off.**
Pressing the button changes nothing an invited person can see until you restart
the server, and until then the address answers as it always did. What stops an
invitation being redeemed is revoking it, which is a decision this plugin makes
and can prove.

**Getting it back is not a button on that page.** After the restart the server's
own plugin list no longer carries this plugin at all, and the route that would
enable it again answers 404. What an operator does on that server line to bring a
disabled plugin back is not something the job above measured, so nothing here
tells you; what it does tell you is not to expect the switch you used to turn it
off to be there to turn it on.

There is nothing lost from the store by disabling. The store is a file the plugin
owns and the plugin being switched off does not touch it, so a plugin that is
running again finds the records exactly as they were.

## Removing the plugin

Uninstalling removes the plugin and the plugin's own state. The accounts it
created stay on the server with the access they have, and after the uninstall
nothing tells them apart from an account you made by hand. They are the server's
accounts and always were, and deleting somebody's account as a side effect of
removing a plugin is not a thing software should do quietly.

What goes with the plugin is the answer to which accounts came from which
invitation. That link lives in the plugin's records and nowhere else, so the
moment before an uninstall is the last moment it exists.

### Take the trail before you remove anything

`GET /Invites` hands back every record with the accounts it produced. Saving what
it answers is the export, and it is a file on your own machine rather than
anything the plugin keeps:

    curl -fsS 'https://your-server.example/Invites' \
      -H 'Authorization: MediaBrowser Token="YOUR_API_KEY"' \
      -o invites-before-uninstall.json

Substitute your server's address and an API key belonging to an administrator.
The header form is the one this repository's own end-to-end jobs send to a real
Jellyfin server, rather than a shape written from memory here:

    git grep -h 'Authorization: MediaBrowser Token' -- .github/workflows/e2e-identity.yaml
                -H "Authorization: MediaBrowser Token=\"${JELLYFIN_TOKEN}\"" > plugins.json

Copying the store file is the other way, and it is still worth knowing about: it
carries the fields the route does not return, and it needs no credential because
it is a file on the server's disk.

There is no export button, and that is a decision rather than a gap. Adding one
would give this plugin a second surface for data an existing route already
answers, kept permanently for a step an operator takes once; the instruction
above serves the same moment and leaves nothing to maintain. Decided on #91 on
2026-08-29, with the alternative that a future need the listing route genuinely
cannot serve arrives as its own issue.

Neither route nor file is offered to you by the server at the moment you press
uninstall. Nothing prompts, so the reminder is this page, and an operator who
removes the plugin without reading it loses the link.

## Where to look next

| Page | What it settles |
| --- | --- |
| [docs/limits.md](limits.md) | Behaviour that is correct, surprising, and reported as a bug. Read this one before you report anything |
| [docs/configuration.md](configuration.md) | One row per setting, its default, its bounds and what breaks |
| [docs/refusal-response.md](refusal-response.md) | The one page every unusable invitation produces, and why it is one page |
| [docs/expiry-rules.md](expiry-rules.md) | The seven decisions behind what looks like one comparison |
| [docs/api.md](api.md) | Every route, its parameters and its responses |
| [docs/disaster-cases.md](disaster-cases.md) | Restore from backup, a cloned server, two servers on one store |
| [docs/personal-data.md](personal-data.md) | Every field held about an invited person, and what removes it |
| [docs/what-is-held-about-a-person.md](what-is-held-about-a-person.md) | The page to send somebody who asks what you hold about them |
