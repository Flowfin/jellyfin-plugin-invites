using System;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Invitations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A code is handed over once, in the response to the mint, and no later
/// response carries it or anything else shaped like one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Over the serialised response rather than over the type.</b>
/// <c>InvitationView</c> says on itself that having no code field and no hash
/// field is the mechanism, and a reading of its members is a reading of what
/// somebody declared. This serialises what the three reading routes hand back,
/// which is what a browser receives, so a member that arrives later through a
/// converter or a base type is in scope without anybody remembering to add it.
/// </para>
/// <para>
/// <b>It refuses a hash as well as a code, and not by naming one.</b> Hexadecimal
/// is drawn from the code alphabet, so a keyed hash rendered into a row trips the
/// same run that a code would. That is the failure #89 names as the one a view
/// arrives at reasonably: the hash is the lookup key, and keying a row on it puts
/// it in the page source and in every screenshot of it.
/// </para>
/// </remarks>
public class MintedCodeIsNotHandedBackTests
{
    /// <summary>
    /// The public address a link is written against, as an operator would set
    /// it. Nothing here derives it from a request, which is #50.
    /// </summary>
    private const string Configured = "https://media.example.org";

    private static readonly DateTimeOffset _now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid _operator = Guid.Parse("11112222-3333-4444-5555-666677778888");

    /// <summary>
    /// The mint hands the code over, and then the three routes that read an
    /// invitation back hand over nothing shaped like one.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task NoReadingRouteHandsBackAnythingShapedLikeACode()
    {
        using var directory = new OwnedDirectory();
        var controller = ControllerOver(directory);

        var minted = Assert.IsType<MintedInvitation>(
            Assert.IsType<OkObjectResult>(
                (await controller.Mint(new MintRequest { Template = "Household" })).Result).Value);

        // The mint response is the one that may carry it, and it does. Without
        // this the assertions below would pass over a route that had stopped
        // minting anything at all.
        Assert.Equal(InvitationCode.Length, InvitationCode.Canonicalise(minted.Code)!.Length);

        // And it may carry the link, which is the same credential with a host
        // in front of it. #50 put it here and only here, so this leg is the one
        // that tells the two responses apart: without it, a change that stopped
        // minting a link would leave every assertion below green.
        Assert.NotNull(minted.Link);
        Assert.Contains(minted.Code, minted.Link, StringComparison.Ordinal);

        var identifier = minted.Invitation.Id;

        NothingShapedLikeACodeIn("the listing", minted.Code, minted.Link, Assert.IsType<OkObjectResult>(controller.List().Result).Value);
        NothingShapedLikeACodeIn("one invitation", minted.Code, minted.Link, Assert.IsType<OkObjectResult>(controller.One(identifier).Result).Value);
        NothingShapedLikeACodeIn(
            "the revocation",
            minted.Code,
            minted.Link,
            Assert.IsType<OkObjectResult>((await controller.Revoke(identifier)).Result).Value);
    }

    private static void NothingShapedLikeACodeIn(string route, string code, string link, object? body)
    {
        Assert.NotNull(body);

        var json = JsonSerializer.Serialize(body);
        var longest = CodeShape.LongestRunIn(json);

        Assert.True(
            longest < InvitationCode.Length,
            "The response of " + route + " carries a run of " + longest
            + " characters of the code alphabet, and a code is " + InvitationCode.Length
            + ". A code or a keyed hash rendered as hexadecimal both read that way, and neither belongs in a response an operator's browser keeps.");

        Assert.DoesNotContain(code, json, StringComparison.OrdinalIgnoreCase);

        // The link is refused by name as well as by shape. A response that
        // carried it would already fail the run above, because the code is in
        // it, and naming it here is what makes the sentence a reader takes from
        // this file the one #50 decided: the mint may carry the link and no
        // route that reads an invitation back may.
        Assert.DoesNotContain(link, json, StringComparison.OrdinalIgnoreCase);
    }

    private static InvitesController ControllerOver(OwnedDirectory directory)
        => new(
            new InvitationOperations(new StubStoreDirectory(directory.Path), new TestClock(_now), new StubPublicAddress(Configured)),
            new StubOperatorIdentity(_operator),
            new StubServerAccounts(Array.Empty<Guid>()))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
}
