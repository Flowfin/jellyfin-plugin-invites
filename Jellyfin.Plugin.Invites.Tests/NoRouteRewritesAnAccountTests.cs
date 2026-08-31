using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MediaBrowser.Model.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A route written the way an undo button would arrive: it takes the policy an
/// operator wants put back. Nothing routes to this type; it is here so the leg
/// that reads parameter and return types can be shown to see one.
/// </summary>
public sealed class ProbePolicyTakingController : ControllerBase
{
    /// <summary>
    /// Accepts a policy from the request.
    /// </summary>
    /// <param name="policy">The policy a caller posted.</param>
    /// <returns>Nothing a caller reads. Nothing routes to this type.</returns>
    [HttpPost("probe/reapply")]
    public IActionResult Reapply([FromBody] UserPolicy policy) => Ok(policy);
}

/// <summary>
/// The same shape in the other direction: a route that hands a policy back, so
/// a page can render it and post it again. Nothing routes to this type.
/// </summary>
public sealed class ProbePolicyReturningController : ControllerBase
{
    /// <summary>
    /// Hands a policy back, wrapped the way this plugin's own reads are.
    /// </summary>
    /// <returns>Nothing a caller reads. Nothing routes to this type.</returns>
    [HttpGet("probe/policy")]
    public ActionResult<UserPolicy> Read() => new UserPolicy();
}

/// <summary>
/// A route that names no verb. The framework routes it for every method, so it
/// answers a POST, and nothing about the source says so. Nothing routes to this
/// type.
/// </summary>
public sealed class ProbeVerblessController : ControllerBase
{
    /// <summary>
    /// Carries a route and no method.
    /// </summary>
    /// <returns>Nothing a caller reads. Nothing routes to this type.</returns>
    [Route("probe/any-verb")]
    public IActionResult Anything() => Ok();
}

