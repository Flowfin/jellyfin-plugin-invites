using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A controller the suite declares and the plugin never does, carrying a route
/// docs/api.md does not name. It is what makes the comparison below provable:
/// without it, the assertions over the plugin assembly would report the same
/// thing for a document that agrees with the source and for a comparison that
/// stopped seeing routes.
/// </summary>
public sealed class ApiDocumentProbeController : ControllerBase
{
    /// <summary>
    /// An undocumented route, which is the mistake this file exists to refuse.
    /// </summary>
    /// <returns>Nothing a caller reads. Nothing routes to this type.</returns>
    [HttpGet("probe/undocumented")]
    public IActionResult Undocumented() => Ok();
}

/// <summary>
/// docs/api.md fixes the routes before they are written, so it is a plan that
/// the source has to grow into rather than a description written after the
/// fact. That only holds while something reads both, and until now nothing did:
/// a route added without a heading, or a heading left behind after the route it
/// names started answering, was invisible to every run.
/// <para>
/// Two sets are compared here. What the assembly registers, read the way the
/// server decides which types become endpoints. What the page names, read off
/// its route headings and off the register of routes it says nothing serves
/// yet. Every route belongs to exactly one of registered or pending, and the
/// page has a heading for it either way.
/// </para>
/// <para>
/// What this does not read is whether a documented parameter, response or
/// refusal is the one the route implements. That is a judgement about meaning
/// and no reading of the tree makes it, so a heading whose body has gone wrong
/// passes here and the review is where it is caught.
/// </para>
/// </summary>
public class ApiDocumentTests
{
    /// <summary>
    /// A route heading, <c>### `GET /redeem/{code}`</c>. The method is upper
    /// case because every heading on the page writes it that way and a lower
    /// case one would be a second spelling of the same route.
    /// </summary>
    private static readonly Regex HeadingPattern =
        new(@"^###\s+`(?<method>[A-Z]+)\s+(?<path>/\S*)`\s*$", RegexOptions.Compiled);

    /// <summary>
    /// A line of the register, <c>- `GET /redeem/{code}`</c>. Read only between
    /// the register's own heading and the next one, so a bullet elsewhere on the
    /// page that happens to look like this is not silently taken for a
    /// declaration.
    /// </summary>
    private static readonly Regex RegisterEntryPattern =
        new(@"^-\s+`(?<method>[A-Z]+)\s+(?<path>/\S*)`\s*$", RegexOptions.Compiled);

    /// <summary>
    /// The heading the register sits under, in docs/api.md.
    /// </summary>
    private const string RegisterHeading = "## What no controller serves yet";

    /// <summary>
    /// Reads docs/api.md out of the working tree.
    /// </summary>
    /// <remarks>
    /// The file is found by walking up from the test binary until a directory
    /// holds both the solution and the page, rather than by counting how many
    /// levels of <c>bin/Release</c> sit under it. The count changes with a
    /// configuration or a target framework and the marker does not. Nothing is
    /// written and nothing outside the repository is read, so this stays inside
    /// the headless rule.
    /// </remarks>
    /// <returns>The lines of the page.</returns>
    private static IReadOnlyList<string> ApiDocumentLines()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var page = Path.Combine(directory.FullName, "docs", "api.md");
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            if (File.Exists(page) && File.Exists(solution))
            {
                return File.ReadAllLines(page);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds both Jellyfin.Plugin.Invites.sln and docs/api.md, so this comparison read nothing. Failing rather than passing over an empty page.");
    }

    /// <summary>
    /// The routes the page gives a heading to.
    /// </summary>
    /// <param name="lines">The lines of the page.</param>
    /// <returns>Each as <c>METHOD /path</c>, ordered.</returns>
    private static IReadOnlyList<string> DocumentedRoutes(IEnumerable<string> lines) =>
        lines
            .Select(line => HeadingPattern.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["method"].Value + " " + match.Groups["path"].Value)
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The routes the page declares nothing serves yet.
    /// </summary>
    /// <param name="lines">The lines of the page.</param>
    /// <returns>Each as <c>METHOD /path</c>, ordered.</returns>
    private static IReadOnlyList<string> PendingRoutes(IEnumerable<string> lines)
    {
        var entries = new List<string>();
        var inside = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inside = string.Equals(line.TrimEnd(), RegisterHeading, StringComparison.Ordinal);
                continue;
            }

            if (!inside)
            {
                continue;
            }

