using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The operator guide carries the cloned-server case, and carries it whole.
/// </summary>
/// <remarks>
/// <para>
/// <c>docs/disaster-cases.md</c> states three things that happen to servers and
/// are neither attacks nor bugs. Two of the three are marked undetected, which
/// means the plugin will never say a word about them and the only place an
/// operator can meet them is the page they read. The cloned server is the one
/// where meeting it late costs something that cannot be taken back: the copy
/// takes the key every stored code is hashed under, and deleting the second
/// machine afterwards does not return it.
/// </para>
/// <para>
/// So the instruction has to be where somebody looks, and it has to arrive with
/// its cost attached. Rotating the key invalidates every invitation minted
/// before it, including the ones nobody has touched, and an instruction that
/// leaves that out is one an operator follows and then discovers they have
/// silently cancelled every link they sent this month.
/// </para>
/// <para>
/// <b>What is refused.</b> An operator guide whose section on the rotation drops
/// any of three things: that the plugin cannot see the copy, that the rotation
/// is what the operator does about it, or what the rotation costs. Each is
/// matched inside the one section that names the rotation route, rather than
/// anywhere on the page, because the three sentences are only an instruction
/// together and a page carrying them in three unrelated places is not one.
/// </para>
/// <para>
/// The negative is held from both ends. <c>docs/disaster-cases.md</c> records
/// this case as undetected, and the leg below reads that page as well, so a
/// guide that quietly grows into saying the plugin notices a clone reddens
/// against the page that decided it did not rather than against a phrase written
/// here.
/// </para>
/// <para>
/// <b>Its bound, stated rather than left to be found.</b> This matches spellings
/// over prose. Whether the paragraphs read well, or whether an operator who
/// followed them would end up with the right machine holding the identity, is a
/// judgement no reading of the tree makes. A green run says the three parts of
/// the instruction are present in one section, which is a smaller claim than the
/// section being good advice.
/// </para>
/// </remarks>
public class OperatorGuideTests
{
    /// <summary>
    /// The route the operator is sent to. Naming the route rather than a heading
    /// keeps this leg attached to the thing that has to be reachable.
    /// </summary>
    private const string RotationRoute = "POST /Invites/HashSecret/Rotate";

