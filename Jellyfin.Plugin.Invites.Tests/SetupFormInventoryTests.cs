using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Setup;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// docs/setup-never-asks.md decides which questions the guided setup may ask,
/// and docs/personal-data.md is where what becomes of an answer is argued. The
/// second rule only means anything while the two agree, and until now nothing
/// read them together: a field added to the form with no row, or a row left
/// behind after a question was dropped, was invisible to every run.
/// </summary>
/// <remarks>
/// <para>
/// Two sets are compared. The field names inside the form the plugin serves,
/// read off the served bytes rather than off a copy on disk, and the field
/// names in the setup-form table of the inventory. They have to be equal.
/// </para>
/// <para>
/// What this does not read is whether the row is <b>true</b>. A row claiming a
/// value is never stored, beside a routine that stores it, passes here. That is
/// a judgement about meaning, the review is where it is caught, and the rules
/// that refuse a credential reaching a log line are in
/// <c>.github/lint/invariants.sh</c> rather than here.
/// </para>
/// </remarks>
public class SetupFormInventoryTests
{
    /// <summary>
    /// The heading the inventory's form table sits under. Rows are read only
    /// between it and the next second-level heading, so a table elsewhere on
    /// the page is not silently taken for this one.
    /// </summary>
    private const string InventoryHeading = "## The setup form";

    /// <summary>
    /// A row of that table, whose first cell is the field name in backticks.
    /// The backticks are what make this exact rather than a match on prose: a
    /// row headed <c>Username</c> in words would name no field.
    /// </summary>
    private static readonly Regex RowPattern =
        new(@"^\|\s*`(?<field>[A-Za-z0-9_-]+)`\s*\|", RegexOptions.Compiled);

    /// <summary>
    /// A named control, <c>name="username"</c>.
    /// </summary>
    private static readonly Regex NamePattern =
        new("name=\"(?<field>[^\"]+)\"", RegexOptions.Compiled);

    /// <summary>
    /// Every named control the served form carries, and whether each one is
    /// hidden.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only the form is read, so the viewport declaration in the head is out of
    /// scope rather than exempted by name. The slice is between the form's own
    /// tags, and each control is then the slice from one <c>&lt;input</c> to the
    /// first <c>&gt;</c> after it. That is a bounded region of a bounded region
    /// and not a parser: a parser here would be a dependency the runtime set
    /// does not carry for one assertion.
    /// </para>
    /// <para>
    /// The hidden flag is read per element rather than per line, because the
    /// controls on this page are written across several lines each and a reading
    /// per line would call every one of them visible.
    /// </para>
    /// </remarks>
    /// <returns>The controls, in the order the page carries them.</returns>
    private static IReadOnlyList<(string Field, bool Hidden)> ControlsOnTheForm()
    {
        var page = SetupPage.Html;
        var open = page.IndexOf("<form", StringComparison.Ordinal);
        var close = page.IndexOf("</form>", StringComparison.Ordinal);
        if (open < 0 || close <= open)
        {
            throw new InvalidOperationException(
                "The served page has no form region between <form and </form>, so this comparison read no fields. Failing rather than passing over nothing.");
        }

        var form = page[open..close];
        var controls = new List<(string, bool)>();
        var at = form.IndexOf("<input", StringComparison.Ordinal);
        while (at >= 0)
        {
            var ends = form.IndexOf('>', at);
            if (ends < 0)
            {
                throw new InvalidOperationException(
                    "The served form opens a control it never closes, so this reading cannot say which attributes belong to it. Failing rather than reading past the end of one element into the next.");
            }

            var element = form[at..ends];
            var named = NamePattern.Match(element);
            if (named.Success)
            {
                controls.Add((
                    named.Groups["field"].Value,
                    element.Contains("type=\"hidden\"", StringComparison.Ordinal)));
            }

            at = form.IndexOf("<input", ends, StringComparison.Ordinal);
        }

        return controls;
    }

    /// <summary>
    /// The names of every control on the form, hidden ones included.
    /// </summary>
    /// <returns>The field names, ordered.</returns>
    private static IReadOnlyList<string> FieldsOnTheForm() =>
        ControlsOnTheForm()
            .Select(control => control.Field)
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The names of the controls a person answers.
    /// </summary>
    /// <remarks>
    /// A hidden control is not a question. docs/setup-never-asks.md decides what
    /// may be ASKED, and the anti-forgery token is a value this plugin put on the
    /// form for itself, so counting it among the questions would make that page
    /// read as asking for four things.
    /// </remarks>
    /// <returns>The field names, ordered.</returns>
    private static IReadOnlyList<string> QuestionsOnTheForm() =>
        ControlsOnTheForm()
            .Where(control => !control.Hidden)
            .Select(control => control.Field)
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The fields the inventory has a row for.
    /// </summary>
    /// <returns>The field names, ordered.</returns>
    private static IReadOnlyList<string> FieldsInTheInventory()
    {
        var fields = new List<string>();
        var inside = false;
        foreach (var line in InventoryLines())
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inside = string.Equals(line.TrimEnd(), InventoryHeading, StringComparison.Ordinal);
                continue;
            }

