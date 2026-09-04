using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Storage;
using Jellyfin.Plugin.Invites.Time;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A clock that moves every time it is read, so a routine reading it twice sees
/// two different instants.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TestClock"/> holds still until a test moves it, which is what
/// makes it useful for a boundary and useless for this: a routine reading it
/// three times inside one decision would pass every assertion made against it.
/// This one steps by the amount it is given on every read and counts the reads,
/// so a second read is both visible in the verdict and countable.
/// </para>
/// <para>
/// It is not a monotonic clock standing in for a real one. Nothing on a server
/// steps by exactly this much per read. What it is is the smallest arrangement
/// in which "one reading served the whole redemption" and "two readings
/// happened to agree" are different outcomes.
/// </para>
/// </remarks>
internal sealed class AMovingClock : IClock
{
    private readonly TimeSpan _step;

    private DateTimeOffset _next;

    public AMovingClock(DateTimeOffset first, TimeSpan step)
    {
        _next = first;
        _step = step;
    }

    /// <summary>
    /// Gets how many times the clock has been read.
    /// </summary>
    public int Reads { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset UtcNow
    {
        get
        {
            Reads++;
            var now = _next;
            _next += _step;

            return now;
        }
    }
}

/// <summary>
/// One clock reading serves a whole redemption, which is the last clause of #51
/// that had no redemption to serve.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is worth a test rather than a reading of the source.</b> The rule
/// is that an invitation whose expiry passes midway through a redemption is not
/// decided differently by two comparisons in the same request, because which one
/// wins would depend on how long the machine took. A reader can see one
/// assignment at the top of the operation and conclude the rule holds; what they
/// cannot see is the routine that is added later, three calls down, reading the
/// seam again. docs/expiry-rules.md argues the rule and says the seam's own
/// suite asserts a controlled clock does not move between two reads. That is the
/// property of the clock rather than of a redemption, and this is the redemption.
/// </para>
/// <para>
/// <b>What is driven is the operation and not the route.</b> A submission also
/// asks the limiter, and the limiter reads the clock for its own windows, which
/// is a different judgement about a different subject. Counting reads across a
/// whole request would therefore count two and say nothing about either. The
/// unit this clause is about is the one that reads the records, decides against
/// them and writes, which is one call.
/// </para>
/// </remarks>
public class OneClockReadingTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A redemption that begins before an expiry is honoured even where the
    /// expiry passes while it runs.
    /// </summary>
    /// <remarks>
    /// The clock steps past the expiry on its second read, so a second
    /// comparison anywhere inside the operation refuses the record as expired.
    /// The invitation is honoured and its use is taken, which is only possible
    /// if every comparison saw the first reading.
    /// </remarks>
    [Fact]
    public void AnExpiryThatPassesMidRedemptionDoesNotChangeTheVerdict()
    {
        using var directory = new OwnedDirectory();
        var validity = TimeSpan.FromDays(7);
        var minted = RedeemRoute.Mint(directory.Path, new TestClock(_minted), uses: 1);
        Assert.Equal(_minted + validity, minted.Invitation.ExpiresAt);

        // The first reading is a second inside the expiry and the second reading
        // is a day past it.
        var clock = new AMovingClock(minted.Invitation.ExpiresAt.AddSeconds(-1), TimeSpan.FromDays(1));
        var operations = RedeemRoute.Operations(directory.Path, clock);

        var reservation = operations.Reserve(minted.Code);

        Assert.True(reservation.MayCreateAnAccount);
        Assert.Equal(0, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);
    }

