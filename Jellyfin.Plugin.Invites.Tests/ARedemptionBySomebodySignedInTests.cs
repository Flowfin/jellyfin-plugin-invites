using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// An invitation presented by somebody who is already signed in changes nothing
/// about the account they are signed in as.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the wide half of #62's ceiling and it is the one with no field to
/// point at.</b> The narrow half is a boolean that must never be set, and there
/// is a rule, an assertion and an operator refusal for it. This half is a
/// property of what the route does NOT do, so nothing about the source names it
/// and every reading of it has to be a comparison.
/// </para>
/// <para>
/// <b>The comparison is between two redemptions that differ only in who is
/// signed in.</b> Two invitations, minted the same way against the same store,
/// redeemed with the same answers, one by a caller the request identifies and
/// one by a caller it does not. The trail the write seam recorded and the record
/// left on disk have to be the same. The reasonable-sounding feature this exists
/// against is reuse: an invitation presented by a signed-in person quietly
/// widening the account they already hold instead of making a new one, which
/// turns the link into a privilege-editing tool for whoever holds it.
/// </para>
/// <para>
/// <b>Why a comparison rather than an assertion about the caller's account.</b>
/// The seam here has one account to give and the route has no way to ask about
/// another, so a test that asserted "the caller's account was not written to"
/// would be asserting something no arrangement in this file could make false.
/// Two runs that have to agree is the reading that a reuse branch actually
/// breaks: it changes what the seam is asked and what the record claims, and it
/// changes them only in the run where somebody is signed in.
/// </para>
/// <para>
/// <b>No web host.</b> The controller is an ordinary object and the identity is
/// a principal the test puts on a context it owns, which is the headless rule
/// rather than a shortcut. What a server's own authentication would put there is
/// not measured here, and the route is declared anonymous, so what is read is
/// that the route ignores an identity rather than that it rejects one.
/// </para>
/// </remarks>
public class ARedemptionBySomebodySignedInTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The account a signed-in caller holds. It is not the one the seam hands
    /// back, so a route that reused it would be visible in the trail.
    /// </summary>
    private static readonly Guid _theirs = Guid.Parse("66666666-6666-4666-8666-666666666666");

    /// <summary>
    /// Two redemptions differing only in whether the request identifies its
    /// caller ask the server for exactly the same thing and leave exactly the
    /// same record.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task BeingSignedInChangesNeitherWhatIsAskedOfTheServerNorWhatIsWritten()
    {
        var anonymous = await Redeem(signedIn: false);
        var signedIn = await Redeem(signedIn: true);

        Assert.Equal(anonymous.Asked, signedIn.Asked);
        Assert.Equal(anonymous.Produced, signedIn.Produced);
        Assert.Equal(anonymous.Status, signedIn.Status);
    }

    /// <summary>
    /// The account the plugin recorded is the one the server made, and never the
    /// one the caller arrived holding.
    /// </summary>
    /// <remarks>
    /// The comparison above reds for any difference between the two runs,
    /// including one nobody meant. This says which difference matters, so a
    /// failure of the pair can be read against a statement of the property
    /// rather than only against its symptom.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheAccountRecordedIsTheOneTheServerMadeAndNotTheCallersOwn()
    {
        var signedIn = await Redeem(signedIn: true);

        Assert.NotEqual(_theirs, signedIn.Produced);
        Assert.DoesNotContain(
            _theirs.ToString(),
            string.Join("|", signedIn.Asked),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// One redemption, against a store of its own.
    /// </summary>
    /// <param name="signedIn">
    /// Whether the request identifies its caller as holding
    /// <see cref="_theirs"/>.
    /// </param>
    /// <returns>What the seam was asked, what the record claims, and the status.</returns>
    private static async Task<(IReadOnlyList<string> Asked, Guid Produced, int Status)> Redeem(bool signedIn)
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();

        var context = RedeemRoute.Request();
        if (signedIn)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, _theirs.ToString())],
                    "the server's own scheme"));
        }

        var answer = await RedeemRoute
            .Over(directory.Path, clock, seam, context)
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);

        return (
            seam.Asked.ToList(),
            stored.AccountsProduced.Single(),
            Assert.IsType<StatusCodeResult>(answer).StatusCode);
    }
}
