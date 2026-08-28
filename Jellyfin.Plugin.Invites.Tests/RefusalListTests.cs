using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// Every name the refusal list writes is a name this solution holds.
/// </summary>
/// <remarks>
/// <para>
/// <c>docs/tests-not-written.md</c> is the list of tests this plugin refuses to
/// write, and every row names what covers the same risk instead. #100 asks that
/// each named replacement exist, so the names are the whole of what a reader
/// follows: a row saying a test covers the risk, whose test cannot be found, is
/// a refusal with nothing behind it while looking exactly like one that is
/// covered.
/// </para>
/// <para>
/// Nothing compared the two. A rename left the suite green, the page holding the
/// old spelling, and the reader who followed it finding nothing. That page has
/// gone stale twice in its status lines already, both times found by somebody
/// re-reading rather than by anything going red, and a method name rots the same
/// way.
/// </para>
/// <para>
/// <b>What is refused.</b> A backticked name whose whole content is one
/// identifier beginning with a capital, and which resolves to none of three
/// things: a test method this assembly runs, a type either assembly declares, or
/// a public member of such a type. Fenced blocks come out first, so a pasted
/// command is not read as a claim about a name.
/// </para>
/// <para>
/// The three are what this page actually writes in backticks. It names tests, it
/// names the types under test, and it names one controller action. A name that
/// is none of the three is either something that was renamed or a word that
/// should not have been in backticks, and the page says so where it declares the
/// convention.
/// </para>
/// <para>
/// <b>Its bounds, stated rather than left to be found.</b> Most of this page's
/// test names are bare, so they carry no class and this resolves the method
/// anywhere in the assembly: a name moved from one test class to another passes.
/// The page's qualified mentions are written as a name and the class in prose
/// rather than joined by a dot, so there is nothing here to hold them together
/// with.
/// </para>
/// <para>
/// It resolves names and judges nothing. Whether the test a name resolves to
/// covers the risk the row claims is a reading a person makes, and it says
/// nothing at all about a status line: the two stale rows on this page were
/// stale in what they SAID about their replacements, not in what they called
/// them, so this would not have caught either. That absence is #100's and is not
/// closed by this.
/// </para>
/// <para>
/// <b>Why this is a third file rather than a widening.</b>
/// <see cref="SecurityPageTests"/> and <see cref="LimitsPageTests"/> ask a
/// narrower version of this question of two other pages, and the second of them
/// records the cost of not folding them together and says the issue that next
/// has reason to touch both is where they should meet. This is not that moment:
/// each of the three pages is owned by a different issue, the resolution rule
/// here is wider than either of theirs because this page names types as well as
/// tests, and folding would edit two files this change has no business in. The
/// cost is three copies of a reflection walk and a later change that tightens
/// one and not the others, which is the same cost that note names, one larger.
/// </para>
/// </remarks>
public class RefusalListTests
{
    /// <summary>
    /// A backticked name standing on its own.
    /// </summary>
    /// <remarks>
    /// The whole content of the backticks has to be one identifier beginning
    /// with a capital, so a path, a dotted name, a hyphenated rule id, a header
    /// written with its colon and a shell fragment are not in the population,
    /// and neither is an ordinary lowercase word.
    /// </remarks>
    private static readonly Regex _written = new(
        @"`([A-Z][A-Za-z0-9_]*)`",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A fenced code block, which is a transcript rather than prose.
    /// </summary>
    private static readonly Regex _fenced = new(
        "^```.*?^```",
        RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The page writes names at all. Without this, a regex that stopped matching
    /// reports the same empty set as a page naming nothing, and only one of those
    /// deserves a green mark.
    /// </summary>
    [Fact]
    public void TheScanFindsTheNamesTheRefusalListCarries()
    {
        Assert.NotEmpty(Written());
    }

    /// <summary>
    /// Every name the refusal list writes resolves to something in this solution.
    /// </summary>
    [Fact]
    public void EveryNameTheRefusalListWritesResolves()
    {
        var tests = typeof(RefusalListTests).Assembly;
        var plugin = typeof(Invites.Plugin).Assembly;

        var unresolved = Written()
            .Where(name => !IsATest(tests, name) && !IsAKnownName(tests, name) && !IsAKnownName(plugin, name))
            .ToArray();

        Assert.True(
            unresolved.Length == 0,
            "docs/tests-not-written.md writes "
            + string.Join(", ", unresolved)
            + " in backticks, and this solution holds no test, type or member of that name. A replacement whose name cannot be followed reads as covered and is not. Either the thing was renamed and the row has to follow it, or the row has lost the replacement it named and has to say so.");
    }

    /// <summary>
    /// Whether a name is a test method this assembly runs.
    /// </summary>
    /// <param name="assembly">The test assembly.</param>
    /// <param name="method">The name the page writes.</param>
    /// <returns>True when some test class declares it as a fact or a theory.</returns>
    private static bool IsATest(Assembly assembly, string method) =>
        assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(candidate => string.Equals(candidate.Name, method, StringComparison.Ordinal))
            .Any(candidate =>
                candidate.GetCustomAttribute<FactAttribute>() is not null
                || candidate.GetCustomAttribute<TheoryAttribute>() is not null);

    /// <summary>
    /// Whether a name is a type an assembly declares, or a public member of one.
    /// </summary>
    /// <param name="assembly">The assembly to look in.</param>
    /// <param name="name">The name the page writes.</param>
    /// <returns>True when the name is declared there.</returns>
    private static bool IsAKnownName(Assembly assembly, string name)
    {
        var types = assembly.GetTypes();

        return types.Any(type => string.Equals(type.Name, name, StringComparison.Ordinal))
            || types
                .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                .Any(member => string.Equals(member.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    /// The names the refusal list writes, once each.
    /// </summary>
    /// <returns>Each name, in the order the page first writes it.</returns>
    private static IReadOnlyList<string> Written() =>
        _written
            .Matches(_fenced.Replace(RefusalList(), string.Empty))
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The refusal list.
    /// </summary>
    /// <remarks>
    /// Found by walking up from the test binary until a directory holds both the
    /// solution and the page, which is how the other legs over a tracked document
    /// find one: the number of levels under the binary moves with the
    /// configuration and the target framework, and the marker does not. Nothing
    /// is written and nothing outside the repository is read.
    /// </remarks>
    /// <returns>The text of the page.</returns>
    private static string RefusalList()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var page = Path.Combine(directory.FullName, "docs", "tests-not-written.md");
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
            + " holds both Jellyfin.Plugin.Invites.sln and docs/tests-not-written.md, so this comparison read nothing. Failing rather than passing over an empty page.");
    }
}
