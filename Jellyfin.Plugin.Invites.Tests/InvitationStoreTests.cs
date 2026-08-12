using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Storage;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A fact that runs only where files have modes, and says why it did not run
/// where they do not.
/// </summary>
/// <remarks>
/// The alternative is a test that asserts a permission the platform does not
/// have, which passes for the wrong reason, and this repository has already
/// decided that a check which read nothing must not report as a check that
/// found nothing. The skip reason is printed by the runner, so a Windows run
/// says on its face which legs it did not take.
/// </remarks>
public sealed class UnixOnlyFactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnixOnlyFactAttribute"/> class.
    /// </summary>
    public UnixOnlyFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "File modes are a POSIX concept and this platform has none. The store reports the mode as unread here, which the Windows leg of this suite asserts.";
        }
    }
}

/// <summary>
/// A fact that runs only where files have no modes, so the disclosure the store
/// makes there is executed rather than trusted.
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsOnlyFactAttribute"/> class.
    /// </summary>
    public WindowsOnlyFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "This platform has file modes, so the store reads them and the Unix legs of this suite assert what it found.";
        }
    }
}

/// <summary>
/// A directory the test owns and deletes, with nothing in it to begin with.
/// </summary>
internal sealed class OwnedDirectory : IDisposable
{
    public OwnedDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "invites-store-" + Guid.NewGuid().ToString("N"));
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover directory under the system temporary path is not worth
            // failing a test over, and nothing outside that path was touched.
        }
    }
}