    /// <summary>
    /// The operation reads the clock once.
    /// </summary>
    /// <remarks>
    /// The assertion above is the property and this is the mechanism, and both
    /// are here because either alone is satisfiable without the other. A second
    /// read whose value happens to fall on the same side of the boundary passes
    /// the first and is caught here; a routine that read once and compared
    /// against something else entirely passes this and is caught there.
    /// </remarks>
    [Fact]
    public void TheOperationThatDecidesAndWritesReadsTheClockOnce()
    {
        using var directory = new OwnedDirectory();
        var minted = RedeemRoute.Mint(directory.Path, new TestClock(_minted), uses: 1);
        var clock = new AMovingClock(_minted, TimeSpan.FromSeconds(1));
        var operations = RedeemRoute.Operations(directory.Path, clock);

        operations.Reserve(minted.Code);

        Assert.Equal(1, clock.Reads);
    }

    /// <summary>
    /// A refused redemption reads the clock once too.
    /// </summary>
    /// <remarks>
    /// The honoured path is the one the rule is argued for and it is not the only
    /// one that matters. A refusal that read the clock twice would be a route
    /// whose refusals cost more clock reads than its acceptances, which is a
    /// difference on the one endpoint a stranger can hammer, and it is the kind
    /// of asymmetry the single indistinguishable refusal exists against.
    /// </remarks>
    [Fact]
    public void ACodeThatMatchesNothingReadsTheClockOnceAsWell()
    {
        using var directory = new OwnedDirectory();
        RedeemRoute.Mint(directory.Path, new TestClock(_minted), uses: 1);
        var clock = new AMovingClock(_minted, TimeSpan.FromSeconds(1));

        var reservation = RedeemRoute.Operations(directory.Path, clock).Reserve("not-a-real-code");

        Assert.False(reservation.MayCreateAnAccount);
        Assert.Equal(1, clock.Reads);
    }

    /// <summary>
    /// The moving clock moves, so the two assertions above are not passing over a
    /// clock that stood still.
    /// </summary>
    /// <remarks>
    /// Without this, a double that had stopped stepping would report the same
    /// green as a redemption that took one reading, and the first assertion above
    /// would be satisfied by an expiry that never arrived.
    /// </remarks>
    [Fact]
    public void TheMovingClockMovesAndCountsItsReads()
    {
        var clock = new AMovingClock(_minted, TimeSpan.FromDays(1));

        Assert.Equal(_minted, clock.UtcNow);
        Assert.Equal(_minted.AddDays(1), clock.UtcNow);
        Assert.Equal(_minted.AddDays(2), clock.UtcNow);
        Assert.Equal(3, clock.Reads);
    }

    /// <summary>
    /// A submission that reaches the record is decided at one reading, whatever
    /// the limiter read before it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The route is driven here rather than the operation, because the two
    /// guards in front of the reservation read the same seam first and a reader
    /// could reasonably wonder whether the decision then gets one of their
    /// readings. It does not: the invitation expires immediately after the last
    /// of them, and it is honoured.
    /// </para>
    /// <para>
    /// THE COUNT HERE WAS TWO AND IS THREE, and the property did not move. The
    /// limiter took a reading and the reservation took a reading; the ceiling on
    /// how many accounts may be created in a window landed between them and
    /// takes one of its own. Each of the three judges a different subject at its
    /// own instant, and the rule this file is about is that ONE redemption is
    /// decided at ONE reading, which is the operation and is asserted above.
    /// </para>
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ASubmissionIsDecidedAtOneReadingWhateverTheGuardsInFrontRead()
    {
        using var directory = new OwnedDirectory();
        var minted = RedeemRoute.Mint(directory.Path, new TestClock(_minted), uses: 1);

        // Three readings before the expiry and everything after it past the
        // expiry: the limiter takes the first, the ceiling the second, the
        // reservation the third, and a fourth would refuse the record.
        var clock = new AMovingClock(minted.Invitation.ExpiresAt.AddSeconds(-3), TimeSpan.FromSeconds(1));
        var seam = new ARecordingWriteSeam();

        var answer = await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status303SeeOther, Assert.IsType<StatusCodeResult>(answer).StatusCode);
        Assert.Equal(3, clock.Reads);
    }
}
