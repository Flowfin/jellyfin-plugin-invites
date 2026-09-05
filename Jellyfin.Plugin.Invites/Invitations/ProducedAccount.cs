using System;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// One account an invitation produced, and when that account expires.
/// </summary>
/// <remarks>
/// <para>
/// <b>The claim is an entry rather than an identifier, and that is the whole
/// of this type.</b> A record used to claim the accounts it produced as bare
/// identifiers, so there was nowhere to put a per-account instant and an expiry
/// could only be worked out from the invitation. #68 refuses that: an expiry
/// derived from the invitation moves when the invitation moves and applies to
/// every account one invitation made, while the thing being disabled is an
/// account and the operator extends one account. A value derived at read time
/// is a value nobody can extend.
/// </para>
/// <para>
/// <b>An absent expiry means the account does not expire.</b> It is not
/// unknown and it is not "ask the invitation": it is a decided value, and it
/// is the one a record brought forward from an older store carries, because a
/// migration that worked an expiry out of the invitation would be the
/// derivation above arriving through the back door.
/// </para>
/// <para>
/// <b>Nothing here acts on the expiry.</b> This type is a place to put one.
/// What disables an account, on what schedule and through which seam, is #68
/// and the issues named from it; a build carrying this type and nothing else
/// disables nobody.
/// </para>
/// <para>
/// The equality a record synthesises is the right one here, unlike on
/// <see cref="Invitation"/>: both members are values, so there is no backing
/// array whose identity could be compared instead of its contents.
/// </para>
/// </remarks>
/// <param name="Account">
/// The server's own identifier for the account. The pointer is kept when the
/// account is gone rather than cleared, which is #45's decision, so this names
/// an account the server may no longer have.
/// </param>
/// <param name="ExpiresAt">
/// When the account expires, or <c>null</c> where it does not. See the remarks
/// for what an absence means and for what still does not act on a value.
/// </param>
public sealed record ProducedAccount(Guid Account, DateTimeOffset? ExpiresAt)
{
    /// <summary>
    /// The claim on an account that does not expire.
    /// </summary>
    /// <remarks>
    /// The value every claim carries today, because nothing sets an expiry
    /// yet. It is a named constructor rather than a default argument so that a
    /// caller writing one is saying it rather than leaving it out, which is
    /// what the day something does set an expiry needs to be able to find.
    /// </remarks>
    /// <param name="account">The server's own identifier for the account.</param>
    /// <returns>The claim.</returns>
    public static ProducedAccount ThatDoesNotExpire(Guid account) => new(account, ExpiresAt: null);
}
