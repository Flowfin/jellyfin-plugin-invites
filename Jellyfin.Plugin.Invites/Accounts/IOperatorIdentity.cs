using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.Invites.Accounts;

/// <summary>
/// Who is calling, as one identifier.
/// </summary>
/// <remarks>
/// <para>
/// A minted invitation is answerable to the operator who minted it and a
/// revocation records who revoked it, so a route has to know which account it is
/// serving. The server already worked that out when it authenticated the
/// request, and this is the one thing this plugin asks it for.
/// </para>
/// <para>
/// It is a seam of this plugin's own rather than the server's own type taken
/// directly, for the same reason <see cref="IServerAccounts"/> is one. The
/// server answers with a record carrying an identifier that is derived from a
/// user entity rather than settable, so a test that wanted to say "this operator
/// is calling" would have to build a user, and building a user needs an assembly
/// this plugin does not reference. Asking for less is what lets the routes be
/// driven by a test with no server anywhere near them.
/// </para>
/// </remarks>
public interface IOperatorIdentity
{
    /// <summary>
    /// The account the request was authenticated as.
    /// </summary>
    /// <param name="request">The request being served.</param>
    /// <returns>
    /// The identifier, or <see cref="Guid.Empty"/> where the server names none.
    /// </returns>
    /// <remarks>
    /// The empty identifier is not a failure to report here. Every route that
    /// asks this question requires an administrator, so a request that reached
    /// one and carries no account is a state the server does not produce, and a
    /// record naming nobody is a record whose defect is visible in the
    /// administrator view rather than one this plugin should invent a refusal
    /// for.
    /// </remarks>
    Task<Guid> OfAsync(HttpContext request);
}
