namespace Jellyfin.Plugin.Invites.Configuration;

/// <summary>
/// The three configured numbers, read off the plugin's own configuration.
/// </summary>
/// <remarks>
/// The one production implementation of <see cref="IConfiguredNumbers"/>, and
/// the one place the static plugin instance is reached for these settings. It
/// answers <c>null</c> before the plugin is constructed, which is the same
/// answer <see cref="PluginPublicAddress"/> gives for the address.
/// </remarks>
public sealed class PluginConfiguredNumbers : IConfiguredNumbers
{
    /// <inheritdoc />
    public int? RecordRetentionDays => Plugin.Instance?.Configuration.RecordRetentionDays;

    /// <inheritdoc />
    public int? RedemptionAttemptsPerAddressInAnHour => Plugin.Instance?.Configuration.RedemptionAttemptsPerAddressInAnHour;

    /// <inheritdoc />
    public int? RedemptionAttemptsPerSecond => Plugin.Instance?.Configuration.RedemptionAttemptsPerSecond;
}
