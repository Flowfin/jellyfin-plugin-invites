using System;
using System.IO;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Jellyfin.Plugin.Invites.Storage;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// An invitation that has passed its expiry is still in the store and still
/// answered for by the operations that read it.
/// </summary>
/// <remarks>
/// <para>
/// This is the entry `docs/limits.md` calls "Expiry is not the same as
/// deletion" held to the suite. That entry promises an operator two separate
/// things: that a presented code stops being honoured at the instant, and that
/// the record stays where it was so the invitation can still be seen and
/// accounted for. The first half was already held, by the boundary assertions
/// over the decision routine. The second half was held by nothing, and a
/// listing rewritten to hide what the clock has passed left the whole suite
/// green.
/// </para>
/// <para>
/// It is asserted through <see cref="InvitationOperations"/> rather than
/// through the store, because the store cannot lose the record without losing
/// the file: the way this promise gets broken is a reading routine that quietly
/// filters, and the store is not where a filter would be written.
/// </para>
/// <para>
/// Nothing here is about the retention sweep. Removing an expired record once
/// the retention rule allows it is <see cref="InvitationOperations.Sweep"/>
/// under #59, and a record removed by that rule is a record deliberately
/// deleted rather than one that vanished at its expiry. The instants below are
/// weeks apart rather than months, so nothing here is inside the retention
/// period by accident; what the sweep does is held in
/// <see cref="RetentionSweepTests"/>.
/// </para>
/// </remarks>
public class ExpiryIsNotDeletionTests
{
    /// <summary>
    /// The public address a link is written against, as an operator would set
    /// it. Nothing here derives it from a request, which is #50.
    /// </summary>
    private const string Configured = "https://media.example.org";

    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan _validity = TimeSpan.FromDays(7);
    private static readonly DateTimeOffset _wellPast = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid _operator = Guid.Parse("44445555-6666-7777-8888-99990000aaaa");

    /// <summary>
    /// The listing an operator reads still carries an invitation the clock has
    /// passed, and the code it was minted for is refused at the same instant.
    /// </summary>
    /// <remarks>
    /// The refusal is asserted through the decision routine rather than by
    /// comparing the record's expiry here. That routine is the one authority for
    /// whether an invitation may be honoured, an invariant refuses the
    /// comparison anywhere else, and a test making it a second time would be the
    /// second authority the rule exists against.
    /// </remarks>
    [Fact]
    public void AnInvitationPastItsExpiryIsStillListed()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var operations = new InvitationOperations(new StubStoreDirectory(directory.Path), clock, new StubPublicAddress(Configured));

        var minted = operations.Mint(_operator, "Household", _validity, uses: 1);
        Assert.Single(operations.All());

        clock.MoveTo(_wellPast);

        var listed = operations.All();
        Assert.Single(listed);
        Assert.Equal(minted.Invitation.Id, listed[0].Id);

        var hash = new InvitationCodeHash(HashSecret.OpenOrCreate(directory.Path, listed).Value);
        Assert.Equal(
            RedemptionOutcome.Expired,
            RedemptionDecision.Decide(minted.Code, hash, listed, clock.UtcNow).Outcome);
    }

    /// <summary>
    /// The identifier an operator has written down, or a log line has, still
    /// finds the record after the invitation has stopped working.
    /// </summary>
    [Fact]
    public void AnInvitationPastItsExpiryIsStillFoundByItsIdentifier()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var operations = new InvitationOperations(new StubStoreDirectory(directory.Path), clock, new StubPublicAddress(Configured));

        var minted = operations.Mint(_operator, "Household", _validity, uses: 1);

        clock.MoveTo(_wellPast);

        var found = operations.One(minted.Invitation.Id);
        Assert.NotNull(found);
        Assert.Equal(minted.Invitation.Id, found!.Id);
    }

    /// <summary>
    /// Passing the expiry writes nothing. The fact is a comparison made when a
    /// code is presented, so nothing marks the record and the bytes on the disk
    /// are the bytes the mint left.
    /// </summary>
    [Fact]
    public void CrossingTheExpiryChangesNothingOnTheDisk()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var operations = new InvitationOperations(new StubStoreDirectory(directory.Path), clock, new StubPublicAddress(Configured));

        var minted = operations.Mint(_operator, "Household", _validity, uses: 1);
        var afterMinting = File.ReadAllBytes(new InvitationStore(directory.Path).Path);

        clock.MoveTo(_wellPast);
        operations.All();
        operations.One(minted.Invitation.Id);

        Assert.Equal(afterMinting, File.ReadAllBytes(new InvitationStore(directory.Path).Path));
    }
}
