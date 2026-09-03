using System;
using System.Collections.Immutable;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// An expiry is a moment rather than a reading on a wall clock, which is the
/// entry docs/limits.md carries under the server's timezone not changing when an
/// invitation expires.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two offsets, one moment.</b> Every record in the decision suite spells its
/// instants at offset zero, and the clock reading handed to the decision is at
/// offset zero too, so a comparison of the two wall-clock readings and a
/// comparison of the two moments give the same answer over all of it. This
/// spells one moment two ways, which is the smallest thing that tells those two
/// comparisons apart.
/// </para>
/// <para>
/// <b>What is not here.</b> The store handing back the moment it was given is
/// asserted by <c>InvitationStoreTests.WhatIsWrittenComesBack</c>, over a record
/// written and read through the real store, and a second assertion of it here
/// would be one property with two owners. The half that had nothing asserting it
/// is the comparison, and that is what this file is.
/// </para>
/// <para>
/// <b>No test reads the machine's own offset.</b> An assertion that depended on
/// it would pass or fail by where the suite ran, and the property does not.
/// </para>
/// </remarks>
public class StoredInstantTests
{
    /// <summary>
    /// One moment: the eighth of May 2026, ten in the morning, UTC.
    /// </summary>
    private static readonly DateTimeOffset _theMoment = new(2026, 5, 8, 10, 0, 0, TimeSpan.Zero);

    private static readonly IInvitationCodeHash _codeHash = new TestCodeHash();

    /// <summary>
    /// The offsets one moment is spelled in. Thirteen hours ahead of UTC is an
    /// offset a server can be set to rather than a number chosen to be extreme,
    /// and zero is what every other record in this suite uses.
    /// </summary>
    /// <returns>Two spellings of one moment.</returns>
    public static TheoryData<int> TheOffsets() => new() { 0, 13 };

    /// <summary>
    /// The decision judges both spellings alike, on both sides of the boundary.
    /// A comparison written against the wall clock rather than the moment
    /// honours one of the two and refuses the other at an instant where they are
    /// the same invitation, which is the entry in docs/limits.md failing rather
    /// than an inconsistency in a format.
    /// </summary>
    /// <param name="offsetHours">The offset this record spells its expiry in.</param>
    [Theory]
    [MemberData(nameof(TheOffsets))]
    public void TheDecisionJudgesTwoSpellingsOfOneMomentAlike(int offsetHours)
    {
        var code = InvitationCode.Mint();
        var records = new[] { AnInvitationExpiring(_theMoment.ToOffset(TimeSpan.FromHours(offsetHours)), code) };

        var before = RedemptionDecision.Decide(code, _codeHash, records, _theMoment - TimeSpan.FromTicks(1));
        var at = RedemptionDecision.Decide(code, _codeHash, records, _theMoment);

        Assert.Equal(RedemptionOutcome.Honoured, before.Outcome);
        Assert.Equal(RedemptionOutcome.Expired, at.Outcome);
    }

    /// <summary>
    /// And the clock reading is the other side of the same comparison, so it is
    /// spelled two ways too. The instant handed to the decision is one moment
    /// whichever offset the caller's clock reports it in.
    /// </summary>
    /// <param name="offsetHours">The offset the clock reading arrives in.</param>
    [Theory]
    [MemberData(nameof(TheOffsets))]
    public void TheDecisionReadsTwoSpellingsOfOneClockReadingAlike(int offsetHours)
    {
        var code = InvitationCode.Mint();
        var records = new[] { AnInvitationExpiring(_theMoment, code) };
        var reading = _theMoment.ToOffset(TimeSpan.FromHours(offsetHours));

        var before = RedemptionDecision.Decide(code, _codeHash, records, reading - TimeSpan.FromTicks(1));
        var at = RedemptionDecision.Decide(code, _codeHash, records, reading);

        Assert.Equal(RedemptionOutcome.Honoured, before.Outcome);
        Assert.Equal(RedemptionOutcome.Expired, at.Outcome);
    }

    private static Invitation AnInvitationExpiring(DateTimeOffset expiresAt, string code)
    {
        return new Invitation(
            id: Guid.Parse("6f0a2d1e-6b3c-4f8a-9f1d-2a7c5e8b0d34"),
            codeHash: _codeHash.Of(InvitationCode.Canonicalise(code)!),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: expiresAt - TimeSpan.FromDays(7),
            expiresAt: expiresAt,
            usesGranted: 1,
            usesRemaining: 1,
            revokedAt: null,
            revokedBy: null,
            templateLabel: "Household",
            template: TestTemplates.Household,
            accountsProduced: ImmutableArray<Guid>.Empty);
    }
}
