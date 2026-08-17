using System;
using Jellyfin.Plugin.Invites.Invitations;

namespace Jellyfin.Plugin.Invites.Controllers;

/// <summary>
/// What minting returns, which is the one response in this API that ever carries
/// a code.
/// </summary>
/// <remarks>
/// It carries it once. No later call to any route returns it again, because
/// nothing after this holds it: the store keeps the keyed hash the code reduces
/// to and <see cref="InvitationView"/> has no field that could express one. A
/// code that is not copied at this moment is gone, and the repair is a new
/// invitation rather than a lookup.
/// </remarks>
public sealed class MintedInvitation
{
    private MintedInvitation(Minting minting)
    {
        Code = minting.Code;
        Invitation = InvitationView.Of(minting.Invitation);
    }

    /// <summary>
    /// Gets the code, in the form it goes into a link.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the record, in the same shape every other route returns it.
    /// </summary>
    public InvitationView Invitation { get; }

    /// <summary>
    /// Reads a minting into the response shape.
    /// </summary>
    /// <param name="minting">What the minting produced.</param>
    /// <returns>The response.</returns>
    /// <exception cref="ArgumentNullException">The minting is null.</exception>
    public static MintedInvitation Of(Minting minting)
    {
        ArgumentNullException.ThrowIfNull(minting);

        return new MintedInvitation(minting);
    }
}
