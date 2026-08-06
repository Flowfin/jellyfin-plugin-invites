using System;
using Jellyfin.Plugin.Template.Time;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// A clock a test owns. Every read returns whatever it was last set to, so a
/// test crosses a boundary by moving the clock rather than by waiting for one.
/// </summary>
internal sealed class TestClock : IClock
{
    public TestClock(DateTimeOffset now)
    {
        UtcNow = now;
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void MoveTo(DateTimeOffset instant)
    {
        UtcNow = instant;
    }

    public void Advance(TimeSpan by)
    {
        UtcNow = UtcNow.Add(by);
    }
}

/// <summary>
/// The clock seam is what lets expiry, rate-limit windows, retention and
/// lockout be tested at all. These are tests of the seam rather than of any
/// rule judged against it: no rule exists yet, and the whole point of landing
/// the seam first is that the rules are written against something steerable.
/// </summary>
public class ClockSeamTests
{
    /// <summary>
    /// The instant the plugin reads is absolute. A clock that handed back a
    /// local time would put the operator's timezone into every stored instant,
    /// and moving the server to another zone would move when invitations
    /// expire.
    /// </summary>
    [Fact]
    public void TheSystemClockReadsAnAbsoluteInstantAtOffsetZero()
    {
        IClock clock = new SystemClock();

        var reading = clock.UtcNow;

        Assert.Equal(TimeSpan.Zero, reading.Offset);
    }

    /// <summary>
    /// The seam is the whole of the plugin's access to the machine clock, so a
    /// caller holding an <see cref="IClock"/> sees exactly what the clock says
    /// and never what the machine says.
    /// </summary>
    [Fact]
    public void AControlledClockDecidesWhatTheCallerSees()
    {
        var chosen = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        IClock clock = new TestClock(chosen);

        Assert.Equal(chosen, clock.UtcNow);
        Assert.NotEqual(DateTimeOffset.UtcNow.Year, clock.UtcNow.Year);
    }

    /// <summary>
    /// A boundary is crossed in both directions without the test sleeping. The
    /// comparison here is the shape every expiry, window and lockout check will
    /// have, and the point is that both sides of it are reachable in one test
    /// that takes no time to run.
    /// </summary>
    [Fact]
    public void TheClockCrossesABoundaryInBothDirections()
    {
        var boundary = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestClock(boundary - TimeSpan.FromSeconds(1));

        Assert.True(clock.UtcNow < boundary);

        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True(clock.UtcNow >= boundary);

        clock.MoveTo(boundary - TimeSpan.FromDays(365));
        Assert.True(clock.UtcNow < boundary);

        clock.MoveTo(boundary);
        Assert.True(clock.UtcNow >= boundary);
    }

    /// <summary>
    /// Reading twice without moving the clock gives the same answer. A rule
    /// that reads the time more than once inside one decision must not see it
    /// change underneath, or the test that covers the decision is testing two
    /// different instants and not the rule.
    /// </summary>
    [Fact]
    public void AControlledClockDoesNotMoveOnItsOwn()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

        var first = clock.UtcNow;
        var second = clock.UtcNow;

        Assert.Equal(first, second);
    }
}
