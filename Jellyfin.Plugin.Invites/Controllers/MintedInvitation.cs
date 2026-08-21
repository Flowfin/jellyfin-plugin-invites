using System;
using Jellyfin.Plugin.Invites.Invitations;

namespace Jellyfin.Plugin.Invites.Controllers;

/// <summary>
/// What minting returns, which is the one response in this API that ever carries
/// a code.
/// </summary>
/// <remarks>
/// <para>
/// It carries it once. No later call to any route returns it again, because
/// nothing after this holds it: the store keeps the keyed hash the code reduces
/// to and <see cref="InvitationView"/> has no field that could express one. A
/// code that is not copied at this moment is gone, and the repair is a new
/// invitation rather than a lookup.
/// </para>
/// <para>
/// <b>The link is the code with a host in front of it</b>, so it is under the
/// same rule and not a second, milder one. It appears here and nowhere else,
/// and the reason it appears here at all is #50: an operator who has to compose
/// the address by hand is an operator retyping a host, and the routine that
/// refuses to guess one is not in that path.
/// </para>
/// </remarks>
public sealed class MintedInvitation
{
    private MintedInvitation(Minting minting)
    {
        Code = minting.Code;
        Link = minting.Link;
        LinkRefusal = minting.LinkRefusal;
        Invitation = InvitationView.Of(minting.Invitation);
    }

    /// <summary>
    /// Gets the code, in the form it goes into a link.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the link to hand to the invited person, or <c>null</c> where no
    /// public address is configured.
    /// </summary>
    /// <remarks>
    /// Built from the configured address and never from anything the request
    /// said, which is what <see cref="InvitationLink"/> exists for and what two
    /// greppable rules refuse the spellings of.
    /// </remarks>
    public string? Link { get; }

    /// <summary>
    /// Gets why no link was built, or <c>null</c> where one was.
    /// </summary>
    /// <remarks>
    /// A minting with no configured address still mints. The invitation is
    /// written, the code is handed over, and this says what to set so that the
    /// next one carries a link, because a link to the wrong host is worse than
    /// no link.
    /// </remarks>
    public string? LinkRefusal { get; }

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
