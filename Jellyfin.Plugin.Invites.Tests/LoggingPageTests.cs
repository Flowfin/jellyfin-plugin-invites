using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The routines <c>docs/logging.md</c> says write log lines are the routines
/// that write log lines, in both directions.
/// </summary>
/// <remarks>
/// <para>
/// That page is the never list, and it opens by naming what does log today and
/// what those lines carry. It is therefore the map somebody audits by: a reader
/// checking the never list against the source uses it to decide where to look.
/// </para>
/// <para>
/// It has already been wrong in the direction that matters. The page said one
/// routine logged while two did, and ten of the thirteen calls in the plugin
/// were in a file it did not mention, so an audit run off it would have read
/// three of them and reported the plugin clean. The page says of itself, under
/// what it does not settle, that no check reads it and no check counts log
/// calls, so the next routine to start logging arrives unmentioned exactly as
/// the retention sweep did. This is that.
/// </para>
/// <para>
/// <b>What is refused.</b> A plugin source file holding a logging call whose
/// declared types are none of the ones the page names, and a routine the page
/// names that declares nowhere in the plugin or declares somewhere that logs
/// nothing. The first is the drift that happened; the second is its repair
/// rotting later, when a routine stops logging or is renamed and the page keeps
/// the old name and reads as a complete map.
/// </para>
/// <para>
/// <b>What it matches.</b> The same expression the page hands its reader, so
/// this and that command answer the same question rather than two neighbouring
/// ones. It is source text: a logging call written through a helper, or on a
/// logger reached some other way, is invisible to both, and so is one named in
/// a comment being counted as real. Whether the sentences beside a routine's
/// name describe what it actually writes is a judgement no reading of the tree
/// makes, and the never list itself is refused by two rules in
/// <c>.github/lint/invariants.sh</c> rather than here.
/// </para>
/// <para>
/// <b>The means.</b> A test in this suite rather than a rule in
/// <c>.github/lint/invariants.sh</c>. The rule set there matches a spelling and
/// subtracts lines from a match; it has no way to say that a match is legal in
/// the files one document names and refused everywhere else, which is exactly
/// this property. The suite already reads a tracked document this way and reads
/// source text in <see cref="SuiteDirectoryTests"/>, and it runs on the same
/// command as everything else.
/// </para>
/// <para>
/// Both sets are built through a constructor rather than through
/// <c>ToHashSet</c>, because <c>secret-compared-through-a-comparer</c> in
/// <c>.github/lint/invariants.sh</c> refuses a line carrying a word holding
/// <c>hash</c> beside a <c>StringComparer</c> and exempts the type's own name
/// rather than every spelling it appears in. Nothing here compares a secret;
/// the spelling is the whole of what that rule reads, which its own record says
/// of it.
/// </para>
/// </remarks>
public class LoggingPageTests
{
    /// <summary>
    /// The sentence on the page that names them, and nothing else on it, so a
    /// backticked name written elsewhere is not read as a claim about logging.
    /// </summary>
    private static readonly Regex _theSentence = new(
        @"The routines in this plugin that write log lines today are ([^.]*)\.",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A name written in backticks.
    /// </summary>
    private static readonly Regex _backticked = new(
        @"`([A-Za-z_][A-Za-z0-9_]*)`",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A logging call, in the spelling the page's own command matches.
    /// </summary>
    private static readonly Regex _logsSomething = new(
        @"\bLog(Information|Warning|Error|Debug|Trace|Critical)\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A type declaration.
    /// </summary>
    private static readonly Regex _declares = new(
        @"\b(?:class|record|struct|interface)\s+([A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The page names routines and the plugin holds logging calls. Without
    /// this, a sentence reworded past the pattern and a plugin that had stopped
    /// logging would both report the same silence as a tree that agrees, and
    /// the two legs below would compare two empty sets.
    /// </summary>
    [Fact]
    public void TheScanFindsTheRoutinesTheLoggingPageNames()
    {
        Assert.NotEmpty(Named());
        Assert.NotEmpty(RoutinesThatLog());
    }

    /// <summary>
    /// Every routine that logs is named on the page. This is the drift that
    /// happened: a second routine started writing log lines and the page went
    /// on describing the first.
    /// </summary>
    [Fact]
    public void EveryRoutineThatLogsIsNamedOnTheLoggingPage()
    {
        var named = Named();

        var unmentioned = RoutinesThatLog()
            .Where(routine => !routine.Value.Any(named.Contains))
            .Select(routine => Path.GetFileName(routine.Key))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unmentioned.Count == 0,
            "These plugin files write log lines and docs/logging.md names none of the types they declare: "
            + string.Join(", ", unmentioned)
            + ". That page is the map an audit of the never list is run off, so a routine missing from it is a routine nobody looks at.");
    }

    /// <summary>
    /// Every routine the page names is a routine that logs. Without it the
    /// repair rots the other way: a name kept after the routine was renamed or
    /// stopped logging leaves the page reading as a complete map.
    /// </summary>
    [Fact]
    public void EveryRoutineTheLoggingPageNamesLogs()
    {
        var logging = new HashSet<string>(
            RoutinesThatLog().SelectMany(routine => routine.Value),
            StringComparer.Ordinal);

        var silent = Named()
            .Where(name => !logging.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            silent.Count == 0,
            "docs/logging.md names these as routines that write log lines and no plugin file declaring them holds a logging call: "
            + string.Join(", ", silent)
            + ". Either the name is stale or the routine stopped logging, and the page reads as a complete map either way.");
    }

    /// <summary>
    /// The routines the page names.
    /// </summary>
    /// <returns>The names, as the page writes them.</returns>
    private static HashSet<string> Named()
    {
        var sentence = _theSentence.Match(LoggingPage());
        Assert.True(
            sentence.Success,
            "docs/logging.md no longer carries the sentence naming which routines write log lines, so these legs read nothing. Failing rather than passing over a page that has stopped being read.");

        return new HashSet<string>(
            _backticked.Matches(sentence.Groups[1].Value).Select(match => match.Groups[1].Value),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// The plugin source files that hold a logging call, with the types each of
    /// them declares.
    /// </summary>
    /// <returns>The file and the type names declared in it.</returns>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> RoutinesThatLog() =>
        PluginSources()
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .Where(source => _logsSomething.IsMatch(source.Text))
            .ToDictionary(
                source => source.Path,
                source => (IReadOnlyList<string>)_declares
                    .Matches(source.Text)
                    .Select(match => match.Groups[1].Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);

    /// <summary>
    /// The source files of the plugin project.
    /// </summary>
    /// <remarks>
    /// Found by walking up from the test binary until a directory holds the
    /// solution and the plugin project, which is how the other legs over a
    /// tracked file find one: the number of levels under the binary moves with
    /// the configuration and the target framework, and the marker does not.
    /// Build output is skipped, because the generated files under it are not
    /// sources anybody writes. Nothing is written and nothing outside the
    /// repository is read.
    /// </remarks>
    /// <returns>Every C# source file of the plugin.</returns>
    private static IReadOnlyList<string> PluginSources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var project = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites");
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
            + " holds both Jellyfin.Plugin.Invites.sln and the plugin project, so these legs read nothing. Failing rather than reporting a rule that ran over an empty set.");
    }

    /// <summary>
    /// Whether a path is build output rather than a source somebody wrote.
    /// </summary>
    /// <param name="project">The plugin project directory.</param>
    /// <param name="path">The file.</param>
    /// <returns>True when it sits under bin or obj.</returns>
    private static bool Generated(string project, string path)
    {
        var relative = Path.GetRelativePath(project, path)
            .Replace('\\', '/');

        return relative.StartsWith("bin/", StringComparison.Ordinal)
            || relative.StartsWith("obj/", StringComparison.Ordinal);
    }

    /// <summary>
    /// The logging page.
    /// </summary>
    /// <returns>The text of the page.</returns>
    private static string LoggingPage()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var page = Path.Combine(directory.FullName, "docs", "logging.md");
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            if (File.Exists(page) && File.Exists(solution))
            {
                return File.ReadAllText(page);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds both Jellyfin.Plugin.Invites.sln and docs/logging.md, so this comparison read nothing. Failing rather than passing over an empty page.");
    }
}
