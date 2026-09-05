using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// An operator deletes an invited account in the server's own user editor, and
/// the invitation that produced it stays spent.
/// </summary>
/// <remarks>
/// <para>
/// <b>The trap this file exists for is one nobody would write on purpose.</b> A
/// single-use invitation whose account was deleted looks, to an implementation
/// that counts accounts rather than uses, exactly like an invitation nobody has
/// used. Such an implementation gives the link back to whoever still has it,
/// every time an operator tidies up, and the operator has no way to see it
/// happen.
/// </para>
/// <para>
/// <b>The use is really spent here rather than arranged.</b> Every record below
/// is spent by presenting its code at the route that redeems one, so the count
/// it carries afterwards is the count that routine wrote. A record written with
/// no uses left would assert that the decision refuses what a test has already
/// declared to be spent, which never goes near the routine that would make the
/// mistake, and #95's own comments record that as the reason this was not
/// written earlier.
/// </para>
/// <para>
/// <b>Nothing deletes an account, because this plugin cannot.</b> The write seam
/// declares three acts and none of them is a delete, which is #91's answer and
/// is asserted in <c>AccountsAreNeverWrittenTests</c>. A deletion outside the
/// plugin is a server that stops reporting an identifier, and that is exactly
/// what the read seam is handed here, which is how <c>AGoneAccountTests</c>
/// arranges the same state for #45.
/// </para>
/// </remarks>
public class ADeletedAccountKeepsTheUseSpentTests
{
    private const string Configured = "https://media.example.org";

    private static readonly DateTimeOffset _now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid _operator = Guid.Parse("12341234-5678-5678-9012-901290129012");

    /// <summary>
    /// A single-use invitation whose account the server no longer has is still
    /// spent, still claims the account, and refuses the same code again.
    /// </summary>
    /// <remarks>
    /// The four assertions are one test because each of them alone passes for an
    /// implementation the other three catch. A record that still reads as spent
    /// while the route honours the code is the trap itself; a route that refuses
    /// while the record has been quietly emptied is the same defect with the
    /// evidence destroyed; and a pointer dropped when the account went takes away
    /// the row an operator would use to find out what happened.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ASpentInvitationWhoseAccountIsDeletedRefusesTheCodeAgain()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();

