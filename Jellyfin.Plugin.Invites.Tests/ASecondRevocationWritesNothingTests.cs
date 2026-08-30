using System;
using System.IO;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Storage;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// Revoking an invitation that is already revoked writes nothing at all.
/// </summary>
/// <remarks>
/// <para>
/// #54 asks that revoking twice be a no-op with the first timestamp kept, and
/// that much was already held: the second call hands back the record it was
/// given, so the operator and the instant do not move. What was held by nothing
/// is the word "nothing". <see cref="InvitationOperations.Revoke"/> compares the
/// record it was handed against the one <see cref="Revocation"/> answered with
/// and returns early when they are the same reference, and removing that
/// comparison leaves a routine that writes the store again with byte-identical
/// contents. The verdict, the timestamp and the file are all unchanged by that
/// mutant, so every assertion in this suite stayed green on it.
/// </para>
/// <para>
/// It was found by the mutation run rather than by reading: the block removal at
/// that comparison is the survivor #376 carries as the one that could be killed
/// by a test that can see a write happen, against four beside it that no
/// assertion can separate at all.
/// </para>
/// <para>
/// <b>Why this needs no seam over the store.</b> #376 records the repair for this
/// survivor as a seam that counts writes, and the store has none. It does not
/// need one. A write here is built up in a file beside the store and moved over
/// it, which is #40's durability answer, so a directory standing where that file
/// goes makes a write impossible without changing anything a caller can reach.
/// <c>InvitationStoreTests.AWriteThatCannotFinishLeavesTheStoreAsItWas</c> proves
/// that the block works, on the store itself; what is new here is asking an
/// operation to run while it is in place. A revocation that writes fails loudly
/// and a revocation that writes nothing does not notice.
/// </para>
/// <para>
/// <b>What it does not say.</b> It reads one operation. Another routine writing
/// the store where it need not is invisible to it, and this is not a general
/// count of writes. It also says nothing about two processes over one store,
/// which is #96.
/// </para>
/// </remarks>
public class ASecondRevocationWritesNothingTests
{
    /// <summary>
    /// The public address a link is written against, as an operator would set
    /// it.
    /// </summary>
    private const string Configured = "https://media.example.org";

    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _later = new(2026, 5, 2, 9, 30, 0, TimeSpan.Zero);
    private static readonly TimeSpan _validity = TimeSpan.FromDays(7);

    private static readonly Guid _operator = Guid.Parse("11112222-3333-4444-5555-666677778888");
    private static readonly Guid _anotherOperator = Guid.Parse("99990000-1111-2222-3333-444455556666");

    /// <summary>
    /// A second revocation answers without the store being written, and the
    /// first operator and instant are what it answers with.
    /// </summary>
    /// <remarks>
    /// The block is put in place after the first revocation rather than before
    /// it, so the first one is an ordinary write that proves the store is
    /// reachable and the record is really revoked. Everything the second call
    /// needs it reads: <see cref="InvitationOperations"/> builds its store per
    /// call and a directory beside the store is not the store, which
    /// <c>InvitationStoreTests.AnUnfinishedFileLeftBehindIsNotTheStore</c>
    /// holds.
    /// </remarks>
    [Fact]
    public void RevokingAnAlreadyRevokedInvitationTouchesNoFile()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var operations = new InvitationOperations(
            new StubStoreDirectory(directory.Path),
            clock,
            new StubPublicAddress(Configured));

        var minted = operations.Mint(_operator, "Household", _validity, uses: 1);

        var first = operations.Revoke(minted.Invitation.Id, _operator);
        Assert.NotNull(first);
        Assert.True(first!.IsRevoked);

        var store = new InvitationStore(directory.Path);
        var afterTheFirst = File.ReadAllBytes(store.Path);

        // No write can begin while a directory stands where the unfinished file
        // goes, so an operation that writes throws here instead of succeeding
        // quietly.
        Directory.CreateDirectory(store.WritingPath);

        clock.MoveTo(_later);
        var second = operations.Revoke(minted.Invitation.Id, _anotherOperator);

        Assert.NotNull(second);
        Assert.Equal(first.RevokedAt, second!.RevokedAt);
        Assert.Equal(first.RevokedBy, second.RevokedBy);
        Assert.Equal(afterTheFirst, File.ReadAllBytes(store.Path));
    }

    /// <summary>
    /// The block this test rests on really does refuse a write, asked of the
    /// same operations object over the same directory.
    /// </summary>
    /// <remarks>
    /// Without this the test above passes on a machine where a directory at that
    /// path stops nothing, and it would read as a revocation that wrote nothing
    /// rather than as a block that did not hold. A mint is the cheapest write
    /// this type makes and it is refused.
    /// </remarks>
    [Fact]
    public void TheBlockThisRestsOnRefusesAWriteThatIsMeant()
    {
        using var directory = new OwnedDirectory();
        var operations = new InvitationOperations(
            new StubStoreDirectory(directory.Path),
            new TestClock(_minted),
            new StubPublicAddress(Configured));

        operations.Mint(_operator, "Household", _validity, uses: 1);

        Directory.CreateDirectory(new InvitationStore(directory.Path).WritingPath);

        Assert.ThrowsAny<Exception>(
            () => operations.Mint(_operator, "Household", _validity, uses: 1));
    }
}
