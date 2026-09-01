using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// docs/personal-data.md is the inventory written for somebody deciding what
/// may be stored at all, and docs/what-is-held-about-a-person.md is the same
/// inventory written for the person it is about. Two pages saying what is held
/// are worse than one unless something holds them together: a reader told what
/// is kept about them, off a page nothing compares against the inventory the
/// record is built to, is being told something nobody checked.
/// </summary>
/// <remarks>
/// <para>
/// Two sets of field names are compared and have to be equal. On the inventory
/// they are the first cell of every row of the record table and the trail table
/// whose last cell is not <c>Not stored</c>; on the person's page they are the
/// first cell of every row of its own table. The filter is what makes the
/// comparison about stored fields rather than about rows, and it is asserted in
/// its own right below rather than being trusted.
/// </para>
/// <para>
/// What this does not read is whether either page is <b>true</b>. A row saying a
/// field is removed by something that removes nothing passes here. That is a
/// judgement about meaning and the review is where it is caught. It also reads
/// no other section of either page, so prose naming a field outside the tables
/// is out of scope rather than exempted by name.
/// </para>
/// </remarks>
public class PersonalDataForAPersonTests
{
    /// <summary>
    /// The headings on docs/personal-data.md whose tables hold stored fields.
    /// The setup-form table is deliberately not among them: none of the three
    /// values on it is stored by this plugin, which is what its own rows say,
    /// and <see cref="SetupFormInventoryTests"/> is what holds that table to
    /// the served form.
    /// </summary>
    private static readonly string[] InventoryHeadings =
    {
        "## The invitation record",
        "## The attempt trail",
    };

    /// <summary>
    /// The heading on docs/what-is-held-about-a-person.md whose table is the
    /// person's copy of the inventory. Rows are read only under it, so a table
    /// added elsewhere on that page is not silently taken for this one.
    /// </summary>
    private const string PersonHeading = "## What is held";

    /// <summary>
    /// What the inventory's last cell says for a field that is written down
    /// nowhere. Such a row is on that page to record a decision not to store
    /// something, so it names no field the person's page could have a line for.
    /// </summary>
    private const string NotStored = "Not stored";

    /// <summary>
    /// The stored fields docs/personal-data.md carries.
    /// </summary>
    /// <returns>The field names, ordered.</returns>
    private static IReadOnlyList<string> StoredFieldsInTheInventory()
    {
        var fields = new List<string>();
        foreach (var heading in InventoryHeadings)
        {
            var rows = RowsUnder(PageLines("personal-data.md"), heading);
            if (rows.Count == 0)
            {
                throw new InvalidOperationException(
                    "docs/personal-data.md has no table rows under "
                    + heading
                    + ", so this comparison read no fields from it. Failing rather than passing over nothing.");
            }

            fields.AddRange(rows.Where(row => !IsNotStored(row)).Select(row => row[0]));
        }

        return fields.OrderBy(field => field, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The fields docs/personal-data.md records a decision not to store.
    /// </summary>
    /// <returns>The field names, ordered.</returns>
    private static IReadOnlyList<string> FieldsTheInventorySaysAreNotStored()
    {
        return InventoryHeadings
            .SelectMany(heading => RowsUnder(PageLines("personal-data.md"), heading))
            .Where(IsNotStored)
            .Select(row => row[0])
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The fields docs/what-is-held-about-a-person.md has a line for.
    /// </summary>
    /// <returns>The field names, ordered.</returns>
    private static IReadOnlyList<string> FieldsOnThePersonsPage()
    {
        return RowsUnder(PageLines("what-is-held-about-a-person.md"), PersonHeading)
            .Select(row => row[0])
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Whether a row records a decision not to store the field it names, which
    /// is its last cell rather than anything about its first.
    /// </summary>
    /// <param name="row">The cells of one row.</param>
    /// <returns>True where the last cell is the not-stored wording.</returns>
    private static bool IsNotStored(IReadOnlyList<string> row)
    {
        return string.Equals(row[^1], NotStored, StringComparison.Ordinal);
    }

    /// <summary>
    /// The cells of every table row between a second-level heading and the next
    /// one. The header row and the alignment row underneath it are not rows of
    /// the table and are dropped, by the first cell they carry rather than by
    /// counting how many lines follow the heading, because a paragraph between
    /// the heading and the table moves the count and not the wording.
    /// </summary>
    /// <param name="lines">The lines of a page.</param>
    /// <param name="heading">The second-level heading to read under.</param>
    /// <returns>One list of trimmed cells per row.</returns>
    private static IReadOnlyList<IReadOnlyList<string>> RowsUnder(
        IReadOnlyList<string> lines,
        string heading)
    {
        var rows = new List<IReadOnlyList<string>>();
        var inside = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inside = string.Equals(line.TrimEnd(), heading, StringComparison.Ordinal);
                continue;
            }

            if (!inside || !line.StartsWith("|", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line
                .Trim()
                .Trim('|')
                .Split('|')
                .Select(cell => cell.Trim())
                .ToList();

            if (cells.Count < 2
                || string.Equals(cells[0], "Field", StringComparison.Ordinal)
                || cells[0].StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            rows.Add(cells);
        }

        return rows;
    }

    /// <summary>
    /// Reads a page out of docs/ in the working tree.
    /// </summary>
    /// <remarks>
    /// The directory is found by walking up from the test binary until one holds
    /// both the solution and the page, rather than by counting how many levels
    /// of bin/Release sit under it. The count changes with a configuration or a
    /// target framework and the marker does not. Nothing is written and nothing
    /// outside the repository is read, so this stays inside the headless rule.
    /// </remarks>
    /// <param name="name">The file name under docs/.</param>
    /// <returns>The lines of the page.</returns>
    private static IReadOnlyList<string> PageLines(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var page = Path.Combine(directory.FullName, "docs", name);
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
            + " holds both Jellyfin.Plugin.Invites.sln and docs/"
            + name
            + ", so this comparison read nothing. Failing rather than passing over an empty page.");
    }

    /// <summary>
    /// The two pages carry the same inventory. It reds in both directions: a
    /// field stored with no line on the person's page, which is something held
    /// about somebody that they are not told about, and a line on that page
    /// naming a field the record does not carry, which tells them something is
    /// held that is not.
    /// </summary>
    [Fact]
    public void EveryStoredFieldOfTheInventoryHasALineOnThePersonsPage()
    {
        var inTheInventory = StoredFieldsInTheInventory();
        var onThePersonsPage = FieldsOnThePersonsPage();

        Assert.NotEmpty(inTheInventory);
        Assert.NotEmpty(onThePersonsPage);
        Assert.Equal(inTheInventory, onThePersonsPage);
    }

    /// <summary>
    /// The assertion above rests on the not-stored filter, and a filter that had
    /// stopped matching would quietly widen the set on one side while both
    /// emptiness checks still passed. This is the narrower statement it rests
    /// on: the inventory records three fields as not stored, they are the three
    /// its own prose argues under the failed test, and none of them is on the
    /// person's page.
    /// </summary>
    [Fact]
    public void TheFieldsTheInventoryRefusesAreNotOnThePersonsPage()
    {
        var refused = FieldsTheInventorySaysAreNotStored();

        Assert.Equal(
            new[] { "Contact address", "Operator label", "Source address" },
            refused);
        Assert.Empty(refused.Intersect(FieldsOnThePersonsPage(), StringComparer.Ordinal));
    }
}
