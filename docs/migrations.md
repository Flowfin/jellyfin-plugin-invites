# Migrations

Two things this plugin leaves on a server outlive the version that wrote them,
and they migrate differently. The configuration is a file the server's own
plugin configuration mechanism writes and reads back. The store is this plugin's
own file, and it declares a version.

This page is the rules both follow. It is #92's, and it exists because a
migration is the one piece of code that runs against bytes nobody can reproduce:
whoever meets the failure has the only copy of the input, and by the time they
report it the plugin has already decided.

## Forward only

A migration runs from an older shape to a newer one and never the other way.

The store enforces it rather than promising it. A document declaring a version
newer than this build reads is refused, with both versions and the file named,
and nothing is written, so putting the newer plugin back is still available
afterwards:

    git grep -n 'declared > Version' -- Jellyfin.Plugin.Invites/Storage/InvitationStore.cs

There is no downgrade path and there will not be one. A newer store may hold
fields this build has no member for, and writing it back through this build's
writer would drop them silently, which is the one failure a downgrade would be
asked to avoid.

The configuration has no version of its own and needs none for this rule: the
server's reader drops an element the type does not declare, so an older build
reading a newer file loses the settings it does not know rather than refusing.
That is the framework's behaviour and not this plugin's choice, and it is stated
here so nobody reads the store's refusal as covering both.

## Nothing is guessed at, and the guess is the stricter option

Where an old shape carries no value for a new field, the migration writes the
value that grants least, and never one it worked out.

The store has two migrations and each is exactly this case. Version one carried
a template's name and no copy of what that template granted. Resolving the name
against the configuration at read time would be the lookup #61 forbids, and any
grant written in without a name to resolve would be a grant nobody decided. So
the record comes forward with its grant absent, which is what an absent grant
already means on that type: minted before the copy existed, and able to create
nothing.

    git grep -n 'ToInvitationWithNoGrant' -- Jellyfin.Plugin.Invites/Storage/InvitationStore.cs

Version two claimed the accounts an invitation produced as bare identifiers, so
there was nowhere on a record to keep an account's own expiry, which is what
#468 gave it. An expiry worked out from the invitation is the derivation #68
refuses - it would move when the invitation moved and would apply to every
account one invitation made - and an expiry invented from anything else is a
value nobody decided. So a claim comes forward with its expiry absent, which is
what an absent expiry already means on that type: an account this plugin never
disables until an operator says so.

    git grep -n 'ClaimsWithNoExpiry' -- Jellyfin.Plugin.Invites/Storage/InvitationStore.cs

Both are the same shape of answer, and the direction is worth stating once:
neither migration can make the plugin do more than it did before the read. A
record with no grant creates no account, and an account with no expiry is
disabled by nothing here.

**No migration widens a permission or a ceiling.** Neither of the two that exist
can, because the strictest value for a grant is its absence and that is what one
of them writes, and the other writes no grant at all. A future migration that had to choose between two values would take the
one that grants less, and a migration that could not choose at all would refuse
rather than pick.

## The plugin says what it did

A record brought forward under the rule above is a record an operator will meet
later as a refusal, and a refusal with no explanation anywhere is the support
thread this page exists to prevent.

So a read that migrated says so. The observation travels back with the records,
because nothing on the redemption path may hold a logger, and
`Jellyfin.Plugin.Invites.Startup.LoadOnStart` writes one line when the server
starts:

    git grep -n 'private void ReportAMigration' -- Jellyfin.Plugin.Invites/Startup/LoadOnStart.cs

The line names both versions and how many records came forward without a grant,
and it carries nothing out of a record. That is `docs/logging.md`'s rule met by
the shape of the message rather than by care: there is no field of an invitation
in it to leave out.

It is a warning and not an error. Nothing failed - a store written by an older
build is exactly what a forward migration is for, and the read leaves the file
untouched. What is worth an operator's attention is the cost, and the sentence
carries it.

## What the configuration does with a field that moved

Three cases, and only the third is code.

**A field that was removed** is read once by a build that still declares it and
dropped by the next one, because the server's reader ignores an element the type
has no member for. Nothing is owed here and nothing is written.

**A field that was added** takes the initialiser on the type, because an element
that is absent from the file leaves the property at the value the constructor
gave it. That is why every setting owes a decided fresh-install value in
`Jellyfin.Plugin.Invites.Tests.FreshInstallConfigurationTests`: on a server that
upgrades, the fresh-install value is what the new setting arrives as, on every
installation at once.

**A field whose meaning changed** gets a real migration and never a coincidence
of names. Reusing an element name for a value that means something else is the
one shape the two cases above cannot cover, because both sides read cleanly and
the result is wrong: an operator who set the old meaning has the new one
applied. Where that is unavoidable, the new meaning takes a new element name and
the old one is read once, mapped in code with a test, and dropped.

## What exists today, and what is vacuous rather than done

Nothing has been published, so the number of shipped version transitions is
zero:

    gh release list --repo Flowfin/jellyfin-plugin-invites

A rule about every shipped transition is met by a set with nothing in it, and
that is worth saying out loud rather than leaving as a green mark: what holds the
rules above is not that they have been exercised across a release, it is the two
transitions the tree already carries and the tests over them.

Those transitions are store version one to the current shape and store version
two to it. Each has a committed document of the old shape, read through the
reader that has to go on reading it, and the fixtures are one per version the
store has ever declared rather than one per shape the writer has produced:

    git ls-files Jellyfin.Plugin.Invites.Tests/StoreShapes/

The configuration has no transition at all, because no shipped version has ever
declared a different shape of it. What it has instead is the three cases above,
two of which are the framework's behaviour asserted rather than described.
