using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// Every test the security page names is a test that exists.
/// </summary>
/// <remarks>
/// <para>
/// #112 asks that every property on the security page name the test that holds
/// it, and that no claim on it be stronger than what the suite proves. A name
/// is what makes both readable: a reader who doubts a property opens the test
/// and reads it. A name that resolves to nothing is worse than no name at all,
/// because it reads as evidence and cannot be followed, and a rename is how it
/// happens. The suite goes green on a rename, the page keeps the old spelling,
/// and nothing in this repository compares the two.
/// </para>
/// <para>
/// <b>What is refused.</b> A <c>SomethingTests.Method</c> written in backticks
/// on <c>SECURITY.md</c> that does not resolve to a public test method of this
/// assembly. Resolution is by reflection rather than by reading source text, so
/// a name that matches a private helper, a field or a method carrying neither
/// <c>[Fact]</c> nor <c>[Theory]</c> is refused as well: a property said to be
/// held by something the runner never executes is the case this leg exists for.
/// </para>
/// <para>
/// <b>Its bound, stated rather than left to be found.</b> This resolves names.
/// Whether the test it resolves to holds the property the sentence beside it
/// claims is a judgement, no reading of the tree makes it, and the review is
/// where a wrong one is caught. A green run says every name on the page can be
/// followed, which is a smaller claim than the page being honest.
/// </para>
/// <para>
/// It also says nothing about a property that names no test. Three do, and that
/// is the clause #112 is open on rather than something this leg can refuse:
/// a page is free to state a property and stay silent about what holds it, and
/// refusing that would be refusing the page for the shape of its prose.
/// </para>
/// </remarks>
public class SecurityPageTests
{
    /// <summary>
    /// A test named in backticks, as class and method.
    /// </summary>
    private static readonly Regex _named = new(
        @"`([A-Za-z0-9_]+Tests)\.([A-Za-z0-9_]+)`",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// Every test the security page names resolves to a test this assembly runs.
    /// </summary>
    [Fact]
    public void EveryTestTheSecurityPageNamesExists()
    {
        var named = Named();

        Assert.NotEmpty(named);

        var assembly = typeof(SecurityPageTests).Assembly;

        var unresolved = named
            .Where(name => !Resolves(assembly, name.Type, name.Method))
            .Select(name => name.Type + "." + name.Method)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unresolved.Length == 0,
            "SECURITY.md names "
            + string.Join(", ", unresolved)
            + ", which this assembly does not run. A property whose evidence cannot be followed reads as evidence and is not. Either the test was renamed and the page has to follow it, or the property has lost the test that held it and the sentence has to say so.");
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
    /// The tests the security page names, in the order it names them.
    /// </summary>
    /// <returns>The class and method of each name.</returns>
    private static IReadOnlyList<(string Type, string Method)> Named() =>
        _named
            .Matches(SecurityPage())
            .Select(match => (match.Groups[1].Value, match.Groups[2].Value))
            .Distinct()
            .ToList();

    /// <summary>
    /// The security page.
    /// </summary>
    /// <remarks>
    /// Found by walking up from the test binary until a directory holds both the
    /// solution and the page, which is how the other legs over a tracked
    /// document find one: the number of levels under the binary moves with the
    /// configuration and the target framework, and the marker does not. Nothing
    /// is written and nothing outside the repository is read.
    /// </remarks>
    /// <returns>The text of the page.</returns>
    private static string SecurityPage()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var page = Path.Combine(directory.FullName, "SECURITY.md");
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
            + " holds both Jellyfin.Plugin.Invites.sln and SECURITY.md, so this comparison read nothing. Failing rather than passing over an empty page.");
    }
}