/// <summary>
/// The real store, against a directory the test creates and removes. Nothing
/// here stands in for the store: a fake would prove that the fake round-trips.
/// </summary>
public class InvitationStoreTests
{
    /// <summary>
    /// An invitation with every field carrying a value that is not the default
    /// for its type, so a field the store silently drops shows up as a
    /// difference rather than as a coincidence.
    /// </summary>
    /// <returns>One invitation.</returns>
    private static Invitation AnInvitation()
    {
        return new Invitation(
            id: Guid.Parse("6f0a2d1e-6b3c-4f8a-9f1d-2a7c5e8b0d34"),
            codeHash: ImmutableArray.Create<byte>(0x01, 0x7f, 0x80, 0xff, 0x00, 0x42),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.FromHours(2)),
            expiresAt: new DateTimeOffset(2026, 3, 11, 5, 6, 7, TimeSpan.FromHours(2)),
            usesGranted: 3,
            usesRemaining: 1,
            revokedAt: new DateTimeOffset(2026, 3, 5, 8, 9, 10, TimeSpan.Zero),
            templateLabel: "Household",
            accountsProduced: ImmutableArray.Create(
                Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                Guid.Parse("99999999-8888-7777-6666-555555555555")));
    }

    /// <summary>
    /// A second invitation, for the cases that need two records in one
    /// document. Its keyed hash is three bytes whose base64 is four plain
    /// letters, so a test that has to find that value in the file can look for
    /// it literally: the writer escapes a plus on the way out and a fixture
    /// searching for one would match nothing and assert nothing.
    /// </summary>
    /// <returns>One invitation, not equal to <see cref="AnInvitation"/>.</returns>
    private static Invitation AnotherInvitation()
    {
        return new Invitation(
            id: Guid.Parse("2c9f7b41-0d5e-4a63-8b12-7e4d3f6a9c05"),
            codeHash: ImmutableArray.Create<byte>(0xaa, 0xbb, 0xcc),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero),
            expiresAt: new DateTimeOffset(2026, 4, 8, 10, 0, 0, TimeSpan.Zero),
            usesGranted: 1,
            usesRemaining: 1,
            revokedAt: null,
            templateLabel: "Friends",
            accountsProduced: ImmutableArray<Guid>.Empty);
    }

    /// <summary>
    /// What is written comes back field for field. The equality this leans on
    /// is the one the record type wrote for this comparison, which compares the
    /// two sequence fields by their contents rather than by the identity of the
    /// arrays behind them.
    /// </summary>
    [Fact]
    public void WhatIsWrittenComesBack()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        var written = AnInvitation();

        store.Write(new[] { written });
        var read = store.Read();

        Assert.Equal(written, Assert.Single(read.Invitations));
    }

    /// <summary>
    /// And the file it came back out of is a file, in the directory the store
    /// was given, which the store created because it was not there.
    /// </summary>
    [Fact]
    public void TheStoreCreatesItsDirectoryAndItsFile()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);

        Assert.False(Directory.Exists(directory.Path));

        store.Write(new[] { AnInvitation() });

        Assert.True(File.Exists(store.Path));
        Assert.Equal(directory.Path, Path.GetDirectoryName(store.Path));
    }

    /// <summary>
    /// A store nobody has written yet reads as no invitations. A server that
    /// has never minted anything and a server whose store was deleted are the
    /// same state to everything downstream, and neither is an error to raise at
    /// a caller that has no way to act on it.
    /// </summary>
    [Fact]
    public void NoFileReadsAsNoInvitations()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);

        var read = store.Read();

        Assert.Empty(read.Invitations);
        Assert.Equal(StorePermissionState.NoStoreFile, read.Permissions.State);
    }

    /// <summary>
    /// An unreadable store is not an empty one. A file that is there and is not
    /// this document answered as no invitations is a server that has quietly
    /// forgotten every live invitation, and the redemptions that follow are all
    /// refusals nobody can explain.
    /// </summary>
    [Fact]
    public void AFileThatIsNotThisDocumentIsRaisedRatherThanReadAsEmpty()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(store.Path, "{ this is not the document");

        Assert.ThrowsAny<Exception>(() => store.Read());
    }

    /// <summary>
    /// A store file with nothing in it, and one holding only the whitespace a
    /// half-finished write left behind. Both are files somebody will hit, and
    /// neither is a store holding no invitations: that one is written as an
    /// empty list.
    /// </summary>
    /// <param name="contents">What the file holds.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   \n")]
    public void AnEmptyFileIsRaisedRatherThanReadAsEmpty(string contents)
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(store.Path, contents);

        Assert.Throws<JsonException>(() => store.Read());
    }

    /// <summary>
    /// A store cut off part way through. The write path no longer produces this
    /// state, since the document is built beside the store and moved over it,
    /// but a full disk, a damaged filesystem and a half-copied backup all still
    /// do. What a truncated document must not do is come back as the
    /// invitations that happened to be readable.
    /// </summary>
    [Fact]
    public void AFileTruncatedMidWriteIsRaisedRatherThanReadAsEmpty()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write(new[] { AnInvitation(), AnotherInvitation() });

        var whole = File.ReadAllText(store.Path);
        File.WriteAllText(store.Path, whole.Substring(0, whole.Length / 2));

        Assert.Throws<JsonException>(() => store.Read());
    }

    /// <summary>
    /// A file that parses as JSON and carries no invitation list. Both spellings
    /// are here because they arrive by different routes: a bare null is what a
    /// serializer somewhere else writes for nothing, and an object with the
    /// member spelled some other way is what an edit by hand produces.
    /// </summary>
    /// <param name="contents">What the file holds.</param>
    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"invitation\":[]}")]
    public void ADocumentWithNoInvitationListIsRaisedRatherThanReadAsEmpty(string contents)
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(store.Path, contents);

        Assert.Throws<JsonException>(() => store.Read());
    }

    /// <summary>
    /// And the case that must stay readable, because it is what the store
    /// itself writes when it holds nothing. Without this the rule above would
    /// be free to harden into one that refuses an empty store, which is a
    /// server that cannot mint its first invitation.
    /// </summary>
    [Fact]
    public void AnEmptyListReadsAsNoInvitations()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);

        store.Write(Array.Empty<Invitation>());

        Assert.Empty(store.Read().Invitations);
    }

    /// <summary>
    /// A document holding a member this store does not know about, beside a
    /// record it does. The unknown member is ignored and the invitation comes
    /// back, which is what the store does today rather than what it must do:
    /// whether a store written by a later version may be read by an earlier one
    /// at all is the version field in #42 and the migration rule in #93, and
    /// neither exists. This asserts the current answer so that changing it is a
    /// change somebody makes on purpose.
    /// </summary>
    [Fact]
    public void AnUnknownMemberBesideTheRecordsIsIgnored()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write(new[] { AnInvitation() });

        var written = File.ReadAllText(store.Path);
        File.WriteAllText(store.Path, "{\n  \"somethingLater\": 7," + written.Substring(written.IndexOf('{') + 1));

        Assert.Equal(AnInvitation(), Assert.Single(store.Read().Invitations));
    }

    /// <summary>
    /// One record in the document is not an invitation. The read raises rather
    /// than returning the ones that parsed, because a partial load is a load
    /// where some revocations are missing and nothing downstream can tell it
    /// from a complete one. The record chosen is one with no keyed hash, which
    /// is the state the record type refuses and the shape a half-written record
    /// has.
    /// </summary>
    [Fact]
    public void OneRecordThatIsNotAnInvitationRaisesRatherThanReturningTheOthers()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write(new[] { AnInvitation(), AnotherInvitation() });

        // The second record's keyed hash is three bytes chosen so that its
        // base64 is four plain letters, which the writer emits verbatim. A
        // value carrying a plus or a slash would be escaped on the way out and
        // this surgery would silently match nothing.
        var whole = File.ReadAllText(store.Path);
        Assert.Contains("\"qrvM\"", whole, StringComparison.Ordinal);
        File.WriteAllText(store.Path, whole.Replace("\"qrvM\"", "\"\"", StringComparison.Ordinal));

        Assert.Throws<ArgumentException>(() => store.Read());
    }

    /// <summary>
    /// The store sits in the plugin's own data directory. This is the location
    /// decision stated as something that runs: not the configuration file, not
    /// the server's database, and not a path picked out of the air.
    /// </summary>
    [Fact]
    public void TheStoreSitsInThePluginsOwnDataDirectory()
    {
        using var paths = new StubApplicationPaths();
        var plugin = new Plugin(paths, new StubXmlSerializer());

        var store = InvitationStore.For(plugin);

        Assert.Equal(plugin.DataFolderPath, Path.GetDirectoryName(store.Path));
        Assert.Equal(InvitationStore.FileName, Path.GetFileName(store.Path));
        Assert.NotEqual(paths.PluginConfigurationsPath, Path.GetDirectoryName(store.Path));
    }

    /// <summary>
    /// The comparison behind the report, on every platform the suite runs on.
    /// Reading a mode off a file needs a platform that has one; deciding
    /// whether a mode is wider than the one the store writes does not, and it
    /// is the half a mistake would live in.
    /// </summary>
    /// <param name="mode">A mode a store file might be found carrying.</param>
    /// <param name="wider">Whether it carries anything beyond the created one.</param>
    [Theory]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite, false)]
    [InlineData(UnixFileMode.UserRead, false)]
    [InlineData(UnixFileMode.None, false)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead, true)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead, true)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, true)]
    [InlineData(UnixFileMode.OtherWrite, true)]
    public void AModeIsWiderOrItIsNot(UnixFileMode mode, bool wider)
    {
        Assert.Equal(wider, InvitationStore.WiderThanCreated(mode));
    }

    /// <summary>
    /// The file is created for its owner and for nobody else. Every reader of
    /// it learns who was invited and by whom, and the stored keyed hashes are
    /// the input to an offline guessing attack for anybody who also holds the
    /// key.
    /// </summary>
    [UnixOnlyFact]
    [UnsupportedOSPlatform("windows")]
    public void TheFileIsCreatedForItsOwnerOnly()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);

        var reported = store.Write(new[] { AnInvitation() });

        Assert.Equal(InvitationStore.CreatedMode, File.GetUnixFileMode(store.Path));
        Assert.Equal(StorePermissionState.AsWritten, reported.State);
        Assert.False(reported.IsAProblem);
    }

    /// <summary>
    /// A store somebody widened afterwards is reported, and the report names
    /// the file and the mode found, because a report an operator cannot act on
    /// is one they will not act on.
    /// </summary>
    [UnixOnlyFact]
    [UnsupportedOSPlatform("windows")]
    public void AWiderModeIsReported()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write(new[] { AnInvitation() });

        File.SetUnixFileMode(
            store.Path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        var read = store.Read();

        Assert.Equal(StorePermissionState.WiderThanWritten, read.Permissions.State);
        Assert.True(read.Permissions.IsAProblem);
        Assert.Contains(store.Path, read.Permissions.Detail, StringComparison.Ordinal);
        Assert.Contains("OtherRead", read.Permissions.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// And it is not repaired. Tightening a mode silently changes something an
    /// operator may have set deliberately, so the store leaves it as it found
    /// it, through a read and through a write, and says so instead.
    /// </summary>
    [UnixOnlyFact]
    [UnsupportedOSPlatform("windows")]
    public void AWiderModeIsNotRepaired()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write(new[] { AnInvitation() });

        var widened = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
        File.SetUnixFileMode(store.Path, widened);

        store.Read();
        Assert.Equal(widened, File.GetUnixFileMode(store.Path));

        var reported = store.Write(new[] { AnInvitation() });
        Assert.Equal(widened, File.GetUnixFileMode(store.Path));
        Assert.Equal(StorePermissionState.WiderThanWritten, reported.State);
    }

    /// <summary>
    /// A widened store is still readable. Refusing to read it would turn a
    /// permissions nit into an outage of the redemption path, which is the
    /// failure an operator actually feels.
    /// </summary>
    [UnixOnlyFact]
    [UnsupportedOSPlatform("windows")]
    public void AWiderModeStillReadsTheInvitations()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        var written = AnInvitation();
        store.Write(new[] { written });

        File.SetUnixFileMode(store.Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);

        Assert.Equal(written, Assert.Single(store.Read().Invitations));
    }

    /// <summary>
    /// Where the platform has no file modes, the store says the mode was not
    /// read rather than reporting that it looked and found nothing wrong. This
    /// is the leg that keeps the disclosure honest on the machine where it
    /// applies.
    /// </summary>
    [WindowsOnlyFact]
    public void WithoutFileModesTheStoreSaysItDidNotLook()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);

        var reported = store.Write(new[] { AnInvitation() });

        Assert.Equal(StorePermissionState.NotCheckedOnThisPlatform, reported.State);
        Assert.False(reported.IsAProblem);
        Assert.Contains(store.Path, reported.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// The order the file lists invitations in is the order they come back in,
    /// so a store rewritten from what a read returned is the same file rather
    /// than a reshuffled one.
    /// </summary>
    [Fact]
    public void TheOrderSurvivesTheRoundTrip()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        var first = AnInvitation();
        var second = new Invitation(
            id: Guid.Parse("00000000-0000-4000-8000-000000000002"),
            codeHash: ImmutableArray.Create<byte>(0x09),
            mintedBy: Guid.Empty,
            mintedAt: DateTimeOffset.UnixEpoch,
            expiresAt: DateTimeOffset.UnixEpoch.AddDays(7),
            usesGranted: 1,
            usesRemaining: 0,
            revokedAt: null,
            templateLabel: "Guest",
            accountsProduced: ImmutableArray<Guid>.Empty);

        store.Write(new List<Invitation> { first, second });

        var read = store.Read().Invitations;

        Assert.Equal(new[] { first, second }, read.ToArray());
    }

    /// <summary>
    /// A write that cannot finish leaves the store exactly as it was. This is
    /// the property #40 asks for, stated as what is observable on disk rather
    /// than as which call was made to get there.
    /// </summary>
    /// <remarks>
    /// The failure is arranged rather than waited for. A directory is put where
    /// the unfinished file goes, so opening it raises and the write cannot even
    /// begin, and the assertion is that the previous records are still all
    /// there. A store that wrote into its own file would sail past this: the
    /// arrangement would stop nothing, the write would succeed, and the second
    /// invitation would be in the file. So both halves matter, and the throw is
    /// asserted as well as the contents.
    /// </remarks>
    [Fact]
    public void AWriteThatCannotFinishLeavesTheStoreAsItWas()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        var kept = AnInvitation();
        store.Write(new[] { kept });

        Directory.CreateDirectory(store.WritingPath);

        Assert.ThrowsAny<Exception>(() => store.Write(new[] { kept, AnotherInvitation() }));
        Assert.Equal(kept, Assert.Single(store.Read().Invitations));
    }

    /// <summary>
    /// The store the plugin reads is the only file a finished write leaves
    /// behind, so a write does not accumulate debris in a directory an operator
    /// looks at.
    /// </summary>
    [Fact]
    public void AFinishedWriteLeavesNothingBesideTheStore()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);

        store.Write(new[] { AnInvitation() });

        Assert.Equal(
            new[] { InvitationStore.FileName },
            Directory.GetFiles(directory.Path).Select(Path.GetFileName).ToArray());
        Assert.False(File.Exists(store.WritingPath));
    }

    /// <summary>
    /// A store left holding an unfinished file from a write that died is read
    /// as the records it committed. The unfinished file is not the store and is
    /// not treated as one, whatever is in it.
    /// </summary>
    [Fact]
    public void AnUnfinishedFileLeftBehindIsNotTheStore()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        var committed = AnInvitation();
        store.Write(new[] { committed });

        File.WriteAllText(store.WritingPath, "{ \"invitations\": [ this is not json");

        Assert.Equal(committed, Assert.Single(store.Read().Invitations));

        store.Write(new[] { committed });

        Assert.Equal(committed, Assert.Single(store.Read().Invitations));
        Assert.False(File.Exists(store.WritingPath));
    }

    /// <summary>
    /// Every write carries the shape the document is in, from the first one. A
    /// store written without a version is one a later reader has to guess
    /// about, and the usual guess is that a missing field means the default.
    /// </summary>
    [Fact]
    public void TheStoreCarriesItsVersionFromTheFirstWrite()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);

        store.Write(new[] { AnInvitation() });

        using var document = JsonDocument.Parse(File.ReadAllText(store.Path));

        Assert.Equal(InvitationStore.Version, document.RootElement.GetProperty("version").GetInt32());
    }

    /// <summary>
    /// A store newer than the plugin reading it is refused, and the message
    /// names both versions so an operator can tell which way round the problem
    /// is.
    /// </summary>
    [Fact]
    public void AStoreNewerThanThisBuildIsRefusedWithBothVersionsNamed()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write(new[] { AnInvitation() });
        var newer = File.ReadAllText(store.Path).Replace(
            "\"version\": 1",
            "\"version\": 2",
            StringComparison.Ordinal);
        File.WriteAllText(store.Path, newer);

        var refused = Assert.Throws<StoreVersionRefusedException>(() => store.Read());

        Assert.Equal(2, refused.Found);
        Assert.Equal(InvitationStore.Version, refused.Understood);
        Assert.Contains("version 2", refused.Message, StringComparison.Ordinal);
        Assert.Contains("version 1", refused.Message, StringComparison.Ordinal);
        Assert.Contains(store.Path, refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The refusal writes nothing. A plugin that will not load has to leave the
    /// file as it found it, or a downgrade somebody could undo by putting the
    /// newer plugin back becomes one they cannot.
    /// </summary>
    [Fact]
    public void ARefusedStoreIsLeftExactlyAsItWas()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write(new[] { AnInvitation() });
        var newer = File.ReadAllText(store.Path).Replace(
            "\"version\": 1",
            "\"version\": 2",
            StringComparison.Ordinal);
        File.WriteAllText(store.Path, newer);

        Assert.Throws<StoreVersionRefusedException>(() => store.Read());

        Assert.Equal(newer, File.ReadAllText(store.Path));
        Assert.Equal(
            new[] { InvitationStore.FileName },
            Directory.GetFiles(directory.Path).Select(Path.GetFileName).ToArray());
    }

    /// <summary>
    /// A document declaring a version this build knows is read, and one
    /// declaring an older one would be too. There is no older shape to build a
    /// fixture from, because nothing has been released, so what this covers is
    /// that the refusal is of a newer version rather than of any version that
    /// is not exactly this one.
    /// </summary>
    /// <param name="declared">The version the document declares.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AVersionThisBuildKnowsIsRead(int declared)
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        var written = AnInvitation();
        store.Write(new[] { written });
        File.WriteAllText(
            store.Path,
            File.ReadAllText(store.Path).Replace(
                "\"version\": 1",
                FormattableString.Invariant($"\"version\": {declared}"),
                StringComparison.Ordinal));

        Assert.Equal(written, Assert.Single(store.Read().Invitations));
    }

    /// <summary>
    /// A document with no version at all is read as the shape it certainly is.
    /// Exactly one has ever been written, because nothing has been released, so
    /// an absent version is not ambiguous yet. It becomes a migration the day a
    /// second shape exists, and this test is where that day is noticed.
    /// </summary>
    [Fact]
    public void ADocumentWrittenBeforeTheVersionExistedIsStillRead()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        var written = AnInvitation();
        store.Write(new[] { written });
        var withoutAVersion = File.ReadAllText(store.Path).Replace(
            "\"version\": 1,",
            string.Empty,
            StringComparison.Ordinal);
        File.WriteAllText(store.Path, withoutAVersion);

        Assert.DoesNotContain("version", withoutAVersion, StringComparison.Ordinal);
        Assert.Equal(written, Assert.Single(store.Read().Invitations));
    }
}
