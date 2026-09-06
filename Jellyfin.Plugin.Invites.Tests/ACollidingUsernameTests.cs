using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A name the server already holds, and the two neighbours that prove the
/// refusal is about the collision.
/// </summary>
/// <remarks>
/// <para>
/// <b>What #67 is about is the cost of getting a name wrong.</b> A redemption
/// that consumed a use and created nothing is the worst outcome available: the
/// person has no account and the operator has to mint again. So the whole
/// property here is an ordering — the name is judged before the use is
/// reserved — and every assertion below reads the store after the post rather
/// than reading the answer the person got.
/// </para>
/// <para>
/// <b>The collision is asked of the server, not reimplemented.</b>
/// <see cref="ServerAccountNames"/> asks the member the creation call asks, so
/// the two cannot disagree about which names are equal. What is asserted here is
/// that the route asks it, asks it with the name exactly as it was typed, and
/// asks it in the right place; what the server answers is the server's.
/// </para>
/// <para>
/// <b>No web host and no server.</b> The controller is an ordinary object over a
/// context this test owns and the seams are stand-ins, which is the headless
/// rule. Nothing here has presented a name to a running Jellyfin server.
/// </para>
/// </remarks>
public class ACollidingUsernameTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A name the server already holds leaves the invitation exactly as it was.
    /// </summary>
    /// <remarks>
    /// Four things at once rather than four tests, because each of them alone
    /// passes for an implementation the other three catch: a use taken for a
    /// name that produced nothing, an account written under a name the server
    /// would have refused, a record claiming an account that does not exist, and
    /// a refusal that reached the write seam before deciding.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ANameTheServerHoldsCostsTheInvitationNothing()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();
        var names = new NamesTheServerHolds("newcomer");
        var context = RedeemRoute.Request();

        var answer = await RedeemRoute.Over(directory.Path, clock, seam, names, context)
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.IsType<BadRequestResult>(answer);
        Assert.Empty(seam.Asked);

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(1, stored.UsesRemaining);
        Assert.Equal(1, stored.UsesGranted);
        Assert.Empty(stored.AccountsProduced);
        Assert.Null(stored.RevokedAt);
    }

    /// <summary>
    /// The same invitation still works for a name the server does not hold.
    /// </summary>
    /// <remarks>
    /// The neighbour that makes the test above worth something. Without it a
    /// route refusing every post would pass, and the person whose name was taken
    /// would be told to pick another one and find that nothing works.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheSameInvitationStillWorksForAFreeName()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();
        var names = new NamesTheServerHolds("newcomer");
        var refused = RedeemRoute.Request();

        await RedeemRoute.Over(directory.Path, clock, seam, names, refused)
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        var accepted = RedeemRoute.Request();
        var answer = await RedeemRoute.Over(directory.Path, clock, seam, names, accepted)
            .Submit(minted.Code, RedeemRoute.Filled("second-comer", "a password long enough"));

        var redirect = Assert.IsType<StatusCodeResult>(answer);
        Assert.Equal(StatusCodes.Status303SeeOther, redirect.StatusCode);

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(0, stored.UsesRemaining);
        Assert.Equal(seam.Answers, Assert.Single(stored.AccountsProduced).Account);
    }

    /// <summary>
    /// A name that differs from an existing account only in case collides.
    /// </summary>
    /// <remarks>
    /// The server's comparison is case-insensitive at both ends of the line this
    /// plugin loads on, and a plugin comparing ordinally would accept the name,
    /// spend the use and hand the server a name it refuses. The stand-in here
    /// answers the way the server does, so what this asserts is that the route
    /// takes the seam's answer rather than making its own.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ANameDifferingOnlyInCaseIsTaken()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();
        var names = new NamesTheServerHolds("newcomer");
        var context = RedeemRoute.Request();

        var answer = await RedeemRoute.Over(directory.Path, clock, seam, names, context)
            .Submit(minted.Code, RedeemRoute.Filled("NewComer", "a password long enough"));

        Assert.IsType<BadRequestResult>(answer);
        Assert.Equal(1, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);
    }

    /// <summary>
    /// The name reaching the seam is the name that was typed.
    /// </summary>
    /// <remarks>
    /// #67's clause that no name is ever silently altered. A route that trimmed
    /// or folded the name before asking would ask a different question from the
    /// one the creation call will ask, and the person would meet a refusal at
    /// the server after the use was gone.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheNameAskedAboutIsTheNameThatWasTyped()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var names = new NamesTheServerHolds("nobody");
        var context = RedeemRoute.Request();

        await RedeemRoute.Over(directory.Path, clock, new ARecordingWriteSeam(), names, context)
            .Submit(minted.Code, RedeemRoute.Filled("A.Newcomer_1", "a password long enough"));

        Assert.Equal("A.Newcomer_1", Assert.Single(names.Asked));
    }

    /// <summary>
    /// A name the server's own expression refuses is refused without the
    /// server's user table being asked about it at all.
    /// </summary>
    /// <remarks>
    /// The order matters twice over. The shape refusal discloses nothing,
    /// because it is decided out of the request alone; the collision question
    /// touches the server's accounts. A route that asked them in the other order
    /// would let a stranger reach the user table with anything at all.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ANameTheShapeRuleRefusesNeverReachesTheServersAccounts()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var names = new NamesTheServerHolds("nobody");
        var context = RedeemRoute.Request();

        var answer = await RedeemRoute.Over(directory.Path, clock, new ARecordingWriteSeam(), names, context)
            .Submit(minted.Code, RedeemRoute.Filled(" leading-space", "a password long enough"));

        Assert.IsType<BadRequestResult>(answer);
        Assert.Empty(names.Asked);
        Assert.Equal(1, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);
    }

    /// <summary>
    /// The collision is asked after the limiter, so the server's user table
    /// cannot be questioned without an attempt being counted.
    /// </summary>
    /// <remarks>
    /// The one ordering decision this change took that the issue does not
    /// state. Asking before the limiter would satisfy the issue's words and hand
    /// an unauthenticated stranger an unbounded name oracle; asking after it
    /// keeps the question behind the same counter every other attempt is behind,
    /// and still leaves the invitation untouched, which is what the clause is
    /// about.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheCollisionIsNotAskedOnceTheLimiterHasRefused()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var limiter = new Jellyfin.Plugin.Invites.Redemption.AttemptLimiter(clock);
        var names = new NamesTheServerHolds("nobody");

        var asked = 0;
        while (limiter.MayJudge(RedeemRoute.From.ToString()))
        {
            asked++;
            Assert.True(asked < 10_000, "The limiter never refused, so this test never reached the state it is about.");
        }

        var context = RedeemRoute.Request();
        var answer = await RedeemRoute
            .Over(directory.Path, clock, limiter, new ARecordingWriteSeam(), names, context)
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.IsType<ContentResult>(answer);
        Assert.Empty(names.Asked);
    }
}

/// <summary>
/// A server holding a fixed set of account names, comparing them the way the
/// server does.
/// </summary>
/// <remarks>
/// The comparison is case-insensitive because the server's is, at both ends of
/// the line this plugin loads on: at the floor <c>GetUserByName</c> compares
/// with <c>OrdinalIgnoreCase</c>, and at the shipping version it compares the
/// stored normalised name against the upper-cased argument. A stand-in that
/// compared ordinally would let a test pass over a name a real server refuses.
/// </remarks>
internal sealed class NamesTheServerHolds : IServerAccountNames
{
    private readonly string[] _held;
    private readonly List<string?> _asked = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="NamesTheServerHolds"/> class.
    /// </summary>
    /// <param name="held">The names the server already has.</param>
    public NamesTheServerHolds(params string[] held)
    {
        _held = held;
    }

    /// <summary>
    /// Gets every name this was asked about, in order and exactly as it arrived.
    /// </summary>
    public IReadOnlyList<string?> Asked => _asked;

    /// <inheritdoc />
    public bool IsTaken(string? username)
    {
        _asked.Add(username);

        return username is not null
            && _held.Any(name => string.Equals(name, username, StringComparison.OrdinalIgnoreCase));
    }
}
