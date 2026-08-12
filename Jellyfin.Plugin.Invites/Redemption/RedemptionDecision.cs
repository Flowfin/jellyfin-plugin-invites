using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Invitations;

namespace Jellyfin.Plugin.Invites.Redemption;

/// <summary>
/// The one place that decides whether a presented code may produce an account.
/// </summary>
/// <remarks>
/// <para>
/// There is one question in this plugin worth getting right, and #56 is that
/// every part of the answer lives here. Nothing else canonicalises a code
/// against the store, compares a stored hash, reads an expiry against a clock
/// reading or looks at a use count to decide anything. The greppable half of
/// that rule is <c>expiry-or-use-count-judged-outside-the-decision</c> in
/// <c>.github/lint/invariants.sh</c>, which exempts this file by name: taking
/// the exemption means moving the comparison in here, where it is reviewed
/// beside the others, rather than writing a comment next to it somewhere else.
/// </para>
/// <para>
/// <b>It decides and does not act.</b> No account is created here, no record is
/// written and no store is opened. That is what lets #102 cover it as a table
/// rather than as a set of arranged worlds, and it is what makes a surviving
/// mutant under #22 mean a missing row instead of a missing fixture.
/// </para>
/// <para>
/// <b>The caller holds the lock and does the reading.</b> #40 wants read,
/// decide and write to be one unit, and #54 wants a revocation to take effect
/// for a redemption already on the page, which follows only if the records are
/// re-read inside that unit. Both are satisfied by the caller taking the lock,
/// reading the records, calling this, and writing before it lets go. A routine
/// that opened the store itself could not be pure and would leave the atomicity
/// nowhere.
/// </para>
/// <para>
/// <b>What it does not check.</b> The global ceilings from #33 are not tested
/// here, because nothing in the tree says what they are yet. When they exist
/// they are one more argument to this routine and one more refusal below,
/// rather than a second routine.
/// </para>
/// </remarks>
public static class RedemptionDecision
{
    /// <summary>
    /// Decides what becomes of one presented code against the records as they
    /// stood when the caller read them.
    /// </summary>
    /// <param name="presented">
    /// What somebody typed, in whatever shape they typed it. Null and rubbish
    /// are ordinary inputs here rather than errors, because this is the one
    /// surface a stranger reaches.
    /// </param>
    /// <param name="codeHash">The keyed hash the store's values were produced with.</param>
    /// <param name="records">
    /// The records to match against, as the caller read them under its lock.
    /// </param>
    /// <param name="now">
    /// The clock reading for this redemption, read once by the caller through
    /// <see cref="Time.IClock"/>. One reading serves the whole decision, which
    /// is the rule in docs/expiry-rules.md: two reads inside one redemption can
    /// straddle an expiry and decide it differently depending on how long the
    /// machine took.
    /// </param>
    /// <returns>The verdict. Never null.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="codeHash"/> or <paramref name="records"/> is null.
    /// </exception>
    public static RedemptionVerdict Decide(
        string? presented,
        IInvitationCodeHash codeHash,
        IReadOnlyList<Invitation> records,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(codeHash);
        ArgumentNullException.ThrowIfNull(records);

        var canonical = InvitationCode.Canonicalise(presented);
        if (canonical is null)
        {
            // What was presented is not a code, so there is nothing to hash and
            // no record it could match. This is the same outcome as a code that
            // reads correctly and matches nothing, and #28 is why: a caller able
            // to tell the two apart is an oracle for which codes exist.
            return RedemptionVerdict.NoSuchInvitation();
        }

        var match = Lookup(codeHash.Of(canonical), records);
        if (match is null)
        {
            return RedemptionVerdict.NoSuchInvitation();
        }

        // Revoked first. All three refusals look the same to whoever presented
        // the code, so the order decides only what the operator's trail says,
        // and an operator who revoked a link is owed that answer rather than the
        // one the calendar happens to give. A revoked invitation also stays
        // revoked once its expiry passes, so the alternative order would quietly
        // rewrite the reason a week later.
        if (match.IsRevoked)
        {
            return RedemptionVerdict.Refused(RedemptionOutcome.Revoked, match);
        }

        // Exclusive, so an invitation whose expiry is the instant T is honoured
        // strictly before T and refused at T. That direction is decided in
        // docs/expiry-rules.md and the exact instant is asserted under #102.
        if (now >= match.ExpiresAt)
        {
            return RedemptionVerdict.Refused(RedemptionOutcome.Expired, match);
        }

        // The record is the only authority for the count, which is #52. Nothing
        // here derives it from how many accounts the record produced: an
        // operator deleting one of those accounts must not restore a use.
        if (match.UsesRemaining <= 0)
        {
            return RedemptionVerdict.Refused(RedemptionOutcome.Spent, match);
        }

        return RedemptionVerdict.Honoured(match);
    }

    /// <summary>
    /// Finds the record whose stored hash is the presented one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every record is compared and the loop never returns early, so the work
    /// done does not depend on where a match sits or on whether there is one.
    /// The comparison itself is
    /// <see cref="CryptographicOperations.FixedTimeEquals(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>,
    /// which is #29.
    /// </para>
    /// <para>
    /// What is claimed is that this routine takes no branch on the outcome of a
    /// comparison and skips no record. Whether the whole redemption is constant
    /// time is a larger claim about everything the caller does, and it has not
    /// been measured; the clause asking for it is #28's.
    /// </para>
    /// </remarks>
    /// <param name="presentedHash">The keyed hash of the canonical presented code.</param>
    /// <param name="records">The records to compare against.</param>
    /// <returns>The matched record, or null.</returns>
    private static Invitation? Lookup(ImmutableArray<byte> presentedHash, IReadOnlyList<Invitation> records)
    {
        Invitation? found = null;

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            if (CryptographicOperations.FixedTimeEquals(record.CodeHash.AsSpan(), presentedHash.AsSpan()))
            {
                found = record;
            }
        }

        return found;
    }
}
