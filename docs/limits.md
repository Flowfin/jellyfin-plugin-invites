# Limits and the known awkward cases

Some behaviours are correct, surprising, and reported as bugs over and over.
Writing them down once is cheaper than answering them one at a time, and it is
much cheaper than discovering halfway through a support thread that two people
have different ideas about what the plugin promised.

Each entry says three things: what happens, why it is that way, and what to do
instead if the reader wanted something else. The third part is what this page
adds and it exists nowhere else.

## What this page is, at the moment you are reading it

Some of these behaviours are in the code now, and this paragraph said none of
them was. More of the pieces they are about are here than it used to say: the
invitation record as `Invitation` under #38, the file that holds records as
`InvitationStore` under #39, the routine that judges a presented code as
`RedemptionDecision` under #56, and the claim on the store directory as
`StoreLock` under #96.

    git ls-files 'Jellyfin.Plugin.Invites' | grep -iE 'store|redemption'
    Jellyfin.Plugin.Invites/Redemption/RedemptionDecision.cs
    Jellyfin.Plugin.Invites/Redemption/RedemptionOutcome.cs
    Jellyfin.Plugin.Invites/Redemption/RedemptionVerdict.cs
    Jellyfin.Plugin.Invites/Storage/IStoreDirectory.cs
    Jellyfin.Plugin.Invites/Storage/InvitationStore.cs
    Jellyfin.Plugin.Invites/Storage/PluginStoreDirectory.cs
    Jellyfin.Plugin.Invites/Storage/StoreContents.cs
    Jellyfin.Plugin.Invites/Storage/StoreInUseException.cs
    Jellyfin.Plugin.Invites/Storage/StoreLoad.cs
    Jellyfin.Plugin.Invites/Storage/StoreLock.cs
    Jellyfin.Plugin.Invites/Storage/StorePermissionState.cs
    Jellyfin.Plugin.Invites/Storage/StorePermissions.cs
    Jellyfin.Plugin.Invites/Storage/StoreVersionRefusedException.cs

The store is called now, and this paragraph said nothing called it. `StoreLoad`
claims the directory and reads the store when the server starts, under #46 and
#96:

    git grep -n 'InvitationStore' -- 'Jellyfin.Plugin.Invites/*.cs' | grep -v 'Storage/InvitationStore.cs' | grep -c ''
    14

So a server running this plugin today does get a directory and a claim file in
it. This paragraph said it does not get an invitations file, because the only
call that writes the records file was inside the file that declares it. Both
halves of that were overtaken without the sentence moving, and the correction is
made here rather than the paragraph deleted. A mint and a revocation both write
the records file now:

    git grep -n '\.Write(' -- 'Jellyfin.Plugin.Invites/*.cs'
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:180:            store.Write(contents.Invitations.Add(minted));
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:253:            store.Write(contents.Invitations.Replace(found, revoked));
    Jellyfin.Plugin.Invites/Storage/HashSecret.cs:291:            file.Write(value, 0, value.Length);
    Jellyfin.Plugin.Invites/Storage/InvitationStore.cs:357:            writer.Write(json);
    Jellyfin.Plugin.Invites/Storage/StoreLock.cs:128:            writer.Write(written);

So an operator who has minted once has an invitations file, and the ceilings on
this page are what bound how large it gets. What still does not exist is a
redemption, so nothing grows the file from the public side: the routine that
decides a presented code has no caller.

    git grep -n 'Decide(' -- 'Jellyfin.Plugin.Invites/*.cs' ':!*RedemptionDecision.cs'
    exit=1

`InvitationStore.Read` answers a directory with no file as no invitations rather
than creating one, so reading at startup does not bring the file into being.

So every entry below names the issue that owns the behaviour, and five of the
nine are held by a test that was seen to fail. Which five, and what each one
was broken with, is at the foot of this page. The other four were not put to that
check here, so nothing on this page says a test holds them. That accounting
matters and is not a formality: an entry without one is a decision the plan has
taken and nothing more, and a decision is something a later change can contradict
without anything going red. When a behaviour lands, its entry gains the test that
holds it and the count at the foot moves.

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
against the accounts the server actually has. Owned by #46. Rotation is a route
and a button rather than something still owed, which is #30 and is written up
under `POST /Invites/HashSecret/Rotate` in [docs/api.md](api.md).

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

