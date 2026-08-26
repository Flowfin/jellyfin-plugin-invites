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
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:263:            store.Write(contents.Invitations.Add(minted));
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:380:            store.Write(contents.Invitations.Replace(found, revoked));
    Jellyfin.Plugin.Invites/Storage/HashSecret.cs:291:            file.Write(value, 0, value.Length);
    Jellyfin.Plugin.Invites/Storage/InvitationStore.cs:357:            writer.Write(json);
    Jellyfin.Plugin.Invites/Storage/StoreLock.cs:128:            writer.Write(written);

The second of those five was pasted at line 336 and the paste is re-run here
rather than the number edited on its own. What moved it was the reverse lookup
in #89 landing above it in the same file; the line it names is the same line of
source and nothing this paragraph says about it has changed.

So an operator who has minted once has an invitations file. This paragraph said
the ceilings are what bound how large it gets, and that is the half to read
carefully now that one of them acts. `InvitationOperations.LiveCeiling` bounds
how many invitations may be LIVE at once, under #33, and a record that has
expired, been spent or been revoked is not live and does not count against it. So
the ceiling bounds what the outstanding set can authorise and not the size of the
file, because the entry below on expiry not being deletion is what happens to the
record instead. What bounds the file is retention, which is #59 and does not
exist. What still does not exist as well is a redemption, so nothing grows the
file from the public side: the routine that decides a presented code has no
caller.

    git grep -n 'Decide(' -- 'Jellyfin.Plugin.Invites/*.cs' ':!*RedemptionDecision.cs'
    exit=1

`InvitationStore.Read` answers a directory with no file as no invitations rather
than creating one, so reading at startup does not bring the file into being.

So every entry below names the issue that owns the behaviour, and six of the
nine are held by a test that was seen to fail. Which six, and what each one
was broken with, is at the foot of this page. The other three were not put to that
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
startup, and a mismatch makes every one of its addresses answer a refusal naming
both versions. No partial operation follows a mismatch: the load that claims the
store directory declines before it claims anything.

The word to read carefully is refuse rather than remove. A plugin's controllers
are discovered from its assembly by the server's own routing, and nothing takes
an address back out of a route table that is already built, so the addresses
continue to exist and none of them does anything.

This plugin reaches server interfaces that move between server lines. A plugin
that loaded anyway would fail somewhere further in, at a moment chosen by whoever
happened to present a code, rather than at startup where the operator is looking.

The comparison is equality on the major and minor parts of the version, against
the `targetAbi` in `build.yaml` and nothing typed a second time. A server on a
later line is refused as firmly as one on an earlier line, which is the whole
difference between this and reading that field as a floor.

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
an uninstall is the last moment it exists. If it matters to you, take it before
you remove the plugin.

This paragraph said the view that presents the trail was not built and told you
to read the store file yourself. It is built. Both directions of it landed under
#89, so an administrator can read the trail off the plugin's own routes and save
what they answer:

    git grep -nE '\[Http(Get|Post)' -- Jellyfin.Plugin.Invites/Controllers/InvitesController.cs
    Jellyfin.Plugin.Invites/Controllers/InvitesController.cs:113:    [HttpPost]
    Jellyfin.Plugin.Invites/Controllers/InvitesController.cs:164:    [HttpGet]
    Jellyfin.Plugin.Invites/Controllers/InvitesController.cs:192:    [HttpGet("{id}")]
    Jellyfin.Plugin.Invites/Controllers/InvitesController.cs:240:    [HttpGet("Accounts/{accountId}")]
    Jellyfin.Plugin.Invites/Controllers/InvitesController.cs:269:    [HttpPost("{id}/Revoke")]
    Jellyfin.Plugin.Invites/Controllers/InvitesController.cs:324:    [HttpPost("HashSecret/Rotate")]

`GET /Invites` hands back every record with the accounts it produced, and each
of those says whether the server still has it. Copying the store file is still
available and is still the only thing that carries the fields the view does not,
so it is a fallback rather than the instruction it used to be.

