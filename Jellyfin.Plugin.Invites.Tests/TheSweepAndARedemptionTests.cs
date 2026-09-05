using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Storage;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The retention sweep and a redemption run against one store at the same time
/// and neither corrupts the other.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the last clause of #59 and it could not be written before.</b> A
/// test naming two operations that both only read would look like the clause and
/// prove none of it, which is what the suite said about itself while nothing
/// consumed a use. Both halves write now: the sweep removes records whose
/// retention has run out, and a honoured redemption takes a use and records the
/// account it produced.
/// </para>
/// <para>
/// <b>What makes it safe is that both are methods on the type that owns the
/// monitor.</b> Neither can hold the file without holding the gate, which is a
/// property of where they are written rather than of anybody remembering to take
/// a lock. <c>RetentionSweepTests.OnlyTheRoutineHoldingTheMonitorConstructsTheStore</c>
/// refuses the shape that would break it, a store constructed inside the
/// scheduled task. What is left for this file is the behaviour: run them
/// together and require every invariant to survive.
/// </para>
/// <para>
/// <b>It is a stress rather than a deterministic reproduction, and that is
/// stated rather than hidden.</b> The interleaving is the scheduler's, so a run
/// that happens to serialise the two proves less than one that does not, and
/// nothing here can tell those apart. What every run does assert is a set of
/// invariants that hold under any interleaving of a correct implementation and
/// are broken by an incorrect one: every code honoured exactly once, every use
/// taken exactly once, every aged record removed, no live record removed, and a
/// file that still reads. A deterministic test of the lock is #106 and is not
/// this.
/// </para>
/// <para>
/// <b>Nothing waits on the machine clock.</b> The two sides are started together
/// on a barrier that is signalled rather than slept against, which is what the
/// headless rule asks for where the wait is for another thread.
/// </para>
/// </remarks>
public class TheSweepAndARedemptionTests
{
    private static readonly DateTimeOffset _now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid _operator = Guid.Parse("11111111-1111-4111-8111-111111111111");

    /// <summary>
    /// How many invitations are redeemed while the sweep runs. Enough that the
    /// two sides overlap on an ordinary machine, and few enough that the run
    /// costs nothing measurable.
    /// </summary>
    private const int Redeemed = 8;

    /// <summary>
    /// How many records are old enough for the sweep to remove, so the sweep
    /// writes rather than reading and finding nothing to do. A sweep with
    /// nothing to remove would satisfy the clause's words and none of it.
    /// </summary>
    private const int Aged = 8;

    /// <summary>
    /// How many times the sweep is run beside the redemptions. The first run
    /// removes the aged records and the rest find nothing, which is the ordinary
    /// case on a server and is the one that has to stay harmless.
    /// </summary>
    private const int Sweeps = 20;

    /// <summary>
    /// A sweep running beside redemptions removes exactly the aged records,
    /// takes exactly one use from each redeemed invitation, and leaves a store
    /// that reads.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ASweepBesideRedemptionsRemovesTheAgedAndSpendsEachUseOnce()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var operations = RedeemRoute.Operations(directory.Path, clock);

        var codes = new List<string>();
        var live = new List<Guid>();
        for (var minted = 0; minted < Redeemed; minted++)
        {
            var one = operations.Mint(_operator, "Household", null, uses: 1);
            codes.Add(one.Code);
            live.Add(one.Invitation.Id);
        }

        var aged = AgeIntoTheStore(directory.Path);

        var verdicts = new Reservation[Redeemed];
        var removed = new List<Guid>();
        using var start = new Barrier(2);

        var redeeming = Task.Run(() =>
        {
            start.SignalAndWait();
            for (var presented = 0; presented < Redeemed; presented++)
            {
                verdicts[presented] = operations.Reserve(codes[presented]);
            }
        });

        var sweeping = Task.Run(() =>
        {
            start.SignalAndWait();
            for (var run = 0; run < Sweeps; run++)
            {
                removed.AddRange(operations.Sweep());
            }
        });

        await Task.WhenAll(redeeming, sweeping).ConfigureAwait(true);

        Assert.All(verdicts, verdict => Assert.True(verdict.MayCreateAnAccount));