The done condition of #115 asks that every entry match the behaviour the tests
assert. Five of the nine are shown below to be held by one, and that clause is
met one entry at a time as the behaviours land rather than by this document.

## Which entries a test holds

Each of the five was checked by breaking the thing the entry rests on and
watching the suite, rather than by reading a test name and deciding it looked
close enough. None of the faults is in the tree, and every run below was made at
the commit this page is on rather than carried across from the commit where the
fault was first written. The rebuild is part of each run: a revert followed by
`--no-build` measures the binary the fault is still in.

**A code is shown once and cannot be recovered.** The mint routine was handed
the code as the template label, one argument along from where it belongs, which
is the slip that puts a live code in the store file:

    $ sed -i '178s/templateLabel: templateLabel);/templateLabel: code);/' Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs
    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      MintedCodeOnDiskTests.NothingTheMintLeavesOnDiskIsShapedLikeACode [FAIL]
      MintedCodeIsNotHandedBackTests.NoReadingRouteHandsBackAnythingShapedLikeACode [FAIL]
      InvitesControllerTests.MintingReturnsACodeThatMatchesTheStoredHash [FAIL]
    Fehler!      : Fehler: 3, erfolgreich: 404, übersprungen: 8, gesamt: 415

**A restored backup revives spent invitations**, for the part this page adds,
which is reading the disagreement the plugin reports on load. The comparison was
inverted by dropping one character:

    $ sed -i '156s/if (!present.Contains(account))/if (present.Contains(account))/' Jellyfin.Plugin.Invites/Storage/ConsistencyReport.cs
    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      ConsistencyReportTests.AStoreTheServerAgreesWithReportsNothing [FAIL]
      ConsistencyReportTests.ARecordThatNamesOneAccountTwiceIsNotTidiedAway [FAIL]
      ConsistencyReportTests.AStoreThatDisagreesInBothDirectionsIsReportedInBoth [FAIL]
      StoreLoadTests.ALoadReportsWhatTheStoreDisagreesWithInBothDirections [FAIL]
      LoadOnStartTests.AStartOverAStoreThatDisagreesNamesBothDirections [FAIL]
      LoadOnStartTests.DisagreementsBeyondTheBoundAreCountedRatherThanNamed [FAIL]
    Fehler!      : Fehler: 6, erfolgreich: 401, übersprungen: 8, gesamt: 415

Two of those six assert that it happens on load, which is the word the entry
uses, rather than only that the comparison answers correctly when somebody calls
it.

**The server's timezone does not change when an invitation expires.** The
comparison was moved off the moments and onto the wall-clock readings, which is
exactly what the entry is about and what a suite spelling every instant at one
offset cannot see:

    $ sed -i '111s/if (now >= match.ExpiresAt)/if (now.DateTime >= match.ExpiresAt.DateTime)/' Jellyfin.Plugin.Invites/Redemption/RedemptionDecision.cs
    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      StoredInstantTests.TheDecisionReadsTwoSpellingsOfOneClockReadingAlike(offsetHours: 13) [FAIL]
      StoredInstantTests.TheDecisionJudgesTwoSpellingsOfOneMomentAlike(offsetHours: 13) [FAIL]
    Fehler!      : Fehler: 2, erfolgreich: 405, übersprungen: 8, gesamt: 415

**Revoking an invitation does not remove the accounts it already created.** A
revocation rebuilds the record field by field, so the mistake this entry is
about is one field left off that call, which reads at the site like tidying a
list that is no longer needed:

    $ sed -i '104s/accountsProduced: invitation.AccountsProduced);/accountsProduced: []);/' Jellyfin.Plugin.Invites/Invitations/Revocation.cs
    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      RevocationTests.TheAccountsAlreadyCreatedAreStillNamed [FAIL]
      RevocationTests.RevokingChangesNothingElseAboutTheRecord [FAIL]
    Fehler!      : Fehler: 2, erfolgreich: 405, übersprungen: 8, gesamt: 415

Two rather than one, and the second is the wider of the pair: it holds every
field the revocation carries across rather than this one, so a change dropping
some other field is caught by the same assertion. The entry's other half, that
revoking stops the invitation from being redeemed again, is the revocation
outcome and is held by `RedemptionDecisionTests.ARevokedInvitationIsRefused`,
which was put to a fault under #57 rather than here.

