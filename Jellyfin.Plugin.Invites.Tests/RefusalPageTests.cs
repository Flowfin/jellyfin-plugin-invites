using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.Invites.Setup;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The page every refusal of a presented code is answered with.
/// </summary>
/// <remarks>
/// <para>
/// <b>The wording is read out of the document rather than repeated here.</b>
/// docs/refusal-response.md decides what the page says and argues each of the
/// three sentences, and a test carrying its own copy of them would let the page
/// and the argument drift while both stayed green. So the sentences are lifted
/// out of that page's own block quote and looked for in the served bytes.
/// </para>
/// <para>
/// <b>What this does not read is whether the wording is right.</b> That is the
/// judgement the document makes, and no reading of the tree makes it. What is
/// refused here is the two halves disagreeing.
/// </para>
/// </remarks>
public class RefusalPageTests
{
    /// <summary>
    /// The spellings an address somewhere else is written in, as
    /// <see cref="SetupPageTests"/> reads them. The refusal is served under the
    /// same presentation rules as the setup page, which
    /// docs/setup-never-asks.md holds and docs/refusal-response.md says apply
    /// unchanged.
    /// </summary>
    private static readonly string[] Elsewhere = ["://", "\"//", "'//", "(//"];

    /// <summary>
    /// The page is served out of the assembly rather than off disk, so a server
    /// with no web client installed and an installation an operator has moved
    /// both serve the same bytes, and there is nowhere to leave a stale copy.
    /// </summary>
    [Fact]
    public void ThePageIsTheEmbeddedResource()
    {
        var assembly = typeof(RefusalPage).Assembly;

        using var stream = assembly.GetManifestResourceStream(RefusalPage.ResourceName);

        Assert.NotNull(stream);
    }

    /// <summary>
    /// Every sentence the document fixes is on the page, and the page is the one
    /// the document is about.
    /// </summary>
    /// <remarks>
    /// The comparison is over the words rather than over the bytes, because the
    /// page is wrapped to its own width and the document to its own, and a
    /// comparison of the two literal blocks would fail on where a line broke
    /// rather than on what was said.
    /// </remarks>
    [Fact]
    public void ThePageSaysWhatTheDocumentFixes()
    {
        var quoted = Quoted();

        Assert.True(quoted.Count >= 3, "docs/refusal-response.md carries fewer quoted sentences than this expected: " + quoted.Count);

        var served = Flattened(RefusalPage.Html);
        var absent = quoted.Where(sentence => !served.Contains(sentence, StringComparison.Ordinal)).ToList();

        Assert.True(
            absent.Count == 0,
            "docs/refusal-response.md fixes the wording of this page and the served bytes do not carry: "
            + string.Join(" | ", absent)
            + ". The page and the argument for what it says are one decision; move both or neither.");
    }

    /// <summary>
    /// The comparison sees a sentence that is missing. Without this, a reader
    /// that found nothing on either side would report the same green as one that
    /// found agreement.
    /// </summary>
    [Fact]
    public void TheComparisonReportsASentenceThePageDoesNotCarry()
    {
        var served = Flattened(RefusalPage.Html);

        Assert.DoesNotContain("this page cannot tell you which one applies, except on Tuesdays", served, StringComparison.Ordinal);
        Assert.Contains(Quoted()[0], served, StringComparison.Ordinal);
    }

