using System;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Invites.Configuration;
using Jellyfin.Plugin.Invites.Invitations;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// What an invitation link is built from, and what it is never built from.
/// </summary>
public class InvitationLinkTests
{
    private const string Configured = "https://media.example.org";
    private const string Code = "ABCD-EFGH-JKLM";

    /// <summary>
    /// The link is the configured address, the redemption route and the code.
    /// </summary>
    [Fact]
    public void TheLinkIsTheConfiguredAddressAndTheRoute()
    {
        Assert.Equal(
            "https://media.example.org/redeem/ABCD-EFGH-JKLM",
            InvitationLink.For(Configured, Code));
    }

    /// <summary>
    /// A request saying the server was reached somewhere else does not change
    /// it. This is the case the whole issue is about: a minting call carrying a
    /// forged host produces a link pointing at the attacker's server, and the
    /// invited person types their new password into it.
    /// </summary>
    /// <remarks>
    /// On its own this leg proves little, and it is worth saying so rather than
    /// letting it read as the whole proof: the request is in the process and
    /// the builder never sees it, so the assertion could not fail. What makes
    /// the property hold is that there is nowhere for a request to enter, and
    /// <see cref="NothingHereCanBeHandedARequest"/> is the leg that asserts
    /// that. This one is here because a reader looking for the forged host
    /// should find it written down rather than argued away.
    /// </remarks>
    [Fact]
    public void AForgedHostDoesNotReachTheLink()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Headers.Host = "attacker.example";

        var link = InvitationLink.For(Configured, Code);

        Assert.DoesNotContain("attacker.example", link, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(Configured + "/", link, StringComparison.Ordinal);
    }

    /// <summary>
    /// And there is no way to hand it one. Every parameter of every public
    /// member is a string, so no request, context or host object can be passed
    /// in for a later change to start reading.
    /// </summary>
    /// <remarks>
    /// This is the machine-checkable form of "link construction reads no
    /// request header". The greppable rules in
    /// <c>.github/lint/invariants.sh</c> refuse the spellings; this refuses the
    /// shape, including the one where a request is accepted and politely
    /// ignored, which is the version that survives review and then gets used.
    /// </remarks>
    [Fact]
    public void NothingHereCanBeHandedARequest()
    {
        var parameters = typeof(InvitationLink)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters())
            .ToList();

        Assert.NotEmpty(parameters);
        Assert.All(parameters, parameter => Assert.Equal(typeof(string), parameter.ParameterType));
    }

    /// <summary>
    /// A base address written with a trailing slash and one written without it
    /// produce the same link, so an operator cannot make a broken one by
    /// copying an address out of a browser.
    /// </summary>
    [Theory]
    [InlineData("https://media.example.org")]
    [InlineData("https://media.example.org/")]
    [InlineData("  https://media.example.org/  ")]
    [InlineData("https://media.example.org:443")]
    public void TheSameServerWrittenFourWaysGivesOneLink(string configured)
    {
        Assert.Equal("https://media.example.org/redeem/" + Code, InvitationLink.For(configured, Code));
    }

    /// <summary>
    /// A path prefix survives, because that is the reverse proxy serving this
    /// server under a subdirectory, and dropping it produces a link that
    /// reaches the proxy and not the server.
    /// </summary>
    [Fact]
    public void APathPrefixSurvives()
    {
        Assert.Equal(
            "https://example.org/jellyfin/redeem/" + Code,
            InvitationLink.For("https://example.org/jellyfin/", Code));
    }

    /// <summary>
    /// The code is escaped for a URL rather than canonicalised. Turning one
    /// spelling of a code into another happens in one place and it is not this
    /// one.
    /// </summary>
    [Fact]
    public void TheCodeIsEscapedAndNotRewritten()
    {
        var link = InvitationLink.For(Configured, "a b/c");

        Assert.Equal("https://media.example.org/redeem/a%20b%2Fc", link);
    }

    /// <summary>
    /// An unconfigured address refuses rather than guessing, and the refusal
    /// names the setting so the operator reading it knows what to do.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AnUnconfiguredAddressRefusesAndNamesTheSetting(string? configured)
    {
        var refused = Assert.Throws<ArgumentException>(() => InvitationLink.For(configured!, Code));

        Assert.Contains(nameof(PluginConfiguration.PublicBaseUrl), refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// So does an address that cannot carry a link. Each of these produces
    /// something that looks like a link and does not reach the redemption
    /// route, which is a failure the person holding it discovers rather than
    /// the operator who made it.
    /// </summary>
    [Theory]
    [InlineData("media.example.org")]
    [InlineData("/redeem")]
    [InlineData("ftp://media.example.org")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://media.example.org/?next=1")]
    [InlineData("https://media.example.org/#top")]
    public void AnAddressThatCannotCarryALinkIsRefused(string configured)
    {
        Assert.Throws<ArgumentException>(() => InvitationLink.For(configured, Code));
    }

    /// <summary>
    /// A link with no code in it is a link to the redemption route and nothing
    /// else, which is worse than no link because it looks like one.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ALinkWithNoCodeIsRefused(string? code)
    {
        Assert.Throws<ArgumentException>(() => InvitationLink.For(Configured, code!));
    }

    /// <summary>
    /// A fresh install has no address, so it builds no links until an operator
    /// sets one. The alternative is a default that works on somebody's network
    /// and not on the operator's.
    /// </summary>
    [Fact]
    public void AFreshInstallBuildsNoLink()
    {
        var configuration = new PluginConfiguration();

        Assert.Throws<ArgumentException>(() => InvitationLink.For(configuration.PublicBaseUrl, Code));
    }
}
