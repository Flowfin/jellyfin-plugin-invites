using System;
using System.Collections.Immutable;
using Jellyfin.Plugin.Invites.Invitations;

namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// What one read of the store returned: the invitations, and what the file's
/// permissions were found to be at the same moment.
/// </summary>
/// <remarks>
/// The two travel together because they are one observation of one file. A
/// caller that reads the invitations has already been told about the mode, so
/// there is no second call to forget.
/// </remarks>
public sealed class StoreContents
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StoreContents"/> class.
    /// </summary>
    /// <param name="invitations">The invitations the file held.</param>
    /// <param name="permissions">What the file's permissions were found to be.</param>
    /// <exception cref="ArgumentNullException">The permission finding is null.</exception>
    public StoreContents(ImmutableArray<Invitation> invitations, StorePermissions permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        Invitations = invitations.IsDefault ? ImmutableArray<Invitation>.Empty : invitations;
        Permissions = permissions;
    }

    /// <summary>
    /// Gets the invitations the store held, in the order the file lists them.
    /// </summary>
    public ImmutableArray<Invitation> Invitations { get; }

    /// <summary>
    /// Gets what the store file's permissions were found to be.
    /// </summary>
    public StorePermissions Permissions { get; }
}