What is not built is an export as a named operation, offered at the moment an
operator removes the plugin rather than found by one who thought of it first.
That is #91 and it is the part this entry cannot promise. Owned by #91, with the
account side in #45 and #94.

## What this page does not do yet

This section said the page is linked from the readme and not from the operator
guide, because that guide does not exist, and pasted a listing of `docs` that
returned nothing for it. The guide exists and links this page, so the clause is
met and both halves of it are read here rather than one:

    git grep -c 'docs/limits.md' -- README.md
    README.md:1
    git grep -c 'limits.md' -- docs/operator-guide.md
    docs/operator-guide.md:1

That is the link clause of #115 and nothing else about this page moves with it.

The done condition of #115 asks that every entry match the behaviour the tests
assert. Six of the nine are shown below to be held by one, and that clause is
met one entry at a time as the behaviours land rather than by this document.

## Which entries a test holds

Each of the six was checked by breaking the thing the entry rests on and
watching the suite, rather than by reading a test name and deciding it looked
close enough. None of the faults is in the tree, and every run below was made at
the commit this page is on rather than carried across from the commit where the
fault was first written. The rebuild is part of each run: a revert followed by
`--no-build` measures the binary the fault is still in.

Four of the ten commands below named a line that carried something else, and
this section says so because it is the failure it already warns about, met
rather than predicted. A line number in a paste is the part that ages without
the claim around it changing, and the failure it produces is the expensive kind:
the reader runs the command, gets a different line, and cannot tell a moved site
from the fault the entry exists to catch.

What moved them is #33's live ceiling extracting a routine into two of these
files, and it moved this page in the same change without the runs being made
again:

    git log --oneline -1 --format='%h %s' 391787d
    391787d Bound how many invitations may be live at once, for #33

So the declaration this paragraph makes had been false for two commits before
anybody ran the commands. Nothing catches this shape: the lint that refuses a
moved reference reads a `path:line:text` paste and a `sed` address is not one,
which is #282's subject rather than this page's and is written into #115.

Every command on this page is the one that produced the output beside it at the
commit this change lands on, and all ten faults were put back afterwards. Every
total below moved, which is the suite having grown, and two of the ten redden
more tests than they did, which is named at each of the two.

**A code is shown once and cannot be recovered.** The mint routine was handed
the code as the template label, one argument along from where it belongs, which
is the slip that puts a live code in the store file:

    $ sed -i '261s/templateLabel: templateLabel);/templateLabel: code);/' Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs
    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      MintedCodeOnDiskTests.NothingTheMintLeavesOnDiskIsShapedLikeACode [FAIL]
      MintedCodeIsNotHandedBackTests.NoReadingRouteHandsBackAnythingShapedLikeACode [FAIL]
      InvitesControllerTests.MintingReturnsACodeThatMatchesTheStoredHash [FAIL]
    Fehler!      : Fehler: 3, erfolgreich: 528, übersprungen: 8, gesamt: 539

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
    Fehler!      : Fehler: 6, erfolgreich: 525, übersprungen: 8, gesamt: 539

Two of those six assert that it happens on load, which is the word the entry
uses, rather than only that the comparison answers correctly when somebody calls
it.

**The server's timezone does not change when an invitation expires.** The
comparison was moved off the moments and onto the wall-clock readings, which is
exactly what the entry is about and what a suite spelling every instant at one
offset cannot see:

    $ sed -i '174s/if (now >= record.ExpiresAt)/if (now.DateTime >= record.ExpiresAt.DateTime)/' Jellyfin.Plugin.Invites/Redemption/RedemptionDecision.cs
    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      StoredInstantTests.TheDecisionReadsTwoSpellingsOfOneClockReadingAlike(offsetHours: 13) [FAIL]
      StoredInstantTests.TheDecisionJudgesTwoSpellingsOfOneMomentAlike(offsetHours: 13) [FAIL]
    Fehler!      : Fehler: 2, erfolgreich: 529, übersprungen: 8, gesamt: 539

