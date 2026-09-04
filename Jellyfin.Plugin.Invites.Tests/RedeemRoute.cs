using System;
using System.Net;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Jellyfin.Plugin.Invites.Time;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
    /// A context carrying a source address, so an attempt from it can be
    /// counted.
    /// </summary>
    /// <returns>The context.</returns>
    public static DefaultHttpContext Request()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = From;

        return context;
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
            accounts)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };

    /// <summary>
    /// A submission carrying the three fields the form defines.
    /// </summary>
    /// <param name="username">The name to ask for.</param>
    /// <param name="password">The password to ask for.</param>
    /// <returns>The submission, with the confirmation matching the password.</returns>
    public static SetupSubmission Filled(string username, string password) =>
        new() { Username = username, Password = password, Confirmation = password };

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
