using System;
using System.Collections.Generic;
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
/// The same goes for where the page loads from, which is the last test here.
/// </summary>
public class ConfigurationPageTests
{
    private const string PageResource = "Jellyfin.Plugin.Invites.Configuration.configPage.html";

    /// <summary>
    /// The spellings an address somewhere else is written in. A scheme and two
    /// slashes covers every absolute address, and a quote or a bracket in front
    /// of two slashes covers the protocol-relative form, which is the one that
    /// looks like a path until it is read twice.
    /// </summary>
    private static readonly string[] Elsewhere = ["://", "\"//", "'//", "(//"];

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

    /// <summary>
    /// The page fetches nothing from anywhere but the server it was served
    /// from. A dashboard page that pulls a script or a stylesheet off another
    /// host gives that host the run of an administrator's browser, on every
    /// installation at once rather than on the one somebody attacked, and it
    /// does so silently while the page keeps working.
    /// </summary>
    /// <remarks>
    /// This is a spelling and not a fetch. It refuses an address in a comment
    /// or in an attribute nothing loads from as readily as one in a script tag,
    /// because a page with no address in it at all is a thing a reader can
    /// check in a second and a page with some is an argument. The page carries
    /// none, so the cost of the wider rule is a sentence the day somebody wants
    /// one.
    /// </remarks>
    [Fact]
    public void PageFetchesFromNowhereElse()
    {
        var lines = ReadPage().Split('\n');
        var found = new List<string>();

        for (var line = 0; line < lines.Length; line++)
        {
            foreach (var spelling in Elsewhere)
            {
                if (lines[line].Contains(spelling, StringComparison.Ordinal))
                {
                    found.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "line {0} carries {1}: {2}",
                        line + 1,
                        spelling,
                        lines[line].Trim()));
                    break;
                }
            }
        }

        Assert.True(
            found.Count == 0,
            "The configuration page names an address somewhere else:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, found));
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
