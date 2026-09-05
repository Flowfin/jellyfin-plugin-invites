using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Storage;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The committed documents, one per shape the store has ever declared, read
/// through the store that has to go on reading them.
/// </summary>
/// <remarks>
/// <para>
/// #105 asks for a fixture per shipped store version, migrated forward and
/// asserted field by field. Which set that means was open, and the answer taken
/// here is one document per version the store has ever declared, which is read
/// off the store rather than counted here:
/// </para>
/// <para>
/// <c>git grep -n 'public const int Version' -- Jellyfin.Plugin.Invites/Storage/InvitationStore.cs</c>
/// </para>
/// <para>
/// The alternative reading, one per shape the writer has ever produced, starts
/// the debt at a moment nobody can name: a shape that was changed before
/// anything was released was never in anybody's store, and a fixture for it is
/// a fixture of a state no reader will ever meet. A version is a number the
/// tree states, so the count is derived rather than remembered, and the day the
/// constant moves is the day this directory owes a second file.
/// </para>
/// <para>
/// <b>What a committed document buys that a round trip does not.</b> Everything
/// else in the suite writes with the current writer and reads with the current
/// reader, so a member renamed on both sides passes: the pair agrees with
/// itself and disagrees with every file already on a server. These bytes were
/// written once and are not regenerated, so they disagree out loud.
/// </para>
/// <para>
/// Nothing here reads or writes outside a directory the test creates and
/// removes. The fixture is copied in rather than opened where it lies.
/// </para>
/// </remarks>
public class StoreShapeTests
{
    /// <summary>
    /// The committed document for the first shape, which carried the
    /// template's name and no grant. This build reads it and never writes it.
    /// </summary>
    private const string VersionOne = "version-1.json";

    /// <summary>
    /// The committed document for the second shape, which carries the grant
    /// #61 copies at minting and claims its accounts as bare identifiers. This
    /// build reads it and never writes it.
    /// </summary>
    private const string VersionTwo = "version-2.json";

    /// <summary>
    /// The committed document for the shape the store declares today, which
    /// claims each account as an entry carrying its own expiry, under #468.
    /// </summary>
    private const string VersionThree = "version-3.json";

    /// <summary>
    /// The first account the live record claims.
    /// </summary>
    private static readonly Guid _firstAccount = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    /// <summary>
    /// The second account the live record claims.
    /// </summary>
    private static readonly Guid _secondAccount = Guid.Parse("99999999-8888-7777-6666-555555555555");

    /// <summary>
    /// The expiry the first account carries in the current shape, and the one
    /// no older shape had anywhere to keep.
    /// </summary>
    /// <remarks>
    /// It is after the record was minted, which the record type refuses the
    /// other way round, and it is the only non-absent expiry in this
    /// directory. Nothing in the plugin writes one yet, so a committed
    /// document is the only place the member is exercised in the state that is
    /// not its default, which is what makes it worth carrying here.
    /// </remarks>
    private static readonly DateTimeOffset _firstAccountExpires =
        new(2027, 3, 4, 5, 6, 7, TimeSpan.FromHours(2));

