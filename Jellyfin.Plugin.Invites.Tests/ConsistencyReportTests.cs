using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Storage;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The report a load produces about the accounts the store claims, against a
/// real store file in a directory the test owns.
/// </summary>
/// <remarks>
/// The store is the real one rather than a stand-in. The case this report exists
/// for is a restored data directory, which is a fact about a file, and a fake
/// store would prove that the fake holds what it was handed.
/// </remarks>
public class ConsistencyReportTests
{
    private static readonly Guid _accountTheServerKept = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid _accountTheServerLost = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid _accountNoRecordClaims = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid _invitation = Guid.Parse("44444444-4444-4444-8444-444444444444");

    /// <summary>
    /// The case this issue is written for, in one test: a store restored from
    /// before some work, and a server that has moved on. It claims an account
    /// that is gone and it has never heard of an account that is there.
    /// </summary>
    [Fact]
    public void AStoreThatDisagreesInBothDirectionsIsReportedInBoth()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write([ARecordClaiming(_accountTheServerKept, _accountTheServerLost)]);

        var report = ConsistencyReport.OfALoad(store, [_accountTheServerKept, _accountNoRecordClaims]);

        Assert.False(report.Agrees);

        var claimedButAbsent = Assert.Single(report.AccountsClaimedButAbsent);
        Assert.Equal(_accountTheServerLost, claimedButAbsent.AccountId);
        Assert.Equal(_invitation, claimedButAbsent.InvitationId);

        Assert.Equal([_accountNoRecordClaims], report.AccountsPresentButUnclaimed.ToArray());
    }

    /// <summary>
    /// The report is a reading and never a repair. What is asserted is what a
    /// person would check after running it: the data directory holds the same
    /// files, byte for byte, as it did before.
    /// </summary>
    /// <remarks>
    /// This is the clause asking that the report never delete or create anything
    /// on its own, and it is asserted against the filesystem rather than against
    /// which methods were called. A repair written into the report, or a write
    /// back of a tidied record set, reds this without anybody having to guess in
    /// advance what shape it would take.
    /// </remarks>
    [Fact]
    public void TheReportLeavesTheDataDirectoryExactlyAsItFoundIt()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write([ARecordClaiming(_accountTheServerKept, _accountTheServerLost)]);

        var before = ContentsOf(directory.Path);

        var report = ConsistencyReport.OfALoad(store, [_accountNoRecordClaims]);

        Assert.False(report.Agrees);
        Assert.Equal(before, ContentsOf(directory.Path));
    }

    /// <summary>
    /// A store whose claims are all met, held against exactly those accounts,
    /// reports nothing in either direction.
    /// </summary>
    [Fact]
    public void AStoreTheServerAgreesWithReportsNothing()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write([ARecordClaiming(_accountTheServerKept, _accountTheServerLost)]);

        var report = ConsistencyReport.OfALoad(store, [_accountTheServerLost, _accountTheServerKept]);

        Assert.True(report.Agrees);
        Assert.Empty(report.AccountsClaimedButAbsent);
        Assert.Empty(report.AccountsPresentButUnclaimed);
    }

    /// <summary>
    /// A data directory with no store file in it is a server that has never
    /// minted anything, so every account handed in is one no invitation claims.
    /// It is not an error and it is not silence either.
    /// </summary>
    [Fact]
    public void AStoreThatIsNotThereClaimsNothing()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);

        var report = ConsistencyReport.OfALoad(store, [_accountTheServerKept]);

        Assert.False(File.Exists(store.Path));
        Assert.Empty(report.AccountsClaimedButAbsent);
        Assert.Equal([_accountTheServerKept], report.AccountsPresentButUnclaimed.ToArray());
    }

    /// <summary>
    /// Two loads over the same two sets read the same way round, whatever order
    /// the records and the accounts arrive in.
    /// </summary>
    /// <remarks>
    /// An operator comparing today's report with yesterday's is comparing two
    /// lists by eye, and lines that move about between loads make that
    /// comparison worthless.
    /// </remarks>
    [Fact]
    public void TheTwoListsReadTheSameWayRoundWhateverOrderTheyArriveIn()
    {
        var first = ConsistencyReport.Of(
            [ARecordClaiming(_accountTheServerLost, _accountNoRecordClaims)],
            [_accountTheServerKept, _invitation]);

        var second = ConsistencyReport.Of(
            [ARecordClaiming(_accountNoRecordClaims, _accountTheServerLost)],
            [_invitation, _accountTheServerKept]);

        Assert.Equal(
            first.AccountsClaimedButAbsent.Select(claim => claim.AccountId),
            second.AccountsClaimedButAbsent.Select(claim => claim.AccountId));
        Assert.Equal(first.AccountsPresentButUnclaimed.ToArray(), second.AccountsPresentButUnclaimed.ToArray());
    }

    /// <summary>
    /// A record naming one account twice is reported twice, because that record
    /// is itself something an operator would want to look at.
    /// </summary>
    [Fact]
    public void ARecordThatNamesOneAccountTwiceIsNotTidiedAway()
    {
        var report = ConsistencyReport.Of(
            [ARecordClaiming(_accountTheServerLost, _accountTheServerLost)],
            []);

        Assert.Equal(2, report.AccountsClaimedButAbsent.Length);
        Assert.All(report.AccountsClaimedButAbsent, claim => Assert.Equal(_accountTheServerLost, claim.AccountId));
    }

    /// <summary>
    /// The sentence a caller would show carries counts and no identifiers, so
    /// putting it somewhere it will be copied does not copy who was invited.
    /// </summary>
    [Fact]
    public void TheSentenceCarriesCountsAndNoIdentifiers()
    {
        var report = ConsistencyReport.Of(
            [ARecordClaiming(_accountTheServerLost)],
            [_accountNoRecordClaims]);

        Assert.DoesNotContain(_accountTheServerLost.ToString(), report.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_accountNoRecordClaims.ToString(), report.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_invitation.ToString(), report.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1 account(s)", report.Summary, StringComparison.Ordinal);
    }

    /// <summary>
    /// Neither side may be left out. A missing list is nobody having decided
    /// which accounts to compare against, and answering that with a report is
    /// answering a question nobody asked.
    /// </summary>
    [Fact]
    public void NeitherSideOfTheComparisonMayBeMissing()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);

        Assert.Throws<ArgumentNullException>(() => ConsistencyReport.OfALoad(null!, []));
        Assert.Throws<ArgumentNullException>(() => ConsistencyReport.OfALoad(store, null!));
        Assert.Throws<ArgumentNullException>(() => ConsistencyReport.Of(null!, []));
        Assert.Throws<ArgumentNullException>(() => ConsistencyReport.Of([], null!));
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
    /// <returns>The name and contents of each file, in a fixed order.</returns>
    private static IReadOnlyList<string> ContentsOf(string path)
    {
        return Directory
            .GetFiles(path)
            .OrderBy(file => file, StringComparer.Ordinal)
            .Select(file => Path.GetFileName(file) + ":" + Convert.ToBase64String(File.ReadAllBytes(file)))
            .ToList();
    }
}
