using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A controller declared by the suite and never by the plugin. It exists so the
/// enumeration below can be shown to see a controller when there is one: an
/// empty result over the plugin assembly has to mean the plugin registers
/// nothing, not that the enumeration went blind.
/// </summary>
public sealed class ProbeController : ControllerBase
{
    /// <summary>
    /// An action, so the type is a controller for the same reason a real one
    /// would be rather than by its name alone.
    /// </summary>
    /// <returns>Nothing a caller reads. Nothing routes to this type.</returns>
    [HttpGet("probe")]
    public IActionResult Probe() => Ok();
}

/// <summary>
/// Every route this plugin registers is administrator-only except the
/// redemption path, which is public by design. This is the inventory that holds
/// that sentence to the assembly, and it is landed while the inventory is empty
/// on purpose: written now, the first route added to the plugin turns this red
/// and its author has to place it in one of the two lists in the same change.
/// Written after the routes exist, it starts as a snapshot of whatever happened
/// to be there, which is the drift it is meant to refuse.
/// </summary>
public class RouteInventoryTests
{
    /// <summary>
    /// The controllers whose every route requires an administrator. Empty
    /// today, because the plugin has no routes at all.
    /// </summary>
    private static readonly HashSet<string> AdministratorControllers = new(StringComparer.Ordinal);

    /// <summary>
    /// The controllers reachable without authentication. There is exactly one
    /// category of these by design, the redemption path a stranger presents an
    /// invitation to, and it is empty today because that route is not written.
    /// A second name here is a second public endpoint and is a decision, not an
    /// addition.
    /// </summary>
    private static readonly HashSet<string> PublicControllers = new(StringComparer.Ordinal);

    /// <summary>
    /// Discovers controllers the way the server does. The framework decides
    /// which types in a plugin assembly become endpoints, and it decides it
    /// with <see cref="ControllerFeatureProvider"/> over the assembly's
    /// application part. Keying on anything else here, a name ending in
    /// Controller or an attribute spelled a particular way, would enumerate a
    /// different set from the one the server serves, and the difference would
    /// be exactly the route nobody meant to publish.
    /// </summary>
    /// <param name="assembly">The assembly to read as an application part.</param>
    /// <returns>The full names of the controller types it holds.</returns>
    private static IReadOnlyList<string> DiscoverControllers(System.Reflection.Assembly assembly)
    {
        var manager = new ApplicationPartManager();
        manager.ApplicationParts.Add(new AssemblyPart(assembly));
        manager.FeatureProviders.Add(new ControllerFeatureProvider());

        var feature = new ControllerFeature();
        manager.PopulateFeature(feature);

        return feature.Controllers
            .Select(controller => controller.FullName ?? controller.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The enumeration sees a controller when one is there. Without this, the
    /// assertion below would report the same thing for a plugin that registers
    /// nothing and for a discovery that stopped working, and only one of those
    /// is worth a green mark.
    /// </summary>
    [Fact]
    public void TheEnumerationFindsAControllerWhenThereIsOne()
    {
        var found = DiscoverControllers(typeof(ProbeController).Assembly);

        Assert.Contains(typeof(ProbeController).FullName!, found, StringComparer.Ordinal);
    }

    /// <summary>
    /// Every controller the plugin assembly holds is in one of the two declared
    /// categories, and there is no third. A route that is in neither list is
    /// not a route somebody forgot to authorize; it is a route nobody has said
    /// out loud is administrator-only or deliberately public.
    /// </summary>
    [Fact]
    public void EveryRegisteredControllerIsAdministratorOnlyOrTheRedemptionRoute()
    {
        var found = DiscoverControllers(typeof(Plugin).Assembly);

        var unplaced = found
            .Where(name => !AdministratorControllers.Contains(name) && !PublicControllers.Contains(name))
            .ToList();

        Assert.True(
            unplaced.Count == 0,
            "These controllers are in neither the administrator list nor the public one in "
            + nameof(RouteInventoryTests)
            + ": "
            + string.Join(", ", unplaced)
            + ". Add each to whichever list it belongs in, in the change that adds the route.");
    }

    /// <summary>
    /// Nothing is in both lists. A name in both would make the assertion above
    /// pass whichever category it was meant to be in, which is the one mistake
    /// the two lists exist to prevent.
    /// </summary>
    [Fact]
    public void NoControllerIsBothAdministratorOnlyAndPublic()
    {
        var inBoth = AdministratorControllers.Intersect(PublicControllers, StringComparer.Ordinal).ToList();

        Assert.Empty(inBoth);
    }
}
