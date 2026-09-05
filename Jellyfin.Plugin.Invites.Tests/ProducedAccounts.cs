using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jellyfin.Plugin.Invites.Invitations;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The claims a test builds, and the identifiers it reads back out of them.
/// </summary>
/// <remarks>
/// <para>
/// A record claims an account as an entry carrying an expiry since #468, and
/// almost every test in this suite is about something other than that expiry:
/// it wants a record that produced two accounts, or it wants to compare the
/// identifiers a record claims against the ones a seam handed out. Written
/// inline, each of those grows a projection that says nothing about what the
/// test is for.
/// </para>
/// <para>
/// Nothing here has an opinion. A claim built by <see cref="ThatDoNotExpire"/>
/// carries the absence <see cref="ProducedAccount"/> declares means an account
/// that does not expire, which is the value everything in this plugin writes
/// today, and a test that wants an expiry builds the claim itself so the
/// expiry is visible where it is asserted.
/// </para>
/// </remarks>
internal static class ProducedAccounts
{
    /// <summary>
    /// Claims on accounts that do not expire, one per identifier.
    /// </summary>
    /// <param name="accounts">The identifiers.</param>
    /// <returns>The claims, in the order they were given.</returns>
    internal static ImmutableArray<ProducedAccount> ThatDoNotExpire(params Guid[] accounts)
    {
        return ThatDoNotExpire((IEnumerable<Guid>)accounts);
    }

    /// <summary>
    /// Claims on accounts that do not expire, one per identifier.
    /// </summary>
    /// <param name="accounts">The identifiers.</param>
    /// <returns>The claims, in the order they were given.</returns>
    internal static ImmutableArray<ProducedAccount> ThatDoNotExpire(IEnumerable<Guid> accounts)
    {
        return ImmutableArray.CreateRange(accounts.Select(ProducedAccount.ThatDoesNotExpire));
    }

    /// <summary>
    /// The identifiers a set of claims names, in the order the claims sit in.
    /// </summary>
    /// <param name="claims">The claims.</param>
    /// <returns>The identifiers.</returns>
    internal static IEnumerable<Guid> Accounts(this IEnumerable<ProducedAccount> claims)
    {
        return claims.Select(claim => claim.Account);
    }
}