        var stored = new InvitationStore(directory.Path).Read().Invitations;
        Assert.Equal(Redeemed, stored.Length);
        Assert.All(stored, record => Assert.Equal(0, record.UsesRemaining));
        Assert.Equal(
            live.OrderBy(id => id).ToArray(),
            stored.Select(record => record.Id).OrderBy(id => id).ToArray());
        Assert.Equal(
            aged.OrderBy(id => id).ToArray(),
            removed.OrderBy(id => id).ToArray());
    }

    /// <summary>
    /// Recording the accounts a redemption produced survives a sweep running
    /// beside it, and the sweep removes none of the records being written to.
    /// </summary>
    /// <remarks>
    /// The write that takes a use and the write that records the account happen
    /// at two different moments with the account creation between them, so a
    /// sweep can land in that gap on a running server. It is the gap this file
    /// exists to put to the fault, and it is the one an operator's nightly task
    /// is most likely to fall into, because that is when the server is otherwise
    /// idle and a redemption is a slow act.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AccountsRecordedBesideASweepAllSurvive()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var operations = RedeemRoute.Operations(directory.Path, clock);

        var reserved = new List<Guid>();
        var accounts = new List<Guid>();
        for (var minted = 0; minted < Redeemed; minted++)
        {
            var one = operations.Mint(_operator, "Household", null, uses: 1);
            Assert.True(operations.Reserve(one.Code).MayCreateAnAccount);
            reserved.Add(one.Invitation.Id);
            accounts.Add(Guid.NewGuid());
        }

        AgeIntoTheStore(directory.Path);

        using var start = new Barrier(2);

        var recording = Task.Run(() =>
        {
            start.SignalAndWait();
            for (var written = 0; written < Redeemed; written++)
            {
                operations.RecordAccount(reserved[written], accounts[written]);
            }
        });

        var sweeping = Task.Run(() =>
        {
            start.SignalAndWait();
            for (var run = 0; run < Sweeps; run++)
            {
                operations.Sweep();
            }
        });

        await Task.WhenAll(recording, sweeping).ConfigureAwait(true);

        var stored = new InvitationStore(directory.Path).Read().Invitations;
        Assert.Equal(Redeemed, stored.Length);
        Assert.Equal(
            accounts.OrderBy(id => id).ToArray(),
            stored.SelectMany(record => record.AccountsProduced.Accounts()).OrderBy(id => id).ToArray());
        Assert.All(stored, record => Assert.Equal(0, record.UsesRemaining));
    }

    /// <summary>
    /// The arrangement really gives the sweep something to remove. Without this,
    /// both assertions above would pass over a store the sweep never wrote to,
    /// and the clause would be satisfied by two operations that never met.
    /// </summary>
    [Fact]
    public void TheAgedRecordsAreRemovableAndTheMintedOnesAreNot()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var operations = RedeemRoute.Operations(directory.Path, clock);
        var minted = operations.Mint(_operator, "Household", null, uses: 1);

        var aged = AgeIntoTheStore(directory.Path);

        var held = new InvitationStore(directory.Path).Read().Invitations;
        Assert.Equal(Aged + 1, held.Length);
        Assert.All(
            held.Where(record => aged.Contains(record.Id)),
            record => Assert.True(Retention.MayBeRemoved(record, _now)));
        Assert.False(
            Retention.MayBeRemoved(
                Assert.Single(held.Where(record => record.Id == minted.Invitation.Id)),
                _now));
    }

    /// <summary>
    /// Adds records old enough for the sweep to remove, beside whatever the
    /// store already holds.
    /// </summary>
    /// <remarks>
    /// They are written rather than minted, because a mint refuses an expiry in
    /// the past, which is a rule this file is not about and could not work
    /// around without breaking one that is.
    /// </remarks>
    /// <param name="directory">The store directory the test owns.</param>
    /// <returns>The identifiers of the records that were added.</returns>
    private static IReadOnlyList<Guid> AgeIntoTheStore(string directory)
    {
        var store = new InvitationStore(directory);
        var held = store.Read().Invitations;
        var expired = _now - Retention.RecordRetention - TimeSpan.FromDays(1);

        var added = new List<Guid>();
        var writing = held.ToBuilder();
        for (var record = 0; record < Aged; record++)
        {
            var id = Guid.NewGuid();
            added.Add(id);
            writing.Add(new Invitation(
                id: id,
                codeHash: ImmutableArray.Create(new byte[32]),
                mintedBy: _operator,
                mintedAt: expired - TimeSpan.FromDays(7),
                expiresAt: expired,
                usesGranted: 1,
                usesRemaining: 0,
                revokedAt: null,
                revokedBy: null,
                templateLabel: "Household",
                template: TestTemplates.Household,
                accountsProduced: ImmutableArray<ProducedAccount>.Empty));
        }

        store.Write(writing.ToImmutable());

        return added;
    }
}