    /// <summary>
    /// The plugin cannot see that a data directory was copied.
    /// </summary>
    private static readonly Regex _cannotSeeIt = new(
        @"indistinguishable|does not detect it|cannot see",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// What the rotation costs: every invitation minted before it stops working.
    /// </summary>
    private static readonly Regex _statesTheCost = new(
        @"no invitation minted before the rotation can be redeemed again",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A run of whitespace, so a hard-wrapped sentence is compared as one line.
    /// </summary>
    private static readonly Regex _whitespace = new(
        @"\s+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The copy itself, so the section is about this case and not about a key
    /// that leaked some other way.
    /// </summary>
    private static readonly Regex _namesTheCopy = new(
        @"cop(y|ies|ied|ying)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// Exactly one section of the guide sends the operator to the rotation, and
    /// it is the section about a copied server.
    /// </summary>
    [Fact]
    public void OneSectionOfTheGuideSendsACopiedServerToTheRotation()
    {
        var sending = Sections(Page("operator-guide.md"))
            .Where(section => section.Body.Contains(RotationRoute, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            sending.Length == 1,
            "docs/operator-guide.md has "
            + sending.Length
            + " section(s) naming "
            + RotationRoute
            + ", and the instruction for a copied server is one of them. An operator who copied a data directory has to be able to find the rotation, and a guide that names it nowhere leaves the only answer to that case in a page written for whoever builds the plugin.");

        Assert.True(
            _namesTheCopy.IsMatch(Unwrapped(sending[0].Body)),
            "docs/operator-guide.md sends an operator to "
            + RotationRoute
            + " under the section "
            + sending[0].Heading
            + ", and that section never mentions copying anything. The rotation is the answer to a copied data directory, so a reader who has just made one has to recognise their own situation in the heading and the first line rather than in the route name.");
    }

    /// <summary>
    /// The section says what the rotation costs, in the same section that asks
    /// for it.
    /// </summary>
    [Fact]
    public void TheSectionStatesWhatRotatingCosts()
    {
        var section = TheRotationSection();

        Assert.True(
            _statesTheCost.IsMatch(Unwrapped(section.Body)),
            "docs/operator-guide.md tells an operator to rotate the key under the section "
            + section.Heading
            + " without saying that no invitation minted before the rotation can be redeemed again. Rotating is a revoke-everything operation, and an instruction that leaves the cost out is one somebody follows and then finds they have cancelled every link they had outstanding.");
    }

    /// <summary>
    /// The guide keeps the negative that <c>docs/disaster-cases.md</c> records,
    /// rather than growing into a claim that the plugin notices a clone.
    /// </summary>
    [Fact]
    public void TheGuideKeepsTheNegativeTheDisasterCasesPageRecords()
    {
        var clone = Sections(Page("disaster-cases.md"))
            .Single(section => section.Heading.Contains("clone", StringComparison.OrdinalIgnoreCase));

        Assert.True(
            clone.Body.Contains("Detected: no", StringComparison.Ordinal),
            "docs/disaster-cases.md no longer records the cloned server as undetected under the section "
            + clone.Heading
            + ", so this comparison has nothing to hold the guide to. If the plugin has learned to notice a clone, that is the page where it is decided and this leg is what has to follow it.");

        var section = TheRotationSection();

        Assert.True(
            _cannotSeeIt.IsMatch(Unwrapped(section.Body)),
            "docs/disaster-cases.md records the cloned server as undetected and docs/operator-guide.md does not say so under the section "
            + section.Heading
            + ". An operator reading only the guide would be left waiting for a warning that is never coming, which is the worse half of an undisclosed absence: not a missing feature, but a person who believes they are being watched over.");
    }

    /// <summary>
    /// A section's text with every run of whitespace collapsed to one space.
    /// </summary>
    /// <remarks>
    /// These pages are hard-wrapped, so a sentence this leg is looking for
    /// arrives broken across two lines and a pattern written as one line finds
    /// nothing. Collapsing first means the comparison is about the words rather
    /// than about where the paragraph happened to wrap, which is the one thing a
    /// later edit to the same sentence is most likely to move.
    /// </remarks>
    /// <param name="body">The text under a heading.</param>
    /// <returns>The same text on one line.</returns>
    private static string Unwrapped(string body) =>
        _whitespace.Replace(body, " ");

    /// <summary>
    /// The one section of the guide that names the rotation route.
    /// </summary>
    /// <remarks>
    /// This fails with a sentence rather than letting the search throw, because
    /// the two legs resting on it are about what that section says and a reader
    /// who deleted the section is owed the same message either way.
    /// </remarks>
    /// <returns>Its heading and body.</returns>
    private static (string Heading, string Body) TheRotationSection()
    {
        var sending = Sections(Page("operator-guide.md"))
            .Where(section => section.Body.Contains(RotationRoute, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            sending.Length == 1,
            "docs/operator-guide.md has "
            + sending.Length
            + " section(s) naming "
            + RotationRoute
            + ", so there is no one section for this leg to read. The instruction for a copied server lives in exactly one place on that page.");

        return sending[0];
    }

    /// <summary>
    /// The second-level sections of a page, each with the text under it.
    /// </summary>
    /// <param name="page">The text of the page.</param>
    /// <returns>One entry per second-level heading, in the order they appear.</returns>
    private static IReadOnlyList<(string Heading, string Body)> Sections(string page)
    {
        var sections = new List<(string Heading, string Body)>();
        var heading = string.Empty;
        var body = new List<string>();

        foreach (var line in page.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (heading.Length != 0)
                {
                    sections.Add((heading, string.Join("\n", body)));
                }

                heading = line[3..].Trim();
                body = new List<string>();
                continue;
            }

            body.Add(line);
        }

        if (heading.Length != 0)
        {
            sections.Add((heading, string.Join("\n", body)));
        }

        return sections;
    }

    /// <summary>
    /// A page under the documentation directory.
    /// </summary>
    /// <remarks>
    /// Found by walking up from the test binary until a directory holds both the
    /// solution and the page, which is how the other legs over a tracked
    /// document find one: the number of levels under the binary moves with the
    /// configuration and the target framework, and the marker does not. Nothing
    /// is written and nothing outside the repository is read.
    /// </remarks>
    /// <param name="name">The file name under the documentation directory.</param>
    /// <returns>The text of the page.</returns>
    private static string Page(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var page = Path.Combine(directory.FullName, "docs", name);
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
            + " holds both Jellyfin.Plugin.Invites.sln and docs/"
            + name
            + ", so this comparison read nothing. Failing rather than passing over an empty page.");
    }
}