    /// <summary>
    /// The first record in a committed document: never revoked, with uses
    /// left.
    /// </summary>
    /// <param name="template">
    /// The grant it carries: the household grant in the two later shapes, and
    /// nothing in the first shape, which had no member to carry one.
    /// </param>
    /// <param name="firstAccountExpires">
    /// When the first of the two accounts it claims expires, or <c>null</c>
    /// where it does not. Every shape before the current one claimed an
    /// account as a bare identifier, so a record read out of one carries the
    /// absence here whatever the account's expiry ought to be.
    /// </param>
    /// <returns>One invitation.</returns>
    private static Invitation TheLiveRecord(AccountTemplate? template, DateTimeOffset? firstAccountExpires)
    {
        return new Invitation(
            id: Guid.Parse("0f1e2d3c-4b5a-4968-8776-65544332211a"),
            codeHash: ImmutableArray.Create<byte>(0x01, 0x7f, 0x80, 0xff, 0x00, 0x42),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.FromHours(2)),
            expiresAt: new DateTimeOffset(2026, 3, 11, 5, 6, 7, TimeSpan.FromHours(2)),
            usesGranted: 3,
            usesRemaining: 1,
            revokedAt: null,
            revokedBy: null,
            templateLabel: "Household",
            template: template,
            accountsProduced: ImmutableArray.Create(
                new ProducedAccount(_firstAccount, firstAccountExpires),
                ProducedAccount.ThatDoesNotExpire(_secondAccount)));
    }

    /// <summary>
    /// The second record in a committed document: revoked, spent, and holding
    /// the account it produced.
    /// </summary>
    /// <remarks>
    /// The document carries both because the two exercise different halves of
    /// the shape. A file of live records alone would say nothing about whether
    /// a revocation survives being read. In the current shape it carries the
    /// guest grant, so the two records carry two different grants and a reader
    /// that handed every record the first grant it met would be caught.
    /// </remarks>
    /// <param name="template">The grant it carries, or nothing in the first shape.</param>
    /// <returns>One invitation.</returns>
    private static Invitation TheRevokedRecord(AccountTemplate? template)
    {
        return new Invitation(
            id: Guid.Parse("2c9f7b41-0d5e-4a63-8b12-7e4d3f6a9c05"),
            codeHash: ImmutableArray.Create<byte>(0xaa, 0xbb, 0xcc),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero),
            expiresAt: new DateTimeOffset(2026, 4, 8, 10, 0, 0, TimeSpan.Zero),
            usesGranted: 1,
            usesRemaining: 0,
            revokedAt: new DateTimeOffset(2026, 4, 2, 9, 30, 0, TimeSpan.Zero),
            revokedBy: Guid.Parse("44445555-6666-7777-8888-99990000aaaa"),
            templateLabel: "Friends",
            template: template,
            accountsProduced: ProducedAccounts.ThatDoNotExpire(
                Guid.Parse("12345678-90ab-cdef-1234-567890abcdef")));
    }

    /// <summary>
    /// The text of a committed document, as it sits beside the test host.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <returns>The document.</returns>
    private static string TheCommitted(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "StoreShapes", name);

        Assert.True(
            File.Exists(path),
            path + " is not beside the test host. The committed shapes are copied there by the project file, and a"
            + " test that silently found none would assert nothing.");

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Line endings taken out of a comparison, and nothing else.
    /// </summary>
    /// <remarks>
    /// The committed file arrives with whatever endings the clone checked it out
    /// with, which is not the same on every machine, and the writer emits line
    /// feeds everywhere. Comparing those would make the assertion about a clone
    /// setting rather than about the document, and the members and values are
    /// what this is for.
    /// </remarks>
    /// <param name="text">The document.</param>
    /// <returns>The same document with one kind of line ending.</returns>
    private static string WithOneKindOfLineEnding(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>
    /// A store over a directory the test owns, holding the committed document.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="name">Which committed document.</param>
    /// <returns>The store.</returns>
    private static InvitationStore AStoreHolding(OwnedDirectory directory, string name)
    {
        var store = new InvitationStore(directory.Path);
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(store.Path, TheCommitted(name));

        return store;
    }

    /// <summary>
    /// The committed version three document reads, and every field of every
    /// record in it, the grant and each account's expiry included, comes back
    /// as it was written.
    /// </summary>
    /// <remarks>
    /// The equality this leans on is the record type's own, which compares the
    /// keyed hash, the accounts and the grant by their contents rather than by
    /// the identity of the arrays behind them, so this is a field-by-field
    /// assertion and not a reference one. A claim is compared by both of its
    /// members, so a reader that dropped every expiry disagrees here rather
    /// than passing on the identifiers alone.
    /// </remarks>
    [Fact]
    public void TheCommittedVersionThreeDocumentReadsFieldByField()
    {
        using var directory = new OwnedDirectory();
        var store = AStoreHolding(directory, VersionThree);

        var contents = store.Read();

        Assert.Equal(
            new[]
            {
                TheLiveRecord(TestTemplates.Household, _firstAccountExpires),
                TheRevokedRecord(TestTemplates.Guest),
            },
            contents.Invitations);
    }

    /// <summary>
    /// The committed version two document is migrated forward: every field it
    /// carried comes back as it was written, and every account it claimed
    /// comes back with no expiry, which that shape had nowhere to keep.
    /// </summary>
    /// <remarks>
    /// The absence is asserted on its own beside the field comparison, because
    /// the comparison would also pass for a migration that invented an expiry
    /// equal to whatever this test happened to hand the builder. Nothing is
    /// invented: an expiry worked out from the invitation is the derivation
    /// #68 refuses and #468 exists against, and the absence is what
    /// <see cref="ProducedAccount"/> declares means an account that does not
    /// expire.
    /// </remarks>
    [Fact]
    public void TheCommittedVersionTwoDocumentMigratesForwardWithNoAccountExpiry()
    {
        using var directory = new OwnedDirectory();
        var store = AStoreHolding(directory, VersionTwo);

        var contents = store.Read();

        Assert.Equal(
            new[]
            {
                TheLiveRecord(TestTemplates.Household, firstAccountExpires: null),
                TheRevokedRecord(TestTemplates.Guest),
            },
            contents.Invitations);
        Assert.All(
            contents.Invitations.SelectMany(record => record.AccountsProduced),
            claim => Assert.Null(claim.ExpiresAt));
        Assert.NotEmpty(contents.Invitations.SelectMany(record => record.AccountsProduced));
    }

    /// <summary>
    /// The committed version one document is migrated forward: every field it
    /// carried comes back as it was written, and the grant, which that shape
    /// never carried, comes back absent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the test #42 handed on to whoever shipped the second shape: a
    /// committed fixture of the older shape, migrated and asserted field by
    /// field. The bytes were written under version one and are not
    /// regenerated, so a reader that stopped understanding that shape
    /// disagrees with them out loud.
    /// </para>
    /// <para>
    /// Absent is asserted rather than left to the equality, because the
    /// equality would also pass for a migration that invented a grant equal to
    /// the one the fixture builder happened to pass. Nothing is invented: a
    /// record minted under version one can create nothing, which is the strict
    /// direction #92 asks a migration to take.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheCommittedVersionOneDocumentMigratesForwardWithNoGrant()
    {
        using var directory = new OwnedDirectory();
        var store = AStoreHolding(directory, VersionOne);

        var contents = store.Read();

        Assert.Equal(
            new[]
            {
                TheLiveRecord(template: null, firstAccountExpires: null),
                TheRevokedRecord(template: null),
            },
            contents.Invitations);
        Assert.All(contents.Invitations, record => Assert.Null(record.Template));
        Assert.All(
            contents.Invitations.SelectMany(record => record.AccountsProduced),
            claim => Assert.Null(claim.ExpiresAt));
    }

    /// <summary>
    /// A revocation in a committed document is still a revocation after it has
    /// been read, in either shape.
    /// </summary>
    /// <remarks>
    /// Asserted separately from the field comparison above because it is the
    /// property #93 is about and the one an upgrade silently loses. The record
    /// type answers it rather than a comparison made here.
    /// </remarks>
    /// <param name="shape">Which committed document.</param>
    [Theory]
    [InlineData(VersionOne)]
    [InlineData(VersionTwo)]
    [InlineData(VersionThree)]
    public void ARevocationInTheCommittedDocumentSurvivesBeingRead(string shape)
    {
        using var directory = new OwnedDirectory();
        var store = AStoreHolding(directory, shape);

        var contents = store.Read();

        Assert.False(contents.Invitations[0].IsRevoked);
        Assert.True(contents.Invitations[1].IsRevoked);
    }

    /// <summary>
    /// What this build writes for the same records is the committed version
    /// three document, byte for byte apart from line endings.
    /// </summary>
    /// <remarks>
    /// This is the direction that catches a rename. A member renamed in the
    /// stored shape passes every round-trip test in the suite, because the
    /// writer and the reader move together, and it silently stops reading every
    /// file already on a server. Here the bytes do not move, so the rename has
    /// something to disagree with.
    /// </remarks>
    [Fact]
    public void ThisBuildStillWritesTheCommittedVersionThreeShape()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);

        store.Write(new[]
        {
            TheLiveRecord(TestTemplates.Household, _firstAccountExpires),
            TheRevokedRecord(TestTemplates.Guest),
        });

        Assert.Equal(
            WithOneKindOfLineEnding(TheCommitted(VersionThree)),
            WithOneKindOfLineEnding(File.ReadAllText(store.Path)));
    }

    /// <summary>
    /// An older document is never written back in the shape it was read from.
    /// Reading one and writing what was read produces the current shape, with
    /// every member the older shape lacked present and carrying its absence.
    /// </summary>
    /// <remarks>
    /// The migration runs forward and only forward, which is #92's rule. A
    /// store that wrote the older shape back would leave a file that the next
    /// read migrates again, and a record that had gained a grant or an account
    /// expiry in the meantime would lose it on the way.
    /// </remarks>
    /// <param name="shape">Which committed document was read.</param>
    [Theory]
    [InlineData(VersionOne)]
    [InlineData(VersionTwo)]
    public void AMigratedDocumentIsWrittenBackInTheCurrentShape(string shape)
    {
        using var directory = new OwnedDirectory();
        var store = AStoreHolding(directory, shape);

        store.Write(store.Read().Invitations);

        var document = JsonNode.Parse(File.ReadAllText(store.Path));
        Assert.NotNull(document);
        Assert.Equal(InvitationStore.Version, document!["version"]!.GetValue<int>());
        foreach (var record in document["invitations"]!.AsArray())
        {
            Assert.True(record!.AsObject().ContainsKey("template"));

            foreach (var claim in record["accountsProduced"]!.AsArray())
            {
                Assert.True(claim!.AsObject().ContainsKey("expiresAt"));
                Assert.Null(claim["expiresAt"]);
            }
        }
    }

    /// <summary>
    /// A version one document loses its grant as well, which the theory above
    /// cannot assert for both shapes because version two carries one.
    /// </summary>
    [Fact]
    public void AVersionOneDocumentIsWrittenBackWithItsGrantAbsent()
    {
        using var directory = new OwnedDirectory();
        var store = AStoreHolding(directory, VersionOne);

        store.Write(store.Read().Invitations);

        var document = JsonNode.Parse(File.ReadAllText(store.Path));
        foreach (var record in document!["invitations"]!.AsArray())
        {
            Assert.Null(record!["template"]);
        }
    }

    /// <summary>
    /// Each committed document declares the version its name carries, and
    /// there is one for every version the store has ever declared, so the
    /// directory owes a further file the day that number moves.
    /// </summary>
    /// <remarks>
    /// The count of committed shapes is derived from the store rather than kept
    /// in a list here. A version bump with no fixture beside it turns this red,
    /// which is the moment #105's clause is owed something rather than a moment
    /// somebody has to remember.
    /// </remarks>
    [Fact]
    public void ThereIsACommittedDocumentForEveryShapeTheStoreDeclares()
    {
        var shapes = Directory.GetFiles(
            Path.Combine(AppContext.BaseDirectory, "StoreShapes"),
            "version-*.json");

        var declared = Enumerable
            .Range(1, InvitationStore.Version)
            .Select(version => FormattableString.Invariant($"version-{version}.json"))
            .ToArray();

        Assert.Equal(
            declared,
            shapes.Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray());

        foreach (var version in Enumerable.Range(1, InvitationStore.Version))
        {
            var document = JsonNode.Parse(TheCommitted(FormattableString.Invariant($"version-{version}.json")));
            Assert.NotNull(document);
            Assert.Equal(version, document!["version"]!.GetValue<int>());
        }
    }

    /// <summary>
    /// A store file nobody may read is raised rather than read as no
    /// invitations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The file state #105 names that nothing else in the suite covers. An
    /// unreadable store answered as an empty one is a server that has quietly
    /// forgotten every live invitation, which is the same failure the store
    /// refuses loudly for a file it cannot parse.
    /// </para>
    /// <para>
    /// Unix only, because a mode is a POSIX concept and the plugin says so
    /// where it inspects one. The first assertion is the premise rather than
    /// the property: a process running as a user that ignores the mode would
    /// read the file anyway, and this fails there rather than passing and
    /// reading as a check that ran.
    /// </para>
    /// </remarks>
    [UnixOnlyFact]
    [UnsupportedOSPlatform("windows")]
    public void AStoreThatCannotBeReadIsRaisedRatherThanReadAsEmpty()
    {
        using var directory = new OwnedDirectory();
        var store = AStoreHolding(directory, VersionOne);

        File.SetUnixFileMode(store.Path, UnixFileMode.None);

        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => File.ReadAllText(store.Path));
            Assert.Throws<UnauthorizedAccessException>(() => store.Read());
        }
        finally
        {
            // Put the mode back so the directory this test owns can be removed
            // on every platform's rules rather than on this one's.
            File.SetUnixFileMode(store.Path, InvitationStore.CreatedMode);
        }
    }
}
