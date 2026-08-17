using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Invites.Invitations;

namespace Jellyfin.Plugin.Invites.Controllers;

/// <summary>
/// One invitation as the administrator routes return it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type has no code field and no hash field, and that is the mechanism
/// rather than a convention.</b> docs/api.md requires that listing never returns
/// a code or a hash and that minting returns the code once and nowhere else. A
/// route that returned the record with two fields left unfilled would satisfy
/// that by remembering to; a type that cannot express either value satisfies it
/// by construction, and the difference is what survives the next field somebody
/// adds to the record.
/// </para>
/// <para>
/// The fields are the ones docs/api.md names for a listing row - the non-secret
/// identifier, the state, the uses remaining, the expiry and what the invitation
/// created - plus the two that say who minted it and when, which
/// docs/personal-data.md already holds rows for.
/// </para>
/// </remarks>
public sealed class InvitationView
{
    private InvitationView(Invitation invitation)
    {
        Id = invitation.Id;
        MintedBy = invitation.MintedBy;
        MintedAt = invitation.MintedAt;
        ExpiresAt = invitation.ExpiresAt;
        UsesGranted = invitation.UsesGranted;
        UsesRemaining = invitation.UsesRemaining;
        IsRevoked = invitation.IsRevoked;
        RevokedAt = invitation.RevokedAt;
        RevokedBy = invitation.RevokedBy;
        Template = invitation.TemplateLabel;
        AccountsProduced = invitation.AccountsProduced.ToArray();
    }

    /// <summary>
    /// Gets the non-secret identifier this invitation is named by.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the operator answerable for it.
    /// </summary>
    public Guid MintedBy { get; }

    /// <summary>
    /// Gets the instant it was minted.
    /// </summary>
    public DateTimeOffset MintedAt { get; }

    /// <summary>
    /// Gets the instant it stops being usable.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Gets how many accounts it was minted for.
    /// </summary>
    public int UsesGranted { get; }

    /// <summary>
    /// Gets how many of those are left.
    /// </summary>
    public int UsesRemaining { get; }

    /// <summary>
    /// Gets a value indicating whether it has been revoked.
    /// </summary>
    public bool IsRevoked { get; }

    /// <summary>
    /// Gets the instant it was revoked, or <c>null</c>.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; }

    /// <summary>
    /// Gets the operator who revoked it, or <c>null</c>.
    /// </summary>
    public Guid? RevokedBy { get; }

    /// <summary>
    /// Gets the name of the template it carries.
    /// </summary>
    public string Template { get; }

    /// <summary>
    /// Gets the accounts it created.
    /// </summary>
    public IReadOnlyList<Guid> AccountsProduced { get; }

    /// <summary>
    /// Reads one record into the shape a route returns.
    /// </summary>
    /// <param name="invitation">The record.</param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The record is null.</exception>
    public static InvitationView Of(Invitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        return new InvitationView(invitation);
    }

    /// <summary>
    /// Reads several.
    /// </summary>
    /// <param name="invitations">The records.</param>
    /// <returns>The views, in the order they were given.</returns>
    /// <exception cref="ArgumentNullException">The records are null.</exception>
    public static IReadOnlyList<InvitationView> Of(IEnumerable<Invitation> invitations)
    {
        ArgumentNullException.ThrowIfNull(invitations);

        return invitations.Select(Of).ToArray();
    }
}
