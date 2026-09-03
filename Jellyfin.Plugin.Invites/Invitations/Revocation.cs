using System;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// Revoking an invitation, as one routine over one record.
/// </summary>
/// <remarks>
/// <para>
/// Revocation is the operator's undo and it is reached for at the worst
/// moment, so what it does is written in one place rather than assembled by
/// whoever calls it. It is #54.
/// </para>
/// <para>
/// <b>It is a function of a record and produces a record.</b> There is no
/// server here, no store and no account manager, and that is the whole of how
/// the clause saying no account is affected is held: this routine cannot reach
/// an account, rather than reaching one and choosing not to touch it. A caller
/// that later wants to disable the accounts an invitation produced is asking
/// for a different operation, which is #91's export and undo rather than this.
/// </para>
/// <para>
/// <b>What it does not do.</b> It does not spend a use, so a revoked
/// invitation still says what it was worth and how much of that was left when
/// it stopped. It does not delete the record, because the record of a
/// revocation is exactly what an operator needs after a restore from backup
/// quietly puts the invitation back, which is written up in
/// docs/disaster-cases.md. And it does not decide anything about a redemption:
/// whether a revoked invitation is honoured is
/// <see cref="Redemption.RedemptionDecision"/> and is refused there.
/// </para>
/// <para>
/// <b>Immediacy is not in this file and cannot be.</b> #54 asks that a
/// revocation take effect for a redemption already on the page with the form
/// filled in. That follows from where the lock sits in the caller, which is
/// #40's arrangement: the redemption reads the record, decides and writes
/// inside one lock that the revocation also takes. Nothing here can provide it
/// and nothing here should grow a second mechanism that looks as though it
/// does.
/// </para>
/// </remarks>
public static class Revocation
{
    /// <summary>
    /// Revokes an invitation, or hands back the one already revoked.
    /// </summary>
    /// <param name="invitation">The record as the caller read it.</param>
    /// <param name="revokedBy">The operator account making the revocation.</param>
    /// <param name="at">
    /// The clock reading for this revocation, read once by the caller through
    /// <see cref="Time.IClock"/> in the same way a redemption reads it.
    /// </param>
    /// <returns>
    /// A record carrying the revocation. Where the invitation was already
    /// revoked it is the record that was handed in, unchanged.
    /// </returns>
    /// <remarks>
    /// <b>Revoking twice is not an error and moves nothing.</b> An operator who
    /// clicks the button again, a request retried by a browser and two
    /// administrators reaching for the same link all arrive here, and each has
    /// already got what they wanted. Returning an error would make the second
    /// click look like a failure of the first, and rewriting the instant would
    /// lose the moment the invitation actually stopped working, which is the
    /// one thing the record is kept for.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="invitation"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="revokedBy"/> names no operator. A revocation recorded
    /// against nobody answers the question the field exists to answer with a
    /// value that reads like an answer.
    /// </exception>
    public static Invitation Of(Invitation invitation, Guid revokedBy, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        if (revokedBy == Guid.Empty)
        {
            throw new ArgumentException(
                "A revocation is recorded against the operator who made it. The empty identifier names nobody and would be stored as though it named somebody.",
                nameof(revokedBy));
        }

        // Already revoked. The first revocation is the one that stopped the
        // invitation, so it is the one kept, operator and instant together.
        // Handing back the same record rather than building an equal one is
        // also what lets a caller compare by reference and see that nothing was
        // written.
        if (invitation.IsRevoked)
        {
            return invitation;
        }

        return new Invitation(
            id: invitation.Id,
            codeHash: invitation.CodeHash,
            mintedBy: invitation.MintedBy,
            mintedAt: invitation.MintedAt,
            expiresAt: invitation.ExpiresAt,
            usesGranted: invitation.UsesGranted,
            usesRemaining: invitation.UsesRemaining,
            revokedAt: at,
            revokedBy: revokedBy,
            templateLabel: invitation.TemplateLabel,
            template: invitation.Template,
            accountsProduced: invitation.AccountsProduced);
    }
}
