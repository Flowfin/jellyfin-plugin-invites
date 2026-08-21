using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// This plugin works alone, and what makes that a statement rather than a hope
/// is that it consumes nothing another plugin provides.
/// </summary>
/// <remarks>
/// <para>
/// #44 asks that the plugin work alone and work beside every supported sibling.
/// The sibling set is empty today, so the second half has nothing to run
/// against and is a refusal in <c>docs/tests-not-written.md</c> rather than a
/// test. This file holds the half that is decidable now: a plugin that
/// consumes no sibling cannot be broken by one being absent, so the graceful
/// degradation the issue asks for has no case to cover.
/// </para>
/// <para>
/// <b>What this cannot see.</b> It reads what the assembly references and what
/// the package ships. A sibling reached by reflection over the server's service
/// collection, or by a type name resolved from a string, is invisible to both
/// and always will be. What it removes is the case where a reference is added
/// and nobody notices that the sentence about working alone stopped being true.
/// </para>
/// </remarks>
public class NoSiblingIsConsumedTests
{
    /// <summary>
    /// The prefix every Jellyfin plugin assembly in this family carries.
    /// </summary>
    private const string PluginPrefix = "Jellyfin.Plugin.";

    /// <summary>
    /// Nothing this assembly is compiled against is another plugin.
    /// </summary>
    /// <remarks>
    /// What it does reference are the server's own contract assemblies, the
    /// MediaBrowser ones the Jellyfin.Controller and Jellyfin.Model packages
    /// carry, which every plugin compiles against and no plugin provides.
    /// </remarks>
    [Fact]
    public void NothingThisPluginIsCompiledAgainstIsAnotherPlugin()
    {
        var assembly = typeof(Plugin).Assembly;

        var siblings = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith(PluginPrefix, StringComparison.Ordinal))
            .Where(name => !string.Equals(name, assembly.GetName().Name, StringComparison.Ordinal))
            .ToList();

        Assert.True(
            siblings.Count == 0,
            "This plugin is compiled against " + string.Join(", ", siblings)
            + ". It declares that it consumes no sibling, and docs/tests-not-written.md refuses the"
            + " together case on the ground that there is no sibling to test against. A reference"
            + " here makes both of those sentences wrong.");
    }

    /// <summary>
    /// And the reading above is looking at something.
    /// </summary>
    /// <remarks>
    /// Without this, an assembly whose references could not be read would
    /// report the same empty list as one that genuinely has no sibling, and the
    /// test above would pass for the wrong reason forever.
    /// </remarks>
    [Fact]
    public void TheReferenceListIsNotEmpty()
    {
        var referenced = typeof(Plugin).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToList();

        // The names are the server's own assemblies rather than the package
        // ids that carry them: Jellyfin.Controller and Jellyfin.Model ship
        // MediaBrowser.*, which is what an assembly reference records.
        Assert.Contains("MediaBrowser.Common", referenced);
        Assert.Contains("MediaBrowser.Model", referenced);
    }

    /// <summary>
    /// The package ships this plugin's own assembly and nothing else.
    /// </summary>
    /// <remarks>
    /// The artefact list in <c>build.yaml</c> is what a server unpacks into its
    /// plugin directory. A second file there would be this plugin putting
    /// somebody else's code on the server, which is the other direction of
    /// working alone and is not visible in the reference list above.
    /// </remarks>
    [Fact]
    public void ThePackageShipsNothingButThisPlugin()
    {
        var artefacts = ArtefactsInBuildManifest();

        var only = Assert.Single(artefacts);
        Assert.Equal(typeof(Plugin).Assembly.GetName().Name + ".dll", only);
    }

    /// <summary>
    /// Reads the artefact list out of build.yaml in the working tree.
    /// </summary>
    /// <remarks>
    /// Read as lines rather than parsed, because a parser is a dependency and
    /// the shape being read is a flat list of strings under one key. The file
    /// is found by walking up for the solution beside it, which is the same
    /// route <c>ApiDocumentTests</c> takes and for the same reason: the number
    /// of directories under the test binary moves with the configuration and
    /// the marker does not.
    /// </remarks>
    /// <returns>The file names the manifest lists.</returns>
    private static string[] ArtefactsInBuildManifest()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var manifest = Path.Combine(directory.FullName, "build.yaml");
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            if (File.Exists(manifest) && File.Exists(solution))
            {
                return Read(File.ReadAllLines(manifest));
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds both Jellyfin.Plugin.Invites.sln and build.yaml, so this read nothing."
            + " Failing rather than passing over an absent manifest.");
    }

    /// <summary>
    /// The entries under the artefact key, in order.
    /// </summary>
    /// <param name="lines">The lines of the manifest.</param>
    /// <returns>Each entry, unquoted.</returns>
    private static string[] Read(string[] lines)
    {
        var start = Array.FindIndex(lines, line => line.StartsWith("artifacts:", StringComparison.Ordinal));
        if (start < 0)
        {
            throw new InvalidOperationException(
                "build.yaml carries no artifacts key, so what a server would unpack is not readable here.");
        }

        return lines
            .Skip(start + 1)
            .TakeWhile(line => line.StartsWith("- ", StringComparison.Ordinal))
            .Select(line => line[2..].Trim().Trim('"'))
            .ToArray();
    }
}
