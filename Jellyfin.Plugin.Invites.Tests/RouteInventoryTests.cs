using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
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
/// The shape the requirement check has to refuse: a class-level attribute and
/// an action carrying nothing of its own. Deleting the class attribute from a
/// type like this opens every action under it at once, and that is the refactor
/// the explicit-per-route rule exists against, so it is the fixture the check is
/// proved on rather than a bare unattributed method.
/// </summary>
[Authorize(Policy = "RequiresElevation")]
public sealed class ProbeClassAttributeOnlyController : ControllerBase
{
    /// <summary>
    /// Covered by the class and by nothing of its own.
    /// </summary>
    /// <returns>Nothing a caller reads. Nothing routes to this type.</returns>
    [HttpGet("probe/class-only")]
    public IActionResult Mint() => Ok();
}

/// <summary>
/// An administrator route written the way the rule asks for: the requirement is
/// on the action, so it survives the class attribute being removed.
/// </summary>
[Authorize(Policy = "RequiresElevation")]
public sealed class ProbeExplicitlyAuthorizedController : ControllerBase
{
    /// <summary>
    /// Carries its own requirement.
    /// </summary>
    /// <returns>Nothing a caller reads. Nothing routes to this type.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet("probe/explicit")]
    public IActionResult Mint() => Ok();
}

/// <summary>
/// The public side, written the same way. The redemption path is reachable
/// without authentication by design, and saying so on the action is what tells a
/// later reader that the absence of a requirement was a decision.
/// </summary>
public sealed class ProbeExplicitlyAnonymousController : ControllerBase
{
    /// <summary>
    /// Carries its own declaration that it is public.
    /// </summary>
    /// <returns>Nothing a caller reads. Nothing routes to this type.</returns>
    [AllowAnonymous]
    [HttpGet("probe/anonymous")]
    public IActionResult Redeem() => Ok();
}

/// <summary>
/// Every route this plugin registers is administrator-only except the
/// redemption path, which is public by design. This is the inventory that holds
/// that sentence to the assembly, and it was landed while the inventory was
/// empty on purpose: written then, the first route added to the plugin turned
/// this red and its author had to place it in one of the two lists in the same
/// change. Written after the routes existed, it would have started as a snapshot
/// of whatever happened to be there, which is the drift it is meant to refuse.
/// That is what happened: the administrator routes arrived under #82 and all
/// three assertions below went red until the name was placed.
/// </summary>
public class RouteInventoryTests
{
    /// <summary>
    /// The controllers whose every route requires an administrator. One today,
    /// the four administrator operations from #82.
    /// </summary>
    private static readonly HashSet<string> AdministratorControllers = new(StringComparer.Ordinal)
    {
        "Jellyfin.Plugin.Invites.Controllers.InvitesController",
    };

    /// <summary>
    /// The controllers reachable without authentication. There is exactly one
    /// category of these by design, the redemption path a stranger presents an
    /// invitation to, and one name in it today, the setup page from #74. A
    /// second name here is a second public endpoint and is a decision, not an
    /// addition.
    /// </summary>
    private static readonly HashSet<string> PublicControllers = new(StringComparer.Ordinal)
    {
        "Jellyfin.Plugin.Invites.Controllers.RedeemController",
    };

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

