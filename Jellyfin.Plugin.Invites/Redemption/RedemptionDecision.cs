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
/// <b>What it does not check.</b> The ceiling on how many accounts the plugin
/// may create in a period, from #33, is not tested here, because nothing in the
/// tree says what that number is yet. When it exists it is one more argument to
/// this routine and one more refusal below, rather than a second routine.
/// </para>
/// <para>
/// <b>The ceiling on live invitations is #33's too and is not a refusal here.</b>
/// It is enforced at minting, so what this file owes it is the judgement rather
/// than the comparison: <see cref="IsLive"/> answers whether one record is still
/// able to produce an account, and minting counts with it. That keeps one
/// authority for a fact two callers need and is why the routine is here rather
/// than beside the store.
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

        var refusal = Refusal(match, now);

        return refusal is null
            ? RedemptionVerdict.Honoured(match)
            : RedemptionVerdict.Refused(refusal.Value, match);
    }

    /// <summary>
    /// Whether this record could still produce an account if somebody presented
    /// its code at this instant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the same question <see cref="Decide"/> asks, and it is asked
    /// of the same routine.</b> #33 bounds how many live invitations may exist
    /// at once, which means minting has to count them, and counting them is a
    /// judgement about expiry and about a use count. The rule in the summary
    /// above is that no such judgement is made anywhere else, so the count is
    /// taken by asking here rather than by a second comparison written next to
    /// the store. Both callers reach one implementation, so a change to what
    /// "live" means cannot move for one of them and not the other.
    /// </para>
    /// <para>
    /// <b>It is not a shorter <see cref="Decide"/>.</b> No code is presented and
    /// nothing is looked up, so this answers only for a record the caller
    /// already holds and can never stand in for a redemption. A caller that has
    /// a presented code calls <see cref="Decide"/>.
    /// </para>
    /// </remarks>
    /// <param name="invitation">The record to judge.</param>
    /// <param name="now">
    /// The clock reading, read once by the caller through <see cref="Time.IClock"/>.
    /// </param>
    /// <returns><c>true</c> where nothing about the record refuses it yet.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="invitation"/> is null.</exception>
    public static bool IsLive(Invitation invitation, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        return Refusal(invitation, now) is null;
    }

    /// <summary>
    /// The instant a retention period is counted from for this record, or
    /// <c>null</c> where the record is still live.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is here for the same reason <see cref="IsLive"/> is.</b> #59
    /// deletes records that stopped being usable long enough ago, so the sweep
    /// has to know when a record stopped being usable, and that is a judgement
    /// about an expiry and a use count. The rule this file states is that no such
    /// judgement is made anywhere else, so the sweep asks here instead of
    /// comparing timestamps beside the store. What "usable" means therefore
    /// cannot move for the sweep without moving for a redemption.
    /// </para>
    /// <para>
    /// <b>Revocation and expiry each name their own instant and the earlier one
    /// wins.</b> A record revoked a week before it would have expired stopped
    /// being usable when it was revoked; a record revoked after it had already
    /// expired stopped being usable at the expiry. Taking the later of the two
    /// would keep the first record a week longer than the rule allows.
    /// </para>
    /// <para>
    /// <b>A spend leaves no instant on the record, and this is where that costs
    /// something.</b> <see cref="Invitation.UsesRemaining"/> reaching zero is not
    /// timestamped, so a record that is spent and not yet expired has nothing on
    /// it saying when that happened. This answers with its expiry, which is
    /// always later than the spend, because a record can only be spent while it
    /// is still live. So such a record is kept longer than the rule requires
    /// rather than deleted before the rule allows, and that is the direction to
    /// err in: deleting a record early destroys the trail an operator needs, and
    /// keeping one late is a record that is deleted at the next sweep after its
    /// expiry. A spent-at instant on the record would close the gap and it is
    /// #52's field to add, not this routine's to guess at.
    /// </para>
    /// </remarks>
    /// <param name="invitation">The record to judge.</param>
    /// <param name="now">
    /// The clock reading, read once by the caller through <see cref="Time.IClock"/>.
    /// </param>
    /// <returns>
    /// The instant retention runs from, or <c>null</c> where the record could
    /// still produce an account.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="invitation"/> is null.</exception>
    public static DateTimeOffset? RetentionStartsAt(Invitation invitation, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        if (Refusal(invitation, now) is null)
        {
            return null;
        }

        if (invitation.RevokedAt is { } revoked && revoked < invitation.ExpiresAt)
        {
            return revoked;
        }

        return invitation.ExpiresAt;
    }

    /// <summary>
    /// What refuses this record at this instant, or <c>null</c> where nothing
    /// does.
    /// </summary>
    /// <remarks>
    /// The three comparisons this plugin makes about a record live here and
    /// nowhere else, which is what the invariant lint refuses outside this file.
    /// </remarks>
    /// <param name="record">The record being judged.</param>
    /// <param name="now">The one clock reading for this judgement.</param>
    /// <returns>The outcome that refuses it, or null.</returns>
    private static RedemptionOutcome? Refusal(Invitation record, DateTimeOffset now)
    {
        // Revoked first. All three refusals look the same to whoever presented
        // the code, so the order decides only what the operator's trail says,
        // and an operator who revoked a link is owed that answer rather than the
        // one the calendar happens to give. A revoked invitation also stays
        // revoked once its expiry passes, so the alternative order would quietly
        // rewrite the reason a week later.
        if (record.IsRevoked)
        {
            return RedemptionOutcome.Revoked;
        }

        // Exclusive, so an invitation whose expiry is the instant T is honoured
        // strictly before T and refused at T. That direction is decided in
        // docs/expiry-rules.md and the exact instant is asserted under #102.
        if (now >= record.ExpiresAt)
        {
            return RedemptionOutcome.Expired;
        }

        // The record is the only authority for the count, which is #52. Nothing
        // here derives it from how many accounts the record produced: an
        // operator deleting one of those accounts must not restore a use.
        if (record.UsesRemaining <= 0)
        {
            return RedemptionOutcome.Spent;
        }

        return null;
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
