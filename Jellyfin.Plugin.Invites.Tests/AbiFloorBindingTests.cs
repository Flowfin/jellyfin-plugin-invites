using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The plugin assembly binds the server's assemblies at the floor the manifest
/// claims, and not at a newer release of the line.
/// </summary>
/// <remarks>
/// <para>
/// An assembly names the version of every assembly it was compiled against, and
/// a server binds such a reference only to a version at or above the one named.
/// <c>targetAbi</c> is the oldest server the manifest invites somebody to
/// install on, so a plugin compiled against a newer package than that passes the
/// catalogue's filter on the floor and is then refused by the server itself. The
/// archive built against 10.11.11 was <c>NotSupported</c> on a 10.11.0 server
/// and <c>Active</c> on 10.11.11, with the server's log naming the assembly it
/// could not load; that reading is on #155 and it is why
/// <c>Directory.Build.props</c> derives the package version from the floor.
/// </para>
/// <para>
/// What is compared is the built assembly's own reference table, read the way
/// the server reads it, against the manifest in the tree. A test of the project
/// file would hold a property; this holds the bytes a server binds, so a
/// package version typed anywhere on the way to the compiler is caught here
/// whatever the property says.
/// </para>
/// </remarks>
public class AbiFloorBindingTests
{
    /// <summary>
    /// Every server assembly the plugin references is named at exactly the
    /// version <c>targetAbi</c> declares.
    /// </summary>
    /// <remarks>
    /// Exactly rather than at most. A reference below the floor would bind on
    /// every server of the line and say that the plugin was compiled against a
    /// server the manifest does not claim, which is a different drift with the
    /// same repair: the two numbers come from one field.
    /// </remarks>
    [Fact]
    public void ThePluginBindsTheServersAssembliesAtTheFloorTheManifestClaims()
    {
        var floor = Version.Parse(PluginManifest.TargetAbi());

        var bound = typeof(Plugin).Assembly.GetReferencedAssemblies()
            .Where(IsAServerAssembly)
            .OrderBy(reference => reference.Name, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(bound);
        Assert.All(bound, reference => Assert.True(
            floor.Equals(reference.Version),
            reference.Name
            + " is bound at "
            + reference.Version
            + " and build.yaml declares targetAbi "
            + floor
            + ". A server of the line below the bound version admits the package on the floor and then refuses this assembly, so the manifest invites an install that cannot load."));
    }

    /// <summary>
    /// Whether a referenced assembly is one the server supplies.
    /// </summary>
    /// <param name="reference">The reference, as the plugin assembly names it.</param>
    /// <returns>True for the server's own assemblies.</returns>
    /// <remarks>
    /// The two prefixes are the ones the server's packages ship under. The
    /// framework's own assemblies are bound at the framework's version and are
    /// not the subject: a server on the floor and one on the newest release of
    /// the line run the same framework.
    /// </remarks>
    private static bool IsAServerAssembly(AssemblyName reference) =>
        reference.Name is not null
        && (reference.Name.StartsWith("MediaBrowser.", StringComparison.Ordinal)
            || reference.Name.StartsWith("Jellyfin.", StringComparison.Ordinal));
}
