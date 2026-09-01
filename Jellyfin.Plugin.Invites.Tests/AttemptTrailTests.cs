using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Invites.Attempts;
using Jellyfin.Plugin.Invites.Codes;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The attempt trail: its entry, the closed set the entry carries, and the bound
/// that keeps an endpoint a stranger can hammer from filling a disk.
/// </summary>
/// <remarks>
/// <para>
/// Nothing appends to a trail on a running server. The route that judges a
/// presented code is #74 and #399 and does not exist, so what is asserted here is
/// the value and its rules rather than a redemption. Every clause of #43 about
/// the shape of an entry, the fixed set, the bound, the drop order and the drop
/// notice is reachable from that; the clause about every redemption attempt
/// appending exactly one entry is not, and no test here is written as though it
/// were.
/// </para>
/// <para>
/// No clock is read. Every instant below is a value the test chose, which is what
/// the seam in #41 exists for and what <c>SuiteDoesNotSleepTests</c> refuses the
/// alternative to.
/// </para>
/// </remarks>
public class AttemptTrailTests
{
    /// <summary>
    /// Every public member of <see cref="AttemptEntry"/>, against the row of the
    /// attempt-trail table in docs/personal-data.md it implements. A member with
    /// no row is a value nobody argued for, and the argument is the only thing
    /// between this entry and a source address, a code or somebody's name.
    /// </summary>
    private static readonly Dictionary<string, string> InventoryRows = new(StringComparer.Ordinal)
    {
        ["Invitation"] = "Invitation identifier, where one matched",
        ["Outcome"] = "Outcome",
        ["At"] = "Time",
        ["AttemptsCovered"] = "Attempts covered",

        // Two members, one stored field. Both are read off the outcome rather
        // than stored beside it, so an entry cannot say it is a drop notice and
        // not say so in its outcome.
        ["IsDropNotice"] = "Outcome",
        ["IsFailure"] = "Outcome",
    };

    /// <summary>
    /// The field the inventory names and refuses. It is in the trail's table on
    /// that page with the reasoning under it, and the reason it is refused is
    /// that a value kept for as long as the trail is a different thing from one
    /// kept for as long as a rate-limiting window.
    /// </summary>
    private const string TheRefusedField = "Source address";

    private static readonly DateTimeOffset Noon = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid AnInvitation = new Guid("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// One failure, at an instant the caller chose.
    /// </summary>
    /// <param name="seconds">Seconds past noon, so entries are orderable.</param>
    /// <returns>The entry.</returns>
    private static AttemptEntry AFailure(int seconds) => AttemptEntry.Of(
        AnInvitation,
        AttemptOutcome.Spent,
        Noon.AddSeconds(seconds),
        1);

    /// <summary>
    /// One success.
    /// </summary>
    /// <param name="seconds">Seconds past noon.</param>
    /// <returns>The entry.</returns>
    private static AttemptEntry ASuccess(int seconds) => AttemptEntry.Of(
        AnInvitation,
        AttemptOutcome.Accepted,
        Noon.AddSeconds(seconds),
        1);

    /// <summary>
    /// The entry carries the inventory and nothing else. A member added without
    /// a row on docs/personal-data.md reds this, and so does a row taken off the
    /// dictionary without its member going too.
    /// </summary>
    [Fact]
    public void EveryPublicMemberOfAnEntryIsARowInThePersonalDataInventory()
    {
        var members = typeof(AttemptEntry)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var expected = InventoryRows.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, members);
    }

    /// <summary>
    /// The dictionary above is a claim about a page, so the page is read. Every
    /// row it names is a row that table actually carries, which is what stops
    /// the mapping being satisfied by inventing a field name nobody wrote down.
    /// </summary>
    [Fact]
    public void EveryRowTheEntryClaimsIsOnThatPage()
    {
        var rows = TrailRowsOnThePersonalDataPage();

        var absent = InventoryRows.Values
            .Distinct(StringComparer.Ordinal)
            .Where(row => !rows.Contains(row, StringComparer.Ordinal))
            .ToArray();

        Assert.True(
            absent.Length == 0,
            "These fields are claimed as rows of the attempt-trail table on docs/personal-data.md and are not on it: "
            + string.Join(", ", absent));
    }

