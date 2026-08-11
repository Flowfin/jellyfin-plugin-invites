using System;

namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// What the store found when it looked at its own file's permissions, and the
/// sentence a person would need to act on it.
/// </summary>
/// <remarks>
/// The finding is returned rather than logged from inside the store. What this
/// plugin logs, and where, is decided by its own issue and there is no logging
/// seam to write into yet; a store that called a logger of its own choosing now
/// would be deciding that by hand. Returning it keeps the finding impossible to
/// lose by accident: ignoring it is a line somebody has to write.
/// </remarks>
public sealed class StorePermissions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorePermissions"/> class.
    /// </summary>
    /// <param name="state">What was found.</param>
    /// <param name="detail">The sentence naming the file and what was found.</param>
    /// <exception cref="ArgumentException">The detail is blank.</exception>
    public StorePermissions(StorePermissionState state, string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            throw new ArgumentException("A permission finding says which file and what was found, or it is not a finding.", nameof(detail));
        }

        State = state;
        Detail = detail;
    }

    /// <summary>
    /// Gets what was found.
    /// </summary>
    public StorePermissionState State { get; }

    /// <summary>
    /// Gets the sentence naming the file and the mode found.
    /// </summary>
    /// <remarks>
    /// It never carries anything out of the store's contents. A permissions
    /// report is read by whoever is not supposed to be reading the invitations.
    /// </remarks>
    public string Detail { get; }

    /// <summary>
    /// Gets a value indicating whether an operator has something to do about
    /// this.
    /// </summary>
    public bool IsAProblem => State == StorePermissionState.WiderThanWritten;
}
