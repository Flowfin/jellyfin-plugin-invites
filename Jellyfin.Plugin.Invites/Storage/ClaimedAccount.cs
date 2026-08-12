using System;

namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// One account a stored record says it created, named together with the
/// invitation that says so.
/// </summary>
/// <remarks>
/// The pair travels rather than the account alone. An operator told that an
/// account is missing can do nothing with that sentence; an operator told which
/// invitation claims it can look the invitation up, see what it granted and see
/// whether its remaining uses still make sense. The invitation is named by the
/// identifier <see cref="Invitations.Invitation.Id"/> carries, which is the one
/// name for an invitation an administrator view or a log line may use.
/// </remarks>
public sealed class ClaimedAccount
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClaimedAccount"/> class.
    /// </summary>
    /// <param name="invitationId">The invitation that claims the account.</param>
    /// <param name="accountId">The account it claims to have created.</param>
    public ClaimedAccount(Guid invitationId, Guid accountId)
    {
        InvitationId = invitationId;
        AccountId = accountId;
    }

    /// <summary>
    /// Gets the identifier of the invitation whose record claims this account.
    /// </summary>
    public Guid InvitationId { get; }

    /// <summary>
    /// Gets the identifier of the account the record claims to have created.
    /// </summary>
    public Guid AccountId { get; }
}
