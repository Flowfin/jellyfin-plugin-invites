using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Invites.Codes;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The code this plugin mints is chained to the arithmetic on
/// <c>docs/code-entropy.md</c>, in both of the two steps that stand between an
/// input on that page and a constant in the source.
/// </summary>
/// <remarks>
/// <para>
/// That page says of itself, under what it does not claim, that nothing reads
/// it: no check derives the requirement from the arithmetic, so raising the
/// requirement there and leaving the constants where they are passes every
/// route. #28 records the same absence in its own words, that no guard exists
/// for that direction. This is that direction.
/// </para>
/// <para>
/// <b>What is refused, in three legs, because the chain has three links.</b>
/// The figures the page pastes have to be the ones its own inputs produce, so
/// an input edited without the block being re-run is refused rather than
/// believed. The requirement the page states in words has to clear the figures
/// its arithmetic derives, so an input raised past the stated number is refused.
/// And the entropy a minted code actually carries has to clear the stated
/// number, so a shortened code and a raised requirement are the same failure
/// arriving from the two ends.
/// </para>
/// <para>
/// <b>Why the alphabet is counted rather than named.</b> The bits a code
/// carries are the mint's property and not a constant's, so the last leg mints
/// a sample and counts the distinct characters that came out. Naming the
/// alphabet here instead would compute the entropy of a literal in this file,
/// which is the number nobody doubts.
/// <see cref="InvitationCodeTests.EveryCharacterOfTheAlphabetIsMinted"/> is the
/// leg that asks whether every character is reachable, and it is a different
/// question from what those characters are worth.
/// </para>
/// <para>
/// <b>Its bounds, stated rather than left to be found.</b> The count is a
/// sample. A mint that produced its alphabet with some characters far rarer
/// than others carries less than <c>log2(alphabet)</c> a character and is
/// invisible here, so what the last leg holds is the alphabet's size and not
/// the draw's uniformity; that rests on the source, which
/// <c>weak-random</c> in <c>.github/lint/invariants.sh</c> refuses the wrong
/// spelling of, and on a mask over a byte. The other two legs read numerals out
/// of prose: an input moved into a sentence the patterns below do not match
/// stops being read, which is what the first leg exists to red on rather than
/// pass quietly.
/// </para>
/// <para>
/// <b>The means.</b> A test in this suite rather than a rule in
/// <c>.github/lint/invariants.sh</c>. The property is arithmetic over four
/// numerals and a comparison against a routine's output, and the rule set there
/// subtracts lines from a match rather than evaluating anything, so a greppable
/// rule could refuse a spelling and never a figure. The suite already reads
/// tracked documents this way and runs on the same command as everything else.
/// </para>
/// </remarks>
public class CodeEntropyPageTests
{
    /// <summary>
    /// The live invitations the arithmetic is taken over.
    /// </summary>
    private static readonly Regex _liveInvitations = new(
        @"\bN=([0-9]+);",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The attempts in each of the two scenarios, as the product the page
    /// writes them as, so a rate and a duration stay separable.
    /// </summary>
    private static readonly Regex _attempts = new(
        @"\bA([12])=([0-9]+)\*([0-9]+);",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The margin, which the page adds as a term of its own so that arguing
    /// with it costs exactly its own bits.
    /// </summary>
    private static readonly Regex _margin = new(
        @"/l2 \+ ([0-9]+);",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The figures the page pastes under the block that produces them.
    /// </summary>
    private static readonly Regex _pasted = new(
        @"required bits, (unthrottled|throttled)\s*=\s*([0-9]+\.[0-9]+)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The requirement the page states in words, which is the number the source
    /// is answerable to.
    /// </summary>
    private static readonly Regex _stated = new(
        @"A code carries ([0-9]+) bits",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The page declares the inputs at all. Without this a pattern that stopped
    /// matching would report the same silence as a page that had been emptied,
    /// and the legs below would pass over nothing.
    /// </summary>
    /// <remarks>
    /// It asks that each piece was found and never what its value is. A value
    /// written here would be this file's copy of the page's arithmetic, which
    /// is the thing the legs below exist so that nobody has to keep.
    /// </remarks>
    [Fact]
    public void TheScanFindsTheInputsTheEntropyPageDeclares()
    {
        var inputs = Inputs();

        Assert.True(inputs.LiveInvitations > 0);
        Assert.Equal(2, inputs.Attempts.Count);
        Assert.True(inputs.Margin > 0);
        Assert.Equal(2, PastedFigures().Count);
        Assert.True(StatedRequirement() > 0);
    }

    /// <summary>
    /// The figures the page pastes are the ones its inputs produce. An input
    /// edited without the block being re-run leaves a page whose prose and
    /// whose numbers disagree, and every sentence after it is read off the
    /// number.
    /// </summary>
    [Fact]
    public void TheFiguresTheEntropyPagePastesAreTheOnesItsInputsProduce()
    {
        var derived = Derived();
        var pasted = PastedFigures();

        foreach (var scenario in derived)
        {
            Assert.True(
                Math.Abs(pasted[scenario.Key] - scenario.Value) < 0.006d,
                "docs/code-entropy.md pastes "
                + pasted[scenario.Key].ToString("F2", CultureInfo.InvariantCulture)
                + " required bits for the "
                + scenario.Key
                + " scenario and its own inputs produce "
                + scenario.Value.ToString("F2", CultureInfo.InvariantCulture)
                + ". An input was edited without the block being re-run, so the figure every sentence below it rests on is not the figure the arithmetic gives.");
        }
    }

    /// <summary>
    /// The requirement the page states clears the arithmetic the page derives.
    /// This is the direction the page names as uncovered: raising an input
    /// past the stated number left every route green.
    /// </summary>
    [Fact]
    public void TheRequirementTheEntropyPageStatesClearsItsOwnArithmetic()
    {
        var required = Derived().Values.Max();
        var stated = StatedRequirement();

        Assert.True(
            stated >= required,
            "docs/code-entropy.md states that a code carries "
            + stated.ToString(CultureInfo.InvariantCulture)
            + " bits and its own arithmetic asks for "
            + required.ToString("F2", CultureInfo.InvariantCulture)
            + ". The number the source is built to no longer clears the calculation it is supposed to be read off.");
    }

    /// <summary>
    /// The code this plugin mints clears the requirement the page states. This
    /// is the other end of the same chain: a code shortened by one character
    /// and a requirement raised on the page are one failure, and neither had
    /// anything to answer to.
    /// </summary>
    [Fact]
    public void TheCodeThisPluginMintsClearsTheRequirementTheEntropyPageStates()
    {
        var alphabet = AlphabetTheMintProduces();
        var carried = InvitationCode.Length * Math.Log2(alphabet);
        var stated = StatedRequirement();

        Assert.True(
            carried >= stated,
            "A minted code is "
            + InvitationCode.Length.ToString(CultureInfo.InvariantCulture)
            + " characters over an alphabet of "
            + alphabet.ToString(CultureInfo.InvariantCulture)
            + ", which is "
            + carried.ToString("F2", CultureInfo.InvariantCulture)
            + " bits, and docs/code-entropy.md requires "
            + stated.ToString(CultureInfo.InvariantCulture)
            + ". The credential is weaker than the page it is defended from says it is.");
    }

    /// <summary>
    /// The number of distinct characters a run of mints produces, which is the
    /// alphabet as the routine uses it rather than as anything declares it.
    /// </summary>
    /// <remarks>
    /// Five hundred mints is thirteen thousand draws, so a character of a
    /// thirty-two character alphabet is missed with probability under
    /// <c>2^-230</c>. Nothing sleeps and nothing is written.
    /// </remarks>
    /// <returns>The count of distinct characters seen.</returns>
    private static int AlphabetTheMintProduces()
    {
        var seen = new HashSet<char>();

        for (var mint = 0; mint < 500; mint++)
        {
            foreach (var character in InvitationCode.Mint())
            {
                seen.Add(character);
            }
        }

        return seen.Count;
    }

    /// <summary>
    /// The required bits in each scenario, computed from the page's inputs.
    /// </summary>
    /// <returns>The scenario and the bits it requires.</returns>
    private static IReadOnlyDictionary<string, double> Derived()
    {
        var inputs = Inputs();

        return new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["unthrottled"] = Math.Log2(inputs.Attempts["1"] * inputs.LiveInvitations) + inputs.Margin,
            ["throttled"] = Math.Log2(inputs.Attempts["2"] * inputs.LiveInvitations) + inputs.Margin,
        };
    }

    /// <summary>
    /// The inputs the arithmetic section declares.
    /// </summary>
    /// <returns>The live invitations, the attempts per scenario and the margin.</returns>
    private static (double LiveInvitations, IReadOnlyDictionary<string, double> Attempts, double Margin) Inputs()
    {
        var section = ArithmeticSection();

        var live = _liveInvitations.Match(section);
        Assert.True(live.Success, "docs/code-entropy.md declares no live invitation count in its arithmetic section, so nothing here was derived from it.");

        var attempts = _attempts
            .Matches(section)
            .ToDictionary(
                match => match.Groups[1].Value,
                match => Number(match.Groups[2].Value) * Number(match.Groups[3].Value),
                StringComparer.Ordinal);

        var margins = _margin
            .Matches(section)
            .Select(match => Number(match.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.True(margins.Count == 1, "docs/code-entropy.md adds more than one margin in its arithmetic section, so which of them the requirement uses is not readable: " + string.Join(", ", margins));

        return (Number(live.Groups[1].Value), attempts, margins[0]);
    }

    /// <summary>
    /// The figures the page pastes beneath the block that produces them.
    /// </summary>
    /// <returns>The scenario and the figure pasted for it.</returns>
    private static IReadOnlyDictionary<string, double> PastedFigures() =>
        _pasted
            .Matches(ArithmeticSection())
            .ToDictionary(
                match => match.Groups[1].Value,
                match => Number(match.Groups[2].Value),
                StringComparer.Ordinal);

    /// <summary>
    /// The requirement the page states in words.
    /// </summary>
    /// <returns>The bits a code is required to carry.</returns>
    private static int StatedRequirement()
    {
        var match = _stated.Match(EntropyPage());
        Assert.True(match.Success, "docs/code-entropy.md no longer states in words how many bits a code carries, so the source has nothing to be answerable to.");

        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The section of the page that holds the arithmetic, so that the worked
    /// examples elsewhere on it are not read as the calculation.
    /// </summary>
    /// <returns>The text between the arithmetic heading and the next one.</returns>
    private static string ArithmeticSection()
    {
        var page = EntropyPage();
        var start = page.IndexOf("## The arithmetic", StringComparison.Ordinal);
        Assert.True(start >= 0, "docs/code-entropy.md carries no arithmetic section, so this comparison read nothing.");

        var end = page.IndexOf("\n## ", start + 1, StringComparison.Ordinal);

        return end < 0 ? page[start..] : page[start..end];
    }

    /// <summary>
    /// Reads a numeral the page writes.
    /// </summary>
    /// <param name="written">The numeral as the page writes it.</param>
    /// <returns>Its value.</returns>
    private static double Number(string written) =>
        double.Parse(written, CultureInfo.InvariantCulture);

    /// <summary>
    /// The entropy page.
    /// </summary>
    /// <remarks>
    /// Found by walking up from the test binary until a directory holds both
    /// the solution and the page, which is how the other legs over a tracked
    /// document find one: the number of levels under the binary moves with the
    /// configuration and the target framework, and the marker does not. Nothing
    /// is written and nothing outside the repository is read.
    /// </remarks>
    /// <returns>The text of the page.</returns>
    private static string EntropyPage()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var page = Path.Combine(directory.FullName, "docs", "code-entropy.md");
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
            + " holds both Jellyfin.Plugin.Invites.sln and docs/code-entropy.md, so this comparison read nothing. Failing rather than passing over an empty page.");
    }
}
