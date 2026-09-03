using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Invites.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A running server that reports whatever the test hands it, standing in for
/// the server's own application host.
/// </summary>
/// <remarks>
/// One member and it reads, which is the whole seam. A version of <c>null</c>
/// is the server that reports none, and it is a case rather than an oversight.
/// </remarks>
internal sealed class StubRunningServer : IRunningServer
{
    public StubRunningServer(Version? version)
    {
        Version = version;
    }

    public Version? Version { get; }
}

/// <summary>
/// A controller of this suite rather than of the plugin. It is what the scope
/// of the convention is proved against: a rule that attached the refusal to
/// every controller in the model would be this plugin deciding whether somebody
/// else's route answers.
/// </summary>
public sealed class ForeignProbeController : ControllerBase
{
    /// <summary>
    /// An action, so the type is a controller for the reason a real one is.
    /// </summary>
    /// <returns>Nothing a caller reads. Nothing routes to this type.</returns>
    [HttpGet("foreign-probe")]
    public IActionResult Probe() => Ok();
}

/// <summary>
/// The comparison between the Jellyfin line this plugin declares and the server
/// it is loaded into, and the refusal that follows a mismatch.
/// </summary>
/// <remarks>
/// <para>
/// #97 decided on 2026-08-20 that the comparison is equality on the major and
/// minor parts rather than a floor, and that the price of it is stranding
/// invitations on the day an operator moves to the next line. What is asserted
/// here is the rule, not the price.
/// </para>
/// <para>
/// <b>What no test here reaches.</b> Nothing starts a server. That the server
/// applies a convention a plugin adds to its <c>MvcOptions</c>, and that a
/// refusal set by a filter is what a browser receives, are claims about a
/// running Jellyfin, and docs/manual-checks.md is the register those belong in.
/// The assertions below are over the comparison, over the filter's own
/// behaviour, and over the model the convention is handed.
/// </para>
/// </remarks>
public class ServerLineTests
{
    /// <summary>
    /// A line no server runs, used wherever a fixture needs a line rather than
    /// this plugin's own. Written here so an assertion about the rule cannot
    /// pass or fail because build.yaml moved.
    /// </summary>
    private const string AFixtureLine = "42.7";

    /// <summary>
    /// The declared line is the one build.yaml names, and there is no second
    /// copy of it in the source.
    /// </summary>
    /// <remarks>
    /// This is the property the whole file rests on. A constant typed into a
    /// class beside the manifest would compile, package and pass every other
    /// check here, and the copy that went stale would be the one deciding
    /// whether the plugin runs at all. The number reaches the assembly through
    /// the project file, so what this compares is the manifest against the built
    /// artefact rather than one string against another.
    /// </remarks>
    [Fact]
    public void TheDeclaredLineIsTheOneTargetAbiNames()
    {
        var line = string.Join('.', PluginManifest.TargetAbi().Split('.').Take(2));

        Assert.Equal(line, DeclaredLine.Value);
    }

