using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Storage;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// What a load finds, against a real directory the test owns.
/// </summary>
/// <remarks>
/// The store, the claim and the comparison are all the real ones. A load is one
/// observation of one directory, so a stand-in for any of the three would prove
/// that the stand-in agreed with itself.
/// </remarks>
public class StoreLoadTests
{
    private static readonly DateTimeOffset _started = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _accountTheServerKept = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid _accountTheServerLost = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid _accountNoRecordClaims = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid _invitation = Guid.Parse("44444444-4444-4444-8444-444444444444");

    /// <summary>
    /// The restored-backup case, arriving through a load rather than through a
    /// comparison somebody called by hand: the store claims an account that is
    /// gone, and the server carries one no record has heard of.
    /// </summary>
    [Fact]
    public void ALoadReportsWhatTheStoreDisagreesWithInBothDirections()
    {
        using var directory = new OwnedDirectory();
        new InvitationStore(directory.Path)
            .Write([ARecordClaiming(_accountTheServerKept, _accountTheServerLost)]);

        using var load = StoreLoad.Of(
            directory.Path,
            "kitchen-server",
            4242,
            new TestClock(_started),
            [_accountTheServerKept, _accountNoRecordClaims]);

        Assert.True(load.HoldsTheStore);
        Assert.Null(load.Refusal);

        var report = Assert.IsType<ConsistencyReport>(load.Report);
        Assert.False(report.Agrees);
        Assert.Equal(_accountTheServerLost, Assert.Single(report.AccountsClaimedButAbsent).AccountId);
        Assert.Equal(_accountNoRecordClaims, Assert.Single(report.AccountsPresentButUnclaimed));
    }

    /// <summary>
    /// A directory with no store in it is a server that has never minted
    /// anything, and a load of it agrees with a server that has no accounts.
    /// </summary>
    [Fact]
    public void ALoadOfADirectoryWithNoStoreAgrees()
    {
        using var directory = new OwnedDirectory();

        using var load = StoreLoad.Of(directory.Path, "kitchen-server", 4242, new TestClock(_started), []);

        Assert.True(load.HoldsTheStore);
        Assert.True(Assert.IsType<ConsistencyReport>(load.Report).Agrees);
    }

    /// <summary>
    /// The load claims the directory, and the claim says who has it.
    /// </summary>
    [Fact]
    public void ALoadClaimsTheDirectoryForThisProcess()
    {
        using var directory = new OwnedDirectory();

        using var load = StoreLoad.Of(directory.Path, "kitchen-server", 4242, new TestClock(_started), []);

        Assert.True(load.HoldsTheStore);

        var claim = Path.Combine(directory.Path, StoreLock.FileName);
        Assert.True(File.Exists(claim));

        var written = File.ReadAllText(claim);
        Assert.Contains("kitchen-server", written, StringComparison.Ordinal);
        Assert.Contains("4242", written, StringComparison.Ordinal);
    }

    /// <summary>
    /// The shared-store case. A second server arriving at a directory the first
    /// one holds is refused, and it takes no report: a comparison read out of a
    /// directory somebody else is writing to describes a store that moved while
    /// it was being read.
    /// </summary>
    [Fact]
    public void ASecondLoadIsRefusedAndReadsNothing()
    {
        using var directory = new OwnedDirectory();
        new InvitationStore(directory.Path)
            .Write([ARecordClaiming(_accountTheServerKept)]);

        using var first = StoreLoad.Of(directory.Path, "kitchen-server", 4242, new TestClock(_started), []);
        using var second = StoreLoad.Of(directory.Path, "attic-server", 4343, new TestClock(_started), []);

        Assert.True(first.HoldsTheStore);

        Assert.False(second.HoldsTheStore);
        Assert.Null(second.Report);

        var refusal = Assert.IsType<StoreInUseException>(second.Refusal);
        Assert.Contains("kitchen-server", refusal.HeldBy, StringComparison.Ordinal);
        Assert.Equal(Path.Combine(directory.Path, StoreLock.FileName), refusal.Path);
    }

