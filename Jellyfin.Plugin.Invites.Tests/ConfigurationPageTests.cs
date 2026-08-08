using System;
using System.Globalization;
using System.IO;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The configuration page is served to the dashboard and then hands an
/// identifier back to it, to ask for the configuration of the plugin the page
/// belongs to. That identifier is a literal in the page, so it is a third copy
/// of a value that also lives in the plugin class and in the packaging
/// manifest. A page holding a stale copy still loads, and reads and writes the
/// configuration of whatever plugin now owns that identifier, or of nothing at
/// all. Nothing about that failure is loud, so it is asserted here instead.
/// </summary>
public class ConfigurationPageTests
{
    private const string PageResource = "Jellyfin.Plugin.Invites.Configuration.configPage.html";

    /// <summary>
    /// The identifier the page hands to the dashboard is this plugin's own.
    /// </summary>
    [Fact]
    public void PageAsksForThisPluginsConfiguration()
    {
        using var paths = new StubApplicationPaths();
        var plugin = new Plugin(paths, new StubXmlSerializer());

        var page = ReadPage();
        var declared = ValueAfter(page, "pluginUniqueId:");

        Assert.Equal(plugin.Id.ToString("D", CultureInfo.InvariantCulture), declared);
    }

    /// <summary>
    /// The identifier appears once. A second copy is a second thing to move,
    /// and the two would have to be found before either could be trusted.
    /// </summary>
    [Fact]
    public void PageCarriesTheIdentifierOnce()
    {
        var page = ReadPage();

        var occurrences = 0;
        var at = page.IndexOf("pluginUniqueId:", StringComparison.Ordinal);
        while (at >= 0)
        {
            occurrences++;
            at = page.IndexOf("pluginUniqueId:", at + 1, StringComparison.Ordinal);
        }

        Assert.Equal(1, occurrences);
    }

    /// <summary>
    /// The page's script queries the two elements by identifier. An element
    /// renamed without its query, or the other way round, is a page whose Save
    /// button does nothing and whose load handler never runs.
    /// </summary>
    [Fact]
    public void PageQueriesElementsItActuallyDeclares()
    {
        var page = ReadPage();

        foreach (var id in new[] { "InvitesConfigPage", "InvitesConfigForm" })
        {
            Assert.Contains("id=\"" + id + "\"", page, StringComparison.Ordinal);
            Assert.Contains("querySelector(\"#" + id + "\")", page, StringComparison.Ordinal);
        }
    }

    private static string ReadPage()
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream(PageResource);
        Assert.NotNull(stream);

        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }

    private static string ValueAfter(string page, string marker)
    {
        var at = page.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(at >= 0, "The page carries no " + marker + " to compare against the plugin.");

        var open = page.IndexOf('"', at);
        Assert.True(open >= 0, "The value after " + marker + " is not a quoted string.");

        var close = page.IndexOf('"', open + 1);
        Assert.True(close > open, "The value after " + marker + " is not a closed quoted string.");

        return page[(open + 1)..close];
    }
}
