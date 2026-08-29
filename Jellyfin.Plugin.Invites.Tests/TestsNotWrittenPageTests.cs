using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The workflow jobs <c>docs/tests-not-written.md</c> says put a question to a
/// real server are the jobs that do, in both directions.
/// </summary>
/// <remarks>
/// <para>
/// That page is where a test this suite refuses is written down with what
/// covers the same risk instead, so its real-server row is the map somebody
/// reads to find out what has been asked of an actual server. A job missing
/// from it is a job nobody counts, and a name left on it after the job went is
/// a page that reads as a complete map while covering less than it says.
/// </para>
/// <para>
/// It has already been wrong in the first direction. The row named two jobs
/// while four had landed, so a reader auditing the refusal against the tree
/// would have found half of what answers for it. The page said of itself that
/// nothing reads it. This is that.
/// </para>
/// <para>
/// <b>What is refused.</b> A workflow that starts a pinned published server
/// image and is not named in the block on that page, and a workflow named in
/// that block which starts no such image. A third leg asks that the block was
/// found and that the tree holds such a workflow at all, so a page reworded
/// past the pattern reds rather than reporting the same silence as a tree that
/// agrees.
/// </para>
/// <para>
/// <b>What it matches.</b> The image reference itself, pinned by digest, which
/// is how every one of these jobs names the server it starts. A job that
/// reached a server some other way, or that assembled the reference out of
/// parts, would be invisible to this, and so would a mention of the image in a
/// comment being read as a job that starts one. Whether the sentence beside a
/// name describes what that job actually asks the server is a judgement no
/// reading of the tree makes.
/// </para>
/// <para>
/// <b>The means.</b> A test in this suite rather than a rule in
/// <c>.github/lint/invariants.sh</c>. That rule set matches a spelling and
/// subtracts lines from a match; it has no way to say that a match is required
/// in one document and means nothing anywhere else, which is exactly this
/// property. The suite already reads a tracked document this way in
/// <see cref="LoggingPageTests"/> and reads tracked text outside the plugin
/// project in <see cref="SuiteDirectoryTests"/>, it runs on the same command as
/// everything else, and it opens no network connection and writes nothing.
/// </para>
/// </remarks>
public class TestsNotWrittenPageTests
{
    /// <summary>
    /// The block on the page that names them, from its lead-in to the end of
    /// its paragraph, so a workflow written elsewhere on the page is not read
    /// as a claim about a real server.
    /// </summary>
    private static readonly Regex _theBlock = new(
        @"The jobs that put a question to a real server are these:(.*?)\r?\n[ \t]*\r?\n",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A workflow file named by its path in the repository.
    /// </summary>
    private static readonly Regex _workflowPath = new(
        @"\.github/workflows/([A-Za-z0-9._-]+\.ya?ml)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The published server image, pinned by digest, which is how a job that
    /// starts a real server names it here.
    /// </summary>
    private static readonly Regex _startsAServer = new(
        @"jellyfin/jellyfin@sha256:",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The page names workflows and the directory holds jobs that start a
    /// server. Without this, a block reworded past the pattern and a directory
    /// that had stopped starting servers would both report the same silence as
    /// a tree that agrees, and the two legs below would compare empty sets.
    /// </summary>
    [Fact]
    public void TheScanFindsTheServerJobsThePageNames()
    {
        Assert.NotEmpty(Named());
        Assert.NotEmpty(JobsThatStartAServer());
    }

    /// <summary>
    /// Every job that starts a real server is named on the page. This is the
    /// drift that happened: two more such jobs landed and the row went on
    /// naming the first two.
    /// </summary>
    [Fact]
    public void EveryJobThatStartsARealServerIsNamedOnThePage()
    {
        var named = Named();

        var unmentioned = JobsThatStartAServer()
            .Where(job => !named.Contains(job))
            .OrderBy(job => job, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unmentioned.Count == 0,
            "These workflows start a pinned published Jellyfin server and docs/tests-not-written.md does not name them where it lists the jobs that put a question to a real server: "
            + string.Join(", ", unmentioned)
            + ". That row is the map an audit of this refusal is run off, so a job missing from it is a job nobody counts.");
    }

    /// <summary>
    /// Every workflow the page names there starts a real server. Without it the
    /// repair rots the other way: a name kept after the job was renamed or
    /// stopped starting a server leaves the page reading as a complete map.
    /// </summary>
    [Fact]
    public void EveryWorkflowThePageNamesThereStartsARealServer()
    {
        var starting = JobsThatStartAServer();

        var silent = Named()
            .Where(name => !starting.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            silent.Count == 0,
            "docs/tests-not-written.md lists these as jobs that put a question to a real server and no such workflow in .github/workflows names the pinned published server image: "
            + string.Join(", ", silent)
            + ". Either the name is stale or the job stopped starting a server, and the page reads as a complete map either way.");
    }

    /// <summary>
    /// The workflows the page names in that block.
    /// </summary>
    /// <returns>The file names, as the page writes them.</returns>
    private static HashSet<string> Named()
    {
        var block = _theBlock.Match(Page());
        Assert.True(
            block.Success,
            "docs/tests-not-written.md no longer carries the block naming the jobs that put a question to a real server, so these legs read nothing. Failing rather than passing over a page that has stopped being read.");

        return new HashSet<string>(
            _workflowPath.Matches(block.Groups[1].Value).Select(match => match.Groups[1].Value),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The workflow files that start a pinned published server image.
    /// </summary>
    /// <returns>The file names.</returns>
    private static HashSet<string> JobsThatStartAServer()
    {
        var starting = Workflows()
            .Where(path => _startsAServer.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetFileName(path));

        return new HashSet<string>(starting, StringComparer.Ordinal);
    }

    /// <summary>
    /// Every workflow file of this repository.
    /// </summary>
    /// <remarks>
    /// Found by walking up from the test binary until a directory holds the
    /// solution, the page and the workflow directory, which is how the other
    /// legs over a tracked file find one: the number of levels under the binary
    /// moves with the configuration and the target framework, and the marker
    /// does not.
    /// </remarks>
    /// <returns>The paths, in a fixed order.</returns>
    private static IReadOnlyList<string> Workflows()
    {
        var directory = Path.Combine(Root(), ".github", "workflows");

        var files = Directory
            .EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".yaml", StringComparison.Ordinal)
                || path.EndsWith(".yml", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(files);

        return files;
    }

    /// <summary>
    /// The page.
    /// </summary>
    /// <returns>The text of the page.</returns>
    private static string Page() =>
        File.ReadAllText(Path.Combine(Root(), "docs", "tests-not-written.md"));

    /// <summary>
    /// The root of the repository.
    /// </summary>
    /// <returns>The directory holding the solution, the page and the workflows.</returns>
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            var page = Path.Combine(directory.FullName, "docs", "tests-not-written.md");
            var workflows = Path.Combine(directory.FullName, ".github", "workflows");
            if (File.Exists(solution) && File.Exists(page) && Directory.Exists(workflows))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds Jellyfin.Plugin.Invites.sln, docs/tests-not-written.md and .github/workflows, so these legs read nothing. Failing rather than reporting a comparison that ran over an empty set.");
    }
}
