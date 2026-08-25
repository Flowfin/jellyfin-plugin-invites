using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Configuration;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The calling operator, as the controller is handed one.
/// </summary>
/// <remarks>
/// The server works the identity out of a request it authenticated. Nothing
/// here authenticates anything, so what this stands in for is the answer rather
/// than the mechanism, which is the whole reason the controller takes the seam
/// instead of reading a claim itself.
/// </remarks>
internal sealed class StubOperatorIdentity : IOperatorIdentity
{
    private readonly Guid _user;

    public StubOperatorIdentity(Guid user)
    {
        _user = user;
    }

    public Task<Guid> OfAsync(HttpContext request) => Task.FromResult(_user);
}

/// <summary>
/// The four administrator routes, called as action methods against a real store
/// in a directory the test owns.
/// </summary>
/// <remarks>
/// <para>
/// <b>No web host, and that is the headless rule rather than a shortcut.</b> A
/// test that hosted the application would open a network connection, which the
/// suite may not do. What is left is instantiating the controller as an ordinary
/// object and reading the result it returns, and that is available only because
/// the controller takes its dependencies through its constructor and every
/// action returns a result rather than writing a response.
/// </para>
/// <para>
/// <b>The store is the real one.</b> A fake store here would prove that the fake
/// round-trips. What these assert is what a caller gets back and what is on disk
/// afterwards, so the thing on disk has to be the thing the plugin writes.
/// </para>
/// </remarks>
public class InvitesControllerTests
{
    /// <summary>
    /// The public address a link is written against, as an operator would set
    /// it. Nothing here derives it from a request, which is #50.
    /// </summary>
    private const string Configured = "https://media.example.org";

    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _operator = new("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Minting answers with the code, and the code is what redeems the record
    /// that was written.
    /// </summary>
    /// <remarks>
    /// The two halves are one assertion rather than two. A route that returned a
    /// code unrelated to the stored hash would pass a test that only read the
    /// response, and the person holding that code would find out at redemption.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task MintingReturnsACodeThatMatchesTheStoredHash()
    {
        using var directory = new OwnedDirectory();
        var controller = ControllerOver(directory, _minted);

        var minted = Assert.IsType<MintedInvitation>(
            Assert.IsType<OkObjectResult>(
                (await controller.Mint(new MintRequest { Template = "guest", ValidityDays = 3, Uses = 2 })).Result).Value);

        Assert.False(string.IsNullOrWhiteSpace(minted.Code));
        Assert.Equal(2, minted.Invitation.UsesGranted);
        Assert.Equal(2, minted.Invitation.UsesRemaining);
        Assert.Equal(_minted.AddDays(3), minted.Invitation.ExpiresAt);
        Assert.Equal(_operator, minted.Invitation.MintedBy);
        Assert.Equal("guest", minted.Invitation.Template);

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        var hash = new InvitationCodeHash(
            HashSecret.OpenOrCreate(directory.Path, Array.Empty<Invitation>()).Value);

        Assert.Equal(
            Convert.ToHexString(hash.Of(InvitationCode.Canonicalise(minted.Code)!).AsSpan()),
            Convert.ToHexString(stored.CodeHash.AsSpan()));
        Assert.Equal(minted.Invitation.Id, stored.Id);
    }

    /// <summary>
    /// A minting with no validity named lasts the default, and one with no use
    /// count is good for one account.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AMintingThatNamesNeitherTakesTheDefaults()
    {
        using var directory = new OwnedDirectory();
        var controller = ControllerOver(directory, _minted);

        var minted = Minted(await controller.Mint(new MintRequest { Template = "guest" }));

        Assert.Equal(_minted + InvitationOperations.DefaultValidity, minted.Invitation.ExpiresAt);
        Assert.Equal(1, minted.Invitation.UsesGranted);
    }

    /// <summary>
    /// The two ceilings are refused at minting rather than at redemption, which
    /// is where docs/expiry-rules.md and #33 both put them. An operator asking
    /// for more is told now instead of finding out later.
    /// </summary>
    /// <param name="days">The validity asked for, in days.</param>
    /// <param name="uses">The use count asked for.</param>
    /// <returns>Nothing a caller reads.</returns>
    [Theory]
    [InlineData(91, 1)]
    [InlineData(0, 1)]
    [InlineData(7, 0)]
    [InlineData(7, 11)]
    public async Task AMintingOutsideACeilingIsRefusedAndWritesNothing(int days, int uses)
    {
        using var directory = new OwnedDirectory();
        var controller = ControllerOver(directory, _minted);

        var refused = await controller.Mint(new MintRequest { Template = "guest", ValidityDays = days, Uses = uses });

        Assert.IsType<BadRequestObjectResult>(refused.Result);
        Assert.Empty(new InvitationStore(directory.Path).Read().Invitations);
    }

    /// <summary>
    /// The ceiling is exclusive at the far end: exactly the maximum is minted
    /// and one day past it is refused.
    /// </summary>
    /// <remarks>
    /// The two cases are one test because the comparison is one character. A
    /// suite that only asserts the refusal passes for a routine that refuses the
    /// maximum too, and an operator who asked for the number the documentation
    /// gives them would meet a refusal quoting that same number back.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheValidityCeilingIsMetAndNotCrossed()
    {
        using var directory = new OwnedDirectory();
        var controller = ControllerOver(directory, _minted);

        var atTheCeiling = Minted(
            await controller.Mint(new MintRequest { Template = "guest", ValidityDays = InvitationOperations.MaximumValidityDays }));

        Assert.Equal(_minted.AddDays(InvitationOperations.MaximumValidityDays), atTheCeiling.Invitation.ExpiresAt);

        Assert.IsType<BadRequestObjectResult>(
            (await controller.Mint(new MintRequest { Template = "guest", ValidityDays = InvitationOperations.MaximumValidityDays + 1 })).Result);
    }

    /// <summary>
    /// An invitation with no template names no grant, so it is refused before
    /// anything is written.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AMintingWithNoTemplateIsRefused()
    {
        using var directory = new OwnedDirectory();
        var controller = ControllerOver(directory, _minted);

        Assert.IsType<BadRequestObjectResult>(
            (await controller.Mint(new MintRequest { Template = "  " })).Result);
        Assert.Empty(new InvitationStore(directory.Path).Read().Invitations);
    }

    /// <summary>
    /// Listing returns what was minted, and the shape it returns has no field a
    /// code or a hash could travel in.
    /// </summary>
    /// <remarks>
    /// The second half is asserted over the type rather than over one response.
    /// A test reading one body proves that body carried no code; reading the
    /// type proves no body ever can, which is the property docs/api.md states.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ListingReturnsTheRecordsAndCannotCarryACodeOrAHash()
    {
        using var directory = new OwnedDirectory();
        var controller = ControllerOver(directory, _minted);

        var first = Minted(await controller.Mint(new MintRequest { Template = "guest" }));
        var second = Minted(await controller.Mint(new MintRequest { Template = "household", Uses = 3 }));

        var listed = Assert.IsAssignableFrom<IReadOnlyList<InvitationView>>(
            Assert.IsType<OkObjectResult>(controller.List().Result).Value);

        Assert.Equal(
            new[] { first.Invitation.Id, second.Invitation.Id },
            listed.Select(view => view.Id));

        // Upper-cased first and compared with the default comparer, because the
        // invariant lint refuses a StringComparer on a line naming one of these
        // fields. The rule matches a spelling and cannot tell an assertion that
        // a field is absent from a comparison of a stored secret, and the right
        // answer to a rule that cannot tell is to write the line another way
        // rather than to exempt it.
        var fields = typeof(InvitationView).GetProperties()
            .Select(property => property.Name.ToUpperInvariant())
            .ToList();

        Assert.DoesNotContain("CODE", fields);
        Assert.DoesNotContain("CODEHASH", fields);
        Assert.DoesNotContain("HASH", fields);
    }

    /// <summary>
    /// One invitation comes back by its non-secret identifier, and an identifier
    /// the store does not hold is not found.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task OneIsReturnedByItsIdentifierAndAnUnknownOneIsNotFound()
    {
        using var directory = new OwnedDirectory();
        var controller = ControllerOver(directory, _minted);

        var minted = Minted(await controller.Mint(new MintRequest { Template = "guest" }));

        var found = Assert.IsType<InvitationView>(
            Assert.IsType<OkObjectResult>(controller.One(minted.Invitation.Id).Result).Value);

        Assert.Equal(minted.Invitation.Id, found.Id);
        Assert.IsType<NotFoundResult>(controller.One(Guid.NewGuid()).Result);
    }

    /// <summary>
    /// Revoking records the operator and the instant, and revoking a second time
    /// is not an error and does not move either.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task RevokingIsRecordedAndRevokingAgainChangesNothing()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var controller = ControllerOver(directory, clock);

        var minted = Minted(await controller.Mint(new MintRequest { Template = "guest" }));

        clock.Advance(TimeSpan.FromHours(1));
        var revoked = View(await controller.Revoke(minted.Invitation.Id));

        Assert.True(revoked.IsRevoked);
        Assert.Equal(_minted.AddHours(1), revoked.RevokedAt);
        Assert.Equal(_operator, revoked.RevokedBy);

        clock.Advance(TimeSpan.FromHours(1));
        var again = View(await controller.Revoke(minted.Invitation.Id));

        Assert.Equal(_minted.AddHours(1), again.RevokedAt);
        Assert.Equal(_operator, again.RevokedBy);

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(_minted.AddHours(1), stored.RevokedAt);
    }

