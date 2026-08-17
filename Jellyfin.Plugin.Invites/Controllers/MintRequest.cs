namespace Jellyfin.Plugin.Invites.Controllers;

/// <summary>
/// What a caller sends to mint an invitation.
/// </summary>
/// <remarks>
/// The three fields are docs/api.md's, in the same order and with the same
/// optionality. Nothing here carries a code, a hash or an account: minting
/// produces the first, never stores the second in this direction, and creates no
/// account at all.
/// </remarks>
public sealed class MintRequest
{
    /// <summary>
    /// Gets or sets which grant the invitation carries, by the template's name.
    /// </summary>
    /// <remarks>
    /// Required. The record stores the label, which is what
    /// <see cref="Invitations.Invitation.TemplateLabel"/> holds today. #61 is
    /// where the grant itself is copied into the invitation rather than
    /// referenced by name, and this field is the name until it is.
    /// </remarks>
    public string? Template { get; set; }

    /// <summary>
    /// Gets or sets how many days the link lasts.
    /// </summary>
    /// <remarks>
    /// Optional. Omitted, the invitation takes
    /// <see cref="Invitations.InvitationOperations.DefaultValidity"/>. Days
    /// rather than an instant, because an expiry a caller computes from its own
    /// clock is an expiry decided by a clock this plugin does not read.
    /// </remarks>
    public int? ValidityDays { get; set; }

    /// <summary>
    /// Gets or sets how many accounts the invitation is good for.
    /// </summary>
    /// <remarks>
    /// Optional, and one when omitted. Refused at zero and above
    /// <see cref="Invitations.InvitationMint.UsesCeiling"/>.
    /// </remarks>
    public int? Uses { get; set; }
}
