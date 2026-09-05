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
    /// <param name="migration">
    /// What the read had to do to bring an older document forward, or
    /// <c>null</c> where the document was already the shape this build writes.
    /// Optional and defaulted to nothing, so a caller constructing contents that
    /// came from no file at all says nothing about a migration by saying
    /// nothing.
    /// </param>
    /// <exception cref="ArgumentNullException">The permission finding is null.</exception>
    public StoreContents(ImmutableArray<Invitation> invitations, StorePermissions permissions, StoreMigration? migration = null)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        Invitations = invitations.IsDefault ? ImmutableArray<Invitation>.Empty : invitations;
        Permissions = permissions;
        Migration = migration;
    }

    /// <summary>
    /// Gets the invitations the store held, in the order the file lists them.
    /// </summary>
    public ImmutableArray<Invitation> Invitations { get; }

    /// <summary>
    /// Gets what the store file's permissions were found to be.
    /// </summary>
    public StorePermissions Permissions { get; }

    /// <summary>
    /// Gets what this read had to do to bring an older document forward, or
    /// <c>null</c> where it read the shape this build writes.
    /// </summary>
    /// <remarks>
    /// It travels with the read for the reason the permissions do: it is one
    /// observation of one file, and there is no second call to forget. What is
    /// done with it is the caller's, and #92 asks that somebody say it out loud,
    /// which <see cref="Startup.LoadOnStart"/> does once when the server starts.
    /// </remarks>
    public StoreMigration? Migration { get; }
}