    /// <summary>
    /// Revoking spends no use and drops no account, which is the half of
    /// revocation that is easiest to lose by being helpful.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task RevokingSpendsNoUse()
    {
        using var directory = new OwnedDirectory();
        var controller = ControllerOver(directory, _minted);

        var minted = Minted(await controller.Mint(new MintRequest { Template = "guest", Uses = 4 }));
        var revoked = View(await controller.Revoke(minted.Invitation.Id));

        Assert.Equal(4, revoked.UsesGranted);
        Assert.Equal(4, revoked.UsesRemaining);
        Assert.Empty(revoked.AccountsProduced);
    }

    /// <summary>
    /// Revoking an identifier the store does not hold is not found rather than a
    /// silent success.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task RevokingAnUnknownIdentifierIsNotFound()
    {
        using var directory = new OwnedDirectory();
        var controller = ControllerOver(directory, _minted);

        Assert.IsType<NotFoundResult>((await controller.Revoke(Guid.NewGuid())).Result);
    }

    /// <summary>
    /// A server that has told this plugin no data directory gets an answer
    /// saying so from every route, rather than an exception out of a route that
    /// tried anyway.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task WithNoDataDirectoryEveryRouteSaysSoRatherThanFailing()
    {
        var controller = new InvitesController(
            new InvitationOperations(new StubStoreDirectory(null), new TestClock(_minted), new StubPublicAddress(Configured)),
            new StubOperatorIdentity(_operator),
            new StubServerAccounts(Array.Empty<Guid>()))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            Assert.IsType<ObjectResult>((await controller.Mint(new MintRequest { Template = "guest" })).Result).StatusCode);
        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            Assert.IsType<ObjectResult>(controller.List().Result).StatusCode);
        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            Assert.IsType<ObjectResult>(controller.One(Guid.NewGuid()).Result).StatusCode);
        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            Assert.IsType<ObjectResult>((await controller.Revoke(Guid.NewGuid())).Result).StatusCode);
        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            Assert.IsType<ObjectResult>(controller.Rotate(new RotateRequest()).Result).StatusCode);
    }

    /// <summary>
    /// Nothing this controller can be handed lets it decide anything about a
    /// record.
    /// </summary>
    /// <remarks>
    /// docs/api.md says no route makes a judgement of its own about expiry, uses
    /// or revocation. A greppable rule cannot see that and a reading of the
    /// bodies goes stale the next time somebody edits one. What holds it is the
    /// constructor: with no clock and no store on the type, the comparison an
    /// action would need to make is one it has no argument for.
    /// </remarks>
    /// <remarks>
    /// The list is three types since #45 and it is still an allow-list rather
    /// than a count. <c>IServerAccounts</c> is a read seam carrying one member
    /// that hands back identifiers; it holds no clock, no store and nothing an
    /// action could compare an expiry or a use count against, so the sentence
    /// above is unchanged by its arrival. What would change it is a fourth type,
    /// and adding one means editing this line and saying why here.
    /// </remarks>
    [Fact]
    public void NothingHereCanBeHandedAClockOrAStore()
    {
        var taken = typeof(InvitesController)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToList();

        Assert.All(
            taken,
            type => Assert.True(
                type == typeof(InvitationOperations)
                    || type == typeof(IOperatorIdentity)
                    || type == typeof(IServerAccounts),
                type.FullName + " can be handed to this controller. It takes the operations, the calling operator and the read seam over the server's accounts, and nothing else, so that no action here can read a clock or a store and form an opinion the model layer already holds."));
    }

    /// <summary>
    /// A mint answers with the link, and a request that says this server was
    /// reached somewhere else does not change what is in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the fourth clause of #50 in the form the clause asks for: a
    /// forged host on a real request that a real action answers with a link.
    /// The leg in <c>InvitationLinkTests</c> that sets a header and then calls
    /// the builder directly says on itself that it could not have failed,
    /// because the builder never sees a request. This one could: the request is
    /// the one the action is answering, and the value asserted is the one it
    /// returned.
    /// </para>
    /// <para>
    /// One spelling of the forgery is written here and it is the only one that
    /// can be. The greppable rules under #50 refuse the request object's own
    /// host member and the forwarded header names as text anywhere in this
    /// tree, taking no exemption for a test, so a leg forging through either
    /// would red <c>Invariant lint</c>. That was met rather than assumed: an
    /// earlier draft of this remark named one of them in prose and the rule
    /// refused the run. What is left is the <c>Host</c> header through the
    /// typed accessor, which is the header a proxy rewrites and the one the
    /// server would answer from. The others are covered by the rules rather
    /// than by this test, and covered more widely: no file here may name those
    /// spellings at all, which is a statement about the source and not about
    /// one call.
    /// </para>
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AForgedHostDoesNotReachTheMintedLink()
    {
        using var directory = new OwnedDirectory();

        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Headers.Host = "attacker.example";

        var controller = ControllerOver(directory, new TestClock(_minted), Configured, context);

        var minted = Minted(await controller.Mint(new MintRequest { Template = "guest" }));

        Assert.NotNull(minted.Link);
        Assert.Null(minted.LinkRefusal);
        Assert.StartsWith(Configured + "/", minted.Link, StringComparison.Ordinal);
        Assert.DoesNotContain("attacker.example", minted.Link, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("/" + minted.Code, minted.Link, StringComparison.Ordinal);
    }

    /// <summary>
    /// With no address configured the mint still mints, and what comes back in
    /// place of the link is the refusal naming the setting.
    /// </summary>
    /// <remarks>
    /// A fresh install is this case, so it is the first thing an operator
    /// meets. The invitation is written and the code is handed over either way,
    /// because the address is only what the link is written against: getting it
    /// wrong affects no record and no account. A link to the wrong host would.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task WithNoConfiguredAddressTheMintCarriesTheRefusalAndStillMints()
    {
        using var directory = new OwnedDirectory();
        var controller = ControllerOver(directory, new TestClock(_minted), null, new DefaultHttpContext());

        var minted = Minted(await controller.Mint(new MintRequest { Template = "guest" }));

        Assert.Null(minted.Link);
        Assert.NotNull(minted.LinkRefusal);
        Assert.Contains(nameof(PluginConfiguration.PublicBaseUrl), minted.LinkRefusal, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(minted.Code));
        Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
    }

    /// <summary>
    /// Asking what a rotation costs reads the store and writes nothing.
    /// </summary>
    /// <remarks>
    /// The key on disk is compared byte for byte before and after, because the
    /// claim is not that the response says nothing happened. It is that nothing
    /// happened.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AskingWhatRotationCostsWritesNothing()
    {
        using var directory = new OwnedDirectory();
        var controller = ControllerOver(directory, _minted);

        await controller.Mint(new MintRequest { Template = "guest" });
        var before = File.ReadAllBytes(HashSecret.PathIn(directory.Path));

        var plan = Rotation(controller.Rotate(new RotateRequest()));

        Assert.False(plan.Rotated);
        Assert.Equal(1, plan.Invalidates);
        Assert.Contains("1 record(s)", plan.Detail, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(HashSecret.PathIn(directory.Path)));
    }

    /// <summary>
    /// Confirming the count rotates the key, leaves every record where it was,
    /// and makes the code that was minted under the old key unverifiable.
    /// </summary>
    /// <remarks>
    /// The last of those three is the whole point and the other two are what
    /// keep it from being achieved by deleting things. A rotation that also
    /// removed the records would pass an assertion about the code and take the
    /// trail of what those invitations produced with it.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ConfirmingTheCountRotatesAndLeavesTheRecords()
    {
        using var directory = new OwnedDirectory();
        var controller = ControllerOver(directory, _minted);

        var minted = Minted(await controller.Mint(new MintRequest { Template = "guest" }));
        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        var before = File.ReadAllBytes(HashSecret.PathIn(directory.Path));

        var done = Rotation(controller.Rotate(new RotateRequest { Invalidates = 1 }));

        Assert.True(done.Rotated);
        Assert.Equal(1, done.Invalidates);

        var after = File.ReadAllBytes(HashSecret.PathIn(directory.Path));
        Assert.NotEqual(before, after);

        var records = new InvitationStore(directory.Path).Read().Invitations;
        Assert.Equal(stored.Id, Assert.Single(records).Id);

        var underTheNewKey = new InvitationCodeHash(
            HashSecret.OpenOrCreate(directory.Path, records).Value);

        Assert.NotEqual(
            Convert.ToHexString(stored.CodeHash.AsSpan()),
            Convert.ToHexString(underTheNewKey.Of(InvitationCode.Canonicalise(minted.Code)!).AsSpan()));
    }

    /// <summary>
    /// A confirmation naming a count the store no longer holds is refused, and
    /// the key it would have replaced is still there.
    /// </summary>
    /// <remarks>
    /// This is the interleaving the route exists to survive: an operator reads
    /// a number, somebody mints while they are reading it, and the cost they
    /// agreed to is no longer the cost that would be paid. A conflict rather
    /// than a bad request, because nothing about the request is malformed.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AConfirmationAgainstAMovedStoreIsRefusedAndWritesNothing()
    {
        using var directory = new OwnedDirectory();
        var controller = ControllerOver(directory, _minted);

        await controller.Mint(new MintRequest { Template = "guest" });
        var plan = Rotation(controller.Rotate(new RotateRequest()));
        await controller.Mint(new MintRequest { Template = "guest" });

        var before = File.ReadAllBytes(HashSecret.PathIn(directory.Path));
        var refused = Assert.IsType<ConflictObjectResult>(
            controller.Rotate(new RotateRequest { Invalidates = plan.Invalidates }).Result);

        Assert.Equal(StatusCodes.Status409Conflict, refused.StatusCode);
        Assert.Contains("Nothing was rotated", (string)refused.Value!, StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(HashSecret.PathIn(directory.Path)));
    }

    private static InvitesController ControllerOver(OwnedDirectory directory, DateTimeOffset now)
        => ControllerOver(directory, new TestClock(now));

    private static InvitesController ControllerOver(OwnedDirectory directory, TestClock clock)
        => ControllerOver(directory, clock, Configured, new DefaultHttpContext());

    private static InvitesController ControllerOver(
        OwnedDirectory directory,
        TestClock clock,
        string? configured,
        HttpContext context)
        => new(
            new InvitationOperations(
                new StubStoreDirectory(directory.Path),
                clock,
                new StubPublicAddress(configured)),
            new StubOperatorIdentity(_operator),
            new StubServerAccounts(Array.Empty<Guid>()))
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };

    private static RotationView Rotation(ActionResult<RotationView> result)
        => Assert.IsType<RotationView>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static MintedInvitation Minted(ActionResult<MintedInvitation> result)
        => Assert.IsType<MintedInvitation>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static InvitationView View(ActionResult<InvitationView> result)
        => Assert.IsType<InvitationView>(Assert.IsType<OkObjectResult>(result.Result).Value);
}
