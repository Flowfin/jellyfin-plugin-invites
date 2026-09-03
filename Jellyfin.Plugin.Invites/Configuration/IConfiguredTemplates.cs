using System.Collections.Generic;

namespace Jellyfin.Plugin.Invites.Configuration;

/// <summary>
/// The account templates as configured, read from wherever the plugin keeps its
/// configuration.
/// </summary>
/// <remarks>
/// A seam for the reason <see cref="IPublicAddress"/> is one: the load the
/// server makes when it starts judges this setting, and a routine that reached
/// for the plugin's static instance to read it could not be driven by a test
/// without a server. It reads and does not write, so nothing handed this can
/// change a template.
/// </remarks>
public interface IConfiguredTemplates
{
    /// <summary>
    /// Gets the templates as configured, or <c>null</c> where no configuration
    /// is loaded.
    /// </summary>
    IReadOnlyList<ConfiguredTemplate?>? Templates { get; }
}
