using System;
using System.Collections.Immutable;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// Which records the retention rule allows to be removed, and from which instant
/// it counts.
/// </summary>
/// <remarks>
/// <para>
/// #59 asks for a sweep that deletes only what retention allows and that never
/// marks anything expired. This file holds the first half at the level where the
/// judgement is made, so a sweep that removed the wrong record fails here rather
/// than in a test about a file on a disk.
/// </para>
/// <para>
/// The instants are chosen so the arithmetic is readable and never lands on a
/// boundary by accident. Everything is minted on 1 January and every assertion
/// says how many days after the expiry the clock is standing at.
/// </para>
/// </remarks>
public class RetentionTests
{
    private static readonly DateTimeOffset _minted = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _expires = new(2026, 1, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _operator = Guid.Parse("11112222-3333-4444-5555-666677778888");

    /// <summary>
    /// The number the rule is written against, kept here so an assertion reads as
    /// an argument about days rather than about a constant.
    /// </summary>
    private static readonly TimeSpan _period = Retention.RecordRetention;

    /// <summary>
    /// The period is the ninety days decision 8 in #11 chose, and this is where a
    /// change to it has to be made deliberately. A sweep that quietly started
    /// deleting after a week would pass every other assertion in this file,
    /// because all of them are written relative to the constant.
    /// </summary>
    [Fact]
    public void TheRetentionPeriodIsTheDecidedNinetyDays()
    {
        Assert.Equal(TimeSpan.FromDays(90), Retention.RecordRetention);
    }

    /// <summary>
    /// A record that is still live at the instant the sweep runs has no instant to
    /// count from, so there is no arithmetic that could remove it. This is #59's
    /// clause that the sweep never deletes an invitation with uses remaining that
    /// has not expired.
    /// </summary>
    /// <remarks>
    /// Liveness is a question about an instant rather than a property of the
    /// record, so it is asked at instants where the record is live: the moment it
    /// was minted, and the last tick before its expiry. A record judged after its
    /// expiry is not live any more and is the case the assertions below cover.
    /// </remarks>
    [Fact]
    public void ARecordThatIsStillLiveHasNothingToCountFrom()
    {
        var live = Record(usesRemaining: 1);

        Assert.Null(RedemptionDecision.RetentionStartsAt(live, _minted));
        Assert.False(Retention.MayBeRemoved(live, _minted));

        Assert.Null(RedemptionDecision.RetentionStartsAt(live, _expires - TimeSpan.FromTicks(1)));
        Assert.False(Retention.MayBeRemoved(live, _expires - TimeSpan.FromTicks(1)));
    }

    /// <summary>
    /// A record whose validity runs longer than the retention period is not
    /// removable while it is still live, which is the case where an arithmetic
    /// slip would bite: ninety days of validity and ninety days of retention are
    /// the same number, so a routine counting from the minting rather than from
    /// the expiry would delete a working invitation on the day it was still
    /// usable.
    /// </summary>
    [Fact]
    public void AValidityAsLongAsTheRetentionPeriodDoesNotMakeALiveRecordRemovable()
    {
        var longLived = new Invitation(
            id: Guid.NewGuid(),
            codeHash: ImmutableArray.Create(new byte[32]),
            mintedBy: _operator,
            mintedAt: _minted,
            expiresAt: _minted + _period,
            usesGranted: 1,
            usesRemaining: 1,
            revokedAt: null,
            revokedBy: null,
            templateLabel: "Household",
            template: TestTemplates.Household,
            accountsProduced: ImmutableArray<Guid>.Empty);

        Assert.False(Retention.MayBeRemoved(longLived, _minted + _period - TimeSpan.FromTicks(1)));
        Assert.True(Retention.MayBeRemoved(longLived, _minted + _period + _period));
    }

    /// <summary>
    /// An expired record counts from its expiry, and the boundary is where it is
    /// said to be. One tick before the period is up the record stays.
    /// </summary>
    [Fact]
    public void AnExpiredRecordCountsFromItsExpiry()
    {
        var expired = Record(usesRemaining: 1);

        Assert.Equal(_expires, RedemptionDecision.RetentionStartsAt(expired, _expires));

        Assert.False(Retention.MayBeRemoved(expired, _expires));
        Assert.False(Retention.MayBeRemoved(expired, _expires + _period - TimeSpan.FromTicks(1)));
        Assert.True(Retention.MayBeRemoved(expired, _expires + _period));
        Assert.True(Retention.MayBeRemoved(expired, _expires + _period + TimeSpan.FromDays(1)));
    }

    /// <summary>
    /// A record revoked before it would have expired counts from the revocation,
    /// which is the earlier of the two instants it carries. Counting from the
    /// expiry instead would keep it for the difference between them on top of the
    /// period, which is a record kept longer than the rule allows for a reason
    /// nobody chose.
    /// </summary>
    [Fact]
    public void ARevocationBeforeTheExpiryIsWhatCounts()
    {
        var revokedAt = _minted.AddDays(2);
        var revoked = Record(usesRemaining: 1, revokedAt: revokedAt);

        Assert.Equal(revokedAt, RedemptionDecision.RetentionStartsAt(revoked, revokedAt));

        Assert.False(Retention.MayBeRemoved(revoked, revokedAt + _period - TimeSpan.FromTicks(1)));
        Assert.True(Retention.MayBeRemoved(revoked, revokedAt + _period));
    }

    /// <summary>
    /// A record revoked after it had already expired counts from the expiry. It
    /// stopped being usable when the clock passed the expiry; the revocation
    /// afterwards changed what an operator sees and not when the record stopped
    /// working.
    /// </summary>
    [Fact]
    public void ARevocationAfterTheExpiryDoesNotRestartThePeriod()
    {
        var revokedAt = _expires.AddDays(30);
        var revoked = Record(usesRemaining: 1, revokedAt: revokedAt);

        Assert.Equal(_expires, RedemptionDecision.RetentionStartsAt(revoked, revokedAt));

        Assert.True(Retention.MayBeRemoved(revoked, _expires + _period));
    }

    /// <summary>
    /// A spent record that has not expired is kept until its expiry has been past
    /// for the period, because nothing on the record says when it was spent.
    /// </summary>
    /// <remarks>
    /// This asserts the disclosed gap rather than a behaviour anybody wanted:
    /// <see cref="Invitation.UsesRemaining"/> reaching zero is not timestamped, so
    /// the earliest instant the record itself can be read as having stopped being
    /// usable is its expiry. The record is therefore kept longer than the rule
    /// requires and never removed sooner, which is the safe direction. A
    /// spent-at instant is #52's field to add, and the day it exists this
    /// assertion is the one that has to change.
    /// </remarks>
    [Fact]
    public void ASpentRecordIsHeldToItsExpiryBecauseNothingSaysWhenItWasSpent()
    {
        var spent = Record(usesRemaining: 0);

        Assert.Equal(_expires, RedemptionDecision.RetentionStartsAt(spent, _minted));

        Assert.False(Retention.MayBeRemoved(spent, _minted + _period));
        Assert.True(Retention.MayBeRemoved(spent, _expires + _period));
    }

    /// <summary>
    /// The judgement is the decision routine's and this asserts it, so an
    /// implementation that compared the record's own fields beside the sweep
    /// would be caught here as well as by the invariant lint. Moving the clock
    /// across the expiry moves the answer with nothing about the record changing.
    /// </summary>
    [Fact]
    public void TheAnswerFollowsTheDecisionRoutineAcrossTheExpiry()
    {
        var record = Record(usesRemaining: 1);

        Assert.True(RedemptionDecision.IsLive(record, _expires - TimeSpan.FromTicks(1)));
        Assert.Null(RedemptionDecision.RetentionStartsAt(record, _expires - TimeSpan.FromTicks(1)));

        Assert.False(RedemptionDecision.IsLive(record, _expires));
        Assert.NotNull(RedemptionDecision.RetentionStartsAt(record, _expires));
    }

    private static Invitation Record(int usesRemaining, DateTimeOffset? revokedAt = null)
    {
        return new Invitation(
            id: Guid.NewGuid(),
            codeHash: ImmutableArray.Create(new byte[32]),
            mintedBy: _operator,
            mintedAt: _minted,
            expiresAt: _expires,
            usesGranted: 1,
            usesRemaining: usesRemaining,
            revokedAt: revokedAt,
            revokedBy: revokedAt is null ? null : _operator,
            templateLabel: "Household",
            template: TestTemplates.Household,
            accountsProduced: ImmutableArray<Guid>.Empty);
    }
}
