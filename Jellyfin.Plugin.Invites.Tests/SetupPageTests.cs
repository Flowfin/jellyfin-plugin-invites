using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The setup page, and the response it is served in.
/// </summary>
/// <remarks>
/// <para>
/// <b>No web host.</b> The controller is an ordinary object here and the
/// response is a <see cref="DefaultHttpContext"/> the test owns, which is the
/// headless rule rather than a shortcut: a test that hosted the application
/// would open a network connection and the suite may not. What that bounds is
/// stated at the bottom of this file rather than left for a reader to work out.
/// </para>
/// <para>
/// <b>The hash in the policy is computed here rather than asked for.</b> Asking
/// <see cref="SetupPage"/> for it and comparing it with itself would pass over
/// any implementation at all. These bytes are hashed independently, so a policy
/// that stopped being derived from the page reds the moment the page moves.
/// </para>
/// </remarks>
public class SetupPageTests
{
    /// <summary>
    /// The spellings an address somewhere else is written in, as
    /// <c>ConfigurationPageTests</c> reads them. The same four, because the
    /// question is the same one and two lists would drift.
    /// </summary>
    private static readonly string[] Elsewhere = ["://", "\"//", "'//", "(//"];

    /// <summary>
    /// The page is served out of the assembly rather than off disk, so a server
    /// with no web client installed and an installation an operator has moved
    /// both serve the same bytes, and there is nowhere to leave a stale copy.
    /// </summary>
    [Fact]
    public void ThePageIsTheEmbeddedResource()
    {
        var assembly = typeof(SetupPage).Assembly;

        using var stream = assembly.GetManifestResourceStream(SetupPage.ResourceName);
        Assert.NotNull(stream);

        using var reader = new StreamReader(stream!, Encoding.UTF8);
        Assert.Equal(reader.ReadToEnd(), SetupPage.Html);
    }

    /// <summary>
    /// The response body is the page with one value written in, and that value
    /// is the anti-forgery token this response itself minted. No value a request
    /// carried reaches the markup, which is the claim this test was written for
    /// and which the token does not weaken.
    /// </summary>
    /// <remarks>
    /// The third assertion is the one doing the work. Putting the placeholder
    /// back where the token went gives the compiled-in page back byte for byte,
    /// so nothing ELSE was substituted: a second insertion anywhere on the page
    /// would survive that reversal and fail here.
    /// </remarks>
    [Fact]
    public void TheRouteServesThePageWithNothingInItButItsOwnToken()
    {
        var served = Serve();
        var minted = RedeemRoute.TokenSetOn(served.Headers);

        Assert.True(FormToken.IsWellFormed(minted));
        Assert.Equal(SetupPage.For(minted), served.Content);
        Assert.Equal(SetupPage.ContentType, served.ContentType);
        Assert.Equal(
            SetupPage.Html,
            served.Content!.Replace(minted, SetupPage.Placeholder, StringComparison.Ordinal));
    }

    /// <summary>
    /// The action takes no argument at all, which is what makes the sentence
    /// above hold by construction rather than by care. A code bound here is a
    /// value that could be written into a response, and the refusal is that
    /// there is nowhere for it to arrive.
    /// </summary>
    [Fact]
    public void TheActionIsHandedNothingFromTheRequest()
    {
        var page = typeof(RedeemController).GetMethod(nameof(RedeemController.Page));

        Assert.NotNull(page);
        Assert.Empty(page!.GetParameters());
    }

    /// <summary>
    /// The route is the one the link builder points at, taken from the same
    /// constant rather than spelled twice, and the code sits in the path.
    /// </summary>
    [Fact]
    public void TheRouteIsTheOneTheLinkPointsAt()
    {
        var route = typeof(RedeemController).GetCustomAttribute<RouteAttribute>();
        var get = typeof(RedeemController)
            .GetMethod(nameof(RedeemController.Page))!
            .GetCustomAttribute<HttpGetAttribute>();

        Assert.NotNull(route);
        Assert.NotNull(get);
        Assert.Equal(InvitationLink.Segment, route!.Template);
        Assert.Equal("{code}", get!.Template);
    }

