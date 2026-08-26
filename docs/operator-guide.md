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

Two steps of the seven have nothing to point at.

**Defining an account template.** The mint form takes a template by name, as free
text, and no screen defines what that name grants. The grant itself is a type in
the source and is not yet copied into an invitation, so today the name is a label
the record carries and an operator's own note to themselves.

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

There is no published release and no repository URL to paste into the server's
plugin catalogue. What exists is a build from source, and the commands are in
[the readme](../README.md#installing). Dropping the resulting file into a plugin
directory on a real server is one of the checks this repository does not
automate, and what a person does instead is
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

There is no screen for this yet, and the section above says why. What the mint
form asks for is a template name, and what the record keeps is that name.

Until a template surface exists, treat the name as your own note about what you
intended, and keep the list short enough that you recognise it in the table
afterwards. Nothing in the plugin reads it, and nothing yet uses it to decide
what an account may see.

**The empty template case, which is the one to get wrong on purpose once rather
than by accident later.** A template that grants no libraries produces an account
that signs in and sees nothing. The plugin's own rule is that a template holds a
resolved list of libraries rather than a grant-everything flag, and that minting
refuses an empty list with a message rather than handing somebody an account with
nothing in it. That refusal is not built, because the template is not built. When
it arrives, an invitation that grants nothing will be refused at minting; today
there is no list to be empty.

## Step 4: mint the first invitation

The Invite somebody form at the top of the page takes three things.

**Template.** Required. Blank is refused.

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
the record stays until a retention sweep removes it, and that sweep is not built,
so today nothing removes a record at all.

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

## Disabling the plugin

Disabling is not the same question as upgrading, and the answer is settled in one
half and honestly unknown in the other.

**Expiry keeps running.** An invitation with a day left, on a plugin disabled for
a week, is expired when the plugin comes back. Expiry is about how long the link
has been loose in the world rather than about the plugin's uptime, and a plugin
that credited its own downtime back would silently extend the exposure of every
link you had already sent.

**Whether the redemption address stops answering is a question about the server,
and this repository does not answer it.** A plugin's routes are discovered from
its assembly when the server builds its route table, and nothing takes an address
back out of a table that is already built. Whether the server stops routing to a
disabled plugin's assembly is the server's own behaviour, and nobody here has
probed it on a running installation. Nothing in this plugin asserts that disabling
it makes the redemption address unreachable.

Until somebody measures that on a real server, do not treat disabling the plugin
as a way of turning redemption off. What stops an invitation being redeemed is
revoking it, which is a decision this plugin makes and can prove.

There is nothing else to lose by disabling. The store is a file the plugin owns
and the plugin being switched off does not touch it, so enabling it again finds
the records exactly as they were.

## Removing the plugin

Uninstalling removes the plugin and the plugin's own state. The accounts it
created stay on the server with the access they have, and after the uninstall
nothing tells them apart from an account you made by hand. They are the server's
accounts and always were, and deleting somebody's account as a side effect of
removing a plugin is not a thing software should do quietly.

What goes with the plugin is the answer to which accounts came from which
invitation. That link lives in the plugin's records and nowhere else, so the
moment before an uninstall is the last moment it exists. If it matters to you,
read it off `GET /Invites` and save what it answers, or copy the store file,
before you remove anything. There is no export offered at the moment you
uninstall, and that is a gap rather than a decision.

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
