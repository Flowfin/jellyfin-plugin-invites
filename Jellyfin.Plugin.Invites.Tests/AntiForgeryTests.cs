using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Setup;
using Jellyfin.Plugin.Invites.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The anti-forgery token: where it comes from, what the page and the cookie do
/// with it, and what a post that does not carry both is answered with.
/// </summary>
/// <remarks>
/// <para>
/// <b>The attack these are about.</b> A page on another site posts the setup
/// form through an invited person's browser, and an account is created with a
/// username and a password a stranger chose, spending an invitation the operator
/// meant for somebody else. docs/threat-model.md carries the row. What defends
/// it is a pair: a value in a cookie the browser sends by itself, and the same
/// value on the form, which a page on another site cannot read in order to
/// forge.
/// </para>
/// <para>
/// <b>Every refusal here is also a claim about the store.</b> The clause this
/// issue turns on is that a post without a valid token consumes no use, so each
/// refusal reads the record back off disk and asserts the count did not move.
/// Asserting the status code alone would pass for a route that refuses the
/// caller after taking the use, which is the failure worth catching: an
/// invitation spent by an attack that got nothing.
/// </para>
/// <para>
/// <b>No web host and no browser.</b> The controller is an ordinary object and
/// the response is a context the test owns, which is the headless rule rather
/// than a shortcut. So nothing here says what a browser does with a
/// <c>SameSite</c> attribute or what a server's own pipeline does with a
/// <c>Set-Cookie</c> on a plugin route: what is read is the header this plugin
/// wrote. That bound is the same one <c>SetupPageTests</c> records.
/// </para>
/// </remarks>
public class AntiForgeryTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A token is thirty-two bytes of a cryptographic source, written out as
    /// hexadecimal, and no two are the same.
    /// </summary>
    /// <remarks>
    /// The repetition leg is a weak reading of a strong property and is worth
    /// having anyway: it cannot say a token is unpredictable, and it does say
    /// that a routine handing back a constant, or one seeded per call from
    /// something that does not move, is not what this is.
    /// </remarks>
    [Fact]
    public void AMintedTokenIsSixtyFourHexadecimalCharactersAndNoTwoAreAlike()
    {
        var minted = Enumerable.Range(0, 64).Select(_ => FormToken.Fresh()).ToList();

        Assert.All(minted, one => Assert.True(FormToken.IsWellFormed(one)));
        Assert.All(minted, one => Assert.Equal(FormToken.Length, one.Length));
        Assert.Equal(minted.Count, minted.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// What a token may look like. The alphabet is the whole of the argument
    /// that the value is safe to write into the page, so a value carrying
    /// anything else is refused rather than escaped.
    /// </summary>
    /// <param name="presented">The value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0f1e2d3c4b5a69788796a5b4c3d2e1f00123456789abcdeffedcba987654321")]
    [InlineData("0f1e2d3c4b5a69788796a5b4c3d2e1f00123456789abcdeffedcba98765432100")]
    [InlineData("0F1E2D3C4B5A69788796A5B4C3D2E1F00123456789ABCDEFFEDCBA9876543210")]
    [InlineData("0f1e2d3c4b5a69788796a5b4c3d2e1f00123456789abcdeffedcba987654321<")]
    [InlineData("\"><script>alert(1)</script>abcdeffedcba98765432100f1e2d3c4b5a6978")]
    public void AValueThisPluginDidNotMintIsNotWellFormed(string? presented)
    {
        Assert.False(FormToken.IsWellFormed(presented));
    }

    /// <summary>
    /// The page refuses to be handed anything but a token. That is what the
    /// substitution rests on: nothing reaching the markup carries a character
    /// HTML gives a meaning to, so there is no escape for anybody to get wrong.
    /// </summary>
    [Fact]
    public void ThePageRefusesToBeHandedAValueThisPluginDidNotMint()
    {
        Assert.Throws<ArgumentException>(
            () => SetupPage.For("\"><script>alert(1)</script>abcdeffedcba98765432100f1e2d3c4b5a6978"));
    }

    /// <summary>
    /// Filling the token in does not move the policy the page is served under.
    /// </summary>
    /// <remarks>
    /// The policy names the page's one style element by hash. A substitution
    /// inside that element would change the hash, the served page would carry a
    /// policy describing a different page, and a browser would refuse the page
    /// its own style. This says the value goes somewhere else.
    /// </remarks>
    [Fact]
    public void TheTokenIsWrittenOutsideTheElementThePolicyHashes()
    {
        var served = SetupPage.For(FormToken.Fresh());

        Assert.Equal(SetupPage.ContentSecurityPolicy, SetupPage.PolicyFor(served));
    }

    /// <summary>
    /// The page a browser is served carries the token the cookie on the same
    /// response carries, on the control the post binds.
    /// </summary>
    [Fact]
    public void TheServedPageCarriesTheTokenTheCookieCarries()
    {
        var context = RedeemRoute.Request();
        var served = Controller(context).Page();

        var written = RedeemRoute.TokenSetOn(context.Response.Headers);

        Assert.True(FormToken.IsWellFormed(written));
        Assert.Contains(
            "name=\"" + FormToken.Field + "\" type=\"hidden\" value=\"" + written + "\"",
            served.Content!,
            StringComparison.Ordinal);
        Assert.DoesNotContain(SetupPage.Placeholder, served.Content!, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every page view gets its own token. A value that did not move would be
    /// one an attacker reads once, by fetching the page themselves, and then
    /// carries in every forged form afterwards.
    /// </summary>
    [Fact]
    public void EveryPageViewGetsItsOwnToken()
    {
        var first = RedeemRoute.Request();
        var second = RedeemRoute.Request();

        Controller(first).Page();
        Controller(second).Page();

        Assert.NotEqual(
            RedeemRoute.TokenSetOn(first.Response.Headers),
            RedeemRoute.TokenSetOn(second.Response.Headers));
    }

    /// <summary>
    /// The cookie is out of reach of script, scoped to the redemption route, and
    /// declared not to travel with a cross-site request.
    /// </summary>
    /// <remarks>
    /// Read off the header this plugin wrote, which is what a browser would be
    /// told. Whether a browser honours any of the three is a fact about that
    /// browser and is not measured anywhere in this repository.
    /// </remarks>
    [Fact]
    public void TheCookieIsHiddenFromScriptScopedToTheRouteAndStaysOnThisSite()
    {
        var context = RedeemRoute.Request();
        Controller(context).Page();

        var written = Assert.Single(context.Response.Headers.SetCookie)!;

        Assert.Contains("httponly", written, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", written, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/redeem", written, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", written, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The secure attribute follows the connection the page was asked for over.
    /// </summary>
    /// <remarks>
    /// Marking it secure unconditionally is the version that reads better and
    /// breaks the flow: a browser does not send a secure cookie back over a
    /// plain connection, so a server reached over HTTP would mint a token,
    /// never see the cookie again and refuse every post. What that costs on such
    /// a server is written at <c>FormToken.OptionsFor</c> and is not softened
    /// here.
    /// </remarks>
    /// <param name="secure">Whether the request arrived over a secure connection.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheCookieIsMarkedSecureExactlyWhenTheRequestWas(bool secure)
    {
        var context = RedeemRoute.Request();
        context.Request.Scheme = secure ? "https" : "http";

        Controller(context).Page();

        var written = Assert.Single(context.Response.Headers.SetCookie)!;

        Assert.Equal(secure, written.Contains("secure", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A browser that loaded the page can finish: the token it was served, sent
    /// back with the cookie it was given, is honoured and the account is made.
    /// </summary>
    /// <remarks>
    /// Without this every other leg in this file would pass for a route that
    /// refuses everything, which is the shape a guard fails into rather than out
    /// of. Nothing is copied between the two requests except what a browser
    /// would copy: the cookie off the response header, and the token off the
    /// served page.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ThePageAndThePostAgreeForABrowserThatLoadedThePage()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();

        var serving = RedeemRoute.Request();
        var page = Controller(serving, directory.Path, clock, seam).Page();
        var carried = RedeemRoute.TokenSetOn(serving.Response.Headers);

        Assert.Contains(carried, page.Content!, StringComparison.Ordinal);

        var answer = await Posting(directory.Path, clock, seam, carried, carried)
            .Submit(minted.Code, Submission(carried));

        Assert.Equal(StatusCodes.Status303SeeOther, Assert.IsType<StatusCodeResult>(answer).StatusCode);
        Assert.Equal(0, Remaining(directory.Path));
        Assert.NotEmpty(seam.Asked);
    }

    /// <summary>
    /// The four ways a post can fail to carry the pair, each refused, each
    /// leaving the invitation exactly as it was.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first is the forgery itself: a page on another site knows the address
    /// and the code, because they are in a link, and cannot read the cookie, so
    /// what it can send is a form with no token or with a guessed one.
    /// </para>
    /// <para>
    /// The last two are what a comparison that stopped early would let through.
    /// A prefix of the real value is the case a comparison returning at the
    /// first differing character leaks one character at a time, and the same
    /// value in upper case is the case a comparison folding case would accept.
    /// Neither is a hypothetical shape: both are what the three rules in
    /// <c>.github/lint/invariants.sh</c> named after this class of mistake exist
    /// against.
    /// </para>
    /// </remarks>
    /// <param name="cookie">What the request's cookie carried, or null for none.</param>
    /// <param name="presented">What the form carried, or null for none.</param>
    /// <returns>Nothing a caller reads.</returns>
    [Theory]
    [InlineData(RedeemRouteToken, null)]
    [InlineData(null, RedeemRouteToken)]
    [InlineData(RedeemRouteToken, "9876543210fedcbaffedcba9876543210abcdef98765432100f1e2d3c4b5a6978")]
    [InlineData(RedeemRouteToken, "0f1e2d3c4b5a69788796a5b4c3d2e1f00123456789abcdeffedcba987654321")]
    [InlineData(RedeemRouteToken, "0F1E2D3C4B5A69788796A5B4C3D2E1F00123456789ABCDEFFEDCBA9876543210")]
    public async Task APostThatDoesNotCarryThePairTakesNoUseAndCreatesNothing(
        string? cookie,
        string? presented)
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();

        var answer = await Posting(directory.Path, clock, seam, cookie, presented)
            .Submit(minted.Code, Submission(presented));

        Assert.IsType<BadRequestResult>(answer);
        Assert.Empty(seam.Asked);
        Assert.Equal(1, Remaining(directory.Path));
    }

    /// <summary>
    /// A forged post is refused before the limiter counts anything, so an
    /// attacker cannot spend an invited person's allowance by making their
    /// browser post rubbish.
    /// </summary>
    /// <remarks>
    /// One limiter across both requests, because a limiter built per request
    /// counts to one and would report this as held whatever the route did. The
    /// second post is a good one and has to be judged, which is what says the
    /// allowance is still there.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AForgedPostIsRefusedBeforeAnAttemptIsCounted()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();
        var limiter = new Redemption.AttemptLimiter(clock);

        for (var attempt = 0; attempt < Redemption.AttemptLimiter.PerAddressCeiling * 3; attempt++)
        {
            var forged = await RedeemRoute
                .Over(directory.Path, clock, limiter, seam, WithNoCookie())
                .Submit(minted.Code, Submission(RedeemRoute.Presented));

            Assert.IsType<BadRequestResult>(forged);
        }

        var honoured = await RedeemRoute
            .Over(directory.Path, clock, limiter, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status303SeeOther, Assert.IsType<StatusCodeResult>(honoured).StatusCode);
    }

    /// <summary>
    /// The answer to a forged post carries the five headers every response from
    /// this route owes.
    /// </summary>
    /// <remarks>
    /// It is the one answer this route gives that <c>RedemptionRouteHeadersTests</c>
    /// cannot reach, because that leg drives each action once and this branch is
    /// taken before the one it reaches. A response leaving without them would be
    /// a page a browser may frame and store, on the address that carries the
    /// code.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheAnswerToAForgedPostCarriesTheHeadersTheRouteOwes()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var context = WithNoCookie();

        var answer = await RedeemRoute
            .Over(directory.Path, clock, new ARecordingWriteSeam(), context)
            .Submit(minted.Code, Submission(RedeemRoute.Presented));

        Assert.IsType<BadRequestResult>(answer);
        Assert.All(
            new[]
            {
                "Content-Security-Policy",
                "X-Frame-Options",
                "X-Content-Type-Options",
                "Cache-Control",
                "Referrer-Policy",
            },
            header => Assert.False(
                string.IsNullOrEmpty(context.Response.Headers[header].ToString()),
                "The answer to a forged post left without " + header + "."));
    }

    /// <summary>
    /// The token the suite's own requests carry, repeated here because an
    /// attribute argument has to be a constant and
    /// <see cref="RedeemRoute.Presented"/> is a property.
    /// </summary>
    /// <remarks>
    /// The two are held equal by <see cref="TheConstantHereIsTheOneTheHarnessUses"/>
    /// rather than by care, because a second spelling that drifted would turn
    /// every row above into a mismatch case and every one of them would still
    /// pass.
    /// </remarks>
    private const string RedeemRouteToken =
        "0f1e2d3c4b5a69788796a5b4c3d2e1f00123456789abcdeffedcba9876543210";

    /// <summary>
    /// The constant above is the value the harness hands out.
    /// </summary>
    [Fact]
    public void TheConstantHereIsTheOneTheHarnessUses()
    {
        Assert.Equal(RedeemRouteToken, RedeemRoute.Presented);
    }

    /// <summary>
    /// A submission carrying whatever token the caller wants it to.
    /// </summary>
    /// <param name="presented">The token, or null for a form that carried none.</param>
    /// <returns>The submission, otherwise filled in acceptably.</returns>
    private static SetupSubmission Submission(string? presented)
    {
        var filled = RedeemRoute.Filled("newcomer", "a password long enough");
        filled.Token = presented;

        return filled;
    }

    /// <summary>
    /// A request context carrying whatever cookie the caller wants it to.
    /// </summary>
    /// <param name="cookie">The cookie value, or null for a request with none.</param>
    /// <returns>The context.</returns>
    private static DefaultHttpContext Carrying(string? cookie)
    {
        var context = RedeemRoute.Request();
        if (cookie is null)
        {
            context.Request.Headers.Remove("Cookie");
        }
        else
        {
            context.Request.Headers.Cookie = FormToken.CookieName + "=" + cookie;
        }

        return context;
    }

    /// <summary>
    /// A request context carrying no cookie at all.
    /// </summary>
    /// <returns>The context.</returns>
    private static DefaultHttpContext WithNoCookie() => Carrying(null);

    /// <summary>
    /// The controller over a context, with nothing behind it.
    /// </summary>
    /// <param name="context">The request and response.</param>
    /// <returns>The controller.</returns>
    private static RedeemController Controller(HttpContext context) =>
        RedeemRoute.Over(
            store: null,
            new TestClock(_minted),
            new ARecordingWriteSeam(),
            context);

    /// <summary>
    /// The controller over a context and a store.
    /// </summary>
    /// <param name="context">The request and response.</param>
    /// <param name="store">The store directory.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="seam">The write seam.</param>
    /// <returns>The controller.</returns>
    private static RedeemController Controller(
        HttpContext context,
        string store,
        TestClock clock,
        ARecordingWriteSeam seam) =>
        RedeemRoute.Over(store, clock, seam, context);

    /// <summary>
    /// The controller over a post carrying one cookie and one form value.
    /// </summary>
    /// <param name="store">The store directory.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="seam">The write seam.</param>
    /// <param name="cookie">What the request's cookie carries, or null.</param>
    /// <param name="presented">What the form carries, or null.</param>
    /// <returns>The controller.</returns>
    private static RedeemController Posting(
        string store,
        TestClock clock,
        ARecordingWriteSeam seam,
        string? cookie,
        string? presented)
    {
        var context = Carrying(cookie);
        var body = RedeemRoute.Body("newcomer", "a password long enough");
        if (presented is null)
        {
            body.Remove(FormToken.Field);
        }
        else
        {
            body[FormToken.Field] = presented;
        }

        context.Request.Method = "POST";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(
            body.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal));

        return RedeemRoute.Over(store, clock, seam, context);
    }

    /// <summary>
    /// How many uses the one invitation in a store has left, read back off disk.
    /// </summary>
    /// <param name="store">The store directory.</param>
    /// <returns>The count.</returns>
    private static int Remaining(string store) =>
        Assert.Single(new InvitationStore(store).Read().Invitations).UsesRemaining;
}
