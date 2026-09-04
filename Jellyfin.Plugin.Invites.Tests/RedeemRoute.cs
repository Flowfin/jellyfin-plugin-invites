using System;
using System.Collections.Generic;
using System.Net;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Jellyfin.Plugin.Invites.Setup;
using Jellyfin.Plugin.Invites.Time;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The redemption controller as an ordinary object, over a response the test
/// owns.
/// </summary>
/// <remarks>
/// <para>
/// One factory rather than one per file, because three test classes now drive
/// this route and a controller built three ways is three different subjects
/// wearing one name. The store is the real one against a directory the caller
/// owns, for the reason <c>InvitesControllerTests</c> gives: a fake store here
/// would prove that the fake round-trips.
/// </para>
/// <para>
/// No web host anywhere in it, which is the headless rule rather than a
/// shortcut. What that bounds is that nothing here says what a server's own
/// pipeline does with a plugin route, and nothing here has run against one.
/// </para>
/// </remarks>
internal static class RedeemRoute
{
    /// <summary>
    /// The address a request the suite makes appears to come from. The limiter
    /// refuses to count an attempt naming no address, and a
    /// <see cref="DefaultHttpContext"/> names none until something sets one, so
    /// a test that forgot this would drive the refusal it did not mean to.
    /// </summary>
    public static IPAddress From { get; } = IPAddress.Loopback;

    /// <summary>
    /// The anti-forgery token a request the suite makes carries, in the cookie
    /// and on the form alike, so that a test about anything else drives the
    /// route as a browser that loaded the page does.
    /// </summary>
    /// <remarks>
    /// A fixed value rather than a minted one, because a test that fails should
    /// fail the same way twice. It is shaped like a real token, which is what
    /// makes it usable: the route refuses anything that is not
    /// <see cref="FormToken.Length"/> hexadecimal characters, and a stand-in
    /// that walked through that refusal would leave every test here driving the
    /// forgery path instead of the one it is about.
    /// </remarks>
    public static string Presented { get; } =
        "0f1e2d3c4b5a69788796a5b4c3d2e1f00123456789abcdeffedcba9876543210";

    /// <summary>
    /// A context carrying a source address, so an attempt from it can be
    /// counted.
    /// </summary>
    /// <returns>The context.</returns>
    /// <remarks>
    /// It carries the anti-forgery cookie the served page would have set, for
    /// the reason it carries an address: a context without one drives the
    /// refusal a test did not mean to reach, and every test here that is not
    /// about forgery would then be asserting the same one thing.
    /// <c>AntiForgeryTests</c> is where a context deliberately missing it is
    /// built.
    /// </remarks>
    public static DefaultHttpContext Request()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = From;
        context.Request.Headers.Cookie = FormToken.CookieName + "=" + Presented;

