using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The packaging manifest, <c>build.yaml</c>, read off the tree the suite was
/// built from.
/// </summary>
/// <remarks>
/// The file is found by walking up from the test binary until a directory
/// holds both the solution and the manifest, which is what the rest of this
/// suite does for a document. Nothing is written and nothing outside the
/// repository is read. One reader rather than one per test class, because the
/// value it hands back is the one every claim about the declared floor is made
/// against, and two readers of one field are two readers that can disagree.
/// </remarks>
internal static class PluginManifest
{
    /// <summary>
    /// The targetAbi build.yaml declares, read off the manifest itself.
    /// </summary>
    /// <returns>The declared value, without its quotation marks.</returns>
    internal static string TargetAbi()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var manifest = Path.Combine(directory.FullName, "build.yaml");
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            if (File.Exists(manifest) && File.Exists(solution))
            {
                var declared = File.ReadAllLines(manifest)
                    .FirstOrDefault(text => text.StartsWith("targetAbi:", StringComparison.Ordinal));

                Assert.NotNull(declared);
                return declared!["targetAbi:".Length..].Trim().Trim('"');
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds both Jellyfin.Plugin.Invites.sln and build.yaml, so this comparison read nothing. Failing rather than passing over an absent manifest.");
    }
}