    /// <summary>
    /// The one field that page names and refuses is not a member. The refusal is
    /// a decision rather than an omission, so it is asserted rather than left to
    /// be true by nobody having added it.
    /// </summary>
    [Fact]
    public void TheFieldTheInventoryRefusesIsNotAMemberOfAnEntry()
    {
        Assert.Contains(TheRefusedField, TrailRowsOnThePersonalDataPage(), StringComparer.Ordinal);

        Assert.DoesNotContain(TheRefusedField, InventoryRows.Values, StringComparer.Ordinal);

        var members = typeof(AttemptEntry)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("Address", members, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The scan above reads properties, so a public field would be a member it
    /// never sees.
    /// </summary>
    [Fact]
    public void AnEntryExposesNoPublicField()
    {
        Assert.Empty(typeof(AttemptEntry).GetFields(BindingFlags.Public | BindingFlags.Instance));
    }

    /// <summary>
    /// No code, no hash and no password can be in an entry, because the type
    /// carries no text at all. This is the structural half of the clause; the
    /// inventory scan above is what refuses a field of some other type that held
    /// one.
    /// </summary>
    [Fact]
    public void NoMemberOfAnEntryIsTextAndNoneIsShapedLikeACode()
    {
        var properties = typeof(AttemptEntry)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToArray();

        Assert.DoesNotContain(typeof(string), properties.Select(property => property.PropertyType));

        var entry = AttemptEntry.Of(AnInvitation, AttemptOutcome.Revoked, Noon, 1);

        Assert.All(
            properties.Select(property => property.GetValue(entry)?.ToString() ?? string.Empty),
            text => Assert.Null(InvitationCode.Canonicalise(text)));
    }

    /// <summary>
    /// An entry saying no record matched may not name a record. The inventory's
    /// own word for that field is that it is empty where the presented code
    /// matched nothing, because there is nothing to name.
    /// </summary>
    [Fact]
    public void AnEntryForACodeThatMatchedNothingMayNotNameAnInvitation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AttemptEntry.Of(
            AnInvitation,
            AttemptOutcome.NoSuchInvitation,
            Noon,
            1));

        var entry = AttemptEntry.Of(null, AttemptOutcome.NoSuchInvitation, Noon, 1);

        Assert.Null(entry.Invitation);
    }