    /// <summary>
    /// The actions of a controller that do not declare <typeparamref name="TRequirement"/>
    /// on themselves.
    /// <para>
    /// The attributes read are the action's own and never the declaring type's,
    /// which is the whole point: a requirement satisfied by a class attribute is
    /// one deletion away from being gone while every action under it keeps
    /// answering. <c>inherit: false</c> refuses the smaller version of the same
    /// thing, a requirement carried only by a base method an override replaced.
    /// </para>
    /// <para>
    /// The methods read are those declared on the controller and on any base of
    /// it inside the same assembly, which stops at the framework's own
    /// <see cref="ControllerBase"/>. That is a narrower set than the one the
    /// framework routes: a public method inherited from a type outside the
    /// assembly is an action to the framework and is invisible here. This plugin
    /// declares no such base, and the day it does, this bound is what has to
    /// move.
    /// </para>
    /// </summary>
    /// <typeparam name="TRequirement">
    /// <see cref="IAuthorizeData"/> for a route that requires something of the
    /// caller, <see cref="IAllowAnonymous"/> for one that deliberately does not.
    /// </typeparam>
    /// <param name="controller">The controller type to read.</param>
    /// <returns>The names of the actions carrying nothing of their own, ordered.</returns>
    private static IReadOnlyList<string> ActionsWithoutTheirOwn<TRequirement>(Type controller)
    {
        var declared = new List<MethodInfo>();
        for (var type = controller; type is not null && type.Assembly == controller.Assembly; type = type.BaseType)
        {
            declared.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        }

        return declared
            .Where(method => !method.IsSpecialName)
            .Where(method => method.GetCustomAttributes(typeof(NonActionAttribute), inherit: true).Length == 0)
            .Where(method => !method.GetCustomAttributes(inherit: false).OfType<TRequirement>().Any())
            .Select(method => (method.DeclaringType?.FullName ?? controller.Name) + "." + method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The check bites, and it bites the shape that actually happens. A class
    /// carrying the requirement over an action carrying none is reported, which
    /// is what makes the assertions below worth their green mark rather than
    /// something that would pass over any assembly at all.
    /// </summary>
    [Fact]
    public void AnActionCoveredOnlyByItsClassIsReportedAsCarryingNothingOfItsOwn()
    {
        var missing = ActionsWithoutTheirOwn<IAuthorizeData>(typeof(ProbeClassAttributeOnlyController));

        Assert.Equal(
            new[] { typeof(ProbeClassAttributeOnlyController).FullName + ".Mint" },
            missing);
    }

    /// <summary>
    /// And it does not bite a route written the way the rule asks for, in either
    /// category. Without this half the assertion above is satisfied by a check
    /// that reports every action ever written.
    /// </summary>
    [Fact]
    public void AnActionCarryingItsOwnDeclarationIsNotReported()
    {
        Assert.Empty(ActionsWithoutTheirOwn<IAuthorizeData>(typeof(ProbeExplicitlyAuthorizedController)));
        Assert.Empty(ActionsWithoutTheirOwn<IAllowAnonymous>(typeof(ProbeExplicitlyAnonymousController)));
    }

    /// <summary>
    /// Every action of every controller the plugin places in a category declares
    /// its own requirement. This is vacuous today, because the plugin registers
    /// no controllers at all, and it is landed vacuous for the same reason the
    /// inventory above was: the first route added has to carry its requirement
    /// in the change that adds it, rather than inherit one from a class
    /// attribute that a later refactor can remove without turning anything red.
    /// </summary>
    [Fact]
    public void EveryActionOfEveryPlacedControllerCarriesItsOwnRequirement()
    {
        var placed = DiscoverControllers(typeof(Plugin).Assembly)
            .ToDictionary(name => name, name => typeof(Plugin).Assembly.GetType(name)!, StringComparer.Ordinal);

        var bare = new List<string>();
        foreach (var (name, type) in placed)
        {
            bare.AddRange(AdministratorControllers.Contains(name)
                ? ActionsWithoutTheirOwn<IAuthorizeData>(type)
                : ActionsWithoutTheirOwn<IAllowAnonymous>(type));
        }

        Assert.True(
            bare.Count == 0,
            "These actions carry no authorization declaration of their own: "
            + string.Join(", ", bare.OrderBy(name => name, StringComparer.Ordinal))
            + ". An administrator route declares what it requires on the action; the redemption route declares that it is public on the action. A requirement held only by the class disappears with the class attribute and takes every action under it with it.");
    }
}