    /// <summary>
    /// The page carries nothing that varies between the cases: no invitation
    /// identifier, no code, and no form to send one back with.
    /// </summary>
    /// <remarks>
    /// A refusal that named the record would tell a stranger the code was real,
    /// which is the disclosure the whole page exists to avoid, and it would do
    /// it while refusing them.
    /// </remarks>
    [Fact]
    public void ThePageCarriesNothingThatVariesBetweenTheCases()
    {
        Assert.DoesNotContain("<form", RefusalPage.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<input", RefusalPage.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<a ", RefusalPage.Html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// No script and no address of any other host, which is
    /// docs/setup-never-asks.md's presentation rule applied to this page
    /// unchanged.
    /// </summary>
    [Fact]
    public void ThePageLoadsNothingAndRunsNothing()
    {
        Assert.DoesNotContain("<script", RefusalPage.Html, StringComparison.OrdinalIgnoreCase);

        var elsewhere = Elsewhere.Where(spelling => RefusalPage.Html.Contains(spelling, StringComparison.Ordinal)).ToList();

        Assert.True(
            elsewhere.Count == 0,
            "The refusal page carries an address somewhere else, spelled: " + string.Join(", ", elsewhere));
    }

    /// <summary>
    /// The policy names the hash of this page's own style element, computed here
    /// rather than asked for.
    /// </summary>
    /// <remarks>
    /// Asking <see cref="RefusalPage"/> for the hash and comparing it with
    /// itself would pass over any implementation at all. These bytes are hashed
    /// independently, so a policy that stopped being derived from the page reds
    /// the moment the page moves.
    /// </remarks>
    [Fact]
    public void ThePolicyNamesTheHashOfThisPagesOwnStyle()
    {
        var open = RefusalPage.Html.IndexOf("<style>", StringComparison.Ordinal) + "<style>".Length;
        var close = RefusalPage.Html.IndexOf("</style>", StringComparison.Ordinal);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(RefusalPage.Html[open..close]));

        Assert.Contains(
            "style-src 'sha256-" + Convert.ToBase64String(digest) + "'",
            RefusalPage.ContentSecurityPolicy,
            StringComparison.Ordinal);
        Assert.Contains("default-src 'none'", RefusalPage.ContentSecurityPolicy, StringComparison.Ordinal);
    }

    /// <summary>
    /// The two pages this route serves are different bytes. A refusal that was
    /// the setup page again would send somebody back to a form that cannot work,
    /// and every assertion above would still pass.
    /// </summary>
    [Fact]
    public void TheRefusalIsNotTheSetupPage()
    {
        Assert.NotEqual(SetupPage.Html, RefusalPage.Html);
    }

    /// <summary>
    /// The sentences docs/refusal-response.md quotes as the page's wording,
    /// flattened the same way the page is.
    /// </summary>
    /// <returns>One string per quoted paragraph, in the order the document has them.</returns>
    private static IReadOnlyList<string> Quoted()
    {
        var quoted = new List<string>();
        var paragraph = new List<string>();
        foreach (var line in DocumentLines())
        {
            if (!line.StartsWith(">", StringComparison.Ordinal))
            {
                Gather(quoted, paragraph);
                continue;
            }

            var text = line[1..].Trim();
            if (text.Length == 0)
            {
                Gather(quoted, paragraph);
                continue;
            }

            paragraph.Add(text);
        }

        Gather(quoted, paragraph);

        return quoted;
    }

    /// <summary>
    /// Closes off one quoted paragraph. The emphasis the block quote writes its
    /// heading in is the document's own markup rather than part of the wording,
    /// so it comes off and the heading is compared like every other line.
    /// </summary>
    /// <param name="quoted">Where finished paragraphs go.</param>
    /// <param name="paragraph">The lines gathered so far, emptied here.</param>
    private static void Gather(List<string> quoted, List<string> paragraph)
    {
        if (paragraph.Count == 0)
        {
            return;
        }

        var joined = string.Join(" ", paragraph).Replace("**", string.Empty, StringComparison.Ordinal);
        paragraph.Clear();

        quoted.Add(Flattened(joined));
    }

    /// <summary>
    /// One line of text with every run of white space reduced to a single space,
    /// so two texts wrapped to different widths compare as what they say.
    /// </summary>
    /// <param name="text">The text to flatten.</param>
    /// <returns>The flattened form.</returns>
    private static string Flattened(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Reads docs/refusal-response.md out of the working tree, by walking up
    /// from the test binary until a directory holds both the solution and the
    /// page. Nothing is written and nothing outside the repository is read.
    /// </summary>
    /// <returns>The lines of the page.</returns>
    private static IReadOnlyList<string> DocumentLines()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var page = Path.Combine(directory.FullName, "docs", "refusal-response.md");
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            if (File.Exists(page) && File.Exists(solution))
            {
                return File.ReadAllLines(page);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds both Jellyfin.Plugin.Invites.sln and docs/refusal-response.md, so this comparison read nothing. Failing rather than passing over an empty page.");
    }
}