            if (!inside)
            {
                continue;
            }

            var row = RowPattern.Match(line);
            if (row.Success)
            {
                fields.Add(row.Groups["field"].Value);
            }
        }

        return fields.OrderBy(field => field, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Reads docs/personal-data.md out of the working tree.
    /// </summary>
    /// <remarks>
    /// The file is found by walking up from the test binary until a directory
    /// holds both the solution and the page, rather than by counting how many
    /// levels of <c>bin/Release</c> sit under it. The count changes with a
    /// configuration or a target framework and the marker does not. Nothing is
    /// written and nothing outside the repository is read, so this stays inside
    /// the headless rule.
    /// </remarks>
    /// <returns>The lines of the page.</returns>
    private static IReadOnlyList<string> InventoryLines()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var page = Path.Combine(directory.FullName, "docs", "personal-data.md");
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
            + " holds both Jellyfin.Plugin.Invites.sln and docs/personal-data.md, so this comparison read nothing. Failing rather than passing over an empty page.");
    }

    /// <summary>
    /// The form asks for exactly what the inventory has rows for. It reds in
    /// both directions: a question added to the form with nobody having argued
    /// what becomes of the answer, and a row left behind after the question it
    /// describes was dropped, which is the row a reader would take for a value
    /// the plugin still receives.
    /// </summary>
    [Fact]
    public void EveryFieldOnTheFormHasARowInThePersonalDataInventory()
    {
        var onTheForm = FieldsOnTheForm();
        var inTheInventory = FieldsInTheInventory();

        Assert.NotEmpty(onTheForm);
        Assert.NotEmpty(inTheInventory);
        Assert.Equal(inTheInventory, onTheForm);
    }

    /// <summary>
    /// The assertion above compares two sets and would report the same thing
    /// for two files that agree as for a reading that stopped seeing anything,
    /// which the two emptiness checks are there to stop. This is the narrower
    /// statement they rest on: the three questions
    /// docs/setup-never-asks.md names are the three that are read.
    /// </summary>
    [Fact]
    public void TheThreeQuestionsTheRefusalListNamesAreTheOnesRead()
    {
        Assert.Equal(
            new[] { "confirmation", "password", "username" },
            QuestionsOnTheForm());
    }

    /// <summary>
    /// The one control the person does not answer is the anti-forgery token, and
    /// it is hidden.
    /// </summary>
    /// <remarks>
    /// The two readings above divide the form between questions and everything
    /// else, and a division nothing states the other half of is one that quietly
    /// absorbs the next hidden field somebody adds. This is that other half: one
    /// control, named by the type that decides the name rather than by a second
    /// spelling of it.
    /// </remarks>
    [Fact]
    public void TheOnlyControlThePersonDoesNotAnswerIsTheAntiForgeryToken()
    {
        Assert.Equal(
            new[] { FormToken.Field },
            ControlsOnTheForm()
                .Where(control => control.Hidden)
                .Select(control => control.Field)
                .ToArray());
    }

    /// <summary>
    /// What the post binds is exactly what the form asks for, and nothing
    /// wider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the direction the two assertions above cannot see, and
    /// docs/what-an-invitation-can-never-do.md named it as the gap while there
    /// was no post: a model bound from a request that carries a member the form
    /// has no control for is a value a stranger can set that nobody meant to
    /// offer, and it looks exactly like the form's own model in every other
    /// reading. A member is matched to a control by its name, compared ignoring
    /// case, because that is how the binder matches them.
    /// </para>
    /// <para>
    /// It reds in both directions on purpose. A member with no control is the
    /// widening; a control with no member is a question the person answers and
    /// the post never receives, which is the quieter half and is how a field
    /// ends up silently ignored.
    /// </para>
    /// <para>
    /// What this does not read is whether the post USES what it bound. That the
    /// confirmation is compared against the password is <c>SetupAnswersTests</c>, and
    /// that a post refused for it takes no use is <c>RedeemPostTests</c>: both are
    /// judgements about an action's body rather than a shape any reading of a type makes.
    /// </para>
    /// </remarks>
    [Fact]
    public void ThePostBindsTheFormsFieldsAndNothingWider()
    {
        var bound = typeof(SetupSubmission)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name.ToLowerInvariant())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(bound);
        Assert.Equal(FieldsOnTheForm(), bound);
    }
}