**Expiry is not the same as deletion**, for the half of it that had nothing.
That entry promises two separate things: a presented code stops being honoured
at the instant, and the record stays where it was so an operator can still see
it and account for it. The first half is the comparison the timezone entry above
rests on. The second was held by nothing, and a reading routine rewritten to hide
what the clock had passed left the whole suite green. Three faults, one per
assertion, each reddening exactly one:

    $ sed -i '195s/.*/            return Store().Read().Invitations.Where(invitation => invitation.ExpiresAt > _clock.UtcNow).ToImmutableArray();/' Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs
    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      ExpiryIsNotDeletionTests.AnInvitationPastItsExpiryIsStillListed [FAIL]
    Fehler!      : Fehler: 1, erfolgreich: 406, übersprungen: 8, gesamt: 415

    $ sed -i '209s/invitation.Id == id);/invitation.Id == id \&\& invitation.ExpiresAt > _clock.UtcNow);/' Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs
    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      ExpiryIsNotDeletionTests.AnInvitationPastItsExpiryIsStillFoundByItsIdentifier [FAIL]
    Fehler!      : Fehler: 1, erfolgreich: 406, übersprungen: 8, gesamt: 415

The third is the one worth reading, because it does not filter anything a caller
sees. It writes the shortened list back, which is the tidy-up somebody adds to a
reading routine believing it changes nothing:

      var seen = Store().Read().Invitations;
      var live = seen.Where(invitation => invitation.ExpiresAt > _clock.UtcNow).ToImmutableArray();
      if (live.Length != seen.Length)
      {
          Store().Write(live);
      }

      return seen;

    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      ExpiryIsNotDeletionTests.CrossingTheExpiryChangesNothingOnTheDisk [FAIL]
    Fehler!      : Fehler: 1, erfolgreich: 406, übersprungen: 8, gesamt: 415

What that does not cover is the sentence about the retention rule. Removing a
record once retention allows it is the sweep in #59 and nothing in this tree
sweeps, so an expired record staying forever and an expired record removed on
schedule are the same tree today.

Each fault was put back afterwards and the suite returns to where it started:

    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
    Bestanden!   : Fehler: 0, erfolgreich: 407, übersprungen: 8, gesamt: 415

The other four entries are the username disclosure, the deleted library, the
server line, and uninstall leaving accounts alone. No fault was run against any
of them here, and for three of the four there is nothing to run one against.
Nothing in the plugin judges a username, resolves a library identifier, or
compares the running server against the line it was built for:

    git grep -niE 'Username' -- 'Jellyfin.Plugin.Invites/*.cs' | grep -cv '///'
    0
    git grep -niE 'EnabledFolders|libraryId|ResolveLibrar' -- 'Jellyfin.Plugin.Invites/*.cs' ; echo "exit=$?"
    exit=1
    git grep -niE 'ApplicationVersion|ServerVersion|TargetAbi' -- 'Jellyfin.Plugin.Invites/*.cs' ; echo "exit=$?"
    exit=1

The first of the three is counted rather than statused. One line in the plugin
carries the word and it is inside a documentation comment, so that grep exits 0
and what has to be zero is the count surviving the filter.

The fourth is different and is worth separating from the other three, because it
reads as covered and is not. Uninstall leaving accounts alone is true today
because the plugin has no way to touch one: the seam over the server's accounts
carries a single member and it reads.

    git grep -nE '^    [A-Za-z?<>,\.]+ [A-Za-z]+ \{ get; \}' -- Jellyfin.Plugin.Invites/Accounts/IServerAccounts.cs
    Jellyfin.Plugin.Invites/Accounts/IServerAccounts.cs:27:    IReadOnlyCollection<Guid>? Identifiers { get; }

That was an absence rather than a guard until #91 turned it into one.
`AccountsAreNeverWrittenTests` refuses a member on that interface that takes an
argument or hands nothing back, refuses a name the seam reaches by reflection
that is not a read on the server's own interface, and refuses a second type in
the plugin that can be handed the user manager at all. The middle one is the
reason the file exists: the seam binds late, so a write hidden behind a
looked-up name is invisible to the compiler and to the invariant lint, which
reads source text.

The entry is still not counted among the five. What is refused is the
capability, and what the entry promises is an uninstall that leaves the accounts
where they are. Exercising that needs a seam that can create an account so there
is something to leave behind, which is #103, and nothing here stands in for it.
