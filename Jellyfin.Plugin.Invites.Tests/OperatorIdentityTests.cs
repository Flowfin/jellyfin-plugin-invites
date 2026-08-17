using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// An authorization context the test holds, standing in for the server's own.
/// </summary>
internal sealed class StubAuthorizationContext : IAuthorizationContext
{
    private readonly AuthorizationInfo _info;

    public StubAuthorizationContext(AuthorizationInfo info)
    {
        _info = info;
    }

    public Task<AuthorizationInfo> GetAuthorizationInfo(HttpContext requestContext) => Task.FromResult(_info);

    public Task<AuthorizationInfo> GetAuthorizationInfo(HttpRequest requestContext) => Task.FromResult(_info);
}

/// <summary>
/// The seam that answers who is calling.
/// </summary>
/// <remarks>
/// One thing is asserted here and it is the reason the seam exists rather than
/// the controller taking the server's own type. The server's answer carries an
/// identifier derived from a user entity rather than one a caller can set, so a
/// test that wanted to say which operator is calling would have to build a user,
/// and the assembly that declares one is not referenced here. What this can
/// still show is that the wrapper reads that field and invents nothing when the
/// server names nobody.
/// </remarks>
public class OperatorIdentityTests
{
    /// <summary>
    /// A request the server names no account for answers with the empty
    /// identifier rather than with a made-up one.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ARequestNamingNobodyAnswersWithTheEmptyIdentifier()
    {
        var identity = new RequestOperatorIdentity(new StubAuthorizationContext(new AuthorizationInfo()));

        Assert.Equal(Guid.Empty, await identity.OfAsync(new DefaultHttpContext()));
    }

    /// <summary>
    /// The seam refuses to be built on nothing, so a registration that forgot
    /// the server's context fails where it is made rather than on the first
    /// request.
    /// </summary>
    [Fact]
    public void TheSeamRefusesToBeBuiltWithoutTheServersContext()
    {
        Assert.Throws<ArgumentNullException>(() => new RequestOperatorIdentity(null!));
    }
}