    /// <summary>
    /// An entry accounts for at least one attempt. Nought would be a line in the
    /// trail that nothing happened for, and a negative count would take attempts
    /// out of the total when the entry was dropped.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnEntryAccountsForAtLeastOneAttempt(int attemptsCovered)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AttemptEntry.Of(
            AnInvitation,
            AttemptOutcome.Spent,
            Noon,
            attemptsCovered));
    }

    /// <summary>
    /// The drop notice is the trail's own admission, so a caller cannot build one
    /// as the outcome of an attempt and cannot hand one to a trail.
    /// </summary>
    [Fact]
    public void ADropNoticeIsTheTrailsOwnAndNotACallersToWrite()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AttemptEntry.Of(
            null,
            AttemptOutcome.FailuresDropped,
            Noon,
            4));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => AttemptTrail.Empty.Append(AttemptEntry.Dropped(4, Noon)));
    }

    /// <summary>
    /// A notice about nothing is not an admission, it is a line saying a drop
    /// happened where none did.
    /// </summary>
    [Fact]
    public void ADropNoticeSaysHowManyWentAndRefusesToSayNone()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AttemptEntry.Dropped(0, Noon));
    }

    /// <summary>
    /// The trail holds failures up to its bound and drops nothing before it. The
    /// number is the one docs/attempt-outcomes.md derived from the two rates in
    /// docs/rate-limit.md and is not chosen again here.
    /// </summary>
    [Fact]
    public void FailuresAreKeptUpToTheBound()
    {
        var trail = FailuresTo(AttemptTrail.FailureBound);

        Assert.Equal(AttemptTrail.FailureBound, trail.Failures);
        Assert.Equal(AttemptTrail.FailureBound, trail.Entries.Length);
        Assert.DoesNotContain(trail.Entries, entry => entry.IsDropNotice);
    }

    /// <summary>
    /// One past the bound drops the oldest failure and says so, and the trail
    /// stays at the bound rather than above it.
    /// </summary>
    [Fact]
    public void OnePastTheBoundDropsTheOldestFailureAndSaysSo()
    {
        var trail = FailuresTo(AttemptTrail.FailureBound + 1);

        Assert.Equal(AttemptTrail.FailureBound, trail.Failures);

        var notices = trail.Entries.Where(entry => entry.IsDropNotice).ToArray();
        Assert.Single(notices);
        Assert.Equal(1, notices[0].AttemptsCovered);

        Assert.DoesNotContain(trail.Entries, entry => entry.At == Noon.AddSeconds(1));
        Assert.Contains(trail.Entries, entry => entry.At == Noon.AddSeconds(2));
    }

    /// <summary>
    /// Oldest first, and only failures. What is left after a drop is the newest
    /// failures rather than an arbitrary thousand of them.
    /// </summary>
    [Fact]
    public void TheOldestFailuresGoFirst()
    {
        const int Over = 25;
        var trail = FailuresTo(AttemptTrail.FailureBound + Over);

        var kept = trail.Entries
            .Where(entry => entry.IsFailure)
            .Select(entry => entry.At)
            .ToArray();

        Assert.Equal(Noon.AddSeconds(Over + 1), kept[0]);
        Assert.Equal(Noon.AddSeconds(AttemptTrail.FailureBound + Over), kept[^1]);
    }

    /// <summary>
    /// A success is never dropped. A single oldest-first ring over the whole
    /// trail is refused by name on docs/attempt-outcomes.md as a history-erasing
    /// attack, and this is that refusal held rather than described: the
    /// successes an operator opened the trail to find survive a stranger
    /// hammering the endpoint past the bound.
    /// </summary>
    [Fact]
    public void SuccessesAreNeverDropped()
    {
        var trail = AttemptTrail.Empty
            .Append(ASuccess(1))
            .Append(ASuccess(2));

        for (var i = 0; i < AttemptTrail.FailureBound + 50; i++)
        {
            trail = trail.Append(AFailure(100 + i));
        }

        var successes = trail.Entries
            .Where(entry => entry.Outcome == AttemptOutcome.Accepted)
            .Select(entry => entry.At)
            .ToArray();

        Assert.Equal(new[] { Noon.AddSeconds(1), Noon.AddSeconds(2) }, successes);
    }

    /// <summary>
    /// The trail holds at most one drop notice, however many drops it has made.
    /// A notice appended on every drop and never removed would be the unbounded
    /// thing the bound exists against, arriving through the door marked honesty.
    /// </summary>
    [Fact]
    public void TheTrailHoldsAtMostOneDropNoticeAndItsCountIsCumulative()
    {
        const int Over = 40;
        var trail = FailuresTo(AttemptTrail.FailureBound + Over);

        var notices = trail.Entries.Where(entry => entry.IsDropNotice).ToArray();

        Assert.Single(notices);
        Assert.Equal(Over, notices[0].AttemptsCovered);
    }

    /// <summary>
    /// A rate-limiting episode is one entry standing for many refused requests,
    /// which is the answer #43 and #31 share. It costs one of the thousand
    /// entries and counts as many attempts as it covers.
    /// </summary>
    [Fact]
    public void AnEpisodeIsOneEntryForAsManyAttemptsAsItCovers()
    {
        var episode = AttemptEntry.Of(AnInvitation, AttemptOutcome.RefusedByRateLimit, Noon, 20);

        var trail = AttemptTrail.Empty.Append(episode);

        Assert.Single(trail.Entries);
        Assert.Equal(1, trail.Failures);
        Assert.Equal(20, trail.AttemptsSeen);
    }

    /// <summary>
    /// A dropped episode takes its whole count with it. Counting a dropped entry
    /// as one attempt would lose nineteen of twenty and leave the trail claiming
    /// it had seen fewer attempts than it had.
    /// </summary>
    [Fact]
    public void ADroppedEpisodeTakesItsWholeCountIntoTheNotice()
    {
        var trail = AttemptTrail.Empty
            .Append(AttemptEntry.Of(AnInvitation, AttemptOutcome.RefusedByRateLimit, Noon, 20));

        for (var i = 1; i <= AttemptTrail.FailureBound; i++)
        {
            trail = trail.Append(AFailure(i));
        }

        var notices = trail.Entries.Where(entry => entry.IsDropNotice).ToArray();

        Assert.Single(notices);
        Assert.Equal(20, notices[0].AttemptsCovered);
    }

    /// <summary>
    /// Every attempt the trail ever saw is accounted for by exactly one entry,
    /// dropped ones included. Two independent statements of the same quantity:
    /// the count kept as the appends happen, and the sum over the entries that
    /// are left. A drop that lost a count would make them disagree.
    /// </summary>
    [Fact]
    public void EveryAttemptIsAccountedForByExactlyOneEntry()
    {
        var trail = AttemptTrail.Empty;
        var appended = 0L;

        for (var i = 1; i <= AttemptTrail.FailureBound + 120; i++)
        {
            var covers = i % 7 == 0 ? 20 : 1;
            var entry = i % 11 == 0
                ? ASuccess(i)
                : AttemptEntry.Of(AnInvitation, AttemptOutcome.RefusedByRateLimit, Noon.AddSeconds(i), covers);

            trail = trail.Append(entry);
            appended += entry.AttemptsCovered;
        }

        Assert.Equal(appended, trail.AttemptsSeen);
        Assert.Equal(appended, trail.Entries.Sum(entry => (long)entry.AttemptsCovered));
        Assert.Equal(AttemptTrail.FailureBound, trail.Failures);
    }

    /// <summary>
    /// A fresh install has an empty trail and nothing in it to read.
    /// </summary>
    [Fact]
    public void AFreshTrailHoldsNothing()
    {
        Assert.Empty(AttemptTrail.Empty.Entries);
        Assert.Equal(0, AttemptTrail.Empty.Failures);
        Assert.Equal(0, AttemptTrail.Empty.AttemptsSeen);
    }

    /// <summary>
    /// Appending answers with a new trail and leaves the one it was asked with
    /// alone, so a caller that kept a reference has the trail it read rather than
    /// one that moved under it.
    /// </summary>
    [Fact]
    public void AppendingLeavesTheTrailItWasAskedWithAlone()
    {
        var before = AttemptTrail.Empty.Append(AFailure(1));
        var after = before.Append(AFailure(2));

        Assert.Single(before.Entries);
        Assert.Equal(2, after.Entries.Length);
    }

    /// <summary>
    /// Nothing may be appended that is not there.
    /// </summary>
    [Fact]
    public void NothingIsAppendedForNoEntry()
    {
        Assert.Throws<ArgumentNullException>(() => AttemptTrail.Empty.Append(null!));
    }

    /// <summary>
    /// A trail with the given number of failures in it, one a second from noon.
    /// </summary>
    /// <param name="count">How many failures to append.</param>
    /// <returns>The trail.</returns>
    private static AttemptTrail FailuresTo(int count)
    {
        var trail = AttemptTrail.Empty;
        for (var i = 1; i <= count; i++)
        {
            trail = trail.Append(AFailure(i));
        }

        return trail;
    }

    /// <summary>
    /// The first cell of every row of the attempt-trail table on
    /// docs/personal-data.md.
    /// </summary>
    /// <returns>The field names that table declares.</returns>
    private static IReadOnlyList<string> TrailRowsOnThePersonalDataPage()
    {
        var lines = File.ReadAllLines(Path.Combine(RepositoryRoot(), "docs", "personal-data.md"));

        var fields = new List<string>();
        var inside = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inside = string.Equals(line.Trim(), "## The attempt trail", StringComparison.Ordinal);
                continue;
            }

            if (!inside || !line.StartsWith("| ", StringComparison.Ordinal))
            {
                continue;
            }

            var first = line.Split('|')[1].Trim();
            if (first.Length == 0 || first.StartsWith("---", StringComparison.Ordinal) || string.Equals(first, "Field", StringComparison.Ordinal))
            {
                continue;
            }

            fields.Add(first);
        }

        Assert.True(
            fields.Count > 0,
            string.Create(
                CultureInfo.InvariantCulture,
                $"docs/personal-data.md carries no rows under its attempt-trail heading, so this comparison read nothing."));

        return fields;
    }

    /// <summary>
    /// The directory holding both the solution and the documents, found by
    /// walking up from the test binary. Nothing is written and nothing outside
    /// the repository is read.
    /// </summary>
    /// <returns>The repository root.</returns>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln"))
                && File.Exists(Path.Combine(directory.FullName, "docs", "personal-data.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds both Jellyfin.Plugin.Invites.sln and docs/personal-data.md, so this comparison read nothing.");
    }
}
