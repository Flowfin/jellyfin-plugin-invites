using System;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The plugin advertises its configuration page to the server by resource name.
/// The server resolves that name against the assembly's manifest resources, and
/// a name that does not resolve produces a configuration page the operator
/// cannot open, with nothing at build time to say so.
/// </summary>
public class PluginPagesTests
{
    /// <summary>
    /// Every page the plugin advertises names a resource that is actually
    /// embedded in the assembly.
    /// </summary>
    [Fact]
    public void EveryAdvertisedPageResolvesToAnEmbeddedResource()
    {
        using var paths = new StubApplicationPaths();
        var plugin = new Plugin(paths, new StubXmlSerializer());

        var embedded = typeof(Plugin).Assembly.GetManifestResourceNames();
        var pages = plugin.GetPages().ToList();

        Assert.NotEmpty(pages);
        foreach (var page in pages)
        {
            Assert.Contains(page.EmbeddedResourcePath, embedded, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// The plugin identifier is the key the server stores a plugin under, so an
    /// empty one is a plugin the server cannot tell apart from any other.
    /// </summary>
    [Fact]
    public void PluginCarriesAnIdentifierAndAName()
    {
        using var paths = new StubApplicationPaths();
        var plugin = new Plugin(paths, new StubXmlSerializer());

        Assert.NotEqual(Guid.Empty, plugin.Id);
        Assert.False(string.IsNullOrWhiteSpace(plugin.Name));
    }
}
