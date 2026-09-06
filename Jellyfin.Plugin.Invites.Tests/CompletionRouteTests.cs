using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Setup;
using Jellyfin.Plugin.Invites.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The completion address, and the whole sequence that ends at it.
/// </summary>
/// <remarks>
/// <para>
/// <b>What #79 asks for is that the last step cannot undo the redemption.</b> A
/// completion rendered out of the post would be resubmitted by a refresh, and a
/// completion that re-read the invitation would meet the record its own
/// redemption spent and refuse. So the assertions here are mostly about what the
/// route does NOT do: it takes no parameter, it reads no store, and driving it
/// twice over a spent invitation changes nothing on disk and answers the same
/// bytes both times.
/// </para>
/// <para>
/// <b>The sequence is driven end to end rather than at the redirect.</b>
/// <see cref="RedeemPostTests"/> asserts the post's answer is a see-other at the
/// completion address; what is missing from that is whether the address the
/// redirect names is one this plugin answers. So the test below reads the
/// location off the response and compares it against the address assembled from
/// the completion action's own attributes, rather than against a string a reader
/// typed twice.
/// </para>
/// <para>
/// <b>No web host.</b> The controller is an ordinary object over a context this
/// test owns, which is the headless rule rather than a shortcut. What it bounds
/// is stated where every file here states it: nothing says what a server's own
/// pipeline does with these routes, no request crossed a socket, and no browser
/// rendered any of these bytes. The route table is read off the actions' own
/// attributes rather than being asked of a running server.
/// </para>
/// </remarks>
public class CompletionRouteTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The address the post redirects to is one this route answers.
    /// </summary>
    /// <remarks>
    /// Read off the attributes rather than asserted as a string a reader typed
    /// twice: the address is the route segment and the action's template
    /// together, so a template edited to something else reds here instead of
    /// leaving a redirect pointing at nothing.
    /// </remarks>
    [Fact]
    public void TheAddressThePostRedirectsToIsOneThisRouteAnswers()
    {
        var served = typeof(RedeemController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HttpGetAttribute>())
            .Select(attribute => "/" + Segment() + "/" + attribute.Template)
            .ToList();

        Assert.Contains(CompletionAddress(), served, StringComparer.Ordinal);
    }

    /// <summary>
    /// The completion action is handed nothing. No route parameter, no query, no
    /// body.
    /// </summary>
    /// <remarks>
    /// This is the clause the rest of the file rests on. An action taking a code
    /// would have something to look up, and something to look up is how a
    /// completion turns a finished redemption into a refusal.
    /// </remarks>
    [Fact]
    public void TheCompletionActionIsHandedNothing()
    {
        Assert.Empty(Done().GetParameters());
    }

    /// <summary>
    /// The completion action serves the compiled-in page, unchanged, under the
    /// policy derived from it.
    /// </summary>
    [Fact]
    public void TheRouteServesTheCompletionPageUnchanged()
    {
        var context = RedeemRoute.Request();
        var controller = RedeemRoute.Over(
            store: null,
            new TestClock(_minted),
            new ARecordingWriteSeam(),
            context);

        var answer = controller.Done();

        Assert.Equal(CompletionPage.Html, answer.Content);
        Assert.Equal(CompletionPage.ContentType, answer.ContentType);
        Assert.Equal(
            CompletionPage.ContentSecurityPolicy,
            context.Response.Headers.ContentSecurityPolicy.ToString());
    }

    /// <summary>
    /// The completion response sets no cookie.
    /// </summary>
    /// <remarks>
    /// There is no form on the page, so there is nothing to mint a token for. A
    /// completion that set one would write a cookie on every visit to an address
    /// anybody may visit, for a post that does not exist.
    /// </remarks>
    [Fact]
    public void TheCompletionResponseMintsNoToken()
    {
        var context = RedeemRoute.Request();
        var controller = RedeemRoute.Over(
            store: null,
            new TestClock(_minted),
            new ARecordingWriteSeam(),
            context);

        controller.Done();

        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
    }

    /// <summary>
    /// The whole sequence: a minted code, the page, the post, the redirect, and
    /// the completion the redirect names.
    /// </summary>
    /// <remarks>
    /// One test rather than four, because what #79 is about is the join between
    /// them. Each step alone passes for a flow whose last step is the server's
    /// own not-found page.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheSequenceEndsAtAPageThisPluginServes()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();

        var serving = RedeemRoute.Request();
        var page = RedeemRoute.Over(directory.Path, clock, seam, serving).Page();
        Assert.Equal(SetupPage.ContentType, page.ContentType);

        var posting = RedeemRoute.Request();
        var answer = await RedeemRoute.Over(directory.Path, clock, seam, posting)
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        var redirect = Assert.IsType<StatusCodeResult>(answer);
        Assert.Equal(StatusCodes.Status303SeeOther, redirect.StatusCode);
        Assert.Equal(CompletionAddress(), posting.Response.Headers.Location.ToString());

        var following = RedeemRoute.Request();
        var completion = RedeemRoute.Over(directory.Path, clock, seam, following).Done();

        Assert.Equal(CompletionPage.Html, completion.Content);
        Assert.Null(completion.StatusCode);
    }

    /// <summary>
    /// Refreshing the completion neither redeems nor errors: the store after two
    /// visits is the store the redemption left, and both answers are the same
    /// page.
    /// </summary>
    /// <remarks>
    /// The invitation is minted for one use and spent, so a completion that read
    /// it would find a spent record. That is the state that makes this test
    /// worth something: over an unspent invitation a completion doing the wrong
    /// thing would still answer a page.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task RefreshingTheCompletionNeitherRedeemsNorErrors()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();

        await RedeemRoute.Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        var afterTheRedemption = new InvitationStore(directory.Path).Read();
        var asked = seam.Asked.Count;

        var one = RedeemRoute.Over(directory.Path, clock, seam, RedeemRoute.Request()).Done();
        var two = RedeemRoute.Over(directory.Path, clock, seam, RedeemRoute.Request()).Done();

        Assert.Equal(one.Content, two.Content);
        Assert.Null(one.StatusCode);
        Assert.Null(two.StatusCode);

        var afterTwoVisits = new InvitationStore(directory.Path).Read();
        var before = Assert.Single(afterTheRedemption.Invitations);
        var after = Assert.Single(afterTwoVisits.Invitations);

        Assert.Equal(0, before.UsesRemaining);
        Assert.Equal(before.UsesRemaining, after.UsesRemaining);
        Assert.Equal(before.AccountsProduced.Count(), after.AccountsProduced.Count());
        Assert.Equal(asked, seam.Asked.Count);
    }

    /// <summary>
    /// The page carries no code and no password, and nothing else about the
    /// account either.
    /// </summary>
    /// <remarks>
    /// It may sit in a browser history on a shared machine, which is the reason
    /// #79 gives for the clause. The code is searched for as the one that was
    /// actually minted rather than as a shape, so a page that interpolated it
    /// reds here whatever a shape assertion would have said.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheCompletionPageCarriesNoCodeAndNoPassword()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();

        await RedeemRoute.Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        var served = RedeemRoute.Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Done()
            .Content!;

        Assert.DoesNotContain(minted.Code, served, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(minted.Invitation.Id.ToString(), served, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("newcomer", served, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("a password long enough", served, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("type=\"password\"", served, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<form", served, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", served, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The page offers the sign-in, and it is one step away and on this server.
    /// </summary>
    /// <remarks>
    /// #79 asks that the person reach a working sign-in in at most one further
    /// step, and that this plugin hand off rather than issue a session. A link
    /// is the one step; an absolute address would be this page deciding which
    /// host the person is on.
    /// </remarks>
    [Fact]
    public void TheCompletionPageOffersTheServersOwnSignIn()
    {
        Assert.Contains(
            "href=\"" + CompletionPage.SignIn + "\"",
            CompletionPage.Html,
            StringComparison.Ordinal);

        Assert.StartsWith("/", CompletionPage.SignIn, StringComparison.Ordinal);
        Assert.DoesNotContain("//", CompletionPage.SignIn, StringComparison.Ordinal);
    }

    /// <summary>
    /// The literal segment cannot shadow a code.
    /// </summary>
    /// <remarks>
    /// The completion sits under the same route as the code, so a literal a real
    /// code could equal would serve the completion page to somebody holding that
    /// invitation and leave them with no form. It cannot happen because a code is
    /// <see cref="InvitationCode.Length"/> characters, and this asserts the
    /// reason rather than the length: what is refused is the segment being
    /// canonicalisable at all.
    /// </remarks>
    [Fact]
    public void TheCompletionSegmentIsNotSomethingACodeCouldBe()
    {
        Assert.Null(InvitationCode.Canonicalise(Template()));
    }

    /// <summary>
    /// The completion action, found as the one action of this route that answers
    /// a get at a template with nothing in it to fill in.
    /// </summary>
    /// <remarks>
    /// The page action also takes no parameters, so what separates the two here
    /// is the template rather than the signature: the page's carries a route
    /// parameter and the completion's is a literal, which is the property the
    /// rest of this file is about.
    /// </remarks>
    /// <returns>The method.</returns>
    private static MethodInfo Done() =>
        Assert.Single(
            typeof(RedeemController)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method =>
                method.GetCustomAttributes<HttpGetAttribute>()
                    .Any(attribute => attribute.Template?.Contains('{', StringComparison.Ordinal) == false));

    /// <summary>
    /// The completion action's own route template.
    /// </summary>
    /// <returns>The template.</returns>
    private static string Template() =>
        Assert.Single(Done().GetCustomAttributes<HttpGetAttribute>()).Template!;

    /// <summary>
    /// The route segment the redemption controller is mounted under.
    /// </summary>
    /// <returns>The segment.</returns>
    private static string Segment() =>
        Assert.Single(typeof(RedeemController).GetCustomAttributes<RouteAttribute>()).Template;

    /// <summary>
    /// The completion address, assembled from the route segment and the
    /// completion action's own template.
    /// </summary>
    /// <returns>The address.</returns>
    private static string CompletionAddress() => "/" + Segment() + "/" + Template();
}
