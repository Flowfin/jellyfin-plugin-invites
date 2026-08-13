using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// Where the presented code stands against the store.
/// </summary>
public enum CodeStanding
{
    /// <summary>Somebody minted a record for it.</summary>
    Minted,

    /// <summary>It reads as a code and no record matches it.</summary>
    Unminted,

    /// <summary>What was presented is not a code at all.</summary>
    NotACode,
}

/// <summary>
/// Where the clock stands against the record's expiry.
/// </summary>
public enum ExpiryStanding
{
    /// <summary>One tick before the instant.</summary>
    Before,

    /// <summary>The instant itself.</summary>
    AtTheInstant,

    /// <summary>One tick after the instant.</summary>
    After,
}

/// <summary>
/// One combination of everything the decision reads, with the verdict it must
/// produce.
/// </summary>
/// <param name="Code">Where the presented code stands.</param>
/// <param name="Expiry">Where the clock stands against the expiry.</param>
/// <param name="Revoked">Whether the record was revoked.</param>
/// <param name="UsesLeft">How many uses the record has left.</param>
/// <param name="Expected">The verdict, written out rather than derived.</param>
public sealed record DecisionRow(
    CodeStanding Code,
    ExpiryStanding Expiry,
    bool Revoked,
    int UsesLeft,
    RedemptionOutcome Expected);