    /// <summary>
    /// A released load lets the next one in. Disposing is what the caller does
    /// when the server is stopping, and a claim that outlived the process that
    /// took it is a directory an operator has to clear by hand.
    /// </summary>
    [Fact]
    public void AReleasedLoadLetsTheNextOneClaimTheDirectory()
    {
        using var directory = new OwnedDirectory();

        var first = StoreLoad.Of(directory.Path, "kitchen-server", 4242, new TestClock(_started), []);
        Assert.True(first.HoldsTheStore);
        first.Dispose();

        using var second = StoreLoad.Of(directory.Path, "attic-server", 4343, new TestClock(_started), []);

        Assert.True(second.HoldsTheStore);
        Assert.Null(second.Refusal);
    }

    /// <summary>
    /// A store that cannot be read raises, and the claim does not survive it.
    /// The load never started using the directory, so leaving it claimed would
    /// hand the operator a second problem on top of the unreadable file.
    /// </summary>
    [Fact]
    public void AnUnreadableStoreRaisesAndReleasesTheClaim()
    {
        using var directory = new OwnedDirectory();
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(Path.Combine(directory.Path, InvitationStore.FileName), "this is not the document");

        Assert.Throws<JsonException>(() =>
            StoreLoad.Of(directory.Path, "kitchen-server", 4242, new TestClock(_started), []));

        Assert.False(File.Exists(Path.Combine(directory.Path, StoreLock.FileName)));
    }

    /// <summary>
    /// A load writes the claim and nothing else, and it removes nothing. The
    /// assertion is over the bytes in the directory rather than over which
    /// calls were made, because #46's constraint is about what a restored data
    /// directory looks like afterwards.
    /// </summary>
    [Fact]
    public void ALoadWritesTheClaimAndLeavesEverythingElseAsItFoundIt()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write([ARecordClaiming(_accountTheServerKept, _accountTheServerLost)]);

        var before = ContentsOf(directory.Path);

        using (StoreLoad.Of(directory.Path, "kitchen-server", 4242, new TestClock(_started), [_accountNoRecordClaims]))
        {
            var held = ContentsOf(directory.Path);
            Assert.Equal(
                before.Keys.Concat([StoreLock.FileName]).OrderBy(name => name, StringComparer.Ordinal).ToList(),
                held.Keys.OrderBy(name => name, StringComparer.Ordinal).ToList());

            foreach (var file in before.Keys)
            {
                Assert.Equal(before[file], held[file], StringComparer.Ordinal);
            }
        }

        Assert.Equal(before, ContentsOf(directory.Path));
    }

    /// <summary>
    /// The two arguments a load cannot do without.
    /// </summary>
    [Fact]
    public void ALoadRefusesAMissingClockOrAMissingAccountList()
    {
        using var directory = new OwnedDirectory();

        Assert.Throws<ArgumentNullException>(() =>
            StoreLoad.Of(directory.Path, "kitchen-server", 4242, null!, []));
        Assert.Throws<ArgumentNullException>(() =>
            StoreLoad.Of(directory.Path, "kitchen-server", 4242, new TestClock(_started), null!));
    }

    /// <summary>
    /// One invitation claiming the accounts it is handed.
    /// </summary>
    /// <param name="accounts">The accounts the record says it created.</param>
    /// <returns>The record.</returns>
    private static Invitation ARecordClaiming(params Guid[] accounts)
    {
        return new Invitation(
            id: _invitation,
            codeHash: ImmutableArray.Create<byte>(0x01, 0x02, 0x03),
            mintedBy: Guid.Parse("55555555-5555-4555-8555-555555555555"),
            mintedAt: new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            expiresAt: new DateTimeOffset(2026, 2, 2, 3, 4, 5, TimeSpan.Zero),
            usesGranted: 4,
            usesRemaining: 1,
            revokedAt: null,
            templateLabel: "Household",
            accountsProduced: [.. accounts]);
    }

    /// <summary>
    /// Every file in the directory, by name and by bytes.
    /// </summary>
    /// <param name="path">The directory to read.</param>
    /// <returns>The contents of each file, against its name.</returns>
    private static SortedDictionary<string, string> ContentsOf(string path)
    {
        var contents = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(path))
        {
            contents[Path.GetFileName(file)] = Convert.ToBase64String(File.ReadAllBytes(file));
        }

        return contents;
    }
}
