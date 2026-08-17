using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Codes;
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
            new InvitationOperations(new StubStoreDirectory(null), new TestClock(_minted)),
            new StubOperatorIdentity(_operator))
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
                type == typeof(InvitationOperations) || type == typeof(IOperatorIdentity),
                type.FullName + " can be handed to this controller. It takes the operations and the calling operator and nothing else, so that no action here can read a clock or a store and form an opinion the model layer already holds."));
    }

    private static InvitesController ControllerOver(OwnedDirectory directory, DateTimeOffset now)
        => ControllerOver(directory, new TestClock(now));

    private static InvitesController ControllerOver(OwnedDirectory directory, TestClock clock)
        => new(
            new InvitationOperations(new StubStoreDirectory(directory.Path), clock),
            new StubOperatorIdentity(_operator))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private static MintedInvitation Minted(ActionResult<MintedInvitation> result)
        => Assert.IsType<MintedInvitation>(Assert.IsType<OkObjectResult>(result.Result).Value);

    private static InvitationView View(ActionResult<InvitationView> result)
        => Assert.IsType<InvitationView>(Assert.IsType<OkObjectResult>(result.Result).Value);
}