**Revoking an invitation does not remove the accounts it already created.** A
revocation rebuilds the record field by field, so the mistake this entry is
about is one field left off that call, which reads at the site like tidying a
list that is no longer needed:

    $ sed -i '104s/accountsProduced: invitation.AccountsProduced);/accountsProduced: []);/' Jellyfin.Plugin.Invites/Invitations/Revocation.cs
    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      RevocationTests.TheAccountsAlreadyCreatedAreStillNamed [FAIL]
      RevocationTests.RevokingChangesNothingElseAboutTheRecord [FAIL]
      AGoneAccountTests.AClaimedAccountTheServerNoLongerHasRendersAsGone [FAIL]
    Fehler!      : Fehler: 3, erfolgreich: 528, übersprungen: 8, gesamt: 539

Three rather than two, and the second is the wider of the pair: it holds every
field the revocation carries across rather than this one, so a change dropping
some other field is caught by the same assertion. The third arrived with #45 and
is a different question again: a revocation that dropped the accounts would make
the row that says which of them are gone say nothing at all, so the entry is now
held on the operator's side as well as on the record's. The entry's other half, that
revoking stops the invitation from being redeemed again, is the revocation
outcome and is held by `RedemptionDecisionTests.ARevokedInvitationIsRefused`,
which was put to a fault under #57 rather than here.

**Expiry is not the same as deletion**, for the half of it that had nothing.
That entry promises two separate things: a presented code stops being honoured
at the instant, and the record stays where it was so an operator can still see
it and account for it. The first half is the comparison the timezone entry above
rests on. The second was held by nothing, and a reading routine rewritten to hide
what the clock had passed left the whole suite green. Three faults, one per
assertion. Two of the three redden exactly one test and the first reddens three
now, because #33's ceiling asks the same routine the same question, which is the
ceiling being held by the same reading rather than by a second one:

    $ sed -i '278s/.*/            return Store().Read().Invitations.Where(invitation => invitation.ExpiresAt > _clock.UtcNow).ToImmutableArray();/' Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs
    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      ExpiryIsNotDeletionTests.AnInvitationPastItsExpiryIsStillListed [FAIL]
      LiveCeilingTests.AnExpiredInvitationDoesNotCountAgainstTheCeiling [FAIL]
      LiveCeilingTests.LivenessIsTheSameQuestionARedemptionAsks [FAIL]
    Fehler!      : Fehler: 3, erfolgreich: 528, übersprungen: 8, gesamt: 539

    $ sed -i '292s/invitation.Id == id);/invitation.Id == id \&\& invitation.ExpiresAt > _clock.UtcNow);/' Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs
    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      ExpiryIsNotDeletionTests.AnInvitationPastItsExpiryIsStillFoundByItsIdentifier [FAIL]
    Fehler!      : Fehler: 1, erfolgreich: 530, übersprungen: 8, gesamt: 539

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
    Fehler!      : Fehler: 1, erfolgreich: 530, übersprungen: 8, gesamt: 539

What that does not cover is the sentence about the retention rule. Removing a
record once retention allows it is the sweep in #59 and nothing in this tree
sweeps, so an expired record staying forever and an expired record removed on
schedule are the same tree today.

**The plugin refuses to run on a server line it was not built for.** Three
faults, one per half of what the entry promises, and the other five that came
with the behaviour are in the change that landed it rather than repeated here.

The comparison loosened from equality to the floor the entry exists against,
which is the shape a reader reaches for when an operator complains that the
plugin will not load on a newer server:

    $ the equality in Jellyfin.Plugin.Invites/Server/ServerLine.cs replaced by  running >= new Version(declared)
    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      ServerLineTests.TheComparisonIsEqualityOnTheLine(declared: "10.11", running: 10.12.0, mayRun: False) [FAIL]
      ServerLineTests.TheComparisonIsEqualityOnTheLine(declared: "10.1", running: 10.11.0, mayRun: False) [FAIL]
      ServerLineTests.TheComparisonIsEqualityOnTheLine(declared: "10.11", running: 11.11.0, mayRun: False) [FAIL]
      ServerLineTests.TheComparisonIsEqualityOnTheLine(declared: "10.11", running: 12.0.0, mayRun: False) [FAIL]
    Fehler!      : Fehler: 4, erfolgreich: 527, übersprungen: 8, gesamt: 539

