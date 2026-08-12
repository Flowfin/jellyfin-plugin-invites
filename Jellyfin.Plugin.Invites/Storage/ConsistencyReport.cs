using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.Invites.Invitations;

namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// What one read of the store says about the accounts it claims, held against
/// the accounts the server has.
/// </summary>
/// <remarks>
/// <para>
/// A restored data directory is a valid store. Nothing inside it says it is
/// older than the server around it, so the restore itself cannot be detected
/// here, and docs/disaster-cases.md says so in as many words. What can be seen
/// is the consequence: the accounts a restored store claims to have created and
/// the accounts the server actually holds stop being the same set. This type is
/// that comparison and nothing else.
/// </para>
/// <para>
/// <b>It decides nothing and repairs nothing.</b> No record is written, no
/// account is created, no account is removed, and no use is handed back. That is
/// a requirement rather than an omission: a report that quietly put a use back
/// after a restore would be deciding, on the operator's behalf, to undo their
/// backup. The type has no way to do any of it, because it is built from two
/// lists of values and holds no store, no user manager and no clock. Ignoring
/// what it says is a line somebody has to write, which is the same shape
/// <see cref="StorePermissions"/> takes.
/// </para>
/// <para>
/// <b>Both directions are named, and they are not equally sharp.</b> An account
/// a record claims and the server does not have is a disagreement about this
/// plugin's own work. An account the server has that no record claims is only a
/// disagreement where the caller hands in accounts this plugin is answerable
/// for: this plugin puts no mark on an account, so it cannot tell one it created
/// and forgot from one an operator made by hand. Handing in every account on the
/// server therefore reads the second direction as "every account this plugin did
/// not create", which is true and is not a finding. What would sharpen it is a
/// record of what the plugin did to an account, which is #94 and is open.
/// </para>
/// </remarks>
public sealed class ConsistencyReport
{
    private ConsistencyReport(
        ImmutableArray<ClaimedAccount> accountsClaimedButAbsent,
        ImmutableArray<Guid> accountsPresentButUnclaimed)
    {
        AccountsClaimedButAbsent = accountsClaimedButAbsent;
        AccountsPresentButUnclaimed = accountsPresentButUnclaimed;
    }

    /// <summary>
    /// Gets the accounts a record claims to have created that the server does
    /// not have, each named with the invitation that claims it.
    /// </summary>
    /// <remarks>
    /// A record naming the same account twice produces two entries here. The
    /// report says what the store says rather than tidying it, because a record
    /// that names one account twice is itself something an operator would want
    /// to see rather than something to be silently folded into one line.
    /// </remarks>
    public ImmutableArray<ClaimedAccount> AccountsClaimedButAbsent { get; }

    /// <summary>
    /// Gets the accounts handed in that no record claims.
    /// </summary>
    /// <remarks>
    /// Read this against the third paragraph on the type. It is the direction a
    /// restore produces, where accounts created after the backup survive in the
    /// server's own database while the store that knew about them is gone, and it
    /// is also the direction every account an operator created by hand falls
    /// into.
    /// </remarks>
    public ImmutableArray<Guid> AccountsPresentButUnclaimed { get; }

    /// <summary>
    /// Gets a value indicating whether the two sides agree in both directions.
    /// </summary>
    public bool Agrees => AccountsClaimedButAbsent.IsEmpty && AccountsPresentButUnclaimed.IsEmpty;

    /// <summary>
    /// Gets one sentence saying what was found, with counts and no identifiers.
    /// </summary>
    /// <remarks>
    /// The counts are here and the identifiers are not, so that a caller putting
    /// this sentence somewhere it will be copied is not thereby copying who was
    /// invited and which account they got. Which accounts they are is on the two
    /// lists above, for a caller that has somewhere to show them, and
    /// docs/personal-data.md is the authority for what this plugin holds about a
    /// person at all.
    /// </remarks>
    public string Summary => Agrees
        ? "The store and the accounts it was compared against agree."
        : string.Format(
            CultureInfo.InvariantCulture,
            "{0} account(s) the store claims to have created are not there, and {1} account(s) that were compared against it are claimed by no invitation.",
            AccountsClaimedButAbsent.Length,
            AccountsPresentButUnclaimed.Length);

    /// <summary>
    /// Reads the store and compares what it claims against the accounts handed
    /// in.
    /// </summary>
    /// <param name="store">The store to read.</param>
    /// <param name="accountsTheServerHas">
    /// The accounts to hold the store against. Which accounts those are is the
    /// caller's decision and is argued on this type: the plugin cannot work out
    /// on its own which accounts it is answerable for.
    /// </param>
    /// <returns>The report. Never null.</returns>
    /// <remarks>
    /// This is the load. <see cref="InvitationStore.Read"/> creates nothing when
    /// the file is not there and writes nothing in any case, so a report taken
    /// this way leaves the data directory exactly as it found it, and the suite
    /// asserts that against a real directory rather than trusting it.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public static ConsistencyReport OfALoad(InvitationStore store, IReadOnlyCollection<Guid> accountsTheServerHas)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(accountsTheServerHas);

        return Of(store.Read().Invitations, accountsTheServerHas);
    }

    /// <summary>
    /// Compares records already read against the accounts handed in.
    /// </summary>
    /// <param name="records">The invitation records, as the caller read them.</param>
    /// <param name="accountsTheServerHas">The accounts to hold them against.</param>
    /// <returns>The report. Never null.</returns>
    /// <remarks>
    /// The records are taken rather than read, so a caller that already holds
    /// them under the lock covering read, decide and write can build the report
    /// from the same reading rather than opening the file a second time and
    /// comparing against a store that moved in between.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public static ConsistencyReport Of(IReadOnlyCollection<Invitation> records, IReadOnlyCollection<Guid> accountsTheServerHas)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(accountsTheServerHas);

        var present = new HashSet<Guid>(accountsTheServerHas);
        var claimed = new HashSet<Guid>();
        var absent = ImmutableArray.CreateBuilder<ClaimedAccount>();

        foreach (var record in records)
        {
            foreach (var account in record.AccountsProduced)
            {
                claimed.Add(account);
                if (!present.Contains(account))
                {
                    absent.Add(new ClaimedAccount(record.Id, account));
                }
            }
        }

        // Both lists are ordered by identifier so that two runs over the same
        // two sets read identically. A report whose lines move about between
        // loads is one nobody can compare with the last one they looked at, and
        // the order records happen to sit in on disk is not information.
        var unclaimed = accountsTheServerHas
            .Where(account => !claimed.Contains(account))
            .Distinct()
            .OrderBy(account => account)
            .ToImmutableArray();

        return new ConsistencyReport(
            absent.ToImmutable().Sort((left, right) =>
            {
                var byInvitation = left.InvitationId.CompareTo(right.InvitationId);
                return byInvitation != 0 ? byInvitation : left.AccountId.CompareTo(right.AccountId);
            }),
            unclaimed);
    }
}