/// <summary>
/// The redemption decision as a table, which is #102.
/// </summary>
/// <remarks>
/// <para>
/// The decision is a pure function over a small input space, so it is covered
/// exhaustively rather than sampled. Every reachable combination of the four
/// dimensions the routine reads has a row below, and each row carries its
/// expected verdict written out by hand. Deriving the expectation from a
/// formula would make the table a second implementation of the routine, which
/// agrees with it by construction and therefore proves nothing.
/// </para>
/// <para>
/// The dimensions are the ones #102 names: whether the code matches a record,
/// where the clock stands against the expiry including the instant itself,
/// whether the record is revoked, and whether it has no uses, one, or more than
/// one. The fifth dimension that issue names, each global ceiling under, at and
/// over, is absent because the ceilings are #33 and nothing in the tree says
/// what they are. When they land they are one more dimension here and the row
/// count multiplies rather than the table changing shape.
/// </para>
/// <para>
/// This is the artefact the mutation run under #22 is measured against. A
/// mutant surviving in the routine means a row is missing here or an
/// expectation is looser than the rule it stands for.
/// </para>
/// </remarks>
public class RedemptionDecisionTableTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _expires = new(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The operator a revoked row was revoked by. The decision reads neither
    /// half of a revocation beyond whether there is one, so this is here to
    /// keep the record buildable rather than to be asserted against.
    /// </summary>
    private static readonly Guid _revoker = Guid.Parse("44445555-6666-7777-8888-99990000aaaa");

    private static readonly IInvitationCodeHash _codeHash = new TestCodeHash();

    private const string MintedCode = "23456789234567892345678923";
    private const string UnmintedCode = "98765432987654329876543298";
    private const string NotACode = "this is not a code";

    private const int UsesGranted = 3;

    /// <summary>
    /// Every reachable combination, one row each.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Twenty rows. Eighteen are a matched record, which is three positions of
    /// the clock times revoked or not times three counts. Two are the ways a
    /// presented code matches nothing, and they collapse the other three
    /// dimensions rather than multiplying by them: with no record there is no
    /// expiry, no revocation and no count for a row to vary, and pretending
    /// otherwise would be eighteen copies of one case.
    /// </para>
    /// <para>
    /// Read down the Expected column and the three rules are visible as blocks.
    /// A revoked record is refused whatever else is true of it. An unrevoked
    /// record at or past its expiry is expired whatever its count. Only an
    /// unrevoked record strictly before its expiry gets as far as the count.
    /// </para>
    /// </remarks>
    /// <returns>The rows, one per xUnit case.</returns>
    public static TheoryData<DecisionRow> Reachable()
    {
        var rows = new TheoryData<DecisionRow>();

        foreach (var row in Rows())
        {
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// The same rows as a plain list, for the assertions that read the table
    /// itself rather than run one row of it.
    /// </summary>
    /// <returns>The rows.</returns>
    private static List<DecisionRow> Rows()
    {
        var rows = new List<DecisionRow>();

        foreach (var expiry in new[] { ExpiryStanding.Before, ExpiryStanding.AtTheInstant, ExpiryStanding.After })
        {
            foreach (var uses in new[] { 0, 1, 2 })
            {
                // Revoked is answered before anything else, so all nine of these
                // are the same verdict for the same reason.
                rows.Add(new DecisionRow(CodeStanding.Minted, expiry, true, uses, RedemptionOutcome.Revoked));
            }
        }

        // Not revoked, strictly before the expiry: the count decides.
        rows.Add(new DecisionRow(CodeStanding.Minted, ExpiryStanding.Before, false, 0, RedemptionOutcome.Spent));
        rows.Add(new DecisionRow(CodeStanding.Minted, ExpiryStanding.Before, false, 1, RedemptionOutcome.Honoured));
        rows.Add(new DecisionRow(CodeStanding.Minted, ExpiryStanding.Before, false, 2, RedemptionOutcome.Honoured));

        // Not revoked, at the instant of the expiry. The boundary is exclusive,
        // so this is the same as past it and not the same as before it. It is
        // the row an implementation gets right by accident and a later change
        // gets wrong in silence.
        rows.Add(new DecisionRow(CodeStanding.Minted, ExpiryStanding.AtTheInstant, false, 0, RedemptionOutcome.Expired));
        rows.Add(new DecisionRow(CodeStanding.Minted, ExpiryStanding.AtTheInstant, false, 1, RedemptionOutcome.Expired));
        rows.Add(new DecisionRow(CodeStanding.Minted, ExpiryStanding.AtTheInstant, false, 2, RedemptionOutcome.Expired));

        // Not revoked, past the expiry. Expired wins over the count, so a record
        // with uses left and a record with none read the same.
        rows.Add(new DecisionRow(CodeStanding.Minted, ExpiryStanding.After, false, 0, RedemptionOutcome.Expired));
        rows.Add(new DecisionRow(CodeStanding.Minted, ExpiryStanding.After, false, 1, RedemptionOutcome.Expired));
        rows.Add(new DecisionRow(CodeStanding.Minted, ExpiryStanding.After, false, 2, RedemptionOutcome.Expired));

        // The two ways nothing matches. They are separate rows because they
        // reach the routine by different branches, and they carry the same
        // verdict because #28 requires a caller to be unable to tell them apart.
        rows.Add(new DecisionRow(CodeStanding.Unminted, ExpiryStanding.Before, false, 1, RedemptionOutcome.NoSuchInvitation));
        rows.Add(new DecisionRow(CodeStanding.NotACode, ExpiryStanding.Before, false, 1, RedemptionOutcome.NoSuchInvitation));

        return rows;
    }

    /// <summary>
    /// Every row, decided.
    /// </summary>
    /// <param name="row">The combination and what it must answer.</param>
    [Theory]
    [MemberData(nameof(Reachable))]
    public void EveryReachableCombinationAnswersItsRow(DecisionRow row)
    {
        var verdict = Decide(row);

        Assert.Equal(row.Expected, verdict.Outcome);
    }

    /// <summary>
    /// A verdict carries the record it matched, on a refusal as much as on a
    /// permission, and carries none exactly when it matched none. Asserted over
    /// the whole table rather than in one place, because it is the property a
    /// caller reading the operator's trail depends on for every row.
    /// </summary>
    /// <param name="row">The combination and what it must answer.</param>
    [Theory]
    [MemberData(nameof(Reachable))]
    public void AVerdictCarriesARecordExactlyWhenItMatchedOne(DecisionRow row)
    {
        var verdict = Decide(row);

        if (row.Expected == RedemptionOutcome.NoSuchInvitation)
        {
            Assert.Null(verdict.Invitation);
        }
        else
        {
            Assert.NotNull(verdict.Invitation);
        }
    }

    /// <summary>
    /// Only one of the five outcomes creates an account, and no row may quietly
    /// grow a second one.
    /// </summary>
    /// <param name="row">The combination and what it must answer.</param>
    [Theory]
    [MemberData(nameof(Reachable))]
    public void OnlyAnHonouredRowMayCreateAnAccount(DecisionRow row)
    {
        var verdict = Decide(row);

        Assert.Equal(row.Expected == RedemptionOutcome.Honoured, verdict.MayCreateAnAccount);
    }

    /// <summary>
    /// The table reaches every branch of the routine, which is the last clause
    /// of #102 and the reason the table is worth having at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The routine has one branch per refusal plus the permission, and the
    /// outcomes are one for one with them: the canonicalisation that fails, the
    /// lookup that finds nothing, the revocation, the expiry, the count, and the
    /// fall-through. The first two share an outcome, so counting outcomes alone
    /// would report five branches reached where six were, and the two rows that
    /// reach them separately are asserted below by their inputs rather than by
    /// their verdicts.
    /// </para>
    /// <para>
    /// What this does not do is measure coverage. No coverage tool runs in this
    /// repository yet, that being #108, so this is a structural assertion over
    /// the table and not a report from an instrumented run. It would not notice
    /// a branch added to the routine that produces an outcome the table already
    /// contains.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheTableReachesEveryOutcomeAndBothRoutesToNoSuchInvitation()
    {
        var rows = Rows();

        var reached = rows.ConvertAll(row => Decide(row).Outcome).Distinct().ToList();

        Assert.Equal(
            Enum.GetValues<RedemptionOutcome>().OrderBy(outcome => outcome),
            reached.OrderBy(outcome => outcome));
        Assert.Contains(rows, row => row.Code == CodeStanding.Unminted);
        Assert.Contains(rows, row => row.Code == CodeStanding.NotACode);
    }

    /// <summary>
    /// The combinations that are not in the table because no record can be in
    /// them, asserted rather than left as a silence.
    /// </summary>
    /// <remarks>
    /// A row that says unreachable and is never executed proves nothing, which
    /// is the same failure as a guard nobody watched bite. So each of these
    /// builds the record the missing row would need and asserts the record type
    /// refuses it. If one of them ever stops raising, the defect is in the
    /// record shape rather than in the decision, and this is where it surfaces.
    /// </remarks>
    /// <param name="usesLeft">A count no record may carry.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(UsesGranted + 1)]
    public void ACountOutsideTheGrantedOnesIsUnreachableRatherThanUntested(int usesLeft)
    {
        Assert.Throws<ArgumentException>(
            () => ARecord(revoked: false, usesLeft: usesLeft));
    }

    /// <summary>
    /// The other unreachable shape. A record with nothing to compare against
    /// would make every redemption a comparison with nothing, so there is no
    /// row for a matched record without a stored hash.
    /// </summary>
    [Fact]
    public void ARecordWithNothingToCompareAgainstIsUnreachable()
    {
        Assert.Throws<ArgumentException>(() => new Invitation(
            id: Guid.NewGuid(),
            codeHash: ImmutableArray<byte>.Empty,
            mintedBy: Guid.NewGuid(),
            mintedAt: _minted,
            expiresAt: _expires,
            usesGranted: UsesGranted,
            usesRemaining: 1,
            revokedAt: null,
            revokedBy: null,
            templateLabel: "Household",
            accountsProduced: ImmutableArray<Guid>.Empty));
    }

    /// <summary>
    /// A verdict that says nothing matched while holding a record is the third
    /// unreachable shape, and it is refused by the verdict's own factory rather
    /// than by anything the routine happens to do.
    /// </summary>
    [Fact]
    public void AVerdictThatMatchedNothingWhileHoldingARecordIsUnreachable()
    {
        var record = ARecord(revoked: false, usesLeft: 1);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => RedemptionVerdict.Refused(RedemptionOutcome.NoSuchInvitation, record));
    }

    private static RedemptionVerdict Decide(DecisionRow row)
    {
        var record = ARecord(row.Revoked, row.UsesLeft);
        var presented = row.Code switch
        {
            CodeStanding.Minted => MintedCode,
            CodeStanding.Unminted => UnmintedCode,
            _ => NotACode,
        };

        return RedemptionDecision.Decide(presented, _codeHash, new[] { record }, At(row.Expiry));
    }

    /// <summary>
    /// The clock reading for one row. The expiry stays where it is and the
    /// clock moves, one tick at a time, so the boundary row is the instant
    /// itself rather than something near it.
    /// </summary>
    /// <param name="standing">Where the clock is wanted.</param>
    /// <returns>The instant to decide at.</returns>
    private static DateTimeOffset At(ExpiryStanding standing) => standing switch
    {
        ExpiryStanding.Before => _expires.AddTicks(-1),
        ExpiryStanding.AtTheInstant => _expires,
        _ => _expires.AddTicks(1),
    };

    private static Invitation ARecord(bool revoked, int usesLeft)
    {
        var canonical = InvitationCode.Canonicalise(MintedCode);
        Assert.NotNull(canonical);

        return new Invitation(
            id: Guid.Parse("6f0a2d1e-6b3c-4f8a-9f1d-2a7c5e8b0d34"),
            codeHash: _codeHash.Of(canonical),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: _minted,
            expiresAt: _expires,
            usesGranted: UsesGranted,
            usesRemaining: usesLeft,
            revokedAt: revoked ? _minted : null,
            revokedBy: revoked ? _revoker : null,
            templateLabel: "Household",
            accountsProduced: ImmutableArray<Guid>.Empty);
    }
}
