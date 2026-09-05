using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A revocation reaches a redemption that had already begun, and reaches
/// nothing that had already finished.
/// </summary>
/// <remarks>
/// <para>
/// <b>Beginning and committing are two moments and the operator acts between
/// them.</b> The person follows the link, is served the form and fills it in;
/// the operator realises the link went somewhere it should not have and revokes
/// it; the person presses the button. That is the sequence #54 is about, and it
/// needs no threads: the two requests are ordinary and the revocation sits
/// between them.
/// </para>
/// <para>
/// <b>What makes it hold is where the record is read.</b> The reservation reads
/// the store, asks for the verdict and takes the use inside one monitor, so the
/// record the decision is made on is the one on disk at the moment of the post
/// rather than anything the page view saw. The tests below drive the same
/// controller for both requests and revoke through a separate operations
/// instance over the same directory, so a route that held anything from the
/// first request would be visible here.
/// </para>
/// <para>
/// <b>The other direction is the one an operator is most likely to expect
/// wrongly.</b> Revoking after an account exists does not remove that account.
/// #54 decides that deliberately: revoking a link stops future accounts and
/// does not disown past ones, and removing an account is a separate operator
/// action. So one leg below asserts that the seam is asked for nothing after
/// the revocation and that the record still names what it produced.
/// </para>
/// <para>
/// <b>No web host.</b> The controller is an ordinary object over contexts the
/// suite owns, and the store is the real one in a directory the test owns,
/// which is the headless rule rather than a shortcut. Nothing here has run
/// against a server.
/// </para>
/// </remarks>
public class ARevocationDuringARedemptionTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The operator who revokes.
    /// </summary>
    private static readonly Guid _operator = Guid.Parse("44445555-6666-7777-8888-99990000aaaa");

    /// <summary>
    /// A form served before the revocation cannot be submitted after it, and
    /// the attempt costs the invitation nothing.
    /// </summary>
    /// <remarks>
    /// The invitation is minted for two uses so that a use taken would be
    /// visible as a number rather than only as the difference between one and
    /// zero, which is the same reading a spent invitation gives.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AFormServedBeforeTheRevocationCannotBeSubmittedAfterIt()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 2);
        var seam = new ARecordingWriteSeam();

        var serving = RedeemRoute.Request();
        var route = RedeemRoute.Over(directory.Path, clock, seam, serving);
        var page = route.Page();
        Assert.NotNull(page.Content);

        clock.Advance(TimeSpan.FromMinutes(3));
        var revoked = RedeemRoute.Operations(directory.Path, clock).Revoke(minted.Invitation.Id, _operator);
        Assert.NotNull(revoked);

        clock.Advance(TimeSpan.FromMinutes(3));
        var answer = await route.Submit(
            minted.Code,
            RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(answer).StatusCode);
        Assert.Empty(seam.Asked);

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(2, stored.UsesRemaining);
        Assert.Empty(stored.AccountsProduced);
        Assert.Equal(revoked!.RevokedAt, stored.RevokedAt);
        Assert.Equal(_operator, stored.RevokedBy);
    }

    /// <summary>
    /// The revoking operator and the moment survive a redemption attempt made
    /// after them, and a second revocation does not move either.
    /// </summary>
    /// <remarks>
    /// Idempotence is asserted on the model elsewhere. What is added here is
    /// that a refused redemption between the two revocations writes nothing:
    /// the attempt reads the record and takes no use, so a route that wrote on
    /// the way past would move a timestamp an operator is reading as the moment
    /// they acted.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task NeitherARefusedRedemptionNorASecondRevocationMovesTheFirstOne()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var operations = RedeemRoute.Operations(directory.Path, clock);

        var first = operations.Revoke(minted.Invitation.Id, _operator);
        Assert.NotNull(first);

        clock.Advance(TimeSpan.FromHours(2));
        var refused = await RedeemRoute
            .Over(directory.Path, clock, new ARecordingWriteSeam(), RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(refused).StatusCode);

        clock.Advance(TimeSpan.FromHours(2));
        Assert.NotNull(operations.Revoke(minted.Invitation.Id, Guid.Parse("bbbbcccc-dddd-4eee-8fff-000011112222")));

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(first!.RevokedAt, stored.RevokedAt);
        Assert.Equal(_operator, stored.RevokedBy);
        Assert.Equal(1, stored.UsesRemaining);
    }

    /// <summary>
    /// Revoking after an account has been created does not touch the account,
    /// and the record goes on naming it.
    /// </summary>
    /// <remarks>
    /// This is the clause #54 states as what revocation does NOT do, and it is
    /// the one an operator reaching for the control at the worst moment is most
    /// likely to expect wrongly. What is asserted is the write seam being asked
    /// for nothing after the three calls a creation makes, because an account
    /// removed or disabled by a revocation would be a fourth.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task RevokingAfterAnAccountExistsLeavesTheAccountAndTheRecordOfIt()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 2);
        var seam = new ARecordingWriteSeam();

        var honoured = await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status303SeeOther, Assert.IsType<StatusCodeResult>(honoured).StatusCode);
        var asked = seam.Asked.Count;

        clock.Advance(TimeSpan.FromDays(1));
        Assert.NotNull(RedeemRoute.Operations(directory.Path, clock).Revoke(minted.Invitation.Id, _operator));

        Assert.Equal(asked, seam.Asked.Count);

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(seam.Answers, Assert.Single(stored.AccountsProduced).Account);
        Assert.Equal(1, stored.UsesRemaining);
        Assert.True(stored.IsRevoked);
    }

    /// <summary>
    /// The use a revoked invitation still has is not spendable, which is what
    /// separates revoking from spending.
    /// </summary>
    /// <remarks>
    /// The leg above leaves an invitation revoked with a use to spare, which is
    /// the state an operator produces when they revoke a two-use link after one
    /// person has taken it. This drives the second person at it.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AUseLeftOnARevokedInvitationCannotBeSpent()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 2);
        var seam = new ARecordingWriteSeam();

        await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("first", "a password long enough"));

        RedeemRoute.Operations(directory.Path, clock).Revoke(minted.Invitation.Id, _operator);
        var asked = seam.Asked.Count;

        var refused = await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("second", "a password long enough"));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(refused).StatusCode);
        Assert.Equal(asked, seam.Asked.Count);
        Assert.Equal(1, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);
    }
}
