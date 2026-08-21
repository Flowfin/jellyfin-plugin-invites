using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Jellyfin.Plugin.Invites.Controllers;
using Microsoft.AspNetCore.Mvc;
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
    /// Every element the page's own script reaches for. A list rather than
    /// something derived, because both directions are worth asserting: an
    /// identifier declared and never queried is dead markup, and one queried
    /// and never declared is a handler that silently does nothing.
    /// </summary>
    private static readonly string[] Driven =
    [
        "InvitesConfigPage",
        "InvitesConfigForm",
        "InvitesMintForm",
        "InvitesMintTemplate",
        "InvitesMintValidityDays",
        "InvitesMintUses",
        "InvitesMintedCode",
        "InvitesMintedCodeValue",
        "InvitesCopyCode",
        "InvitesList",
        "InvitesRotateKey",
    ];

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
    /// The page's script queries every element it drives by identifier. An
    /// element renamed without its query, or the other way round, is a page
    /// whose button does nothing and whose load handler never runs, and the
    /// dashboard reports none of it.
    /// </summary>
    [Fact]
    public void PageQueriesElementsItActuallyDeclares()
    {
        var page = ReadPage();

        foreach (var id in Driven)
        {
            Assert.Contains("id=\"" + id + "\"", page, StringComparison.Ordinal);
            Assert.Contains("querySelector(\"#" + id + "\")", page, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The page calls the routes the controller declares, rather than paths
    /// that were right when the page was written.
    /// </summary>
    /// <remarks>
    /// The page is served as bytes and its calls are strings, so a renamed
    /// route leaves it compiling, shipping and failing at the moment an
    /// operator presses a button. Nothing else in this repository reads both
    /// sides: the route inventory reads the controller and the tests above read
    /// the page. This reads the controller's own attributes and compares them
    /// against what the page will ask for.
    /// </remarks>
    [Fact]
    public void PageCallsTheRoutesTheControllerDeclares()
    {
        var page = ReadPage();

        var route = typeof(InvitesController).GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(route);
        Assert.Equal(route!.Template, ValueAfter(page, "invitations:"));

        var revoke = typeof(InvitesController)
            .GetMethod(nameof(InvitesController.Revoke))!
            .GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(revoke);
        Assert.Equal("{id}/" + ValueAfter(page, "revoke:"), revoke!.Template);

        // The fifth operation, read the same way. It is the one route on this
        // page whose template carries no identifier, so the page holds the
        // whole of it rather than a suffix, and a comparison written as a
        // suffix would pass against a template that had lost its first segment.
        var rotate = typeof(InvitesController)
            .GetMethod(nameof(InvitesController.Rotate))!
            .GetCustomAttribute<HttpPostAttribute>();
        Assert.NotNull(rotate);
        Assert.Equal(ValueAfter(page, "rotate:"), rotate!.Template);
    }

    /// <summary>
    /// The four operations an operator does are all on this page, so the job is
    /// done without leaving it.
    /// </summary>
    /// <remarks>
    /// Read off what the page sends rather than off the buttons, because a
    /// button that posts nowhere looks the same in the markup as one that
    /// works. Minting is a post to the route, listing is the read the page
    /// makes when it opens, revoking is a post to the revoke path, and copying
    /// is the field the code lands in.
    /// </remarks>
    [Fact]
    public void PageMintsListsAndRevokesWithoutLeaving()
    {
        var page = ReadPage();

        Assert.Contains("ApiClient.getUrl(InvitesConfig.invitations)", page, StringComparison.Ordinal);
        Assert.Contains("ApiClient.getJSON(", page, StringComparison.Ordinal);
        Assert.Contains(
            "InvitesConfig.invitations + \"/\" + id + \"/\" + InvitesConfig.revoke",
            page,
            StringComparison.Ordinal);
        Assert.Contains("window.confirm(", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// Rotating the key is two calls to one route, and the page cannot make the
    /// second without the first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read off what the page sends, like the assertions above. What matters
    /// here is the order rather than the presence: the count the second call
    /// confirms is taken out of what the first call answered, so a page that
    /// posted a number of its own would be confirming a cost the server never
    /// stated. The route refuses that anyway, and this is the half that says
    /// the page does not try.
    /// </para>
    /// <para>
    /// The sentence shown to the operator is the server's. A page that wrote
    /// its own would be a second description of what rotation costs, and the
    /// two would disagree the first time either moved.
    /// </para>
    /// </remarks>
    [Fact]
    public void PageAsksWhatRotationCostsBeforeItConfirms()
    {
        var page = ReadPage();

        Assert.Contains("JSON.stringify({ Invalidates: null })", page, StringComparison.Ordinal);
        Assert.Contains("window.confirm(plan.Detail)", page, StringComparison.Ordinal);
        Assert.Contains(
            "JSON.stringify({ Invalidates: plan.Invalidates })",
            page,
            StringComparison.Ordinal);

        var planned = page.IndexOf("Invalidates: null", StringComparison.Ordinal);
        var asked = page.IndexOf("window.confirm(plan.Detail)", StringComparison.Ordinal);
        var confirmed = page.IndexOf("Invalidates: plan.Invalidates", StringComparison.Ordinal);

        Assert.True(
            planned < asked && asked < confirmed,
            "The page asks, shows and confirms in that order, and the source has them at "
            + planned + ", " + asked + " and " + confirmed
            + ". A confirmation written before the question is a rotation an operator meets after it has happened.");
    }

    /// <summary>
    /// The code is shown once, the page says so, and the field it is shown in
    /// can be copied with nothing but a keyboard.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The warning is the only thing on this surface that tells an operator a
    /// code they did not copy is gone. What makes the sentence true is
    /// elsewhere and by construction rather than by wording:
    /// <c>InvitationView</c> has no field a code could be expressed in, so no
    /// listing returns one.
    /// </para>
    /// <para>
    /// The field is <c>readonly</c> rather than disabled, because a disabled
    /// field cannot be focused and therefore cannot be copied by anybody
    /// working without a mouse or a script. The copy button is an enhancement
    /// beside it and not the route.
    /// </para>
    /// </remarks>
    [Fact]
    public void PageSaysTheCodeIsShownOnce()
    {
        var page = ReadPage();

        Assert.Contains("only time this code is shown", page, StringComparison.Ordinal);
        Assert.Contains("readonly", ElementCarrying(page, "InvitesMintedCodeValue"), StringComparison.Ordinal);
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

    /// <summary>
    /// The whole tag carrying an identifier, from its opening angle bracket to
    /// its closing one, so an assertion about an attribute does not depend on
    /// the order the attributes are written in.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="id">The identifier on the element.</param>
    /// <returns>The element's tag.</returns>
    private static string ElementCarrying(string page, string id)
    {
        var at = page.IndexOf("id=\"" + id + "\"", StringComparison.Ordinal);
        Assert.True(at >= 0, "The page declares no element with the identifier " + id + ".");

        var open = page.LastIndexOf('<', at);
        Assert.True(open >= 0, "The identifier " + id + " is not inside an element.");

        var close = page.IndexOf('>', at);
        Assert.True(close > open, "The element carrying " + id + " is not closed.");

        return page[open..(close + 1)];
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
