using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// Every test the limits page names is a test that exists.
/// </summary>
/// <remarks>
/// <para>
/// <c>docs/limits.md</c> ends with a section saying which of its entries a test
/// holds, and the whole of that section is names: each entry is followed by the
/// fault that was applied and the tests it reddened. A reader who doubts an
/// entry follows the name to the test and reads it. Nothing compared the two,
/// so a rename left the suite green, the page holding the old spelling, and an
/// entry whose evidence cannot be followed.
/// </para>
/// <para>
/// That is not hypothetical on this page. Three of the fault sites it names sat
/// at a line that had moved, found by re-reading the pastes rather than by
/// anything going red, and a method name rots the same way with nothing to say
/// so. The count at the foot of the page, five entries of nine, is what a reader
/// takes from it, and a name that resolves to nothing inflates that count while
/// looking exactly like one that does not.
/// </para>
/// <para>
/// <b>What is refused.</b> A <c>SomethingTests.Method</c> written anywhere on
/// <c>docs/limits.md</c> that does not resolve to a public test method of this
/// assembly. Resolution is by reflection rather than by reading source text, so
/// a name matching a private helper, a field or a method carrying neither
/// <c>[Fact]</c> nor <c>[Theory]</c> is refused as well: an entry said to be
/// held by something the runner never executes is the case this exists for.
/// </para>
/// <para>
/// The names are read unquoted, because this page writes most of them inside
/// pasted runner output rather than in backticks, and a rule reading only
/// backticks would see one of the seventeen. A qualified name is not matched:
/// the class part has to be a token of its own, so the namespace
/// <c>Jellyfin.Plugin.Invites.Tests</c> followed by a type is not read as a
/// class followed by a method.
/// </para>
/// <para>
/// <b>Its bound, stated rather than left to be found.</b> This resolves names.
/// Whether the test it resolves to holds the entry the sentence beside it claims
/// is a judgement, no reading of the tree makes it, and the review is where a
/// wrong one is caught. A green run says every name on the page can be followed,
/// which is a smaller claim than the page being honest. It also says nothing
/// about an entry that names no test at all: four do, the page says so about
/// itself, and refusing that would be refusing the page for the shape of its
/// prose.
/// </para>
/// <para>
/// <b>Why this is a second file rather than a widening.</b>
/// <see cref="SecurityPageTests"/> asks the same question of
/// <c>SECURITY.md</c> and arrived under #112, which is the issue that owns that
/// page and that file. Folding the two into one helper would be a change to a
/// file another issue is working in, for a saving of one reflection walk. The
/// cost of not folding them is real and is named here rather than left to be
/// discovered: two copies of the resolution rule, and a later change that
/// tightens one and not the other. Whichever issue next has reason to touch both
/// is where they should meet.
/// </para>
/// </remarks>
public class LimitsPageTests
{
    /// <summary>
    /// A test named as class and method, with the class a token of its own.
    /// </summary>
    private static readonly Regex _named = new(
        @"(?<![A-Za-z0-9_.])([A-Za-z0-9_]+Tests)\.([A-Za-z0-9_]+)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The page names tests at all. Without this, a regex that stopped matching
    /// reports the same empty set as a page that named nothing, and only one of
    /// those deserves a green mark.
    /// </summary>
    [Fact]
    public void TheScanFindsTheNamesTheLimitsPageCarries()
    {
        Assert.NotEmpty(Named());
    }

    /// <summary>
    /// Every test the limits page names resolves to a test this assembly runs.
    /// </summary>
    [Fact]
    public void EveryTestTheLimitsPageNamesExists()
    {
        var assembly = typeof(LimitsPageTests).Assembly;

        var unresolved = Named()
            .Where(name => !Resolves(assembly, name.Type, name.Method))
            .Select(name => name.Type + "." + name.Method)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unresolved.Length == 0,
            "docs/limits.md names "
            + string.Join(", ", unresolved)
            + ", which this assembly does not run. An entry whose evidence cannot be followed reads as evidence and is not. Either the test was renamed and the page has to follow it, or the entry has lost the test that held it and the count at the foot of the page has to say so.");
    }

    /// <summary>
    /// Whether a name resolves to a public method the runner executes.
    /// </summary>
    /// <param name="assembly">The test assembly.</param>
    /// <param name="type">The class the page names.</param>
    /// <param name="method">The method the page names.</param>
    /// <returns>True when the method exists and carries a test attribute.</returns>
    private static bool Resolves(Assembly assembly, string type, string method)
    {
        var found = assembly
            .GetTypes()
            .SingleOrDefault(candidate => string.Equals(candidate.Name, type, StringComparison.Ordinal));

        if (found is null)
        {
            return false;
        }

        return found
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(candidate => string.Equals(candidate.Name, method, StringComparison.Ordinal))
            .Any(candidate =>
                candidate.GetCustomAttribute<FactAttribute>() is not null
                || candidate.GetCustomAttribute<TheoryAttribute>() is not null);
    }

    /// <summary>
    /// The tests the limits page names, in the order it names them.
    /// </summary>
    /// <returns>The class and method of each name.</returns>
    private static IReadOnlyList<(string Type, string Method)> Named() =>
        _named
            .Matches(LimitsPage())
            .Select(match => (match.Groups[1].Value, match.Groups[2].Value))
            .Distinct()
            .ToList();

    /// <summary>
    /// The limits page.
    /// </summary>
    /// <remarks>
    /// Found by walking up from the test binary until a directory holds both the
    /// solution and the page, which is how the other legs over a tracked
    /// document find one: the number of levels under the binary moves with the
    /// configuration and the target framework, and the marker does not. Nothing
    /// is written and nothing outside the repository is read.
    /// </remarks>
    /// <returns>The text of the page.</returns>
    private static string LimitsPage()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var page = Path.Combine(directory.FullName, "docs", "limits.md");
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
            + " holds both Jellyfin.Plugin.Invites.sln and docs/limits.md, so this comparison read nothing. Failing rather than passing over an empty page.");
    }
}