/// <summary>
/// The plugin offers no control that rewrites an account, and this is where
/// that is refused rather than observed.
/// </summary>
/// <remarks>
/// <para>
/// #94 asks for a view of what the plugin applied to an account and explicitly
/// not for a button that reapplies it, because reapplying is a write to an
/// account that already exists and #62 forbids exactly that. The reading last
/// taken on #94 is that the clause holds today because no such button can be
/// built rather than because anything asserts the operator surface has none,
/// and that nothing names a check that would catch the loss. This is that
/// check.
/// </para>
/// <para>
/// <b>Two legs, because the button arrives in two halves.</b> A control that
/// rewrites something needs a route that changes something, and a control that
/// rewrites a POLICY needs a policy to cross the boundary in one direction or
/// the other. The first leg holds the write inventory to a declared list with a
/// reason per entry; the second refuses a user policy on any action's
/// parameters or result at all.
/// </para>
/// <para>
/// <b>An action naming no verb is counted as a write.</b> The framework routes
/// such an action for every method, so it answers a POST while its source says
/// nothing about one. That is the cheaper mistake of the two this file is for,
/// and it is one attribute short of the correct spelling rather than a shape
/// somebody has to mean.
/// </para>
/// <para>
/// <b>What this does not reach.</b> It reads signatures. A route that builds a
/// policy inside its own body, names no policy type on its surface and hands it
/// somewhere is invisible here, which is the same bound
/// <c>AccountsAreNeverWrittenTests</c> states for the seam over the server's
/// accounts. What stops that shape today is that nothing in this plugin can
/// hand a policy to a server, and that is asserted there rather than here.
/// </para>
/// </remarks>
public class NoRouteRewritesAnAccountTests
{
    /// <summary>
    /// The HTTP methods that change something. A route reachable by one of
    /// these is a control an operator can be given a button for.
    /// </summary>
    private static readonly HashSet<string> Mutating = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST",
        "PUT",
        "PATCH",
        "DELETE",
    };

    /// <summary>
    /// Every write this plugin offers, with what it changes. None of them names
    /// an account, and a fourth entry here is a decision rather than an
    /// addition: it is a second thing an operator's click can alter, and #94's
    /// restraint is that the plugin alters nothing about an account it did not
    /// just create.
    /// </summary>
    private static readonly Dictionary<string, string> DeclaredWrites = new(StringComparer.Ordinal)
    {
        ["Jellyfin.Plugin.Invites.Controllers.InvitesController.Mint"] = "writes one invitation record",
        ["Jellyfin.Plugin.Invites.Controllers.InvitesController.Revoke"] = "writes the revocation onto one invitation record",
        ["Jellyfin.Plugin.Invites.Controllers.InvitesController.Rotate"] = "writes the secret the stored hashes are keyed with",
    };

    /// <summary>
    /// Discovers controllers the way the server does, which is what three other
    /// classes in this suite already do and for the reason
    /// <c>RouteInventoryTests</c> writes out: keying on a name or on an
    /// attribute would enumerate a different set from the one the server
    /// serves, and the difference would be the route nobody meant to publish.
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
    /// The action methods a controller declares, inside its own assembly. The
    /// bound is the one <c>RouteInventoryTests</c> records for the same walk: a
    /// public method inherited from a type outside the assembly is an action to
    /// the framework and is invisible here, and this plugin declares no such
    /// base.
    /// </summary>
    /// <param name="controller">The controller type to read.</param>
    /// <returns>Its action methods.</returns>
    private static IEnumerable<MethodInfo> Actions(Type controller)
    {
        var declared = new List<MethodInfo>();
        for (var type = controller; type is not null && type.Assembly == controller.Assembly; type = type.BaseType)
        {
            declared.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        }

        return declared
            .Where(method => !method.IsSpecialName)
            .Where(method => method.GetCustomAttributes(typeof(NonActionAttribute), inherit: true).Length == 0);
    }

    /// <summary>
    /// Whether a request that changes something can reach this action. An
    /// action naming a mutating method can; so can one naming no method at all,
    /// because the framework then routes it for every one.
    /// </summary>
    /// <param name="action">The action to read.</param>
    /// <returns><c>true</c> if a mutating request reaches it.</returns>
    private static bool ReachableByAWrite(MethodInfo action)
    {
        var declared = action.GetCustomAttributes(inherit: false)
            .OfType<IActionHttpMethodProvider>()
            .SelectMany(provider => provider.HttpMethods)
            .ToList();

        return declared.Count == 0 || declared.Any(Mutating.Contains);
    }

    /// <summary>
    /// Every action of every controller in an assembly that a write can reach.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The full names of those actions, ordered.</returns>
    private static IReadOnlyList<string> WritesIn(Assembly assembly) =>
        Controllers(assembly)
            .SelectMany(controller => Actions(controller)
                .Where(ReachableByAWrite)
                .Select(action => (action.DeclaringType?.FullName ?? controller.Name) + "." + action.Name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The types a signature mentions, with every generic argument opened.
    /// <c>Task&lt;ActionResult&lt;UserPolicy&gt;&gt;</c> mentions a policy, and
    /// a leg comparing the outer type alone would not see it.
    /// </summary>
    /// <param name="type">The type to open.</param>
    /// <returns>It and every type argument beneath it.</returns>
    private static IEnumerable<Type> Opened(Type type)
    {
        yield return type;

        if (type.HasElementType)
        {
            foreach (var inner in Opened(type.GetElementType()!))
            {
                yield return inner;
            }
        }

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var inner in Opened(argument))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    /// Every action of every controller in an assembly whose parameters or
    /// result mention a user policy.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The full names of those actions, ordered.</returns>
    private static IReadOnlyList<string> ActionsMentioningAPolicy(Assembly assembly) =>
        Controllers(assembly)
            .SelectMany(controller => Actions(controller)
                .Where(action => action.GetParameters()
                    .Select(parameter => parameter.ParameterType)
                    .Append(action.ReturnType)
                    .SelectMany(Opened)
                    .Any(mentioned => mentioned == typeof(UserPolicy)))
                .Select(action => (action.DeclaringType?.FullName ?? controller.Name) + "." + action.Name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Every route of this plugin that a write can reach is one of the writes
    /// declared above, and each of those changes an invitation or the secret
    /// its hashes are keyed with rather than an account.
    /// </summary>
    [Fact]
    public void EveryWriteThisPluginOffersIsOneItHasDeclared()
    {
        var undeclared = WritesIn(typeof(Plugin).Assembly)
            .Where(name => !DeclaredWrites.ContainsKey(name))
            .ToList();

        Assert.True(
            undeclared.Count == 0,
            "These routes can be reached by a request that changes something and are not declared in "
            + nameof(NoRouteRewritesAnAccountTests)
            + ": "
            + string.Join(", ", undeclared)
            + ". Every write an operator can be given a button for is listed there with what it changes. "
            + "#94 asks that this plugin offer no control that rewrites an account it did not just create, "
            + "and a write nobody placed in that list is a control nobody has said out loud does not.");
    }

    /// <summary>
    /// And the declared list holds nothing that has gone. A name left behind
    /// after its route was deleted makes the assertion above pass for a write
    /// that no longer exists, and would admit a new one under the old name.
    /// </summary>
    [Fact]
    public void EveryDeclaredWriteIsStillARouteThisPluginServes()
    {
        var served = WritesIn(typeof(Plugin).Assembly);

        var gone = DeclaredWrites.Keys
            .Where(name => !served.Contains(name, StringComparer.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            gone.Count == 0,
            "These writes are declared in " + nameof(NoRouteRewritesAnAccountTests)
            + " and this plugin serves no such route: " + string.Join(", ", gone)
            + ". Take the entry out in the change that takes the route out.");
    }

    /// <summary>
    /// No route of this plugin takes a user policy or hands one back. That is
    /// the half of an undo button that is about accounts rather than about
    /// writing: a route accepting one is a route an operator can post a policy
    /// to, and a route handing one back is the read half of a page that would
    /// then post it.
    /// </summary>
    [Fact]
    public void NoRouteOfThisPluginCarriesAUserPolicyInEitherDirection()
    {
        var carrying = ActionsMentioningAPolicy(typeof(Plugin).Assembly);

        Assert.True(
            carrying.Count == 0,
            "These routes carry a user policy on their surface: " + string.Join(", ", carrying)
            + ". A policy crossing the boundary in either direction is the undo button #62 refuses and "
            + "#94 asks not to be offered.");
    }

    /// <summary>
    /// The write leg sees a write, including the one that names no verb. Without
    /// this the assertions above would report the same thing for a plugin that
    /// offers no write and for a walk that stopped working.
    /// </summary>
    [Fact]
    public void TheWriteLegSeesAPostAndAnActionThatNamesNoVerbAtAll()
    {
        var found = WritesIn(typeof(ProbePolicyTakingController).Assembly);

        Assert.Contains(typeof(ProbePolicyTakingController).FullName + ".Reapply", found, StringComparer.Ordinal);
        Assert.Contains(typeof(ProbeVerblessController).FullName + ".Anything", found, StringComparer.Ordinal);

        // And it leaves a read alone, or it would report every route ever
        // written and its green mark over the plugin would mean nothing.
        Assert.DoesNotContain(typeof(ProbePolicyReturningController).FullName + ".Read", found, StringComparer.Ordinal);
    }

    /// <summary>
    /// The policy leg sees a policy in both directions, including one wrapped
    /// in the result type this plugin's own reads use.
    /// </summary>
    [Fact]
    public void ThePolicyLegSeesAPolicyArrivingAndAPolicyLeaving()
    {
        var found = ActionsMentioningAPolicy(typeof(ProbePolicyTakingController).Assembly);

        Assert.Contains(typeof(ProbePolicyTakingController).FullName + ".Reapply", found, StringComparer.Ordinal);
        Assert.Contains(typeof(ProbePolicyReturningController).FullName + ".Read", found, StringComparer.Ordinal);

        // And it leaves an action mentioning no policy alone.
        Assert.DoesNotContain(typeof(ProbeVerblessController).FullName + ".Anything", found, StringComparer.Ordinal);
    }
}
