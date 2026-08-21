namespace Jellyfin.Plugin.Invites.Configuration;

/// <summary>
/// The public address outside a test: the setting an operator wrote on the
/// plugin's own configuration page.
/// </summary>
/// <remarks>
/// It has no logic of its own, for the same reason
/// <see cref="Storage.PluginStoreDirectory"/> has none. Everything a test would
/// want to steer lives in whatever took an <see cref="IPublicAddress"/>, so
/// nothing is lost by this being the one type in the pair the suite never
/// exercises. The answer is <c>null</c> before the server has constructed the
/// plugin, which is a state the caller reports rather than one this type
/// invents an address for.
/// </remarks>
public sealed class PluginPublicAddress : IPublicAddress
{
    /// <inheritdoc />
    public string? PublicBaseUrl => Plugin.Instance?.Configuration.PublicBaseUrl;
}
