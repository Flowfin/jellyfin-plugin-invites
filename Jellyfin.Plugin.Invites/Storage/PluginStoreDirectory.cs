namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// The store directory outside a test: the data directory the server hands this
/// plugin.
/// </summary>
/// <remarks>
/// It has no logic of its own, for the same reason
/// <see cref="Time.SystemClock"/> has none. Everything a test would want to
/// steer lives in whatever took an <see cref="IStoreDirectory"/>, so nothing is
/// lost by this being the one type in the pair the suite never exercises. The
/// answer is <c>null</c> before the server has constructed the plugin, which is
/// a state the caller reports rather than one this type invents a path for.
/// </remarks>
public sealed class PluginStoreDirectory : IStoreDirectory
{
    /// <inheritdoc />
    public string? Path => Plugin.Instance?.DataFolderPath;
}
