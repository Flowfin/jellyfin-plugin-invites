using System;
using System.Globalization;

namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// Raised where a store file declares a version this build does not know.
/// </summary>
/// <remarks>
/// <para>
/// A store newer than the code reading it is a downgrade: the plugin was
/// replaced with an older one, or a data directory was carried backwards. The
/// refusal is the whole point of the version. A reader that carried on would
/// meet fields it does not understand and fill in defaults, and the default for
/// a revocation is that there is not one, which turns a revoked invitation back
/// into a live one.
/// </para>
/// <para>
/// It is its own type rather than a general failure so a caller can tell a
/// store it must not touch from a store that is damaged. The two need opposite
/// responses: a damaged store is a repair, and this one is putting the newer
/// plugin back.
/// </para>
/// </remarks>
public class StoreVersionRefusedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StoreVersionRefusedException"/> class.
    /// </summary>
    public StoreVersionRefusedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreVersionRefusedException"/> class.
    /// </summary>
    /// <param name="message">What went wrong.</param>
    public StoreVersionRefusedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreVersionRefusedException"/> class.
    /// </summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">What it happened during.</param>
    public StoreVersionRefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreVersionRefusedException"/> class
    /// for a store whose version this build does not know.
    /// </summary>
    /// <param name="path">The store file.</param>
    /// <param name="found">The version the file declares.</param>
    /// <param name="understood">The newest version this build reads.</param>
    public StoreVersionRefusedException(string path, int found, int understood)
        : base(string.Format(
            CultureInfo.InvariantCulture,
            "{0} is a version {1} store and this plugin reads version {2}. It is left exactly as it was. Put back the plugin that wrote it, or move the file aside if the invitations in it are not wanted.",
            path,
            found,
            understood))
    {
        Found = found;
        Understood = understood;
    }

    /// <summary>
    /// Gets the version the file declares, or zero where this was not built
    /// from a file.
    /// </summary>
    public int Found { get; }

    /// <summary>
    /// Gets the newest version this build reads, or zero where this was not
    /// built from a file.
    /// </summary>
    public int Understood { get; }
}