    /// <summary>
    /// An assembly that carries no declaration is refused rather than read as
    /// some default. A plugin that could not say which line it was built for
    /// would compare nothing.
    /// </summary>
    [Fact]
    public void AnAssemblyCarryingNoDeclarationIsRefused()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => DeclaredLine.Of(typeof(Assert).Assembly));

        Assert.Contains(DeclaredLine.MetadataKey, thrown.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// What the comparison answers, one row per case. The rows that are not the
    /// happy path are the reason this table exists: a patch version inside the
    /// line is what a stricter comparison gets wrong. The two rows pairing 10.1
    /// with 10.11 are the prefix trap, one in each direction: a comparison
    /// written as a starts-with test rather than as equality answers one of them
    /// wrongly whichever way round it is written.
    /// </summary>
    /// <returns>The declared line, what the server reports, and whether the
    /// plugin may run.</returns>
    public static TheoryData<string, Version, bool> Servers() => new()
    {
        { "10.11", new Version(10, 11, 0), true },
        { "10.11", new Version(10, 11, 11), true },
        { "10.11", new Version(10, 11, 0, 0), true },
        { "10.11", new Version(10, 12, 0), false },
        { "10.11", new Version(10, 1, 0), false },
        { "10.1", new Version(10, 11, 0), false },
        { "10.11", new Version(11, 11, 0), false },
        { "10.11", new Version(12, 0, 0), false },
        { "10.11", new Version(1, 0), false },
    };

    /// <summary>
    /// The comparison is equality on the major and minor parts.
    /// </summary>
    /// <param name="declared">The line the plugin declares.</param>
    /// <param name="running">What the server reports.</param>
    /// <param name="mayRun">Whether the plugin may run.</param>
    [Theory]
    [MemberData(nameof(Servers))]
    public void TheComparisonIsEqualityOnTheLine(string declared, Version running, bool mayRun)
    {
        Assert.Equal(mayRun, ServerLine.Judge(declared, running).Matches);
    }

    /// <summary>
    /// A server that reports no version at all is refused rather than assumed
    /// to be on the line. Reading an unanswered question as agreement is how a
    /// comparison ends up passing everything the day the member it reads moves.
    /// </summary>
    [Fact]
    public void AServerThatReportsNoVersionIsRefused()
    {
        var verdict = ServerLine.Judge(AFixtureLine, null);

        Assert.False(verdict.Matches);
        Assert.Contains(AFixtureLine, verdict.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A refusal names both versions, which is the clause #97 writes. An
    /// operator who reads only this line has to be able to see what the plugin
    /// wanted and what it found.
    /// </summary>
    [Fact]
    public void ARefusalNamesBothVersions()
    {
        var verdict = ServerLine.Judge(AFixtureLine, new Version(9, 3, 1));

        Assert.False(verdict.Matches);
        Assert.Contains(AFixtureLine, verdict.Message, StringComparison.Ordinal);
        Assert.Contains("9.3.1", verdict.Message, StringComparison.Ordinal);
        Assert.Equal(AFixtureLine, verdict.Declared);
        Assert.Equal("9.3.1", verdict.Running);
    }

    /// <summary>
    /// A mismatch answers the action instead of the action running.
    /// </summary>
    /// <remarks>
    /// The result being set in <c>OnActionExecuting</c> is what short-circuits
    /// the pipeline. A filter that recorded the refusal and let the action run
    /// would create the account and then hide the answer.
    /// </remarks>
    [Fact]
    public void AMismatchAnswersTheActionWithARefusalCarryingBothVersions()
    {
        var gate = new ServerLineGate(AFixtureLine, new StubRunningServer(new Version(9, 3, 1)));
        var context = AnAction();

        new RefuseOnAServerLineMismatch(gate).OnActionExecuting(context);

        var refusal = Assert.IsType<ContentResult>(context.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, refusal.StatusCode);
        Assert.Equal(RefuseOnAServerLineMismatch.ContentType, refusal.ContentType);
        Assert.Equal(gate.Verdict.Message, refusal.Content);
    }

    /// <summary>
    /// A server on the declared line is left alone. Without this the assertion
    /// above would hold equally for a filter that refused everything, which is a
    /// plugin that never runs anywhere.
    /// </summary>
    [Fact]
    public void AServerOnTheDeclaredLineIsLeftAlone()
    {
        var gate = new ServerLineGate(AFixtureLine, new StubRunningServer(new Version(42, 7, 3)));
        var context = AnAction();

        new RefuseOnAServerLineMismatch(gate).OnActionExecuting(context);

        Assert.True(gate.MayRun);
        Assert.Null(context.Result);
    }

    /// <summary>
    /// Every controller the plugin assembly holds carries the refusal, and the
    /// list is derived from the assembly rather than written here, so a
    /// controller added later carries it without anybody remembering to.
    /// </summary>
    [Fact]
    public void EveryControllerThisPluginHoldsCarriesTheRefusal()
    {
        var controllers = ControllersOf(typeof(Plugin).Assembly);
        Assert.NotEmpty(controllers);

        var application = AModelOf(controllers);
        new ThisPluginsControllers(typeof(Plugin).Assembly).Apply(application);

        Assert.All(
            application.Controllers,
            controller => Assert.Contains(
                controller.Filters.OfType<ServiceFilterAttribute>(),
                filter => filter.ServiceType == typeof(RefuseOnAServerLineMismatch)));
    }

    /// <summary>
    /// A controller declared outside this plugin is left alone. The model a
    /// convention is handed is the server's whole application model, so the
    /// mistake this refuses is the plugin deciding whether the server's own
    /// routes answer.
    /// </summary>
    [Fact]
    public void AControllerOutsideThisPluginIsLeftAlone()
    {
        var application = AModelOf(
            ControllersOf(typeof(Plugin).Assembly)
                .Append(typeof(ForeignProbeController))
                .ToList());

        new ThisPluginsControllers(typeof(Plugin).Assembly).Apply(application);

        var foreign = Assert.Single(
            application.Controllers,
            controller => controller.ControllerType.AsType() == typeof(ForeignProbeController));

        Assert.Empty(foreign.Filters);
    }

    /// <summary>
    /// The scope is the assembly it was given rather than a name. Pointed at the
    /// suite's own assembly it attaches the refusal to the suite's controller
    /// and to none of the plugin's, which is the same rule read from the other
    /// end.
    /// </summary>
    [Fact]
    public void TheScopeIsTheAssemblyItWasGiven()
    {
        var application = AModelOf(
            ControllersOf(typeof(Plugin).Assembly)
                .Append(typeof(ForeignProbeController))
                .ToList());

        new ThisPluginsControllers(typeof(ForeignProbeController).Assembly).Apply(application);

        var carrying = application.Controllers
            .Where(controller => controller.Filters.Count > 0)
            .Select(controller => controller.ControllerType.AsType())
            .ToList();

        Assert.Equal([typeof(ForeignProbeController)], carrying);
    }

    /// <summary>
    /// What reads as a line and what does not, one row per case.
    /// </summary>
    /// <returns>The declared value and whether it reads as a line.</returns>
    public static TheoryData<string?, bool> Declarations() => new()
    {
        { "10.11", true },
        { "0.0", true },
        { null, false },
        { string.Empty, false },
        { "   ", false },
        { "10", false },
        { "10.", false },
        { "10.11.0", false },
        { "10.11.0.0", false },
        { "10.x", false },
        { "v10.11", false },
    };

    /// <summary>
    /// A declaration is two dot-separated numbers or it is refused.
    /// </summary>
    /// <param name="value">The declared value.</param>
    /// <param name="reads">Whether it reads as a line.</param>
    /// <remarks>
    /// The four-part row is the one worth having. targetAbi is four parts, so a
    /// project file that handed the whole field across rather than its first two
    /// parts would produce a declaration that looks right and matches no server.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Declarations))]
    public void ADeclarationIsTwoNumbersOrItIsNotALine(string? value, bool reads)
    {
        Assert.Equal(reads, DeclaredLine.IsALine(value));
    }

    /// <summary>
    /// The gate the server builds judges against the line the assembly carries,
    /// rather than against one a caller chose.
    /// </summary>
    /// <remarks>
    /// Every other assertion here drives both sides of the comparison, which is
    /// what makes them independent of build.yaml. This one is the production
    /// constructor, and it is the one that would still be green if the declared
    /// line were never read at all.
    /// </remarks>
    [Fact]
    public void TheGateTheServerBuildsJudgesAgainstTheDeclaredLine()
    {
        var parts = DeclaredLine.Value.Split('.');
        var onTheLine = new Version(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture), 4);

        Assert.True(new ServerLineGate(new StubRunningServer(onTheLine)).MayRun);
        Assert.False(new ServerLineGate(new StubRunningServer(new Version(0, 0, 1))).MayRun);
        Assert.Equal(DeclaredLine.Value, new ServerLineGate(new StubRunningServer(null)).Verdict.Declared);
    }

    /// <summary>
    /// The convention the server is handed is scoped to this plugin's assembly
    /// without being told which one that is.
    /// </summary>
    [Fact]
    public void TheConventionTheServerIsHandedIsScopedToThisPlugin()
    {
        var application = AModelOf(
            ControllersOf(typeof(Plugin).Assembly)
                .Append(typeof(ForeignProbeController))
                .ToList());

        new ThisPluginsControllers().Apply(application);

        var carrying = application.Controllers
            .Where(controller => controller.Filters.Count > 0)
            .Select(controller => controller.ControllerType.AsType())
            .ToList();

        Assert.NotEmpty(carrying);
        Assert.DoesNotContain(typeof(ForeignProbeController), carrying);
    }

    /// <summary>
    /// An action that ran is not judged again on the way out. The refusal has
    /// one moment, which is before the action, and a second one after it would
    /// be a result discarded rather than an action prevented.
    /// </summary>
    [Fact]
    public void AnActionThatRanIsNotJudgedOnTheWayOut()
    {
        var gate = new ServerLineGate(AFixtureLine, new StubRunningServer(new Version(9, 3, 1)));
        var action = AnAction();
        var executed = new ActionExecutedContext(action, action.Filters, action.Controller);

        new RefuseOnAServerLineMismatch(gate).OnActionExecuted(executed);

        Assert.Null(executed.Result);
    }

    /// <summary>
    /// An action context with nothing in it, which is all the filter reads.
    /// </summary>
    /// <returns>The context.</returns>
    private static ActionExecutingContext AnAction()
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(StringComparer.Ordinal),
            controller: new object());
    }

    /// <summary>
    /// The controller types an assembly holds, discovered the way the server
    /// discovers them rather than by a name ending in Controller.
    /// </summary>
    /// <param name="assembly">The assembly to read as an application part.</param>
    /// <returns>The controller types.</returns>
    private static IReadOnlyList<Type> ControllersOf(Assembly assembly)
    {
        var manager = new ApplicationPartManager();
        manager.ApplicationParts.Add(new AssemblyPart(assembly));
        manager.FeatureProviders.Add(new ControllerFeatureProvider());

        var feature = new ControllerFeature();
        manager.PopulateFeature(feature);

        return feature.Controllers.Select(controller => controller.AsType()).ToList();
    }

    /// <summary>
    /// An application model holding those controllers and nothing else.
    /// </summary>
    /// <param name="controllers">The controller types.</param>
    /// <returns>The model.</returns>
    private static ApplicationModel AModelOf(IReadOnlyList<Type> controllers)
    {
        var application = new ApplicationModel();
        foreach (var controller in controllers)
        {
            application.Controllers.Add(new ControllerModel(controller.GetTypeInfo(), []));
        }

        return application;
    }
}
