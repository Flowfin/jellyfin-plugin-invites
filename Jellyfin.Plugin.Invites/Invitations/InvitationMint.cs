using System;
using System.Collections.Immutable;
using System.Globalization;
using Jellyfin.Plugin.Invites.Accounts;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// The one place an invitation record comes into existence, and the only place
/// its use count is chosen.
/// </summary>
/// <remarks>
/// <para>
/// <b>The count is decided once, here, and read everywhere else.</b> #52 makes
/// the record's own field the authority for how many accounts an invitation is
/// still worth. Nothing derives it from how many accounts the record has
/// produced, because an account deleted afterwards would then hand a use back
/// to a link somebody has already used, and nobody would see it happen. A
/// minted record starts with every use it was granted and the redemption
/// decision is what takes them away.
/// </para>
/// <para>
/// <b>What is refused here.</b> A count of zero or less, and a count above the
/// ceiling. Both are decisions about what may be minted rather than states a
/// record cannot be in, which is the line <see cref="Invitation"/> draws in its
/// own constructor: that type refuses a remaining count outside the granted
/// one, and this one refuses a granted count nobody should be able to ask for.
/// Nothing is refused twice.
/// </para>
/// <para>
/// <b>What is not refused here.</b> The expiry is carried and never compared.
/// Every judgement of an expiry or a use count against a clock or a bound lives
/// in <c>RedemptionDecision</c>, which is #56, and
/// <c>expiry-or-use-count-judged-outside-the-decision</c> in
/// .github/lint/invariants.sh is the greppable half of that rule. Minting is
/// the moment the record is written, not a moment anything is honoured.
/// </para>
/// </remarks>
public static class InvitationMint
{
    /// <summary>
    /// The largest number of accounts one invitation may be minted for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The number is #33's and so is the reasoning. The case the feature exists
    /// for is a household or a small group invited with one link, and ten
    /// covers a family and a few guests with room left. Above that the operator
    /// is no longer inviting people they know by name; they are running an open
    /// registration page with extra steps, and this plugin declines to be one.
    /// </para>
    /// <para>
    /// The two error directions are not the same size. A ceiling set too low
    /// costs the operator one more mint. A ceiling set too high raises what a
    /// leaked link is worth, and the bound in SECURITY.md is written per
    /// remaining use, so this number is a term in that sentence rather than a
    /// preference.
    /// </para>
    /// <para>
    /// It is a constant here rather than a configured value because nothing in
    /// this tree carries configuration yet. #86 is where the settings live.
    /// </para>
    /// <para>
    /// #33 holds two more ceilings and one of them has since landed. The count
    /// of live invitations is <see cref="InvitationOperations.LiveCeiling"/>,
    /// enforced where the store can be read rather than here, because this
    /// routine is handed one record and never sees the others. How many accounts
    /// were created in a rolling window is the third and still needs the attempt
    /// trail.
    /// </para>
    /// </remarks>
    public const int UsesCeiling = 10;

    /// <summary>
    /// Mints one invitation record.
    /// </summary>
    /// <param name="id">The non-secret identifier this invitation is named by.</param>
    /// <param name="codeHash">
    /// The keyed hash of the code, over its canonical form. The code itself is
    /// not an argument and is never held: what a caller mints, shows once and
    /// forgets is the code, and what it hands here is what the code reduces to.
    /// </param>
    /// <param name="mintedBy">The operator answerable for this invitation.</param>
    /// <param name="mintedAt">The instant it was minted, read through the clock seam.</param>
    /// <param name="expiresAt">The instant it stops being usable. Carried, not compared.</param>
    /// <param name="uses">How many accounts it is good for.</param>
    /// <param name="templateLabel">The name of the template the operator picked.</param>
    /// <param name="template">
    /// The grant that name stood for at this moment, copied onto the record.
    /// It is the caller's to resolve, because this routine is handed one record
    /// and never sees the configuration; what it holds is that no record leaves
    /// here carrying a name and no grant, which is #61's rule in the one place
    /// a record comes into existence.
    /// </param>
    /// <returns>The record, with every use it was granted still on it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The count is zero or less, or above <see cref="UsesCeiling"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">The template is null.</exception>
    public static Invitation Mint(
        Guid id,
        ImmutableArray<byte> codeHash,
        Guid mintedBy,
        DateTimeOffset mintedAt,
        DateTimeOffset expiresAt,
        int uses,
        string templateLabel,
        AccountTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        // An invitation good for no account is not a stricter invitation, it is
        // a link that refuses everybody who follows it while reading, to the
        // operator who minted it, exactly like one that works.
        if (uses < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(uses),
                uses,
                "An invitation is minted for at least one account. A count of zero produces a link that refuses everybody and says nothing about why.");
        }

        if (uses > UsesCeiling)
        {
            throw new ArgumentOutOfRangeException(
                nameof(uses),
                uses,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "One invitation may be minted for at most {0} accounts, and {1} were asked for. The ceiling is what one operator action can authorise at once.",
                    UsesCeiling,
                    uses));
        }

        return new Invitation(
            id: id,
            codeHash: codeHash,
            mintedBy: mintedBy,
            mintedAt: mintedAt,
            expiresAt: expiresAt,
            usesGranted: uses,
            usesRemaining: uses,
            revokedAt: null,
            revokedBy: null,
            templateLabel: templateLabel,
            template: template,
            accountsProduced: ImmutableArray<Guid>.Empty);
    }
}
