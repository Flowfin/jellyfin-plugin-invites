using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A write seam that reads the invitation store on disk at the moment the
/// server is first asked to create anything.
/// </summary>
/// <remarks>
/// The window #53 is about is one line wide and it cannot be observed from
/// outside: both orders leave the same state once the redemption finishes, and
/// they differ only in what is on disk while the server is being written to.
/// So the reading is taken from inside the first call, which is the only moment
/// that tells the two orders apart.
/// </remarks>
internal sealed class ASeamThatReadsTheStore : IServerAccountWrites
{
    private readonly string _directory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ASeamThatReadsTheStore"/>
    /// class.
    /// </summary>
    /// <param name="directory">The store directory to read.</param>
    public ASeamThatReadsTheStore(string directory)
    {
        _directory = directory;
    }

    /// <summary>
    /// Gets the uses the record had left when the server was first asked to
    /// create an account, or null where it was never asked.
    /// </summary>
    public int? UsesOnDiskWhenAsked { get; private set; }

    /// <summary>
    /// Gets the accounts the record claimed at that same moment.
    /// </summary>
    public int? AccountsClaimedWhenAsked { get; private set; }

    /// <inheritdoc />
    public Task<Guid> CreateAccountAsync(string username)
    {
        var stored = new InvitationStore(_directory).Read().Invitations[0];
        UsesOnDiskWhenAsked = stored.UsesRemaining;
        AccountsClaimedWhenAsked = stored.AccountsProduced.Length;

        return Task.FromResult(Guid.Parse("66666666-6666-4666-8666-666666666666"));
    }

    /// <inheritdoc />
    public Task SetCredentialAsync(Guid account, string password) => Task.CompletedTask;

    /// <inheritdoc />
    public Task ApplyTemplateAsync(Guid account, AccountTemplate template) => Task.CompletedTask;
}

/// <summary>
/// A write seam that records what it was asked and is expected to be asked
/// nothing.
/// </summary>
/// <remarks>
/// The assertion a restart test needs is that the server was never touched, and
/// an empty list is a clearer subject for that than a trail nobody reads.
/// </remarks>
internal sealed class ASeamThatShouldNotBeAsked : IServerAccountWrites
{
    private readonly List<string> _asked = new();

    /// <summary>
    /// Gets what the seam was asked for, in order.
    /// </summary>
    public IReadOnlyList<string> Asked => _asked;

    /// <inheritdoc />
    public Task<Guid> CreateAccountAsync(string username)
    {
        _asked.Add("create " + username);
        return Task.FromResult(Guid.Parse("77777777-7777-4777-8777-777777777777"));
    }

    /// <inheritdoc />
    public Task SetCredentialAsync(Guid account, string password)
    {
        _asked.Add("credential");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ApplyTemplateAsync(Guid account, AccountTemplate template)
    {
        _asked.Add("template");
        return Task.CompletedTask;
    }
}

/// <summary>
/// The window between an account existing on the server and the record saying
/// so, and which way a death inside it falls.
/// </summary>
/// <remarks>
/// <para>
/// <b>#53 states the preference and these assert that the tree has it.</b> A
/// lost invitation is an operator minting a second one; an extra account is a
/// stranger on the server. So the use is written to disk before the server is
/// asked for anything, and a death anywhere after that costs the invitation
/// rather than handing out a second account.
/// </para>
/// <para>
/// <b>A death is modelled as a restart rather than as an exception.</b> An
/// exception is a graceful failure the route already answers, and
/// <c>RedeemPostTests.AServerThatRefusesTheWriteLeavesTheUseTaken</c> asserts
/// what it leaves. What that does not reach is the question this issue asks:
/// whether the invitation can produce a SECOND account afterwards. Answering it
/// needs the store read again from disk by something that was not part of the
/// interrupted redemption, which is what a restart is, so the second test below
/// builds a fresh controller over the same directory rather than reusing the
/// one that crashed.
/// </para>
/// <para>
/// <b>The bound.</b> No process was killed. What is asserted is the state on
/// disk at the moment the server is written to, and what a component reading
/// that disk afterwards decides. A machine losing power mid-write is a question
/// about the store's own write, which is where the atomic replace is argued, and
/// nothing here measures it.
/// </para>
/// </remarks>
public class ACrashLosesTheInvitationTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The use is already spent on disk when the server is first asked to
    /// create an account, so there is no moment at which an account exists and
    /// the record still offers the use that made it.
    /// </summary>
    /// <remarks>
    /// This is the whole of #53's chosen direction as a single assertion, and it
    /// is the one that reds if somebody reorders the route to create first and
    /// spend afterwards. That order is the tempting one, because it means a
    /// server that refuses the write costs the person nothing, and it is exactly
    /// the window this issue exists against: a death between the two leaves an
    /// account created and an invitation that still works.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheUseIsOnDiskBeforeTheServerIsAskedToCreateAnything()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ASeamThatReadsTheStore(directory.Path);
        var controller = RedeemRoute.Over(directory.Path, clock, seam, RedeemRoute.Request());

        var answer = await controller.Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status303SeeOther, Assert.IsType<StatusCodeResult>(answer).StatusCode);
        Assert.Equal(0, seam.UsesOnDiskWhenAsked);
        Assert.Equal(0, seam.AccountsClaimedWhenAsked);
        Assert.Equal(minted.Invitation.Id, new InvitationStore(directory.Path).Read().Invitations[0].Id);
    }

    /// <summary>
    /// An account that exists on the server with the record never updated to
    /// claim it cannot be followed by a second account from the same
    /// invitation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The kill is at the exact point this issue names. The reservation runs, so
    /// the use is spent and on disk; the account is created through the seam, so
    /// it exists on the server; and the call that records it against the
    /// invitation never happens, which is the death. Then a controller that
    /// knows nothing about any of it reads the same directory, which is the
    /// restart.
    /// </para>
    /// <para>
    /// What is asserted is #53's own words: no extra account is obtainable. The
    /// second redemption is refused and the seam it was handed was asked for
    /// nothing at all, so the refusal is the invitation being spent rather than
    /// an account being created and then discarded.
    /// </para>
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ARestartAfterTheAccountExistsCannotProduceASecondAccount()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);

        var interrupted = new ARecordingWriteSeam();
        var reservation = RedeemRoute.Operations(directory.Path, clock).Reserve(minted.Code);
        Assert.True(reservation.MayCreateAnAccount);
        await AccountCreation.CreateAsync(
            interrupted,
            "newcomer",
            "a password long enough",
            reservation.Reserved!.Template!).ConfigureAwait(true);

        // The death: RecordAccount is never called, so the account exists on the
        // server and the record does not name it.
        var spent = new InvitationStore(directory.Path).Read().Invitations[0];
        Assert.Equal(0, spent.UsesRemaining);
        Assert.Empty(spent.AccountsProduced);

        var afterTheRestart = new ASeamThatShouldNotBeAsked();
        var controller = RedeemRoute.Over(directory.Path, clock, afterTheRestart, RedeemRoute.Request());

        var answer = await controller.Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(answer).StatusCode);
        Assert.Empty(afterTheRestart.Asked);
        Assert.Equal(0, new InvitationStore(directory.Path).Read().Invitations[0].UsesRemaining);
    }
}
