using System;
using Jellyfin.Plugin.Invites.Invitations;

namespace Jellyfin.Plugin.Invites.Redemption;

/// <summary>
/// What one call of the decision routine concluded, as a value.
/// </summary>
/// <remarks>
/// <para>
/// A verdict is returned rather than acted on. The routine that produces it
/// creates no account, writes no record and touches nothing, which is what lets
/// the table in #102 cover it exhaustively: every input arrives as an argument
/// and the whole result is this value, so a row is a pair of values rather than
/// a world to set up.
/// </para>
/// <para>
/// It carries the matched record when there is one, including on a refusal,
/// because the operator's trail is about a particular invitation and an
/// operator asking why a link failed is asking about that record. What it never
/// carries is anything derived from the presented code.
/// </para>
/// </remarks>
public sealed class RedemptionVerdict
{
    private RedemptionVerdict(RedemptionOutcome outcome, Invitation? invitation)
    {
        Outcome = outcome;
        Invitation = invitation;
    }

    /// <summary>
    /// Gets what was concluded.
    /// </summary>
    public RedemptionOutcome Outcome { get; }

    /// <summary>
    /// Gets the record the presented code matched, or null when it matched
    /// none.
    /// </summary>
    /// <remarks>
    /// Null exactly when <see cref="Outcome"/> is
    /// <see cref="RedemptionOutcome.NoSuchInvitation"/>, which is asserted by
    /// the suite rather than left as a convention, because a caller reading
    /// this on a refusal is the ordinary case.
    /// </remarks>
    public Invitation? Invitation { get; }

    /// <summary>
    /// Gets a value indicating whether an account may be created from this
    /// invitation.
    /// </summary>
    /// <remarks>
    /// The one question a caller asks. It is a property of this value rather
    /// than a comparison a caller writes for itself, so that no second place
    /// decides what an honoured redemption is.
    /// </remarks>
    public bool MayCreateAnAccount => Outcome == RedemptionOutcome.Honoured;

    /// <summary>
    /// The presented code matched no stored record.
    /// </summary>
    /// <returns>A verdict carrying no record.</returns>
    public static RedemptionVerdict NoSuchInvitation() =>
        new(RedemptionOutcome.NoSuchInvitation, null);

    /// <summary>
    /// The presented code matched a record, and the record refuses it.
    /// </summary>
    /// <param name="outcome">Which refusal. Never
    /// <see cref="RedemptionOutcome.Honoured"/> and never
    /// <see cref="RedemptionOutcome.NoSuchInvitation"/>.</param>
    /// <param name="invitation">The record it matched.</param>
    /// <returns>A verdict carrying that record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="invitation"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="outcome"/> is not a refusal against a matched record.
    /// </exception>
    public static RedemptionVerdict Refused(RedemptionOutcome outcome, Invitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        // A refusal that carries no record and a refusal that carries one are
        // different things to whoever reads the trail, and the two factories
        // exist so a caller cannot build the third state: an outcome saying
        // there is no such invitation while holding one.
        if (outcome is not (RedemptionOutcome.Revoked or RedemptionOutcome.Expired or RedemptionOutcome.Spent))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "A refusal against a matched record is revoked, expired or spent. The other two outcomes have their own factories.");
        }

        return new RedemptionVerdict(outcome, invitation);
    }

    /// <summary>
    /// The invitation may produce an account.
    /// </summary>
    /// <param name="invitation">The record it matched.</param>
    /// <returns>A verdict carrying that record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="invitation"/> is null.</exception>
    public static RedemptionVerdict Honoured(Invitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        return new RedemptionVerdict(RedemptionOutcome.Honoured, invitation);
    }
}
