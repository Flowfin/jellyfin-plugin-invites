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
/// It also says nothing about a property that names no test, and that is the
/// clause #112 is open on rather than something this leg can refuse: a page is
/// free to state a property and stay silent about what holds it, and refusing
/// that would be refusing the page for the shape of its prose. How many such
/// properties there are is deliberately not written here. It was, as a count,
/// and the count went stale the day the rate-limit property acquired its tests;
/// the page is where that is read, section by section, because a name can sit
/// in a section that is not the property it holds.
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
    /// A backticked name standing on its own, with no class in front of it.
    /// </summary>
    /// <remarks>
    /// The whole content of the backticks has to be one identifier, so a path,
    /// a dotted name, a hyphenated rule id and a shell fragment are not in the
    /// population. It has to begin with a capital as well, which is what keeps
    /// an ordinary lowercase word in backticks - a branch name, a setting, a
    /// literal - out of a population this leg would otherwise refuse for not
    /// being a C# name. No such word is on the page today; the restriction is
    /// for the one somebody writes next year.
    /// </remarks>
    private static readonly Regex _written = new(
        @"`([A-Z][A-Za-z0-9_]*)`",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A fenced code block, which is a transcript rather than prose.
    /// </summary>
    /// <remarks>
    /// A pasted command is not the page claiming anything about a name, so the
    /// blocks come out before the names are read. Nothing in the page carries a
    /// backtick inside a fence today; taking them out anyway is what keeps a
    /// future paste from being read as a claim.
    /// </remarks>
    private static readonly Regex _fenced = new(
        "^```.*?^```",
        RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A lint rule named in backticks, which on this page is a hyphenated
    /// lowercase id.
    /// </summary>
    /// <remarks>
    /// The two legs above cannot see this shape and one of them says so: the
    /// first wants a qualified <c>SomethingTests.Method</c>, and the second
    /// wants a single identifier beginning with a capital and names a
    /// hyphenated rule id among what that keeps out. So the rule ids this page
    /// cites as evidence were read by nothing. Every hyphenated backticked name
    /// the page carries is one of them.
    /// </remarks>
    private static readonly Regex _ruleId = new(
        @"`([a-z][a-z0-9]*(?:-[a-z0-9]+)+)`",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// One row of the invariant lint's rule table, read for the id at its head.
    /// </summary>
    private static readonly Regex _lintRule = new(
        @"^\s*'(?<id>[a-z0-9-]+)@#[0-9]+@",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The head of one branch of the lint's failure-message table.
    /// </summary>
    private static readonly Regex _lintBranch = new(
        @"^\s{4}(?<id>[a-z0-9-]+)\)\s*$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
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
    /// Every backticked name the page writes resolves to a test or to a type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The leg above reads a name written as <c>SomethingTests.Method</c>. This
    /// page names a class once and then drops it, so most of the names it uses
    /// as evidence are a bare method name and that leg never saw them: twenty
    /// seven qualified against fourteen bare, on the day this arrived. A rename
    /// of any of the fourteen left the old spelling standing here, reading as
    /// evidence, with the suite green - which is the failure the leg above
    /// exists against, on two thirds of its subject.
    /// </para>
    /// <para>
    /// <b>What is refused.</b> A backticked name whose whole content is one
    /// identifier and which resolves to neither a test method this assembly
    /// runs nor a type either assembly declares. The page's convention
    /// paragraph says a name here is one of those two, so a name that is
    /// neither is either a rename this page has not followed or a word that
    /// should not have been in backticks.
    /// </para>
    /// <para>
    /// <b>Its bounds.</b> A bare name carries no class, so this resolves the
    /// method anywhere in the assembly and cannot say the sentence names the
    /// right class; the qualified leg is what holds that, for the names that
    /// carry one. And it resolves names rather than judging them: whether the
    /// test holds the property beside it is a reading a person makes.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryNameThisPageWritesResolves()
    {
        var page = _fenced.Replace(SecurityPage(), string.Empty);

        var written = _written
            .Matches(page)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(written);

        var tests = typeof(SecurityPageTests).Assembly;
        var plugin = typeof(Invites.Plugin).Assembly;

        var unresolved = written
            .Where(name => !IsATest(tests, name) && !IsAType(tests, name) && !IsAType(plugin, name))
            .ToArray();

        Assert.True(
            unresolved.Length == 0,
            "SECURITY.md writes "
            + string.Join(", ", unresolved)
            + " in backticks, and neither assembly holds a test or a type of that name. This page says every backticked name is one of the two, so a name that is neither reads as evidence and cannot be followed. Either the thing was renamed and the page has to follow it, or the word does not belong in backticks.");
    }

    /// <summary>
    /// Every lint rule the page names is a rule the lint carries.
    /// </summary>
    /// <remarks>
    /// The same failure as the two legs above, on the third population this
    /// page uses as evidence. A rule id is renamed in
    /// <c>.github/lint/invariants.sh</c>, the lint stays green because it reads
    /// its own table, and the page goes on naming the old id as what refuses a
    /// thing. Nothing here compared the two.
    /// </remarks>
    [Fact]
    public void EveryLintRuleTheSecurityPageNamesExists()
    {
        var page = _fenced.Replace(SecurityPage(), string.Empty);

        var named = _ruleId
            .Matches(page)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(named);

        var carried = LintRules();

        Assert.NotEmpty(carried);

        var unresolved = named
            .Where(name => !carried.Contains(name))
            .ToArray();

        Assert.True(
            unresolved.Length == 0,
            "SECURITY.md names "
            + string.Join(", ", unresolved)
            + " as a rule of the invariant lint, and .github/lint/invariants.sh carries no rule of that id. A refusal that cannot be found is a refusal a reader cannot check. Either the rule was renamed and the page has to follow it, or the rule is gone and the property has lost what held it.");
    }

    /// <summary>
    /// Every rule whose failure sends somebody to a constant-time comparison is
    /// named where this page claims one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The leg above resolves the ids the page writes. It cannot see the id the
    /// page leaves out, and that is the direction this section was wrong in:
    /// three rules refuse the spellings that take the hash comparison back to
    /// an early-returning one, and the paragraph named two of them and called
    /// them the two spellings. A page describing a narrower guard than the one
    /// that is there is read by somebody deciding whether a spelling is
    /// available.
    /// </para>
    /// <para>
    /// The family is derived rather than listed here. A rule belongs to it when
    /// its own failure message sends the reader to
    /// <c>CryptographicOperations.FixedTimeEquals</c>, which is what the page's
    /// paragraph is about, so a fourth rule added to the lint for the same
    /// reason arrives red here rather than quietly.
    /// </para>
    /// <para>
    /// <b>Its bound.</b> It reads a failure message, so a rule of that family
    /// whose message reaches the same conclusion in other words is outside the
    /// population and is refused by nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryConstantTimeRuleIsNamedOnThePage()
    {
        var page = _fenced.Replace(SecurityPage(), string.Empty);

        var named = _ruleId
            .Matches(page)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var family = ConstantTimeRules();

        Assert.NotEmpty(family);

        var missing = family
            .Where(id => !named.Contains(id))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "The invariant lint carries "
            + string.Join(", ", missing)
            + ", whose failure sends somebody to CryptographicOperations.FixedTimeEquals, and SECURITY.md names it nowhere. The page states that comparison as a property and names the rules that refuse taking it back, so a rule of that family it does not name is a guard the page describes as narrower than it is.");
    }

    /// <summary>
    /// Whether a bare name is a test method this assembly runs.
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
    /// Whether a bare name is a type an assembly declares.
    /// </summary>
    /// <param name="assembly">The assembly to look in.</param>
    /// <param name="name">The name the page writes.</param>
    /// <returns>True when a type of that name is declared there.</returns>
    private static bool IsAType(Assembly assembly, string name) =>
        assembly.GetTypes().Any(type => string.Equals(type.Name, name, StringComparison.Ordinal));

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

    /// <summary>
    /// The ids of the rules the invariant lint carries.
    /// </summary>
    /// <returns>Each id at the head of a row of the lint's rule table.</returns>
    private static IReadOnlyCollection<string> LintRules() =>
        _lintRule
            .Matches(InvariantLint())
            .Select(match => match.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The rules whose failure message sends somebody to a constant-time
    /// comparison.
    /// </summary>
    /// <remarks>
    /// The lint explains a failure in a table keyed by rule id, so the family is
    /// read out of that table rather than typed here. A branch head is only
    /// taken when the id is one the rule table declares, so a shell construct
    /// that happens to end in a bracket is not read as a rule.
    /// </remarks>
    /// <returns>The ids, in order.</returns>
    private static IReadOnlyList<string> ConstantTimeRules()
    {
        var declared = LintRules();
        var found = new List<string>();
        string? branch = null;

        foreach (var line in InvariantLint().Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            var head = _lintBranch.Match(trimmed);

            if (head.Success && declared.Contains(head.Groups["id"].Value))
            {
                branch = head.Groups["id"].Value;
                continue;
            }

            if (branch is not null && trimmed.Contains("FixedTimeEquals", StringComparison.Ordinal))
            {
                found.Add(branch);
            }

            if (trimmed.Contains(";;", StringComparison.Ordinal))
            {
                branch = null;
            }
        }

        return found
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The invariant lint.
    /// </summary>
    /// <remarks>
    /// Found the same way the page is, by walking up from the test binary until
    /// a directory holds the solution and the file, so the number of levels
    /// under the binary can move with the configuration and the target
    /// framework. Nothing is written and nothing outside the repository is read.
    /// </remarks>
    /// <returns>The text of the lint.</returns>
    private static string InvariantLint()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            var lint = Path.Combine(directory.FullName, ".github", "lint", "invariants.sh");
            if (File.Exists(solution) && File.Exists(lint))
            {
                return File.ReadAllText(lint);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds both Jellyfin.Plugin.Invites.sln and .github/lint/invariants.sh, so this comparison read nothing. Failing rather than passing over an empty lint.");
    }
}