        var honoured = await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));
        Assert.Equal(StatusCodes.Status303SeeOther, Assert.IsType<StatusCodeResult>(honoured).StatusCode);

        var created = seam.Answers;
        var afterRedemption = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(0, afterRedemption.UsesRemaining);
        Assert.Equal(created, Assert.Single(afterRedemption.AccountsProduced).Account);

        // The operator deletes the account in the server's own user editor. To
        // this plugin that is the server no longer reporting the identifier, and
        // nothing here writes to the server to arrange it.
        var afterDeletion = await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(afterDeletion).StatusCode);

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(0, stored.UsesRemaining);
        Assert.Equal(created, Assert.Single(stored.AccountsProduced).Account);
        Assert.Equal(1, seam.Asked.Count(asked => asked.StartsWith("create ", StringComparison.Ordinal)));
    }

    /// <summary>
    /// A multi-use invitation spends its uses one at a time whatever becomes of
    /// the accounts, and a deletion between two redemptions gives nothing back.
    /// </summary>
    /// <remarks>
    /// The single-use case above is the one an operator meets, and it is also
    /// the one where the two readings of the count agree by accident: zero uses
    /// left and zero accounts held both say the same thing. Here they disagree,
    /// which is what makes the count a count of uses rather than of accounts.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ADeletionBetweenTwoRedemptionsGivesNoUseBack()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 2);
        var seam = new ARecordingWriteSeam { Answers = Guid.Parse("00000000-0000-4000-8000-00000000000a") };

        await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("first", "a password long enough"));
        Assert.Equal(1, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);

        // The first account is deleted on the server, and the second person
        // follows the same link.
        seam.Answers = Guid.Parse("00000000-0000-4000-8000-00000000000b");
        await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("second", "a password long enough"));

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(0, stored.UsesRemaining);
        Assert.Equal(
            new[]
            {
                Guid.Parse("00000000-0000-4000-8000-00000000000a"),
                Guid.Parse("00000000-0000-4000-8000-00000000000b"),
            },
            stored.AccountsProduced.Accounts().ToArray());

        var third = await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("third", "a password long enough"));
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(third).StatusCode);
        Assert.Equal(0, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);
    }

    /// <summary>
    /// The record a spent redemption left renders its account as gone once the
    /// server stops reporting it, and it still says the invitation is spent.
    /// </summary>
    /// <remarks>
    /// <c>AGoneAccountTests</c> holds that a claimed account the server does not
    /// have renders as gone, over a record a test wrote. This is the same
    /// sentence over a record a redemption wrote, which is the state an operator
    /// actually meets: they follow a row, find the account gone, and need the
    /// row to say the link was used rather than to look untouched.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheRecordSaysSpentAndSaysTheAccountIsGone()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();

        await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        var view = View(Administrator(directory, new StubServerAccounts([])).One(minted.Invitation.Id));

        Assert.Equal(0, view.UsesRemaining);
        Assert.Equal(1, view.UsesGranted);
        var account = Assert.Single(view.AccountsProduced);
        Assert.Equal(seam.Answers, account.Id);
        Assert.Equal(AccountPresence.Gone, account.Presence);
    }

    /// <summary>
    /// The reverse direction answers for an account that has gone: the operator
    /// asking where a vanished account came from still gets its invitation.
    /// </summary>
    /// <remarks>
    /// This is the question the trail exists for, asked at the worst moment. An
    /// answer that dropped the row once the server stopped reporting the account
    /// would leave an operator with a deletion they cannot explain, which is the
    /// opposite of what keeping the pointer was for.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheAccountCanStillBeTracedToItsInvitationAfterItIsDeleted()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();

        await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        var claiming = Assert.IsAssignableFrom<IReadOnlyList<InvitationView>>(
            Assert.IsType<OkObjectResult>(
                Administrator(directory, new StubServerAccounts([])).WhichInvitationsCreated(seam.Answers).Result).Value);

        var view = Assert.Single(claiming);
        Assert.Equal(minted.Invitation.Id, view.Id);
        Assert.Equal(0, view.UsesRemaining);
        Assert.Equal(AccountPresence.Gone, Assert.Single(view.AccountsProduced).Presence);
    }

    /// <summary>
    /// A use taken by a redemption that produced no account is still taken, and
    /// no later redemption gets it back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the case that separates the count on the record from a count
    /// derived out of the accounts it claims, and it is the reason it is here
    /// rather than in the file that owns the route. On every other path the two
    /// agree: a record that has spent one use claims one account, so an
    /// implementation deriving the count is right by coincidence and this issue's
    /// trap is invisible.
    /// </para>
    /// <para>
    /// They part when a redemption takes a use and creates nothing, which is what
    /// a server refusing the write leaves behind and is the direction #53 owns.
    /// After it, granted minus accounts held is one higher than the record says,
    /// and a derived count hands that use to whoever still has the link. The
    /// sequence below drives exactly that: one refused write, one honoured
    /// redemption, and a third presentation that has to be refused.
    /// </para>
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AUseTakenByARedemptionThatCreatedNothingIsNotGivenBack()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 2);
        var refusing = new ARecordingWriteSeam
        {
            CredentialRefusal = new ServerAccountWriteRefusedException("the server refused"),
        };

        var refused = await RedeemRoute
            .Over(directory.Path, clock, refusing, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("first", "a password long enough"));
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(refused).StatusCode);

        var afterTheRefusal = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(1, afterTheRefusal.UsesRemaining);
        Assert.Empty(afterTheRefusal.AccountsProduced);

        var seam = new ARecordingWriteSeam();
        await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("second", "a password long enough"));

        var afterTheAccount = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(0, afterTheAccount.UsesRemaining);
        Assert.Equal(seam.Answers, Assert.Single(afterTheAccount.AccountsProduced).Account);

        var third = await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("third", "a password long enough"));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(third).StatusCode);
        Assert.Equal(1, seam.Asked.Count(asked => asked.StartsWith("create ", StringComparison.Ordinal)));
        Assert.Equal(0, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);
    }

    private static InvitationView View(ActionResult<InvitationView> result)
        => Assert.IsType<InvitationView>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static InvitesController Administrator(OwnedDirectory directory, IServerAccounts accounts)
        => new(
            new InvitationOperations(
                new StubStoreDirectory(directory.Path),
                new TestClock(_now),
                new StubPublicAddress(Configured),
                TestTemplates.AsConfigured),
            new StubOperatorIdentity(_operator),
            accounts)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
}
