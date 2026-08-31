using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// <c>CONTRIBUTING.md</c> sends a reader to <c>docs/tests-not-written.md</c> and
/// states a count of what is on it. Both are held here against that page rather
/// than by whoever last edited either file.
/// </summary>
/// <remarks>
/// <para>
/// #100's last clause is that the refusal list is referenced from the
/// contributing document, and that reference is the whole of what makes the list
/// reachable: somebody about to add a test that needs a browser reads the
/// contributing document, not the list. A reference deleted in a tidy-up leaves
/// the list in the tree and out of the path anybody walks, and every route in
/// this repository stayed green while it happened.
/// </para>
/// <para>
/// <b>The count is the half with a history.</b> The same paragraph says how many
/// of the replacements are a person doing something once per release. That is a
/// count of rows on another page, typed as a word, in a third file. The two
/// counts that page states about itself have both been wrong, and
/// <see cref="TestsNotWrittenPageTests"/> is the repair for those; this one sits
/// one document further out and was read by nothing at all. A row added on that
/// page's own instructions, naming a manual check, leaves this word saying what
/// it said before.
/// </para>
/// <para>
/// <b>What is refused.</b> A contributing document that does not name the
/// refusal list, a reference to a path that is not in the tree, and a stated
/// count that is not the number of rows naming a manual check. A fourth leg asks
/// that both sentences were found at all, so a paragraph reworded past the
/// patterns reds rather than reporting the same silence as two documents that
/// agree.
/// </para>
/// <para>
/// <b>What these legs do not do.</b> They resolve a path and compare a number.
/// Whether the paragraph around either still says something true is a reading a
/// person makes, and so is whether a row's replacement covers the risk the row
/// claims. #100 is where that stays open, in the same words that page uses about
/// itself.
/// </para>
/// <para>
/// A row counts as naming a manual check when its body names
/// <c>docs/manual-checks.md</c>. That is the page a run is recorded on, so a row
/// pointing at it is a row whose replacement is a person; a row describing a
/// manual check without naming that page is outside this population and is
/// refused by nothing, which is the bound to read before treating the count as
/// derived from meaning rather than from a reference.
/// </para>
/// <para>
/// <b>The means.</b> A test in this suite rather than a rule in
/// <c>.github/lint/invariants.sh</c>. That rule set matches a spelling and
/// cannot say that a match is required in one document and compared against a
/// count in another, which is exactly this property. The suite already reads
/// tracked text outside the plugin project in
/// <see cref="TestsNotWrittenPageTests"/> and in
/// <see cref="SuiteDirectoryTests"/>, it runs on the same command as everything
/// else, and it opens no network connection and writes nothing.
/// </para>
/// </remarks>
public class ContributingNamesTheRefusalListTests
{
    /// <summary>
    /// The refusal list, named by its path in the repository.
    /// </summary>
    private static readonly Regex _namesTheList = new(
        @"docs/tests-not-written\.md",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The page a run of a manual check is recorded on, named by its path.
    /// </summary>
    private static readonly Regex _namesTheRunRecord = new(
        @"docs/manual-checks\.md",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The sentence stating how many replacements are a person doing something
    /// once per release, capturing the word that is the count.
    /// </summary>
    /// <remarks>
    /// The words are separated by <c>\s+</c> rather than by spaces because the
    /// sentence is wrapped and the count sits at the end of a line, so a pattern
    /// written with literal spaces would depend on where the paragraph happens
    /// to break.
    /// </remarks>
    private static readonly Regex _statedCount = new(
        @"\b([A-Za-z]+)\s+of\s+the\s+replacements\s+are\s+a\s+person\s+doing\s+something\s+once\s+per\s+release\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The heading the rows of the refusal list sit under.
    /// </summary>
    /// <remarks>
    /// The line has to end after the word, so the other headings on that page
    /// opening with the same two words are not read as the one over the rows. A
    /// carriage return is admitted before the end of the line, because a clone
    /// with <c>core.autocrlf</c> set would otherwise make the verdict depend on
    /// the reader's git configuration rather than on the page.
    /// </remarks>
    private static readonly Regex _headingOverTheRows = new(
        @"^## The ([A-Za-z]+)[ \t]*\r?$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A row of the refusal list.
    /// </summary>
    private static readonly Regex _row = new(
        @"^### ",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// A heading of the rank the rows sit under, which is where the section
    /// holding them ends.
    /// </summary>
    private static readonly Regex _section = new(
        @"^## ",
        RegexOptions.Multiline | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The words the count is written as.
    /// </summary>
    /// <remarks>
    /// A document a person reads spells a small count out, so the map lives here
    /// rather than the document being asked to write digits. It stops where it
    /// does because the count is a count of rows on one page, and a list of
    /// refusals running past twelve would be a different document.
    /// </remarks>
    private static readonly Dictionary<string, int> _numberWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "none", 0 },
            { "one", 1 },
            { "two", 2 },
            { "three", 3 },
            { "four", 4 },
            { "five", 5 },
            { "six", 6 },
            { "seven", 7 },
            { "eight", 8 },
            { "nine", 9 },
            { "ten", 10 },
            { "eleven", 11 },
            { "twelve", 12 },
        };

    /// <summary>
    /// Both sentences are in the contributing document and the refusal list
    /// carries rows. Without this, a paragraph reworded past either pattern and
    /// a list that had lost its rows would report the same silence as two
    /// documents that agree, and the legs below would compare nothing.
    /// </summary>
    [Fact]
    public void TheScanFindsTheSentencesTheContributingDocumentStates()
    {
        var contributing = Contributing();

        Assert.Matches(_namesTheList, contributing);
        Assert.Matches(_statedCount, contributing);
        Assert.NotEmpty(Rows());
    }

    /// <summary>
    /// The contributing document names the refusal list, and the paths it names
    /// are in the tree. #100 asks for the reference; the rest is what stops a
    /// reference outliving the file it points at.
    /// </summary>
    [Fact]
    public void TheContributingDocumentNamesTheRefusalList()
    {
        Assert.True(
            _namesTheList.IsMatch(Contributing()),
            "CONTRIBUTING.md no longer names docs/tests-not-written.md, so the list of tests this suite refuses is in the tree and out of the path anybody reads before adding one. #100's last clause is that reference.");

        Assert.True(
            File.Exists(Path.Combine(Root(), "docs", "tests-not-written.md")),
            "CONTRIBUTING.md names docs/tests-not-written.md and no such file is in the tree, so the reference sends a reader nowhere.");

        Assert.True(
            File.Exists(Path.Combine(Root(), "docs", "manual-checks.md")),
            "CONTRIBUTING.md names docs/manual-checks.md as where a run of a manual check is recorded and no such file is in the tree, so the replacement it points at has nowhere to be recorded.");
    }

    /// <summary>
    /// The count the contributing document states is the number of rows on the
    /// refusal list whose replacement is a person doing something once per
    /// release. It is one word, in a third file, counting rows on a second one.
    /// </summary>
    [Fact]
    public void TheCountOfManualReplacementsIsTheNumberOfRowsThatNameOne()
    {
        var rows = Rows().Count(row => _namesTheRunRecord.IsMatch(row));

        var match = _statedCount.Match(Contributing());
        Assert.True(
            match.Success,
            "CONTRIBUTING.md no longer states how many replacements are a person doing something once per release, in the words this leg reads, so nothing compares that number against the list. Failing rather than passing over a count that has stopped being read.");

        var word = match.Groups[1].Value;
        Assert.True(
            _numberWords.ContainsKey(word),
            "CONTRIBUTING.md states how many replacements are a person doing something once per release as \""
            + word
            + "\", which is not a number word this leg can read, so the count it states is compared against nothing.");

        Assert.True(
            _numberWords[word] == rows,
            "CONTRIBUTING.md says "
            + word
            + " of the replacements are a person doing something once per release, and "
            + rows.ToString(CultureInfo.InvariantCulture)
            + " row(s) of docs/tests-not-written.md name docs/manual-checks.md. A row added on that page's own instructions leaves this word saying what it said before.");
    }

    /// <summary>
    /// The rows of the refusal list, each as its own text.
    /// </summary>
    /// <remarks>
    /// Taken from inside the section the heading opens rather than over the
    /// whole page, so a subheading written anywhere else is not read as a
    /// refusal.
    /// </remarks>
    /// <returns>One entry per row, in the order the page carries them.</returns>
    private static IReadOnlyList<string> Rows()
    {
        var page = RefusalList();

        var heading = _headingOverTheRows.Match(page);
        Assert.True(
            heading.Success,
            "docs/tests-not-written.md no longer carries the heading the rows sit under, so nothing knows where the list begins. Failing rather than reading the whole page as rows.");

        var after = page[(heading.Index + heading.Length)..];
        var next = _section.Match(after);
        var body = next.Success ? after[..next.Index] : after;

        var starts = _row.Matches(body).Select(match => match.Index).ToList();

        return starts
            .Select((start, index) =>
                index + 1 < starts.Count ? body[start..starts[index + 1]] : body[start..])
            .ToList();
    }

    /// <summary>
    /// The contributing document.
    /// </summary>
    /// <returns>Its text.</returns>
    private static string Contributing() =>
        File.ReadAllText(Path.Combine(Root(), "CONTRIBUTING.md"));

    /// <summary>
    /// The refusal list.
    /// </summary>
    /// <returns>Its text.</returns>
    private static string RefusalList() =>
        File.ReadAllText(Path.Combine(Root(), "docs", "tests-not-written.md"));

    /// <summary>
    /// The root of the repository.
    /// </summary>
    /// <remarks>
    /// Found by walking up from the test binary until a directory holds the
    /// solution and both documents, which is how the other legs over tracked
    /// text find one: the number of levels under the binary moves with the
    /// configuration and the target framework, and the marker does not.
    /// </remarks>
    /// <returns>The directory holding the solution and both documents.</returns>
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            var contributing = Path.Combine(directory.FullName, "CONTRIBUTING.md");
            var page = Path.Combine(directory.FullName, "docs", "tests-not-written.md");
            if (File.Exists(solution) && File.Exists(contributing) && File.Exists(page))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds Jellyfin.Plugin.Invites.sln, CONTRIBUTING.md and docs/tests-not-written.md, so these legs read nothing. Failing rather than reporting a comparison that ran over an empty set.");
    }
}
