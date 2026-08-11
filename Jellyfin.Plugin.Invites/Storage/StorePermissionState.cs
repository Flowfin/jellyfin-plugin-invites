namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// What was learned about the store file's permissions, as four states rather
/// than a boolean.
/// </summary>
/// <remarks>
/// A check that could not be made and a check that found nothing wrong are
/// different facts, and collapsing them is how a store nobody ever looked at
/// comes to read as a store that was found to be fine.
/// </remarks>
public enum StorePermissionState
{
    /// <summary>
    /// There is no store file, so there was no mode to read.
    /// </summary>
    NoStoreFile,

    /// <summary>
    /// The platform has no file modes, so nothing was read.
    /// </summary>
    /// <remarks>
    /// This is Windows. The protection that means anything there is the access
    /// control on the data directory, which this plugin does not set and does
    /// not understand well enough to judge, so it says which of the two it
    /// looked at rather than reporting a check that read nothing.
    /// </remarks>
    NotCheckedOnThisPlatform,

    /// <summary>
    /// The mode was read and is no wider than the one the store creates the
    /// file with.
    /// </summary>
    AsWritten,

    /// <summary>
    /// The mode was read and carries at least one bit beyond the ones the store
    /// creates the file with.
    /// </summary>
    /// <remarks>
    /// Somebody or something widened it after the store wrote it. It is
    /// reported and not repaired.
    /// </remarks>
    WiderThanWritten,
}
