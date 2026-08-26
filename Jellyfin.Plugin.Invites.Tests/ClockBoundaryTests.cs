using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The clock-driven boundaries this plugin has, each asked at the tick before
/// it, at it, and the tick after, and each asked again after the clock jumps
/// across it.
/// </summary>
/// <remarks>
/// <para>
/// #104 names four clock-driven behaviours and this file is what was missing
/// from two of them. Expiry has its three points in
/// <see cref="RedemptionDecisionTests"/> and its jumps in
/// <see cref="ClockJumpTests"/>. Retention has its three points in
/// <see cref="RetentionTests"/>, which arrived with the rule, and had no jump
/// cases at all; the limiter had neither. So what is here is the retention jumps
/// and both limiter windows, and nothing is restated that another file already
/// holds. The fourth behaviour, an account expiry, does not exist in this plugin
/// and nothing here stands in for it.
/// </para>
/// <para>
/// <b>Three points prove the comparison and nothing about the unit.</b> Every
/// step here is one <see cref="DateTimeOffset"/> tick, which is the smallest the
/// type has, so for the comparisons as written there is no gap between the two
/// verdicts. That says nothing about an instant rounded on its way through a
/// file, which would show in a round trip rather than here.
/// </para>
/// <para>
/// <b>Where each direction is declared.</b> The limiter's window shape is stated
/// on docs/rate-limit.md, and it is read below rather than paraphrased, so a page
/// that stops saying it reddens rather than passing. Retention's direction is
/// declared only in the remark on
/// <see cref="Retention.MayBeRemoved(Invitation, DateTimeOffset)"/>; no page under
/// docs/ states it, so the assertions here and in <see cref="RetentionTests"/>
/// pin the direction the source declares and there is nothing independent to
/// compare either against.
/// </para>
/// <para>
/// Nothing here sleeps. Every boundary is crossed by moving an injected clock or
/// by handing an instant to a routine that takes one, which is what the seam
/// under #41 exists for and what <see cref="SuiteDoesNotSleepTests"/> refuses the
/// alternative to.
/// </para>
/// </remarks>
public class ClockBoundaryTests
{
    private static readonly DateTimeOffset _minted = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _expires = new(2026, 1, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _operator = Guid.Parse("11112222-3333-4444-5555-666677778888");

    /// <summary>
    /// An instant on a whole hour, which is the first tick of a per-address
    /// window and one tick after the last tick of the one before it.
    /// </summary>
    /// <remarks>
    /// The limiter numbers its windows by dividing the ticks since the calendar's
    /// origin by the window length, and that origin is a midnight, so every whole
    /// hour opens a per-address window and every whole second opens a global one.
    /// Choosing the instants that way is what lets a boundary be named here rather
    /// than searched for.
    /// </remarks>
    private static readonly DateTimeOffset _hourBoundary = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// An instant on a whole second, which is the first tick of a global window.
    /// </summary>
    private static readonly DateTimeOffset _secondBoundary = new(2026, 3, 1, 9, 0, 1, TimeSpan.Zero);

    /// <summary>
    /// A clock stepping backwards across the retention boundary makes the record
    /// unremovable again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a cost rather than a defect, and it is the cheap direction of the
    /// fault <see cref="ClockJumpTests"/> pins for expiry. The rule holds no memory
    /// of the readings it has seen, so it cannot tell a clock that went back from
    /// one that never went forward, and what a backwards jump buys here is a record
    /// kept longer than the period asks. Every rounding in the rule is towards
    /// keeping, and so is this one.
    /// </para>
    /// <para>
    /// A record a sweep has already removed is not brought back by any of this.
    /// Removal is a write to a file, and this is the question asked before one.
    /// </para>
    /// </remarks>
    [Fact]
    public void AClockSteppingBackwardsAcrossTheRetentionBoundaryKeepsTheRecord()
    {
        var record = ExpiredAt(_expires);
        var boundary = _expires + Retention.RecordRetention;

        Assert.True(Retention.MayBeRemoved(record, boundary));
        Assert.False(Retention.MayBeRemoved(record, boundary - TimeSpan.FromTicks(1)));
    }

    /// <summary>
    /// A jump forward past several retention boundaries at once reaches every one
    /// of them, including the record whose boundary is the reading itself.
    /// </summary>
    /// <remarks>
    /// One reading serves a whole sweep, so a jump is not a sequence of steps one
    /// of which could be missed. The last record is the inclusive case from the
    /// side a three-point test never visits: its period ends exactly at the instant
    /// the sweep is standing at, and it is removed with the two that ran out long
    /// before it.
    /// </remarks>
    [Fact]
    public void AJumpPastSeveralRetentionBoundariesReachesEveryOneOfThem()
    {
        var oldest = ExpiredAt(_expires);
        var middle = ExpiredAt(_expires + TimeSpan.FromDays(30));
        var newest = ExpiredAt(_expires + TimeSpan.FromDays(60));

        var before = _expires + TimeSpan.FromTicks(1);

        Assert.False(Retention.MayBeRemoved(oldest, before));
        Assert.False(Retention.MayBeRemoved(middle, before));
        Assert.False(Retention.MayBeRemoved(newest, before));

        var after = _expires + TimeSpan.FromDays(60) + Retention.RecordRetention;

        Assert.True(Retention.MayBeRemoved(oldest, after));
        Assert.True(Retention.MayBeRemoved(middle, after));
        Assert.True(Retention.MayBeRemoved(newest, after));
    }

    /// <summary>
    /// The per-address window turns at the boundary and not a tick before it: an
    /// exhausted address is refused at the last tick of its window and admitted at
    /// the first tick of the next.
    /// </summary>
    /// <remarks>
    /// The attempts that exhaust the allowance are placed a second apart, because
    /// ten a second is a different limit from twenty an hour and a test of the
    /// second one must not be answered by the first. The refusal at the tick before
    /// the boundary is therefore the per-address ceiling: the global window at that
    /// second has nothing counted in it.
    /// </remarks>
    [Fact]
    public void ThePerAddressWindowTurnsAtTheBoundary()
    {
        var clock = new TestClock(_hourBoundary - TimeSpan.FromHours(1));
        var limiter = new AttemptLimiter(clock);

        for (var attempt = 1; attempt <= AttemptLimiter.PerAddressCeiling; attempt++)
        {
            Assert.True(
                limiter.MayJudge("198.51.100.7"),
                "Attempt " + attempt.ToString(CultureInfo.InvariantCulture) + " was refused inside the per-address ceiling.");
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        clock.MoveTo(_hourBoundary - TimeSpan.FromTicks(1));
        Assert.False(limiter.MayJudge("198.51.100.7"));

        clock.MoveTo(_hourBoundary);
        Assert.True(limiter.MayJudge("198.51.100.7"));

        clock.MoveTo(_hourBoundary + TimeSpan.FromTicks(1));
        Assert.True(limiter.MayJudge("198.51.100.7"));
    }

    /// <summary>
    /// The global window turns at the boundary and not a tick before it.
    /// </summary>
    /// <remarks>
    /// Every attempt here is made from an address of its own and no address is
    /// asked twice, so nothing in this test can be answered by the per-address
    /// limit: none of them is anywhere near its own ceiling.
    /// </remarks>
    [Fact]
    public void TheGlobalWindowTurnsAtTheBoundary()
    {
        var clock = new TestClock(_secondBoundary - TimeSpan.FromTicks(1));
        var limiter = new AttemptLimiter(clock);

        for (var attempt = 1; attempt <= AttemptLimiter.GlobalCeiling; attempt++)
        {
            Assert.True(
                limiter.MayJudge("198.51.100." + attempt.ToString(CultureInfo.InvariantCulture)),
                "Attempt " + attempt.ToString(CultureInfo.InvariantCulture) + " was refused inside the global ceiling.");
        }

        Assert.False(limiter.MayJudge("203.0.113.1"));

        clock.MoveTo(_secondBoundary);
        Assert.True(limiter.MayJudge("203.0.113.2"));

        clock.MoveTo(_secondBoundary + TimeSpan.FromTicks(1));
        Assert.True(limiter.MayJudge("203.0.113.3"));
    }

    /// <summary>
    /// A fixed window lets twice the stated rate through across a boundary, which
    /// is the cost docs/rate-limit.md states and the reason the window is fixed
    /// rather than sliding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Twenty judged attempts in two ticks, against a limit that reads ten a
    /// second. The sentence on the page is read rather than paraphrased, so a page
    /// that stopped stating the cost reddens here instead of leaving an assertion
    /// pinning a behaviour nothing admits to.
    /// </para>
    /// <para>
    /// What this does not say is that the doubling is harmless. The page argues
    /// that it adds exactly one bit to what a search needs against the headroom in
    /// docs/code-entropy.md, and that argument is a reading a person makes; what is
    /// asserted here is only that the doubling is what the code does.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheFixedWindowLetsTwiceTheRateThroughAcrossABoundary()
    {
        var stated = new Regex(
            @"A fixed window lets somebody run at twice the stated rate across a\s+boundary",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5));

        Assert.True(
            stated.IsMatch(RateLimitPage()),
            "docs/rate-limit.md no longer states what a fixed window costs, so this assertion pins a behaviour no page admits to. Restore the sentence or move this assertion to whatever replaced it.");

        var clock = new TestClock(_secondBoundary - TimeSpan.FromTicks(1));
        var limiter = new AttemptLimiter(clock);
        var judged = 0;

        for (var attempt = 1; attempt <= AttemptLimiter.GlobalCeiling; attempt++)
        {
            if (limiter.MayJudge("198.51.100." + attempt.ToString(CultureInfo.InvariantCulture)))
            {
                judged++;
            }
        }

        clock.MoveTo(_secondBoundary);

        for (var attempt = 1; attempt <= AttemptLimiter.GlobalCeiling; attempt++)
        {
            if (limiter.MayJudge("203.0.113." + attempt.ToString(CultureInfo.InvariantCulture)))
            {
                judged++;
            }
        }

        Assert.Equal(AttemptLimiter.GlobalCeiling * 2, judged);
    }

    /// <summary>
    /// A jump forward past several windows at once gives the allowance back once
    /// rather than once per window crossed.
    /// </summary>
    /// <remarks>
    /// The counter is a window number and a count rather than a queue of windows,
    /// so there is nothing for a jump to accumulate. Asserting the ceiling again
    /// after the jump is what separates that from a limiter which handed back a
    /// window's allowance for every window it skipped.
    /// </remarks>
    [Fact]
    public void AJumpPastSeveralWindowsGivesOneAllowanceBack()
    {
        var clock = new TestClock(_hourBoundary - TimeSpan.FromHours(1));
        var limiter = new AttemptLimiter(clock);

        Spend(limiter, clock, "198.51.100.7");
        Assert.False(limiter.MayJudge("198.51.100.7"));

        clock.MoveTo(_hourBoundary + TimeSpan.FromHours(5));

        Spend(limiter, clock, "198.51.100.7");
        Assert.False(limiter.MayJudge("198.51.100.7"));
    }

    /// <summary>
    /// A clock stepping backwards across a window boundary hands the allowance
    /// back early.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Recorded because it is a cost rather than a property anybody chose. The
    /// limiter asks whether the reading falls in the window it last counted in and
    /// starts again where it does not, so a reading that moved backwards into the
    /// previous window is a different window and the count starts again. A
    /// comparison that only started again on a later window would keep the count
    /// instead.
    /// </para>
    /// <para>
    /// What it costs is bounded by what the limiter is for. docs/rate-limit.md
    /// settles that the counter leaves with the process and that the guarantee may
    /// never rest on it: an attacker resets it by waiting and an operator resets it
    /// by upgrading. A backwards clock is one more way to reset a counter that is
    /// already resettable, and what actually bounds a guess is the entropy in
    /// docs/code-entropy.md.
    /// </para>
    /// </remarks>
    [Fact]
    public void AClockSteppingBackwardsAcrossAWindowHandsTheAllowanceBack()
    {
        var clock = new TestClock(_hourBoundary);
        var limiter = new AttemptLimiter(clock);

        Spend(limiter, clock, "198.51.100.7");
        Assert.False(limiter.MayJudge("198.51.100.7"));

        clock.MoveTo(_hourBoundary - TimeSpan.FromTicks(1));

        Assert.True(limiter.MayJudge("198.51.100.7"));
    }

    /// <summary>
    /// Spends one address's whole per-address allowance, a second apart so the
    /// global limit is not what answers.
    /// </summary>
    /// <param name="limiter">The limiter under test.</param>
    /// <param name="clock">Its clock, which this moves.</param>
    /// <param name="address">The address to spend.</param>
    private static void Spend(AttemptLimiter limiter, TestClock clock, string address)
    {
        for (var attempt = 1; attempt <= AttemptLimiter.PerAddressCeiling; attempt++)
        {
            Assert.True(
                limiter.MayJudge(address),
                "Attempt " + attempt.ToString(CultureInfo.InvariantCulture) + " was refused inside the per-address ceiling.");
            clock.Advance(TimeSpan.FromSeconds(1));
        }
    }

    /// <summary>
    /// A record that has expired at the given instant and was never revoked, so
    /// its retention counts from that expiry.
    /// </summary>
    /// <param name="expiresAt">When the record stops being usable.</param>
    /// <returns>The record.</returns>
    private static Invitation ExpiredAt(DateTimeOffset expiresAt)
    {
        return new Invitation(
            id: Guid.NewGuid(),
            codeHash: ImmutableArray.Create(new byte[32]),
            mintedBy: _operator,
            mintedAt: _minted,
            expiresAt: expiresAt,
            usesGranted: 1,
            usesRemaining: 1,
            revokedAt: null,
            revokedBy: null,
            templateLabel: "Household",
            accountsProduced: ImmutableArray<Guid>.Empty);
    }

    /// <summary>
    /// The page that decides the limiter's windows, read from the tree rather than
    /// quoted here.
    /// </summary>
    /// <returns>The page.</returns>
    private static string RateLimitPage()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var page = Path.Combine(directory.FullName, "docs", "rate-limit.md");
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
            + " holds both Jellyfin.Plugin.Invites.sln and docs/rate-limit.md, so this comparison read nothing. Failing rather than passing over an empty page.");
    }
}
