using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Invites.Attempts;
using Jellyfin.Plugin.Invites.Redemption;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The outcome set on docs/attempt-outcomes.md, held against the two types that
/// carry it.
/// </summary>
/// <remarks>
/// <para>
/// That page states its own failure mode and had nothing behind it: an issue
/// that adds a refusal without adding its member there produces an attempt with
/// no entry, which fails the one-entry-per-attempt property quietly rather than
/// loudly. <see cref="AttemptOutcome"/> is the set the trail writes and is
/// compared here in both directions; <see cref="RedemptionOutcome"/> is the
/// narrower set one routine concludes and is compared in one, because it is a
/// subset of the page by design rather than an equal of it.
/// </para>
/// <para>
/// <b>Four members rather than five.</b> <c>Honoured</c> deliberately has no row.
/// The decision's <c>Honoured</c> says the invitation may produce an account;
/// the page's <c>Accepted</c> says one was created, and a redemption that was
/// honoured and then failed to create an account is the difference between them.
/// Requiring a row named <c>Honoured</c> would ask the page to collapse two
/// states into one, so the fifth member is asserted absent instead, with the
/// page's own word for that case asserted present beside it.
/// </para>
/// <para>
/// <b>What it cannot see.</b> It compares names. Whether a row's description is
/// right, whether the issue in the third column is open, and whether the set is
/// the right set are all judgements no reading of this tree makes. It also says
/// nothing about what is appended: nothing on a running server appends an entry,
/// and this compares lists rather than watching an attempt.
/// </para>
/// </remarks>
public class AttemptOutcomeSetTests
{
    /// <summary>
    /// The one member of the decision's set that the page deliberately spells
    /// differently, because it names a different state.
    /// </summary>
    private const string TheMemberWithNoRow = "Honoured";

    /// <summary>
    /// The page's word for a redemption that was honoured and produced an
    /// account.
    /// </summary>
    private const string ThePageWordForACreatedAccount = "Accepted";

    /// <summary>
    /// Every verdict the decision routine can reach, other than the one above,
    /// has a row on the page under the same spelling.
    /// </summary>
    /// <remarks>
    /// Adding a refusal to <see cref="RedemptionOutcome"/> and leaving the page
    /// alone is what this refuses, and it is the change somebody makes while
    /// adding the refusal itself rather than a separate mistake.
    /// </remarks>
    [Fact]
    public void EveryVerdictTheDecisionReachesHasARowOnThisPage()
    {
        var rows = OutcomeRows();

        var missing = Enum.GetNames<RedemptionOutcome>()
            .Where(name => name != TheMemberWithNoRow)
            .Where(name => !rows.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "These members of RedemptionOutcome have no row in the set on docs/attempt-outcomes.md, so an attempt reaching one of them would be recorded under a name that page does not carry: "
            + string.Join(", ", missing));
    }

    /// <summary>
    /// The page keeps its own word for the honoured case, and does not gain the
    /// decision's word for it.
    /// </summary>
    /// <remarks>
    /// This is the assertion that stops the test above being satisfied by
    /// renaming a row. A page that added a <c>Honoured</c> row would pass the
    /// first leg and would have lost the distinction between an invitation that
    /// may produce an account and one that did.
    /// </remarks>
    [Fact]
    public void TheHonouredVerdictAndTheCreatedAccountAreTwoWordsForTwoStates()
    {
        var rows = OutcomeRows();

        Assert.True(
            rows.Contains(ThePageWordForACreatedAccount),
            "docs/attempt-outcomes.md no longer carries a row named "
            + ThePageWordForACreatedAccount
            + ", which is its word for a redemption that produced an account. Nothing here compared the two sets against each other.");

        Assert.False(
            rows.Contains(TheMemberWithNoRow),
            "docs/attempt-outcomes.md has gained a row named "
            + TheMemberWithNoRow
            + ", which is the decision's word for an invitation that MAY produce an account rather than one that did. The trail needs both states, so the two sets keep two words.");
    }

    /// <summary>
    /// Every member of the trail's own set has a row, spelled the same way.
    /// </summary>
    /// <remarks>
    /// The narrower comparison above reads the decision's set, which is four of
    /// these names. This one reads the set the trail actually writes, so a
    /// refusal that never reaches the decision routine - the rate limit, the
    /// ceiling, the cross-site check, the validation - is held here or nowhere.
    /// </remarks>
    [Fact]
    public void EveryOutcomeTheTrailCanCarryHasARowOnThisPage()
    {
        var rows = OutcomeRows();

        var missing = Enum.GetNames<AttemptOutcome>()
            .Where(name => !rows.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "These members of AttemptOutcome have no row in the set on docs/attempt-outcomes.md, so an entry carrying one would be recorded under a name that page does not carry: "
            + string.Join(", ", missing));
    }

    /// <summary>
    /// Every row of the set is a member of the trail's type.
    /// </summary>
    /// <remarks>
    /// The other direction, and it is the one that catches a page describing an
    /// outcome nothing can produce. A reader takes the table for what the trail
    /// holds, so a row nobody implemented is a promise this plugin does not keep,
    /// and it reads exactly like a row that is kept.
    /// </remarks>
    [Fact]
    public void EveryRowOfTheSetIsAnOutcomeTheTrailCanCarry()
    {
        var members = Enum.GetNames<AttemptOutcome>();

        var unimplemented = OutcomeRows()
            .Where(row => !members.Contains(row, StringComparer.Ordinal))
            .ToArray();

        Assert.True(
            unimplemented.Length == 0,
            "These rows of the set on docs/attempt-outcomes.md name no member of AttemptOutcome, so the page promises an outcome no entry can carry: "
            + string.Join(", ", unimplemented));
    }

    /// <summary>
    /// The names in the first column of the table under the set's heading.
    /// </summary>
    /// <remarks>
    /// Found by walking up from the test binary until a directory holds both the
    /// solution and the page, which is how every other leg over a tracked
    /// document here finds one. Nothing is written and nothing outside the
    /// repository is read.
    /// </remarks>
    /// <returns>The outcome names the page's table declares.</returns>
    private static string[] OutcomeRows()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var page = Path.Combine(directory.FullName, "docs", "attempt-outcomes.md");
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            if (File.Exists(page) && File.Exists(solution))
            {
                var names = new Regex(
                    @"(?m)^\|\s*`([A-Za-z]+)`\s*\|",
                    RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(5))
                    .Matches(File.ReadAllText(page))
                    .Select(match => match.Groups[1].Value)
                    .ToArray();

                Assert.True(
                    names.Length > 0,
                    "docs/attempt-outcomes.md carries no table row naming an outcome, so this comparison read nothing. Failing rather than passing over an empty set.");

                return names;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds both Jellyfin.Plugin.Invites.sln and docs/attempt-outcomes.md, so this comparison read nothing. Failing rather than passing over an empty page.");
    }
}
