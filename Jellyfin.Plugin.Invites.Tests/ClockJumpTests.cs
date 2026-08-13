using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// What a redemption decides when the clock does not move forwards by one tick
/// at a time.
/// </summary>
/// <remarks>
/// <para>
/// #104 asks for the jump cases beside the three-point boundary tests, and it
/// gives the reason: the behaviours downstream of a clock reading are the ones
/// that disable accounts and delete records, and a wrong reading is exactly
/// what an injected clock can produce on demand and a real one cannot. The
/// boundary itself is asserted in <c>RedemptionDecisionTests</c> and by the
/// table under #102, and nothing here repeats it. These are movements rather
/// than positions.
/// </para>
/// <para>
/// One of the two directions is a fault this plugin does not repair, and the
/// tests below assert what docs/expiry-rules.md already says it costs rather
/// than a behaviour anybody would want. That page is the authority; if the
/// answer changes it changes there first and these go red, which is the way
/// round #104 asks for.
/// </para>
/// <para>
/// Three of the four clock-driven behaviours #104 names have no subject in the
/// tree. There is no rate-limit window, no retention sweep and no account
/// expiry, so expiry is the only one that can be jumped over, and each of the
/// others arrives with its own jump cases rather than being covered here in
/// advance.
/// </para>
/// <para>
/// Nothing here sleeps. Every instant is an argument.
/// </para>
/// </remarks>
public class ClockJumpTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _expires = new(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

    private static readonly IInvitationCodeHash _codeHash = new TestCodeHash();

    /// <summary>
    /// A code in the alphabet's own characters and at its own length, so it
    /// canonicalises and the decision reaches the comparison rather than
    /// stopping before it.
    /// </summary>
    private const string PresentableCode = "23456789234567892345678923";

    private static Invitation ARecordFor(
        string code,
        DateTimeOffset expiresAt,
        int usesRemaining = 1,
        DateTimeOffset? revokedAt = null)
    {
        var canonical = InvitationCode.Canonicalise(code);
        Assert.NotNull(canonical);

        return new Invitation(
            id: Guid.Parse("7c1b3e5a-9d2f-4a6b-8c0e-1f3a5b7d9e02"),
            codeHash: _codeHash.Of(canonical),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: _minted,
            expiresAt: expiresAt,
            usesGranted: 3,
            usesRemaining: usesRemaining,
            revokedAt: revokedAt,
            revokedBy: revokedAt is null ? null : Guid.Parse("44445555-6666-7777-8888-99990000aaaa"),
            templateLabel: "Household",
            accountsProduced: ImmutableArray<Guid>.Empty);
    }

    private static RedemptionOutcome DecideAt(IReadOnlyList<Invitation> records, DateTimeOffset at) =>
        RedemptionDecision.Decide(PresentableCode, _codeHash, records, at).Outcome;

    /// <summary>
    /// A clock that steps backwards across an expiry makes the invitation
    /// usable again, for as long as the reading stays behind it.
    /// </summary>
    /// <remarks>
    /// This is the cost docs/expiry-rules.md states under
    /// "A backwards clock jump is accepted, and this is what it costs", and it
    /// is asserted here so the sentence is measured rather than believed. The
    /// decision holds no memory of the readings it has seen, so it cannot tell
    /// a clock that went back from one that never went forward. Both handlings
    /// the page considers are argued there and both are worse than the fault.
    /// </remarks>
    [Fact]
    public void AClockSteppingBackwardsAcrossTheExpiryMakesTheInvitationUsableAgain()
    {
        var record = ARecordFor(PresentableCode, _expires);
        var pastIt = _expires.AddHours(1);

        Assert.Equal(RedemptionOutcome.Expired, DecideAt(new[] { record }, pastIt));

        // The same record, the same decision, a reading an hour before the one
        // above rather than after it.
        Assert.Equal(RedemptionOutcome.Honoured, DecideAt(new[] { record }, pastIt.AddHours(-2)));
    }

    /// <summary>
    /// A backwards jump moves expiry and nothing else. A revoked invitation and
    /// a spent one stay refused at every reading.
    /// </summary>
    /// <remarks>
    /// This is what bounds the disclosure above. Revocation is an instant on
    /// the record and the use count is a number on it, so neither is decided
    /// against the clock and neither can be undone by moving it. Without this
    /// the page's sentence reads as though a backwards jump revives whatever it
    /// reaches.
    /// </remarks>
    [Fact]
    public void AClockSteppingBackwardsRevivesNeitherARevokedNorASpentInvitation()
    {
        var revoked = ARecordFor(PresentableCode, _expires, revokedAt: _minted);
        var spent = ARecordFor(PresentableCode, _expires, usesRemaining: 0);

        var longBeforeAnythingHappened = _minted.AddYears(-1);

        Assert.Equal(RedemptionOutcome.Revoked, DecideAt(new[] { revoked }, longBeforeAnythingHappened));
        Assert.Equal(RedemptionOutcome.Spent, DecideAt(new[] { spent }, longBeforeAnythingHappened));
    }

    /// <summary>
    /// A jump forward far enough to cross several expiries at once refuses
    /// every one of them, including the invitation whose expiry is the reading
    /// itself.
    /// </summary>
    /// <remarks>
    /// The decision is handed one reading for the whole redemption, so a jump
    /// is not a sequence of steps it might miss one of. What this asserts is
    /// that nothing about the size of the movement changes the comparison: the
    /// record whose expiry the jump lands exactly on is refused, which is the
    /// exclusive boundary holding at a position no three-point test visits.
    /// </remarks>
    [Fact]
    public void AJumpPastSeveralExpiriesRefusesEveryOneOfThem()
    {
        var readingAfterTheJump = _expires.AddDays(30);

        var wellInsideTheJump = ARecordFor(PresentableCode, _expires);
        var justInsideTheJump = ARecordFor(PresentableCode, readingAfterTheJump.AddTicks(-1));
        var exactlyAtTheReading = ARecordFor(PresentableCode, readingAfterTheJump);
        var beyondTheJump = ARecordFor(PresentableCode, readingAfterTheJump.AddTicks(1));

        Assert.Equal(RedemptionOutcome.Expired, DecideAt(new[] { wellInsideTheJump }, readingAfterTheJump));
        Assert.Equal(RedemptionOutcome.Expired, DecideAt(new[] { justInsideTheJump }, readingAfterTheJump));
        Assert.Equal(RedemptionOutcome.Expired, DecideAt(new[] { exactlyAtTheReading }, readingAfterTheJump));
        Assert.Equal(RedemptionOutcome.Honoured, DecideAt(new[] { beyondTheJump }, readingAfterTheJump));
    }
}