    /// <summary>
    /// The route says on the action that it is public. The person following the
    /// link has no account, so the absence of a requirement here is a decision,
    /// and a decision written where a reader of the action sees it.
    /// </summary>
    [Fact]
    public void TheRouteDeclaresItselfPublicOnTheActionItself()
    {
        var page = typeof(RedeemController).GetMethod(nameof(RedeemController.Page))!;

        Assert.NotEmpty(page.GetCustomAttributes(inherit: false).OfType<IAllowAnonymous>());
    }

    /// <summary>
    /// The five headers, each with the value it is set to. A page that takes a
    /// password on an address carrying a credential in its path owes all five,
    /// and a header that has quietly stopped being sent looks exactly like one
    /// that is.
    /// </summary>
    /// <param name="header">The header name.</param>
    /// <param name="expected">What it is set to.</param>
    [Theory]
    [InlineData("X-Frame-Options", "DENY")]
    [InlineData("X-Content-Type-Options", "nosniff")]
    [InlineData("Cache-Control", "no-store")]
    [InlineData("Referrer-Policy", "no-referrer")]
    public void TheResponseCarriesTheHeaderThatMatters(string header, string expected)
    {
        var served = Serve();

        Assert.Equal(expected, served.Headers[header].ToString());
    }

    /// <summary>
    /// The policy names no origin. <c>default-src 'none'</c> covers everything
    /// the page could fetch, and the only thing opened back up is the page's own
    /// style, by hash. This is the browser-side half of the presentation rule in
    /// docs/setup-never-asks.md: the assertion below reads the page and this one
    /// reads what the browser is told about it.
    /// </summary>
    [Fact]
    public void ThePolicyNamesNoOriginAndAllowsNoInlineScript()
    {
        var policy = Serve().Headers["Content-Security-Policy"].ToString();

        Assert.StartsWith("default-src 'none';", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("form-action 'self'", policy, StringComparison.Ordinal);
        Assert.Contains("base-uri 'none'", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-inline", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe-eval", policy, StringComparison.Ordinal);

        foreach (var spelling in Elsewhere)
        {
            Assert.DoesNotContain(spelling, policy, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The hash in the policy is the hash of the style element the page carries.
    /// A browser refuses a style whose hash it was not given, so a page edited
    /// without its header is a page rendered unstyled, and that failure is worth
    /// finding here rather than by looking at it.
    /// </summary>
    [Fact]
    public void TheHashInThePolicyIsTheHashOfThePagesOwnStyle()
    {
        var page = SetupPage.Html;
        var from = page.IndexOf("<style>", StringComparison.Ordinal) + "<style>".Length;
        var to = page.IndexOf("</style>", StringComparison.Ordinal);
        Assert.True(to > from, "The page carries no style element to hash.");

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(page[from..to]));
        var expected = "'sha256-" + Convert.ToBase64String(digest) + "'";

        Assert.Contains(expected, Serve().Headers["Content-Security-Policy"].ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A page carrying two style elements, or none, is refused rather than
    /// covered by a policy describing one of them. This is the near-miss the
    /// derivation actually has: somebody adds a second block and the first keeps
    /// working, so the failure would be one unstyled section on one page.
    /// </summary>
    /// <param name="page">A page shape the policy cannot describe.</param>
    [Theory]
    [InlineData("<html><body></body></html>")]
    [InlineData("<html><style>a{}</style><style>b{}</style></html>")]
    public void APageThePolicyCannotDescribeIsRefused(string page)
    {
        Assert.Throws<InvalidOperationException>(() => SetupPage.PolicyFor(page));
    }

    /// <summary>
    /// The page loads nothing from anywhere else, read off its own bytes. The
    /// policy above tells a browser so; this says the page has nothing in it to
    /// be told about, which is the claim a reader can check in a second.
    /// </summary>
    [Fact]
    public void ThePageFetchesFromNowhereElse()
    {
        var lines = SetupPage.Html.Split('\n');
        var found = new List<string>();

        for (var line = 0; line < lines.Length; line++)
        {
            foreach (var spelling in Elsewhere)
            {
                if (lines[line].Contains(spelling, StringComparison.Ordinal))
                {
                    found.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "line {0} carries {1}: {2}",
                        line + 1,
                        spelling,
                        lines[line].Trim()));
                    break;
                }
            }
        }

        Assert.True(
            found.Count == 0,
            "The setup page names an address somewhere else:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, found));
    }

    /// <summary>
    /// There is no script on the page. #80 asks that the flow work with script
    /// disabled, and the cheapest way to hold that is for there to be none, so
    /// the assertion is written here where the page is rather than waiting for
    /// the issue that argues it.
    /// </summary>
    [Fact]
    public void ThePageRunsNoScript()
    {
        Assert.DoesNotContain("<script", SetupPage.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", SetupPage.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, Handlers(SetupPage.Html));
    }

    /// <summary>
    /// The form asks for the three things docs/setup-never-asks.md says it asks
    /// for, carries the one control this plugin fills in for itself, and carries
    /// no fifth. Which questions are refusals is read by a person against that
    /// list, and no check can decide whether a field asks for a legal name; that
    /// another field arrived at all is what this says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The count is over <c>name="</c> across the whole page rather than inside
    /// the form, so the viewport declaration in the head is one of the five. A
    /// narrower count would need the page parsed, and a parser here is a
    /// dependency the runtime set does not carry for one assertion.
    /// </para>
    /// <para>
    /// It said four until the anti-forgery token landed. The token is not a
    /// question and <c>SetupFormInventoryTests</c> is where the two kinds are
    /// told apart; what this leg is for is that nothing arrives on the form
    /// without somebody having changed this number.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheFormAsksForThreeThingsAndCarriesOneOfItsOwn()
    {
        var page = SetupPage.Html;

        Assert.Contains("name=\"username\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"password\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmation\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"" + FormToken.Field + "\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"viewport\"", page, StringComparison.Ordinal);

        var named = 0;
        var at = page.IndexOf("name=\"", StringComparison.Ordinal);
        while (at >= 0)
        {
            named++;
            at = page.IndexOf("name=\"", at + 1, StringComparison.Ordinal);
        }

        Assert.Equal(5, named);
    }

    /// <summary>
    /// The form posts back to the address it was served from. That is what
    /// carries the code to the post without the code ever being written into the
    /// markup, and an action attribute here would be a second place deciding
    /// what the redemption address is.
    /// </summary>
    [Fact]
    public void TheFormPostsBackToWhereItCameFrom()
    {
        Assert.Contains("<form method=\"post\">", SetupPage.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("action=", SetupPage.Html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The attribute-looking substrings of the page that would be event
    /// handlers. Read as whole attribute names rather than as the two letters,
    /// because prose on the page carries those two letters constantly.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <returns>A string carrying every handler attribute found, or empty.</returns>
    private static string Handlers(string page)
    {
        string[] handlers = ["onclick", "onload", "onsubmit", "onerror", "onfocus", "oninput", "onchange"];

        return string.Concat(handlers.Where(handler => page.Contains(handler, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Calls the action against a response the test owns.
    /// </summary>
    /// <returns>What was returned and what was set on the response.</returns>
    private static (string? Content, string? ContentType, IHeaderDictionary Headers) Serve()
    {
        var context = new DefaultHttpContext();
        var controller = RedeemRoute.Over(
            store: null,
            new TestClock(DateTimeOffset.UnixEpoch),
            new ARecordingWriteSeam(),
            context);

        var result = controller.Page();

        return (result.Content, result.ContentType, context.Response.Headers);
    }
}
