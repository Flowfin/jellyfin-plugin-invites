using System.Collections.Generic;

namespace Jellyfin.Plugin.Invites.Configuration;

/// <summary>
/// The configured templates, read off the plugin's own configuration.
/// </summary>
/// <remarks>
/// The one production implementation of <see cref="IConfiguredTemplates"/>,
/// and the one place the static plugin instance is reached for this setting.
/// It answers <c>null</c> before the plugin is constructed, which is the same
/// answer <see cref="PluginPublicAddress"/> gives for the address.
/// </remarks>
public sealed class PluginConfiguredTemplates : IConfiguredTemplates
{
    /// <inheritdoc />
    public IReadOnlyList<ConfiguredTemplate?>? Templates => Plugin.Instance?.Configuration.Templates;
}
