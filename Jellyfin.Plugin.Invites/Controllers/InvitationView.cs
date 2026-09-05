using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Invites.Accounts;
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
    private InvitationView(Invitation invitation, IReadOnlyCollection<Guid>? serverAccounts)
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
        Grant = invitation.Template;
        AccountsProduced = invitation.AccountsProduced
            .Select(claim => new AccountView(claim.Account, Presence(claim.Account, serverAccounts)))
            .ToArray();
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
    /// Gets what the template granted at the moment this invitation was minted,
    /// or <c>null</c> where the record carries no copy of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The copy rather than the configured template of the same name.</b>
    /// #94 asks that an operator be able to see what this plugin did to an
    /// account months later, and a name resolved against the configuration at
    /// viewing time answers a different question: what that name means today,
    /// which is not what was applied. The record carries the value since #61 and
    /// this is that value rendered.
    /// </para>
    /// <para>
    /// <b>Null is a record minted before the copy existed</b>, which the store
    /// brings forward from version one with the grant absent rather than
    /// guessing one, and it is the same absence
    /// <see cref="Invitation.Template"/> already means. Such an invitation
    /// creates no account, so a caller reading null here is reading a row that
    /// can do nothing rather than a field somebody forgot to fill in.
    /// </para>
    /// <para>
    /// <b>It is the grant and never the account's policy.</b> What the account
    /// carries now is a different fact, it is not in this plugin's reach, and
    /// #94's second clause is where the difference between the two is settled.
    /// Nothing here should be read as saying the account still has this.
    /// </para>
    /// </remarks>
    public AccountTemplate? Grant { get; }

    /// <summary>
    /// Gets the accounts it created, each with what became of it.
    /// </summary>
    /// <remarks>
    /// The pointer is kept when the account is gone rather than cleared, which
    /// is #45's decision, so the list is the same length whatever the server
    /// still holds and the difference is on each entry.
    /// </remarks>
    public IReadOnlyList<AccountView> AccountsProduced { get; }

    /// <summary>
    /// Reads one record into the shape a route returns.
    /// </summary>
    /// <param name="invitation">The record.</param>
    /// <param name="serverAccounts">
    /// Every account identifier the server has, or <c>null</c> where it does not
    /// answer that question in a shape this plugin knows. There is no overload
    /// without this argument: a caller that could leave it out is a caller that
    /// can hand back a row claiming an account is there without having asked.
    /// </param>
    /// <returns>The view.</returns>
    /// <exception cref="ArgumentNullException">The record is null.</exception>
    public static InvitationView Of(Invitation invitation, IReadOnlyCollection<Guid>? serverAccounts)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        return new InvitationView(invitation, serverAccounts);
    }

    /// <summary>
    /// Reads several.
    /// </summary>
    /// <param name="invitations">The records.</param>
    /// <param name="serverAccounts">
    /// Every account identifier the server has, or <c>null</c>. Read once for
    /// the whole listing rather than per row, so two rows of one response cannot
    /// disagree about an account because the server changed between them.
    /// </param>
    /// <returns>The views, in the order they were given.</returns>
    /// <exception cref="ArgumentNullException">The records are null.</exception>
    public static IReadOnlyList<InvitationView> Of(
        IEnumerable<Invitation> invitations,
        IReadOnlyCollection<Guid>? serverAccounts)
    {
        ArgumentNullException.ThrowIfNull(invitations);

        return invitations.Select(invitation => Of(invitation, serverAccounts)).ToArray();
    }

    /// <summary>
    /// What became of one claimed account.
    /// </summary>
    /// <remarks>
    /// An unanswered server is <see cref="AccountPresence.Unknown"/> rather than
    /// an empty set, because reading it as an empty set reports every account
    /// this plugin created as deleted.
    /// </remarks>
    /// <param name="account">The identifier the record claims.</param>
    /// <param name="serverAccounts">What the server answered, or null.</param>
    /// <returns>The state to render.</returns>
    private static AccountPresence Presence(Guid account, IReadOnlyCollection<Guid>? serverAccounts)
    {
        if (serverAccounts is null)
        {
            return AccountPresence.Unknown;
        }

        return serverAccounts.Contains(account) ? AccountPresence.Present : AccountPresence.Gone;
    }
}
