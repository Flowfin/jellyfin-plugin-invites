using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Invites.Time;

namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// What one process found when it took the store directory: either the claim
/// and a report on what the store disagrees with, or the refusal that says
/// somebody else has it.
/// </summary>
/// <remarks>
/// <para>
/// Two decisions that were already written down meet here for the first time.
/// docs/disaster-cases.md says the shared-store case is detected at startup and
/// refused rather than warned about, which is <see cref="StoreLock"/>. #46 says
/// a load compares what the store claims to have created against the accounts
/// the server actually has, which is <see cref="ConsistencyReport"/>. Neither
/// had a moment to happen at, because nothing read the store when the plugin
/// loaded, and this is that moment.
/// </para>
/// <para>
/// <b>The claim comes first and the report is not taken without it.</b> A report
/// read out of a directory another process is writing to describes a store that
/// moved while it was being read, and an operator cannot tell that from a store
/// that really disagrees. So a refused claim produces no report at all rather
/// than a report with a caveat next to it.
/// </para>
/// <para>
/// <b>Nothing here repairs anything.</b> The report names disagreements and the
/// claim writes one file that says who holds the directory. A load that put a
/// use back, removed a record naming an account that is gone, or created an
/// account a record claims would be deciding, on a restored backup, to undo the
/// restore. That is #46's constraint and it holds for the whole of this routine
/// rather than for the comparison alone.
/// </para>
/// <para>
/// <b>What it does not do.</b> It does not tell an operator anything. Where the
/// answer is shown is the caller's, and the only caller today writes it to the
/// log under the rules in docs/logging.md. An operator surface for it is #89.
/// </para>
/// </remarks>
public sealed class StoreLoad : IDisposable
{
    private readonly StoreLock? _claim;

    private StoreLoad(StoreLock? claim, StoreInUseException? refusal, ConsistencyReport? report, StoreMigration? migration = null)
    {
        _claim = claim;
        Refusal = refusal;
        Report = report;
        Migration = migration;
    }

    /// <summary>
    /// Gets a value indicating whether this process holds the store directory.
    /// </summary>
    public bool HoldsTheStore => _claim is not null;

    /// <summary>
    /// Gets the refusal, where somebody else holds the directory, and
    /// <c>null</c> where the claim was taken.
    /// </summary>
    /// <remarks>
    /// It carries the holder line and the path of the file to remove, because
    /// whoever meets this is looking at a plugin that will not use its store and
    /// the answer has to be in front of them.
    /// </remarks>
    public StoreInUseException? Refusal { get; }

    /// <summary>
    /// Gets what the store disagreed with the server about, and <c>null</c>
    /// where no comparison was made: either the claim was refused, or no account
    /// list was handed in to compare against.
    /// </summary>
    public ConsistencyReport? Report { get; }

    /// <summary>
    /// Gets what the read had to do to bring an older document forward, and
    /// <c>null</c> where the document was already the shape this build writes or
    /// where nothing was read.
    /// </summary>
    /// <remarks>
    /// It is carried rather than acted on for the reason the report is: what a
    /// load does is claim and read, and who tells an operator anything is the
    /// caller. #92 asks that a value that cannot be mapped forward produce the
    /// strict option and that the plugin say what it did, and this is the half
    /// of that sentence a load can hold.
    /// </remarks>
    public StoreMigration? Migration { get; }

    /// <summary>
    /// Claims the store directory and reads it once.
    /// </summary>
    /// <param name="directory">The directory the store sits in.</param>
    /// <param name="host">The machine making the claim.</param>
    /// <param name="process">The process making the claim.</param>
    /// <param name="clock">The time source, read once for the claim.</param>
    /// <param name="accountsTheServerHas">
    /// The accounts to hold the store against, or <c>null</c> where this server
    /// could not be asked. Which accounts those are is the caller's decision,
    /// for the reason argued on <see cref="ConsistencyReport"/>. A <c>null</c>
    /// takes the claim and makes no comparison, because an account list nobody
    /// could read is not an empty one: comparing against nothing would report
    /// every account the store claims as an account the server has lost.
    /// </param>
    /// <returns>
    /// The load. Release the claim by disposing it, which is what the caller
    /// does when the server is stopping.
    /// </returns>
    /// <exception cref="ArgumentNullException">The clock is null.</exception>
    /// <exception cref="ArgumentException">The directory is null or blank.</exception>
    public static StoreLoad Of(
        string directory,
        string host,
        int process,
        IClock clock,
        IReadOnlyCollection<Guid>? accountsTheServerHas)
    {
        ArgumentNullException.ThrowIfNull(clock);

        StoreLock claim;
        try
        {
            claim = StoreLock.Take(directory, host, process, clock.UtcNow);
        }
        catch (StoreInUseException refused)
        {
            return new StoreLoad(claim: null, refusal: refused, report: null);
        }

        if (accountsTheServerHas is null)
        {
            return new StoreLoad(claim, refusal: null, report: null);
        }

        try
        {
            // One read rather than two. The comparison and the migration
            // observation are two facts about the same load of the same file,
            // and reading twice would let them disagree about a store somebody
            // wrote to in between.
            var contents = new InvitationStore(directory).Read();

            return new StoreLoad(
                claim,
                refusal: null,
                ConsistencyReport.Of(contents.Invitations, accountsTheServerHas),
                contents.Migration);
        }
        catch
        {
            // A store that cannot be read is raised rather than answered as an
            // empty one, which is InvitationStore.Read's decision and not this
            // routine's to soften. What this routine owes is that the claim does
            // not outlive the load that failed: a directory left claimed by a
            // process that gave up is one an operator has to clear by hand, for
            // a load that never started using it.
            claim.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Releases the claim, where one was taken.
    /// </summary>
    public void Dispose()
    {
        _claim?.Dispose();
    }
}