            var match = RegisterEntryPattern.Match(line);
            if (match.Success)
            {
                entries.Add(match.Groups["method"].Value + " " + match.Groups["path"].Value);
            }
        }

        return entries.OrderBy(route => route, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Discovers controllers the way the server does, through
    /// <see cref="ControllerFeatureProvider"/> over the assembly's application
    /// part. Keying on a name or on an attribute spelled a particular way would
    /// enumerate a different set from the one the server serves, and the
    /// difference would be exactly the route nobody meant to publish.
    /// </summary>
    /// <param name="assembly">The assembly to read as an application part.</param>
    /// <returns>The controller types it holds.</returns>
    private static IReadOnlyList<Type> Controllers(Assembly assembly)
    {
        var manager = new ApplicationPartManager();
        manager.ApplicationParts.Add(new AssemblyPart(assembly));
        manager.FeatureProviders.Add(new ControllerFeatureProvider());

        var feature = new ControllerFeature();
        manager.PopulateFeature(feature);

        return feature.Controllers.Select(controller => controller.AsType()).ToList();
    }

    /// <summary>
    /// The routes a set of controllers answers, as the page writes them.
    /// </summary>
    /// <remarks>
    /// A template on the action is combined with the one on its class unless it
    /// begins with a slash or a tilde, which is the framework's own rule for an
    /// absolute template. An action with a method attribute and no template of
    /// its own answers at the class template, which is how a mint and a list on
    /// one prefix are ordinarily written.
    /// </remarks>
    /// <param name="controllers">The controller types to read.</param>
    /// <returns>Each as <c>METHOD /path</c>, ordered and without duplicates.</returns>
    private static IReadOnlyList<string> RoutesOf(IEnumerable<Type> controllers)
    {
        var routes = new List<string>();

        foreach (var controller in controllers)
        {
            var prefix = controller
                .GetCustomAttributes(inherit: true)
                .OfType<IRouteTemplateProvider>()
                .Select(provider => provider.Template)
                .FirstOrDefault(template => !string.IsNullOrEmpty(template));

            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                foreach (var http in action.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>())
                {
                    var path = Combine(prefix, http.Template);
                    foreach (var method in http.HttpMethods)
                    {
                        routes.Add(method.ToUpperInvariant() + " " + path);
                    }
                }
            }
        }

        return routes
            .Distinct(StringComparer.Ordinal)
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Joins a class template and an action template into the one path the page
    /// writes.
    /// </summary>
    /// <param name="prefix">The class template, or <c>null</c>.</param>
    /// <param name="template">The action template, or <c>null</c>.</param>
    /// <returns>The path, always beginning with a slash.</returns>
    private static string Combine(string? prefix, string? template)
    {
        if (!string.IsNullOrEmpty(template) && (template[0] == '/' || template[0] == '~'))
        {
            return "/" + template.TrimStart('~').TrimStart('/');
        }

        var parts = new[] { prefix, template }
            .Where(part => !string.IsNullOrEmpty(part))
            .Select(part => part!.Trim('/'))
            .Where(part => part.Length > 0);

        return "/" + string.Join("/", parts);
    }

    /// <summary>
    /// The page is read and route headings come out of it. Without this, every
    /// assertion below would be satisfied by a reader that found nothing, and a
    /// document nobody could parse would look exactly like a document nothing
    /// disagrees with.
    /// </summary>
    [Fact]
    public void TheHeadingsOfTheApiDocumentAreRead()
    {
        var documented = DocumentedRoutes(ApiDocumentLines());

        Assert.Contains("POST /Invites", documented, StringComparer.Ordinal);
    }

    /// <summary>
    /// The route reader sees a route when there is one, and it reads the method
    /// and the template rather than the name of the action.
    /// </summary>
    [Fact]
    public void TheRouteReaderFindsARouteWhenThereIsOne()
    {
        var routes = RoutesOf(new[] { typeof(ApiDocumentProbeController) });

        Assert.Equal(new[] { "GET /probe/undocumented" }, routes);
    }

    /// <summary>
    /// And it refuses one the page does not name. This is the shape the check
    /// exists against: a route added to the plugin in a change whose author did
    /// not open docs/api.md.
    /// </summary>
    [Fact]
    public void ARouteTheDocumentDoesNotNameIsReported()
    {
        var lines = ApiDocumentLines();
        var documented = DocumentedRoutes(lines);

        var undocumented = RoutesOf(new[] { typeof(ApiDocumentProbeController) })
            .Where(route => !documented.Contains(route, StringComparer.Ordinal))
            .ToList();

        Assert.Equal(new[] { "GET /probe/undocumented" }, undocumented);
    }

    /// <summary>
    /// Every route the plugin registers has a heading on the page. Vacuous
    /// today, because the plugin registers none, and that is why the two
    /// assertions above are here: they are what says this one is empty for the
    /// reason it claims to be.
    /// </summary>
    [Fact]
    public void EveryRouteThePluginRegistersHasAHeadingInTheApiDocument()
    {
        var documented = DocumentedRoutes(ApiDocumentLines());

        var undocumented = RoutesOf(Controllers(typeof(Plugin).Assembly))
            .Where(route => !documented.Contains(route, StringComparer.Ordinal))
            .ToList();

        Assert.True(
            undocumented.Count == 0,
            "These routes answer and docs/api.md gives them no heading: "
            + string.Join(", ", undocumented)
            + ". Write the heading in the change that adds the route. A route people find in a browser's network tab rather than on that page is a route they will call anyway.");
    }

    /// <summary>
    /// Every route the page names is either registered by the plugin or in the
    /// register of routes it says nothing serves yet, and never both. That is
    /// the other direction, and it is what keeps the register from becoming a
    /// list nobody prunes: a route that starts answering while its line is
    /// still there reds, and so does a heading with neither a controller nor a
    /// line behind it.
    /// </summary>
    [Fact]
    public void EveryDocumentedRouteIsServedOrDeclaredUnserved()
    {
        var lines = ApiDocumentLines();
        var documented = DocumentedRoutes(lines);
        var pending = PendingRoutes(lines);
        var registered = RoutesOf(Controllers(typeof(Plugin).Assembly));

        var expected = documented
            .Where(route => !registered.Contains(route, StringComparer.Ordinal))
            .ToList();

        Assert.Equal(expected, pending);
    }

    /// <summary>
    /// Nothing in the register is missing from the page. A line naming a route
    /// with no heading is a dangling declaration: it retires nothing, and it
    /// would make the assertion above pass by cancelling a heading that is not
    /// there.
    /// </summary>
    [Fact]
    public void NothingInTheRegisterIsMissingFromThePage()
    {
        var lines = ApiDocumentLines();
        var documented = DocumentedRoutes(lines);

        var dangling = PendingRoutes(lines)
            .Where(route => !documented.Contains(route, StringComparer.Ordinal))
            .ToList();

        Assert.True(
            dangling.Count == 0,
            "These lines of the register in docs/api.md name a route the page gives no heading: "
            + string.Join(", ", dangling)
            + ". A line there retires a heading; one naming no heading retires nothing.");
    }
}
