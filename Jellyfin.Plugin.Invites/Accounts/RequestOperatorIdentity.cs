using System;
using System.Threading.Tasks;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.Invites.Accounts;

/// <summary>
/// The calling operator, read off the server's own authorization context.
/// </summary>
/// <remarks>
/// One call and one field. Everything else the server works out about a request
/// - the device, the client, the token - is deliberately not carried through
/// this seam, because none of it belongs in an invitation record and a seam that
/// offered it would be an invitation to put it there.
/// </remarks>
public sealed class RequestOperatorIdentity : IOperatorIdentity
{
    private readonly IAuthorizationContext _authorization;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestOperatorIdentity"/> class.
    /// </summary>
    /// <param name="authorization">The server's own authorization context.</param>
    /// <exception cref="ArgumentNullException">The context is null.</exception>
    public RequestOperatorIdentity(IAuthorizationContext authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        _authorization = authorization;
    }

    /// <inheritdoc />
    public async Task<Guid> OfAsync(HttpContext request)
    {
        var info = await _authorization.GetAuthorizationInfo(request).ConfigureAwait(false);

        return info.UserId;
    }
}
