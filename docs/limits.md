# Limits and the known awkward cases

Some behaviours are correct, surprising, and reported as bugs over and over.
Writing them down once is cheaper than answering them one at a time, and it is
much cheaper than discovering halfway through a support thread that two people
have different ideas about what the plugin promised.

Each entry says three things: what happens, why it is that way, and what to do
instead if the reader wanted something else. The third part is what this page
adds and it exists nowhere else.

## What this page is, at the moment you are reading it

None of these behaviours is in the code. Two of the pieces they are about are:
the invitation record, as `Invitation` under #38, and the file that holds
records, as `InvitationStore` under #39.

    git ls-files 'Jellyfin.Plugin.Invites' | grep -iE 'store|redemption'
    Jellyfin.Plugin.Invites/Storage/InvitationStore.cs
    Jellyfin.Plugin.Invites/Storage/StoreContents.cs
    Jellyfin.Plugin.Invites/Storage/StorePermissionState.cs
    Jellyfin.Plugin.Invites/Storage/StorePermissions.cs

Nothing calls either of them. There is no redemption path and nothing in the
plugin so much as names the store outside the file that declares it, so a server
running this plugin today has no invitations file at all:

    git grep -nE 'ControllerBase|ApiController|HttpGet|HttpPost' -- '*.cs' ':!Jellyfin.Plugin.Invites.Tests'
    exit=1
    git grep -n 'InvitationStore' -- 'Jellyfin.Plugin.Invites/*.cs' | grep -v 'Storage/InvitationStore.cs' ; echo "exit=$?"
    exit=1

So every entry below names the issue that owns the behaviour, and none of them
is held by a test. That is the honest status and it is not a formality: an entry
here is a decision the plan has taken, and a decision is something a later change
can contradict without anything going red. When a behaviour lands, its entry
gains the test that holds it, and this line gets smaller.

Four of these cases are already stated in fixed words under what is not defended
in [docs/threat-model.md](threat-model.md) and in
[SECURITY.md](../SECURITY.md). This page points at those sentences rather than
repeating them. Three copies of one sentence is two of them going stale, and the
copy that goes stale is the one nobody is reading when the behaviour changes.

## A code is shown once and cannot be recovered

The invitation code appears in the response to the mint action and nowhere
afterwards. No route returns it, no listing shows it, and the operator's own view
holds the invitation without holding the code.

The store keeps a keyed hash rather than the code, so after the mint response
there is nothing left to show. A route that could hand the code back would make
every later read of the store a way to obtain a live account-creation credential,
which is the property the hashing exists for.

If a code is lost, revoke the invitation and mint another. This is one operator
action and it costs nothing except sending a new link. Owned by #85, with the
store shape in #29 and the secret that keys it in #30.

## Revoking an invitation does not remove the accounts it already created

Revocation stops the invitation being redeemed again, from the moment the
operator reaches for it. Accounts created before that point stay on the server
with the access they were given.

The invitation record and the account have one-way lifetimes. A record may point
at an account, and nothing about revoking or removing the record reaches the
account. That direction is what keeps uninstalling the plugin safe, so it is
deliberate rather than an omission.

To remove access from somebody already invited, disable or delete the account on
the server, which is an action the operator already has. Owned by #54, with the
lifetime direction in #45 and the operator's route to it in #94.

## A restored backup revives spent invitations

