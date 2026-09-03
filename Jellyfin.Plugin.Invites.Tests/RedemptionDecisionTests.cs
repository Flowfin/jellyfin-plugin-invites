using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A keyed hash a test owns, so the decision can be exercised before #30
/// decides where the real secret comes from.
/// </summary>
/// <remarks>
/// The key is a constant here on purpose. What the suite needs from this seam
/// is that the same code reduces to the same value every time, which is what
/// lets a test build a record from a code and then present that code. What the
/// key is worth, where it is generated and what rotating it costs are #30 and
/// nothing here answers them.
/// </remarks>
internal sealed class TestCodeHash : IInvitationCodeHash
{
    private static readonly byte[] _key = Encoding.UTF8.GetBytes("a key this suite owns and nothing ships");

    public ImmutableArray<byte> Of(string canonicalCode)
    {
        var digest = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(canonicalCode));

        return ImmutableArray.Create(digest);
    }
}

/// <summary>
/// The decision routine from #56, against the cases each of its branches
/// exists for.
/// </summary>
/// <remarks>
/// <para>
/// These are not the table. #102 is the table, and it is what asserts that
/// every reachable combination has a row and that the unreachable ones are
/// unreachable. What is here is one case per branch plus the properties the
/// routine is built to have, so the routine arrives with the evidence that it
/// does what it says rather than with a promise that a later issue will check.
/// </para>
/// <para>
/// Nothing here sleeps and nothing here reads the machine clock. The instant is
/// an argument, which is the whole reason the seam under #41 exists.
/// </para>
/// </remarks>
public class RedemptionDecisionTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _expires = new(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _wellInside = new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly IInvitationCodeHash _codeHash = new TestCodeHash();

    /// <summary>
    /// A code that canonicalises, so a test can present it and build the record
    /// that matches it. Written in the alphabet's own characters and at its own
    /// length, since anything else is not a code and would test the wrong
    /// branch.
    /// </summary>
    private const string PresentableCode = "23456789234567892345678923";

    /// <summary>
    /// A second code nobody minted a record for.
    /// </summary>
    private const string UnmintedCode = "98765432987654329876543298";

    private static Invitation ARecordFor(
        string code,
        int usesRemaining = 1,
        DateTimeOffset? revokedAt = null,
        DateTimeOffset? expiresAt = null)
    {
        var canonical = InvitationCode.Canonicalise(code);
        Assert.NotNull(canonical);

        return new Invitation(
            id: Guid.Parse("6f0a2d1e-6b3c-4f8a-9f1d-2a7c5e8b0d34"),
            codeHash: _codeHash.Of(canonical),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: _minted,
            expiresAt: expiresAt ?? _expires,
            usesGranted: 3,
            usesRemaining: usesRemaining,
            revokedAt: revokedAt,
            // The pair is whole or absent, which the record refuses to be
            // asked otherwise. Which operator revoked is nothing the decision
            // reads, so it is one value rather than a parameter.
            revokedBy: revokedAt is null ? null : Guid.Parse("44445555-6666-7777-8888-99990000aaaa"),
            templateLabel: "Household",
            template: TestTemplates.Household,
            accountsProduced: ImmutableArray<Guid>.Empty);
    }

    private static RedemptionVerdict Decide(
        string? presented,
        IReadOnlyList<Invitation> records,
        DateTimeOffset? at = null)
    {
        return RedemptionDecision.Decide(presented, _codeHash, records, at ?? _wellInside);
    }

    /// <summary>
    /// The case the whole plugin exists for. A code somebody minted, inside its
    /// validity, not revoked, with a use left.
    /// </summary>
    [Fact]
    public void AnInvitationWithTimeAndUsesLeftIsHonoured()
    {
        var record = ARecordFor(PresentableCode);

        var verdict = Decide(PresentableCode, new[] { record });

        Assert.Equal(RedemptionOutcome.Honoured, verdict.Outcome);
        Assert.True(verdict.MayCreateAnAccount);
        Assert.Same(record, verdict.Invitation);
    }

    /// <summary>
    /// Revocation is the operator's undo and it wins over everything else the
    /// record says.
    /// </summary>
    [Fact]
    public void ARevokedInvitationIsRefused()
    {
        var record = ARecordFor(PresentableCode, revokedAt: _minted);

        var verdict = Decide(PresentableCode, new[] { record });

        Assert.Equal(RedemptionOutcome.Revoked, verdict.Outcome);
        Assert.False(verdict.MayCreateAnAccount);
    }

    /// <summary>
    /// The expiry boundary is exclusive, which docs/expiry-rules.md decides:
    /// honoured strictly before the instant, refused at it. One tick separates
    /// the two and only an assertion at the exact instant tells them apart.
    /// </summary>
    /// <param name="ticksFromTheBoundary">Where the clock stands relative to the expiry.</param>
    /// <param name="expected">What the routine must answer there.</param>
    [Theory]
    [InlineData(-1, RedemptionOutcome.Honoured)]
    [InlineData(0, RedemptionOutcome.Expired)]
    [InlineData(1, RedemptionOutcome.Expired)]
    public void TheExpiryBoundaryIsExclusive(int ticksFromTheBoundary, RedemptionOutcome expected)
    {
        var record = ARecordFor(PresentableCode);

        var verdict = Decide(PresentableCode, new[] { record }, _expires.AddTicks(ticksFromTheBoundary));

        Assert.Equal(expected, verdict.Outcome);
    }

    /// <summary>
    /// A record with no uses left is spent, and the count comes from the record
    /// rather than from how many accounts it produced.
    /// </summary>
    [Fact]
    public void AnInvitationWithNoUsesLeftIsSpent()
    {
        var record = ARecordFor(PresentableCode, usesRemaining: 0);

        var verdict = Decide(PresentableCode, new[] { record });

        Assert.Equal(RedemptionOutcome.Spent, verdict.Outcome);
    }

    /// <summary>
    /// A record can be refused by more than one rule at once, and which reason
    /// the operator is given is decided rather than left to the order somebody
    /// happened to write the branches in. Revoked is the answer, because it is
    /// the one an operator did on purpose.
    /// </summary>
    [Fact]
    public void ARecordThatIsBothRevokedAndExpiredAndSpentReadsAsRevoked()
    {
        var record = ARecordFor(PresentableCode, usesRemaining: 0, revokedAt: _minted);

        var verdict = Decide(PresentableCode, new[] { record }, _expires.AddDays(1));

        Assert.Equal(RedemptionOutcome.Revoked, verdict.Outcome);
    }

    /// <summary>
    /// An expired record with a use left is expired rather than honoured, which
    /// is the ordering between the two refusals below revocation.
    /// </summary>
    [Fact]
    public void AnExpiredRecordWithUsesLeftIsExpiredRatherThanSpent()
    {
        var record = ARecordFor(PresentableCode, usesRemaining: 3);

        var verdict = Decide(PresentableCode, new[] { record }, _expires.AddDays(1));

        Assert.Equal(RedemptionOutcome.Expired, verdict.Outcome);
    }

    /// <summary>
    /// A code that reads correctly and matches nothing, and a string that is not
    /// a code at all, are one outcome carrying one shape. #28 asks that the four
    /// ways a redemption fails be indistinguishable, and the first place that
    /// can be lost is here, where a routine that reported "unreadable" would
    /// hand a caller an oracle for which codes exist.
    /// </summary>
    [Fact]
    public void RubbishAndAnUnmintedCodeAreTheSameVerdict()
    {
        var records = new[] { ARecordFor(PresentableCode) };

        var unminted = Decide(UnmintedCode, records);
        var rubbish = Decide("not a code at all", records);
        var nothing = Decide(null, records);
        var empty = Decide(string.Empty, records);

        Assert.Equal(RedemptionOutcome.NoSuchInvitation, unminted.Outcome);
        Assert.Equal(unminted.Outcome, rubbish.Outcome);
        Assert.Equal(unminted.Outcome, nothing.Outcome);
        Assert.Equal(unminted.Outcome, empty.Outcome);
        Assert.Null(unminted.Invitation);
        Assert.Null(rubbish.Invitation);
        Assert.Null(nothing.Invitation);
        Assert.Null(empty.Invitation);
    }

    /// <summary>
    /// The presented code goes through the one canonicalisation before it is
    /// matched, so a code typed back in the shape somebody read it out in still
    /// finds its record.
    /// </summary>
    /// <param name="presented">The same code, typed differently.</param>
    [Theory]
    [InlineData("2345-6789-2345-6789-2345-6789-23")]
    [InlineData("  23456789234567892345678923  ")]
    [InlineData("23456789234567892345678923")]
    public void ACodeIsMatchedThroughTheOneCanonicalisation(string presented)
    {
        var record = ARecordFor(PresentableCode);

        var verdict = Decide(presented, new[] { record });

        Assert.Equal(RedemptionOutcome.Honoured, verdict.Outcome);
    }

    /// <summary>
    /// Where a record sits in the list the caller read does not change the
    /// answer. The lookup compares every record and returns early from none,
    /// which is the shape #28 asks for and the shape a later change is most
    /// likely to optimise away.
    /// </summary>
    /// <param name="position">Where the matching record sits.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TheAnswerDoesNotDependOnWhereTheRecordSits(int position)
    {
        var wanted = ARecordFor(PresentableCode);
        var others = new List<Invitation>
        {
            ARecordFor(UnmintedCode),
            ARecordFor("34567892345678923456789234"),
        };
        others.Insert(position, wanted);

        var verdict = Decide(PresentableCode, others);

        Assert.Equal(RedemptionOutcome.Honoured, verdict.Outcome);
        Assert.Same(wanted, verdict.Invitation);
    }

    /// <summary>
    /// The routine decides and does nothing else. Two calls with the same
    /// arguments answer the same, and the records handed in come back
    /// unchanged, which is what makes the table in #102 a table of values
    /// rather than of arranged worlds.
    /// </summary>
    [Fact]
    public void DecidingTwiceAnswersTheSameAndChangesNothing()
    {
        var record = ARecordFor(PresentableCode);
        var before = ARecordFor(PresentableCode);
        var records = new[] { record };

        var first = Decide(PresentableCode, records);
        var second = Decide(PresentableCode, records);

        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Same(first.Invitation, second.Invitation);
        Assert.Equal(before, Assert.Single(records));
    }

    /// <summary>
    /// A record that matched is carried on a refusal as well as on a permission,
    /// because the operator's trail is about one invitation. A verdict with no
    /// record is exactly the one that matched none.
    /// </summary>
    [Fact]
    public void EveryVerdictThatMatchedARecordCarriesIt()
    {
        var record = ARecordFor(PresentableCode, usesRemaining: 0);

        var refused = Decide(PresentableCode, new[] { record });
        var unmatched = Decide(UnmintedCode, new[] { record });

        Assert.Same(record, refused.Invitation);
        Assert.Null(unmatched.Invitation);
    }

    /// <summary>
    /// Neither the hash nor the records may be absent. A caller that passed
    /// nothing would otherwise be told there is no such invitation, which is a
    /// refusal that reads exactly like a wrong code and would hide the fault.
    /// </summary>
    [Fact]
    public void TheRoutineRefusesArgumentsItCannotDecideWithout()
    {
        var records = new[] { ARecordFor(PresentableCode) };

        Assert.Throws<ArgumentNullException>(
            () => RedemptionDecision.Decide(PresentableCode, null!, records, _wellInside));
        Assert.Throws<ArgumentNullException>(
            () => RedemptionDecision.Decide(PresentableCode, _codeHash, null!, _wellInside));
    }

    /// <summary>
    /// An empty store is an ordinary case rather than an error, and it is the
    /// state of every fresh install.
    /// </summary>
    [Fact]
    public void NoRecordsAtAllIsNoSuchInvitation()
    {
        var verdict = Decide(PresentableCode, Array.Empty<Invitation>());

        Assert.Equal(RedemptionOutcome.NoSuchInvitation, verdict.Outcome);
    }

    /// <summary>
    /// The verdict's own factories refuse the states the routine never builds,
    /// so a later caller cannot produce a refusal that claims there is no such
    /// invitation while holding one.
    /// </summary>
    /// <param name="outcome">An outcome that is not a refusal against a record.</param>
    [Theory]
    [InlineData(RedemptionOutcome.Honoured)]
    [InlineData(RedemptionOutcome.NoSuchInvitation)]
    public void ARefusalCarryingARecordIsOneOfTheThree(RedemptionOutcome outcome)
    {
        var record = ARecordFor(PresentableCode);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => RedemptionVerdict.Refused(outcome, record));
    }
}