Four rows rather than three. The fourth is the pair that reads 10.1 against
10.11, which a floor comparison answers as agreement because 10.1 sorts below
10.11, and it is the row a comparison written as a prefix test answers wrongly
in the other direction.

The refusal scoped by a name rather than by the assembly a controller was
declared in, which is the version that reads as tidier and quietly attaches this
plugin's refusal to every controller the server has:

    $ sed -i 's|if (controller.ControllerType.Assembly == _plugin)|if (controller.ControllerType.Name.EndsWith("Controller", StringComparison.Ordinal))|' Jellyfin.Plugin.Invites/Server/ThisPluginsControllers.cs
    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      ServerLineTests.TheConventionTheServerIsHandedIsScopedToThisPlugin [FAIL]
      ServerLineTests.TheScopeIsTheAssemblyItWasGiven [FAIL]
      ServerLineTests.AControllerOutsideThisPluginIsLeftAlone [FAIL]
    Fehler!      : Fehler: 3, erfolgreich: 528, übersprungen: 8, gesamt: 539

And the third part, that no partial operation follows a mismatch. The load is
the only thing in this plugin that acts without a request, and it takes a claim
on the store directory for the lifetime of the process. The refusal was left
reporting and the early return dropped, which is the fault that leaves a plugin
holding a directory it can never use against a second server that could:

    $ the  return Task.CompletedTask;  removed from the mismatch branch of Jellyfin.Plugin.Invites/Startup/LoadOnStart.cs
    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
      LoadOnStartTests.AStartOnAnotherServerLineClaimsNothingAndReadsNothing [FAIL]
    Fehler!      : Fehler: 1, erfolgreich: 530, übersprungen: 8, gesamt: 539

What none of the three reaches is the server. Nothing here starts a Jellyfin, so
that the server applies a convention a plugin adds to its own options, and that a
browser meeting one of these addresses receives the refusal, are claims about a
running installation rather than about this suite. The end-to-end install job is
what stands nearest to them and it exercises a server on the declared line, where
the plugin runs, rather than one on another.

Each fault was put back afterwards and the suite returns to where it started:

    $ dotnet build --configuration Release --no-restore && dotnet test --configuration Release --no-build
    Bestanden!   : Fehler: 0, erfolgreich: 531, übersprungen: 8, gesamt: 539

The other three entries are the username disclosure, the deleted library, and
uninstall leaving accounts alone. No fault was run against any of them here, and
for two of the three there is nothing to run one against. Nothing in the plugin
judges a username or resolves a library identifier:

    git grep -niE 'Username' -- 'Jellyfin.Plugin.Invites/*.cs' | grep -cv '///'
    0
    git grep -niE 'EnabledFolders|libraryId|ResolveLibrar' -- 'Jellyfin.Plugin.Invites/*.cs' ; echo "exit=$?"
    exit=1

The first of the two is counted rather than statused. One line in the plugin
carries the word and it is inside a documentation comment, so that grep exits 0
and what has to be zero is the count surviving the filter.

A third grep stood beside these two until this revision. It asked whether
anything in the plugin compares the running server against the line it was built
for, and it found nothing. It finds something now, which is the server-line entry
having moved up into the list above rather than a change to either of the two
left here.

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

The entry is still not counted among the six. What is refused is the
capability, and what the entry promises is an uninstall that leaves the accounts
where they are. Exercising that needs a seam that can create an account so there
is something to leave behind, which is #103, and nothing here stands in for it.
