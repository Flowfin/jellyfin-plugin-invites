using System;
using System.Linq;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// The two writes a honoured redemption makes to the record it was honoured
/// against: one use taken, and the account it produced recorded.
/// </summary>
/// <remarks>
/// <para>
/// <b>It decides nothing.</b> Whether the record may be honoured at all is
/// <see cref="Redemption.RedemptionDecision"/>'s and is asked before either of
/// these is called. Nothing here reads an expiry, compares a use count or looks
/// at a revocation, which is the same rule <see cref="Revocation"/> states from
/// its own side: a second opinion about those three fields is the defect the
/// invariant lint refuses two spellings of.
/// </para>
/// <para>
/// <b>The two acts are separate because they happen at different moments.</b>
/// The use is taken before the account is created and the account is recorded
/// after, so a failure between them leaves a use spent and no account rather
/// than an account with the use still standing. That direction is the one to
/// fail in: an invitation that produced nothing costs the operator a fresh mint,
/// and an invitation that produced an account and still counts as unused is a
/// single-use link that creates accounts for as long as it has not expired.
/// #53 owns closing that window; nothing here claims it is closed.
/// </para>
/// <para>
/// <b>Neither act is a count derived from the other.</b> The use count is a
/// field on the record and is never worked out from how many accounts the record
/// claims, which <c>UseCountIsNeverDerivedFromAccountsTests</c> holds: the two
/// disagree exactly when something went wrong, and a derivation would hide the
/// disagreement the operator needs to see.
/// </para>
/// </remarks>
public static class Spending
{
    /// <summary>
    /// Takes one use off a record.
    /// </summary>
    /// <param name="invitation">The record a redemption was honoured against.</param>
    /// <returns>The record with one use fewer. Every other field is carried across.</returns>
    /// <remarks>
    /// There is no check here that a use is left to take. A record with none is
    /// one <see cref="Redemption.RedemptionDecision"/> refuses, so a caller that
    /// reached this without asking has skipped the decision, and what catches it
    /// is <see cref="Invitation"/>'s own invariant that the remaining count is a
    /// count of the granted ones. A comparison written here would be the second
    /// opinion about a use count that the whole arrangement exists against, and
    /// it would answer this caller's mistake by silently doing nothing.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="invitation"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The record had no use left, so taking one leaves a count no invitation
    /// can be in. Nothing is written by this routine either way.
    /// </exception>
    public static Invitation Of(Invitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        return new Invitation(
            id: invitation.Id,
            codeHash: invitation.CodeHash,
            mintedBy: invitation.MintedBy,
            mintedAt: invitation.MintedAt,
            expiresAt: invitation.ExpiresAt,
            usesGranted: invitation.UsesGranted,
            usesRemaining: invitation.UsesRemaining - 1,
            revokedAt: invitation.RevokedAt,
            revokedBy: invitation.RevokedBy,
            templateLabel: invitation.TemplateLabel,
            template: invitation.Template,
            accountsProduced: invitation.AccountsProduced);
    }

    /// <summary>
    /// Records the account a honoured redemption created.
    /// </summary>
    /// <param name="invitation">The record the redemption was honoured against.</param>
    /// <param name="account">The server's own identifier for the account.</param>
    /// <returns>
    /// The record claiming that account, or the record it was given where it
    /// already claimed it. Handing the same instance back is what lets a caller
    /// see by reference that there is nothing to write.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The empty identifier is refused for the reason <see cref="Revocation"/>
    /// refuses it on its own field: a claim recorded against nobody answers the
    /// question the field exists for with a value that reads like an answer, and
    /// the operator's view of which invitation produced which account is the one
    /// place that costs the most.
    /// </para>
    /// <para>
    /// <b>The claim is recorded with no expiry, and that is a decided value
    /// rather than a gap.</b> #468 gives the claim somewhere to carry one and
    /// deliberately owns nothing that sets one: an expiry worked out here from
    /// the invitation is the derivation #68 refuses, and the routine that
    /// decides what an account's expiry should be does not exist. So an
    /// account created today does not expire until something an operator can
    /// see sets a value on it.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="invitation"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="account"/> is the empty identifier.</exception>
    public static Invitation With(Invitation invitation, Guid account)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        if (account == Guid.Empty)
        {
            throw new ArgumentException(
                "An account produced by an invitation is recorded by the server's own identifier for it. The empty identifier names no account and would be stored as though it named one.",
                nameof(account));
        }

        if (invitation.AccountsProduced.Any(claim => claim.Account == account))
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
            revokedAt: invitation.RevokedAt,
            revokedBy: invitation.RevokedBy,
            templateLabel: invitation.TemplateLabel,
            template: invitation.Template,
            accountsProduced: invitation.AccountsProduced.Add(ProducedAccount.ThatDoesNotExpire(account)));
    }
}
