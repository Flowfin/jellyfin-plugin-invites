using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The suite waits for nothing on the machine clock.
/// </summary>
/// <remarks>
/// <para>
/// #104 ends with a clause about the suite rather than about a boundary: no test
/// sleeps. The reason is written at the headless rule in <c>CONTRIBUTING.md</c>
/// and it is about the suite staying worth running: four behaviours here are
/// clock-driven, and a suite that waits on a real clock gets slower until people
/// stop running it. A wait is also the weaker assertion, because it can only
/// reach the far side of a boundary while an injected clock reaches the instant
/// itself, which is what <c>docs/tests-not-written.md</c> says of the test it
/// refuses.
/// </para>
/// <para>
/// <b>Why it is a leg and not a sentence.</b> Six files in this suite say in
/// their own comments that nothing in them sleeps, and until this class nothing
/// refused one. A rule carried only by comments is a rule the next test class
/// does not know about, and the shape somebody actually writes is a wait added
/// to a concurrency test to make a race come out the same way twice. That is the
/// one-character version of the mistake: the test passes, it passes on every
/// rerun, and the seconds it costs are paid by everybody afterwards.
/// </para>
/// <para>
/// <b>The means, and why it is not the greppable lint.</b> A source-text rule is
/// what this property wants and <c>.github/lint/invariants.sh</c> is where those
/// live, but every rule in that file is the machine-readable half of a rule
/// about the plugin's own source, and the subject here is the test project. It
/// is also a file changed under an issue of its own, and a rule landed into it
/// from here would be two hands on one file. <see cref="SuiteDirectoryTests"/>
/// took the same decision for the same reasons and this class follows it.
/// </para>
/// <para>
/// <b>Its bound, stated rather than left to be found.</b> This reads text. A
/// wait reached through a helper, through an alias, or through a package this
/// suite calls walks past every leg below, and so does one inside the plugin
/// that a test drives. What it refuses is the spelling written in a test file,
/// which is the spelling somebody reaches for.
/// </para>
/// <para>
/// Every needle is assembled from two pieces, because this file is inside what
/// it reads. Written out, this class would be one of its own matches and the
/// legs would be reporting on their own text instead of on the suite.
/// </para>
/// </remarks>
public class SuiteDoesNotSleepTests
{
    /// <summary>
    /// The ways a test could wait on the machine clock, one per case, with what
    /// each one does.
    /// </summary>
    /// <returns>The needle and the sentence naming what it does.</returns>
    public static TheoryData<string, string> Waits() => new()
    {
        { "Thread." + "Sleep", "blocks the running test until the machine clock has moved" },
        { "Task." + "Delay", "the same wait, awaited rather than blocked" },
        { "Spin" + "Until", "burns the processor until a condition holds or a deadline passes" },
    };

    /// <summary>
    /// No test in this suite waits on the machine clock.
    /// </summary>
    /// <param name="needle">The spelling that would wait.</param>
    /// <param name="what">What that spelling does, for the failure message.</param>
    [Theory]
    [MemberData(nameof(Waits))]
    public void NoWaitOnTheMachineClockIsNamedInTheSuite(string needle, string what)
    {
        var named = Sources()
            .Where(source => File.ReadAllText(source).Contains(needle, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            named.Length == 0,
            needle
            + " "
            + what
            + ", which is the wait the headless rule refuses and #104 asks this suite not to carry. Named in: "
            + string.Join(", ", named)
            + ". Drive the behaviour through IClock instead, and where the wait is for another thread rather than for the clock, take a handle that is signalled.");
    }

    /// <summary>
    /// The source files of this suite.
    /// </summary>
    /// <remarks>
    /// Found by walking up from the test binary until a directory holds the
    /// solution and the test project, which is how the other legs over this
    /// suite's own text find it: the number of levels under the binary moves
    /// with the configuration and the target framework, and the marker does not.
    /// Build output is skipped, because the generated files under it are not
    /// sources anybody writes. Nothing is written and nothing outside the
    /// repository is read.
    /// </remarks>
    /// <returns>Every C# source file in the test project.</returns>
    private static IReadOnlyList<string> Sources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var project = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.Tests");
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            if (File.Exists(solution) && Directory.Exists(project))
            {
                var sources = Directory
                    .EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
                    .Where(path => !Generated(project, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList();

                Assert.NotEmpty(sources);

                return sources;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds both Jellyfin.Plugin.Invites.sln and the test project, so this leg read nothing. Failing rather than reporting a rule that ran over an empty set.");
    }

    /// <summary>
    /// Whether a path is build output rather than a source somebody wrote.
    /// </summary>
    /// <param name="project">The test project directory.</param>
    /// <param name="path">The file.</param>
    /// <returns>True when it sits under bin or obj.</returns>
    private static bool Generated(string project, string path)
    {
        var relative = Path.GetRelativePath(project, path)
            .Replace('\\', '/');

        return relative.StartsWith("bin/", StringComparison.Ordinal)
            || relative.StartsWith("obj/", StringComparison.Ordinal);
    }
}
