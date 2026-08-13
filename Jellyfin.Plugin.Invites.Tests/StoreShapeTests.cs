using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
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
/// here is one document per version the store has ever declared, which today is
/// one:
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
    /// The committed document for the shape the store declares today.
    /// </summary>
    private const string VersionOne = "version-1.json";

    /// <summary>
    /// The first record in the committed document: never revoked, with uses
    /// left.
    /// </summary>
    /// <returns>One invitation.</returns>
    private static Invitation TheLiveRecord()
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
            accountsProduced: ImmutableArray.Create(
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                Guid.Parse("99999999-8888-7777-6666-555555555555")));
    }

    /// <summary>
    /// The second record in the committed document: revoked, spent, and holding
    /// the account it produced.
    /// </summary>
    /// <remarks>
    /// The document carries both because the two exercise different halves of
    /// the shape. A file of live records alone would say nothing about whether
    /// a revocation survives being read.
    /// </remarks>
    /// <returns>One invitation.</returns>
    private static Invitation TheRevokedRecord()
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
            accountsProduced: ImmutableArray.Create(
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
    /// The committed version one document reads, and every field of every
    /// record in it comes back as it was written.
    /// </summary>
    /// <remarks>
    /// The equality this leans on is the record type's own, which compares the
    /// keyed hash and the accounts by their contents rather than by the
    /// identity of the arrays behind them, so this is a field-by-field
    /// assertion and not a reference one.
    /// </remarks>
    [Fact]
    public void TheCommittedVersionOneDocumentReadsFieldByField()
    {
        using var directory = new OwnedDirectory();
        var store = AStoreHolding(directory, VersionOne);

        var contents = store.Read();

        Assert.Equal(
            new[] { TheLiveRecord(), TheRevokedRecord() },
            contents.Invitations);
    }

    /// <summary>
    /// A revocation in a committed document is still a revocation after it has
    /// been read.
    /// </summary>
    /// <remarks>
    /// Asserted separately from the field comparison above because it is the
    /// property #93 is about and the one an upgrade silently loses. The record
    /// type answers it rather than a comparison made here.
    /// </remarks>
    [Fact]
    public void ARevocationInTheCommittedDocumentSurvivesBeingRead()
    {
        using var directory = new OwnedDirectory();
        var store = AStoreHolding(directory, VersionOne);

        var contents = store.Read();

        Assert.False(contents.Invitations[0].IsRevoked);
        Assert.True(contents.Invitations[1].IsRevoked);
    }

    /// <summary>
    /// What this build writes for the same records is the committed document,
    /// byte for byte apart from line endings.
    /// </summary>
    /// <remarks>
    /// This is the direction that catches a rename. A member renamed in the
    /// stored shape passes every round-trip test in the suite, because the
    /// writer and the reader move together, and it silently stops reading every
    /// file already on a server. Here the bytes do not move, so the rename has
    /// something to disagree with.
    /// </remarks>
    [Fact]
    public void ThisBuildStillWritesTheCommittedVersionOneShape()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);

        store.Write(new[] { TheLiveRecord(), TheRevokedRecord() });

        Assert.Equal(
            WithOneKindOfLineEnding(TheCommitted(VersionOne)),
            WithOneKindOfLineEnding(File.ReadAllText(store.Path)));
    }

    /// <summary>
    /// The committed document declares the version this build writes, so the
    /// directory owes a second file the day that number moves.
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

        var document = JsonNode.Parse(TheCommitted(VersionOne));
        Assert.NotNull(document);
        Assert.Equal(1, document!["version"]!.GetValue<int>());
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