Stated in
[what is not defended](threat-model.md#what-is-not-defended), and this page does
not restate it.

What to do instead is the part that belongs here. Rotate the hash secret after
restoring, which is a revoke-everything operation and is offered as one, then
mint again whatever should still be live. Read the disagreement the plugin
reports on load, which compares the accounts the store claims to have created
against the accounts the server actually has. Owned by #46, with rotation in #30.

## The server's timezone does not change when an invitation expires

Expiry is stored as an absolute instant. Moving the server's timezone moves what
a clock on the wall reads and does not move the moment an invitation stops
working.

A window measured in local time would lengthen or shorten every outstanding
invitation the moment the server moved, silently, as a side effect of an
administrative change made for an unrelated reason. Storing the instant is what
makes the operator's answer to "when does this stop working" survive that.

If a different deadline is wanted for an invitation already sent, revoke it and
mint another with the validity you meant. Owned by #51, on the clock seam
from #41.

## The setup form discloses whether a username is taken

Stated in
[what is not defended](threat-model.md#what-is-not-defended), and this page does
not restate it.

There is nothing to do instead, and saying so is the point of the entry. A form
that has to tell somebody their chosen name is taken is a form that tells
anybody holding a code which names exist, and no wording removes that. What
bounds it is that a valid code is needed first, so the disclosure reaches whoever
the operator invited rather than the internet. An operator who cares about it
mints shorter-lived invitations rather than looking for a setting. Owned by #67.

## An invitation whose template names a library that no longer exists

Not decided. #70 owns the question and it is open, so this entry records that
there is no answer rather than inventing one.

The two directions are worth knowing while you wait, because they are not
equivalent. Creating the account with the libraries that do remain gives somebody
a working account that quietly grants less than the operator chose. Refusing the
redemption tells the operator something is wrong at the cost of an invited person
meeting a refusal they cannot act on. Whichever #70 chooses, the outcome reaches
the operator through the attempt trail in #43 rather than through the page.

## The plugin refuses to run on a server line it was not built for

The plugin checks the running server against the line it was built for at
startup, and a mismatch disables its routes with a message naming both versions.
No partial operation follows a mismatch.

This plugin reaches server interfaces that move between server lines. A plugin
that loaded anyway would fail somewhere further in, at a moment chosen by whoever
happened to present a code, rather than at startup where the operator is looking.

Install the build for the line the server runs. Invitations already sent are
unaffected by the plugin being unable to load, because their expiry is an
absolute instant and keeps running while the plugin does not, which is the entry
above and is decided in #47. Owned by #97.

## Expiry is not the same as deletion

An invitation stops being redeemable at its expiry instant, decided by a
comparison made when a code is presented. The record itself stays until the
retention rule removes it, so an expired invitation is still something the
operator can see and account for.

Deciding expiry by comparison keeps one authority for the fact. A scheduled task
that marked records expired would create a second authority and a window in which
an expired invitation is still honoured because the task has not run yet. The
task removes records and changes nothing the redemption decision reads.

If you want the record gone rather than expired, that is the retention rule, and
it is ninety days from the moment an invitation stops being usable. The number
and the reasoning behind it are in
[docs/personal-data.md](personal-data.md#retention), which is the page that owns
it; this entry points there rather than holding a second copy. Owned by #59,
which is the sweep that applies it, with the expiry rules in #51.

## Removing the plugin leaves every account it created

Uninstalling removes the plugin and the plugin's own state. The accounts it
created stay on the server with the access they have, and after the uninstall
nothing tells them apart from an account an operator made by hand.

They are the server's accounts and always were. Deleting somebody's account as a
side effect of removing a plugin is not a thing software should do quietly, and
a deleted account does not come back. That is decision 7 in #11 and it is the
same one-way direction as the revocation entry above.

What goes with the plugin is the answer to which accounts came from invitations.
That link lives in the invitation records and nowhere else, so the moment before
an uninstall is the last moment it exists. If it matters to you, take a copy of
the store file out of the plugin's data directory first, and expect to read it
yourself: the view that presents the trail is #89 and the export is #91, and
neither is built. Owned by #91, with the account side in #45 and #94.

## What this page does not do yet

It is linked from the readme, in the table of documents, and it is not linked
from the operator guide, which does not exist. #111 is that guide, and the link
is its change to make rather than something this file can assert about itself.

    git grep -c 'docs/limits.md' -- README.md
    README.md:1
    git ls-files docs | grep -i 'guide' ; echo "exit=$?"
    exit=1

No entry here is asserted by a test, for the reason at the top. The done
condition of #115 asks that every entry match the behaviour the tests assert, so
that clause is met one entry at a time as the behaviours land, and not by this
document.