        return context;
    }

    /// <summary>
    /// A context whose anti-forgery half is in order and whose request names no
    /// source address, for the one test about a caller the server cannot place.
    /// </summary>
    /// <returns>The context.</returns>
    public static DefaultHttpContext WithoutAnAddress()
    {
        var context = Request();
        context.Connection.RemoteIpAddress = null;

        return context;
    }

    /// <summary>
    /// The token a response set, read back off the header the route wrote it on.
    /// </summary>
    /// <param name="headers">The response headers.</param>
    /// <returns>The value, or the empty string where no such cookie was set.</returns>
    /// <remarks>
    /// Read out of <c>Set-Cookie</c> rather than out of anything the test kept,
    /// because what the browser will send back is what that header said and not
    /// what the routine intended to say.
    /// </remarks>
    public static string TokenSetOn(IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        var prefix = FormToken.CookieName + "=";
        foreach (var written in headers.SetCookie)
        {
            if (written is null || !written.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var value = written[prefix.Length..];
            var ends = value.IndexOf(';', StringComparison.Ordinal);

            return ends < 0 ? value : value[..ends];
        }

        return string.Empty;
    }

    /// <summary>
    /// The controller, over a store directory and a clock the caller owns.
    /// </summary>
    /// <param name="store">Where the store sits, or null for a server that gave the plugin none.</param>
    /// <param name="clock">
    /// The clock every instant in the plugin is read from. It is the seam rather
    /// than <see cref="TestClock"/>, because a test about how many times the
    /// clock is read hands in a clock that moves.
    /// </param>
    /// <param name="accounts">The write seam the creation routine is handed.</param>
    /// <param name="context">The request and response the test reads.</param>
    /// <returns>The controller.</returns>
    public static RedeemController Over(
        string? store,
        IClock clock,
        IServerAccountWrites accounts,
        HttpContext context) =>
        new(
            new InvitationOperations(
                new StubStoreDirectory(store),
                clock,
                new StubPublicAddress("https://media.example.org"),
                TestTemplates.AsConfigured),
            new AttemptLimiter(clock),
            new CreationCeiling(clock),
            accounts)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };

    /// <summary>
    /// The controller with a limiter the caller holds, for a test that drives
    /// the same limiter over its threshold.
    /// </summary>
    /// <param name="store">Where the store sits.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="limiter">The limiter, shared with whatever else the test does.</param>
    /// <param name="accounts">The write seam.</param>
    /// <param name="context">The request and response.</param>
    /// <returns>The controller.</returns>
    public static RedeemController Over(
        string? store,
        IClock clock,
        AttemptLimiter limiter,
        IServerAccountWrites accounts,
        HttpContext context) =>
        new(
            new InvitationOperations(
                new StubStoreDirectory(store),
                clock,
                new StubPublicAddress("https://media.example.org"),
                TestTemplates.AsConfigured),
            limiter,
            new CreationCeiling(clock),
            accounts)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };

    /// <summary>
    /// The controller with a creation ceiling the caller holds, for a test that
    /// drives the same ceiling over its threshold.
    /// </summary>
    /// <param name="store">Where the store sits.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="ceiling">The ceiling, shared with whatever else the test does.</param>
    /// <param name="accounts">The write seam.</param>
    /// <param name="context">The request and response.</param>
    /// <returns>The controller.</returns>
    public static RedeemController Over(
        string? store,
        IClock clock,
        CreationCeiling ceiling,
        IServerAccountWrites accounts,
        HttpContext context) =>
        new(
            new InvitationOperations(
                new StubStoreDirectory(store),
                clock,
                new StubPublicAddress("https://media.example.org"),
                TestTemplates.AsConfigured),
            new AttemptLimiter(clock),
            ceiling,
            accounts)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };

    /// <summary>
    /// A context whose request carries a posted body, so the keys of that body
    /// are there to be read.
    /// </summary>
    /// <remarks>
    /// The other factory here hands the action a bound object and leaves the
    /// request carrying nothing, which is enough for every assertion about what
    /// the action does with its answers. It is not enough for the one about
    /// which FIELDS arrived: a body is what carries a field the form does not
    /// define, and a context with no body carries none of them however the
    /// bound object is filled in. So a test about the widening builds its
    /// request here and binds the object from the same dictionary.
    /// </remarks>
    /// <param name="fields">The body, one entry per posted field.</param>
    /// <returns>The context.</returns>
    public static DefaultHttpContext Posting(IDictionary<string, StringValues> fields)
    {
        var context = Request();
        context.Request.Method = "POST";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>(fields));

        return context;
    }

    /// <summary>
    /// The three fields the form defines, filled in, as a posted body.
    /// </summary>
    /// <param name="username">The name to ask for.</param>
    /// <param name="password">The password to ask for, and its confirmation.</param>
    /// <returns>The body, which a caller adds to before posting it.</returns>
    public static Dictionary<string, StringValues> Body(string username, string password) =>
        new(StringComparer.Ordinal)
        {
            ["username"] = username,
            ["password"] = password,
            ["confirmation"] = password,
            [FormToken.Field] = Presented,
        };

    /// <summary>
    /// A submission carrying the three fields the form defines.
    /// </summary>
    /// <param name="username">The name to ask for.</param>
    /// <param name="password">The password to ask for.</param>
    /// <returns>The submission, with the confirmation matching the password.</returns>
    public static SetupSubmission Filled(string username, string password) =>
        new()
        {
            Username = username,
            Password = password,
            Confirmation = password,
            Token = Presented,
        };

    /// <summary>
    /// The operations over a store directory, for a test that has to arrange
    /// one through them rather than through the route.
    /// </summary>
    /// <param name="store">The store directory.</param>
    /// <param name="clock">The clock.</param>
    /// <returns>The operations.</returns>
    public static InvitationOperations Operations(string? store, IClock clock) =>
        new(
            new StubStoreDirectory(store),
            clock,
            new StubPublicAddress("https://media.example.org"),
            TestTemplates.AsConfigured);

    /// <summary>
    /// Mints one invitation against a store and hands back the code.
    /// </summary>
    /// <param name="store">The store directory.</param>
    /// <param name="clock">The clock the mint reads.</param>
    /// <param name="uses">How many accounts the invitation is good for.</param>
    /// <returns>The code and the record.</returns>
    public static Minting Mint(string store, IClock clock, int uses) =>
        Operations(store, clock)
            .Mint(Guid.Parse("11111111-1111-4111-8111-111111111111"), "Household", null, uses);
}
