using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.Invites.Accounts;

/// <summary>
/// The accounts the server has, as identifiers.
/// </summary>
/// <remarks>
/// Nothing in this plugin needs an account for anything except its identifier,
/// and asking for less is what keeps the load away from the rest of the user
/// manager. It is also the seam that lets a caller be driven by a test without
/// a server anywhere near it.
/// </remarks>
public interface IServerAccounts
{
    /// <summary>
    /// Gets the identifier of every account on the server, or <c>null</c> where
    /// this server does not answer the question in a shape this plugin knows.
    /// </summary>
    /// <remarks>
    /// The null is not a stylistic choice. It is the case
    /// <see cref="ServerAccounts"/> exists for, and a caller that treated it as
    /// an empty list would report every account the store claims as one the
    /// server has lost.
    /// </remarks>
    IReadOnlyCollection<Guid>? Identifiers { get; }
}
