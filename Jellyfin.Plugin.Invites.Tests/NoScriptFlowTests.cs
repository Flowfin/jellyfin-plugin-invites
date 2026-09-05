using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Setup;
using Jellyfin.Plugin.Invites.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The flow driven the way a browser with script disabled would drive it:
/// nothing is typed into the request that the served page did not hand over.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is not the same as the post tests beside it.</b> Those build the
/// submission in code, which is the right shape for a test about what the post
/// does with an answer and says nothing about where the answer came from. A
/// browser without script has exactly two sources for what it sends: what the
/// person typed into the controls the page carries, and what the page already
/// filled in. So the body here is read OFF the served bytes, control by control,
/// and a page that grew a control the flow cannot complete without script would
/// fail here rather than pass with the value supplied from the test.
/// </para>
/// <para>
/// <b>What that makes checkable.</b> #80 asks that the whole flow work with
/// script disabled and that the route-level tests exercise it without executing
/// any script. A route-level test never executes script either way, which is the
/// weakness the issue's own notes record: it cannot say a browser with script
/// off behaves the same. What it CAN say is the half that matters, that nothing
/// in the request needed anything but the page and the person. The other half is
/// closed by the page carrying no script at all, which
/// <c>SetupPageTests.ThePageRunsNoScript</c> and
/// <c>RefusalPageTests.ThePageLoadsNothingAndRunsNothing</c> hold for the two
/// pages this route serves.
/// </para>
/// <para>
/// <b>No web host and no browser.</b> The controller is an ordinary object and
/// the two contexts are ones the suite owns, which is the headless rule rather
/// than a shortcut. Nothing here has rendered a page, and no browser setting was
/// changed anywhere.
/// </para>
/// </remarks>
public class NoScriptFlowTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// What the person types. Two values, because the third control the person
    /// answers is the confirmation and a browser has no idea it is a copy.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> _typed =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["username"] = "newcomer",
            ["password"] = "a password long enough",
            ["confirmation"] = "a password long enough",
        };

    /// <summary>
    /// One control of the served form, as the attributes a browser reads off it.
    /// </summary>
    private static readonly Regex ControlPattern = new(
        "<input(?<attributes>[^>]*)>",
        RegexOptions.Compiled);

    private static readonly Regex AttributePattern = new(
        "(?<name>[a-z-]+)=\"(?<value>[^\"]*)\"",
        RegexOptions.Compiled);

    /// <summary>
    /// A person who never ran a line of script gets an account: one request for
    /// the page, one request carrying back what the page gave them plus what
    /// they typed, and the redirect that ends the flow.
    /// </summary>
    /// <remarks>
    /// The cookie is carried the way a browser carries one, taken off the
    /// <c>Set-Cookie</c> the first response wrote and cut at the first
    /// attribute. Nothing else crosses between the two requests, so a flow that
    /// needed a value neither the page nor the person supplied could not be
    /// completed here at all.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task APersonWhoRanNoScriptGetsAnAccountFromWhatThePageGaveThem()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();

        var serving = RedeemRoute.Request();
        var page = RedeemRoute.Over(directory.Path, clock, seam, serving).Page();

        var body = WhatABrowserWouldSend(page.Content!);
        var posting = Carrying(WhatABrowserWouldKeep(serving.Response.Headers), body);

        var answer = await RedeemRoute
            .Over(directory.Path, clock, seam, posting)
            .Submit(minted.Code, Bound(body));

        Assert.Equal(StatusCodes.Status303SeeOther, Assert.IsType<StatusCodeResult>(answer).StatusCode);
        Assert.Equal("/redeem/done", posting.Response.Headers.Location.ToString());

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(0, stored.UsesRemaining);
        Assert.Equal(seam.Answers, Assert.Single(stored.AccountsProduced).Account);
    }

    /// <summary>
    /// Every control the page carries is one the person answers or one the page
    /// filled in, and there is no third kind.
    /// </summary>
    /// <remarks>
    /// This is what the test above rests on and it is asserted rather than
    /// assumed. A control the page leaves empty for something other than the
    /// person to fill in is a control only script could fill, and the flow above
    /// would then be completing on a value this file supplied rather than on one
    /// the page did. It reds in both directions: a fourth question, and a hidden
    /// control the page did not fill in.
    /// </remarks>
    [Fact]
    public void EveryControlIsOneThePersonAnswersOrOneThePageFilledIn()
    {
        var page = SetupPage.For(FormToken.Fresh());
        var unanswerable = new List<string>();

        foreach (var control in ControlsOn(page))
        {
            var answered = _typed.ContainsKey(control.Field);
            var filled = control.Value.Length > 0;
            if (answered == filled)
            {
                unanswerable.Add(
                    control.Field
                    + (answered
                        ? " is a question the page also filled in, so what the person typed is not what is sent"
                        : " is neither a question the person answers nor a value the page filled in, so only script could supply it"));
            }
        }

        Assert.True(
            unanswerable.Count == 0,
            "A browser with script disabled sends what the person typed and what the page already carried, and nothing else. These are neither: "
            + string.Join("; ", unanswerable));
    }

    /// <summary>
    /// The form names no address of its own, so the second request goes back to
    /// the one the first came from.
    /// </summary>
    /// <remarks>
    /// The neighbouring assertion in <c>SetupPageTests</c> reads the same two
    /// facts off the page. What is added here is the reason they matter to this
    /// issue: an <c>action</c> naming somewhere else is the one thing on a
    /// scriptless form that could send the person's password to an address this
    /// plugin never chose, and it is the shape script would otherwise be needed
    /// to produce.
    /// </remarks>
    [Fact]
    public void TheFormSendsThePasswordBackToTheAddressItCameFrom()
    {
        var page = SetupPage.For(FormToken.Fresh());

        Assert.Contains("<form method=\"post\">", page, StringComparison.Ordinal);
        Assert.DoesNotContain("action=", page, StringComparison.Ordinal);
        Assert.DoesNotContain("formaction=", page, StringComparison.Ordinal);
    }

    /// <summary>
    /// The controls of the served form, in the order the page carries them.
    /// </summary>
    /// <param name="page">The served page.</param>
    /// <returns>The name and the filled-in value of each control.</returns>
    private static IReadOnlyList<(string Field, string Value)> ControlsOn(string page)
    {
        var open = page.IndexOf("<form", StringComparison.Ordinal);
        var close = page.IndexOf("</form>", StringComparison.Ordinal);
        if (open < 0 || close <= open)
        {
            throw new InvalidOperationException(
                "The served page has no form region between <form and </form>, so this read no controls. Failing rather than driving a flow with an empty body.");
        }

        var controls = new List<(string, string)>();
        foreach (Match control in ControlPattern.Matches(page[open..close]))
        {
            var attributes = AttributePattern
                .Matches(control.Groups["attributes"].Value)
                .ToDictionary(
                    attribute => attribute.Groups["name"].Value,
                    attribute => attribute.Groups["value"].Value,
                    StringComparer.Ordinal);

            if (attributes.TryGetValue("name", out var field))
            {
                controls.Add((field, attributes.GetValueOrDefault("value", string.Empty)));
            }
        }

        Assert.NotEmpty(controls);

        return controls;
    }

    /// <summary>
    /// The body a browser with no script would send after the person filled the
    /// form in.
    /// </summary>
    /// <param name="page">The served page.</param>
    /// <returns>One entry per control.</returns>
    private static Dictionary<string, StringValues> WhatABrowserWouldSend(string page)
    {
        var body = new Dictionary<string, StringValues>(StringComparer.Ordinal);
        foreach (var control in ControlsOn(page))
        {
            body[control.Field] = _typed.TryGetValue(control.Field, out var typed)
                ? typed
                : control.Value;
        }

        return body;
    }

    /// <summary>
    /// What a browser keeps out of a <c>Set-Cookie</c> and sends back: the pair,
    /// and none of the attributes after it.
    /// </summary>
    /// <param name="headers">The first response's headers.</param>
    /// <returns>The value of the request's cookie header.</returns>
    private static string WhatABrowserWouldKeep(IHeaderDictionary headers)
    {
        var written = Assert.Single(headers.SetCookie)!;
        var ends = written.IndexOf(';', StringComparison.Ordinal);

        return ends < 0 ? written : written[..ends];
    }

    /// <summary>
    /// The second request, carrying the cookie and the body and nothing else the
    /// suite invented.
    /// </summary>
    /// <param name="cookie">What the browser kept from the first response.</param>
    /// <param name="body">What the browser would send.</param>
    /// <returns>The context.</returns>
    private static DefaultHttpContext Carrying(string cookie, IDictionary<string, StringValues> body)
    {
        var context = RedeemRoute.Request();
        context.Request.Headers.Cookie = cookie;
        context.Request.Method = "POST";
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>(body));

        return context;
    }

    /// <summary>
    /// The submission the model binder would produce from that body.
    /// </summary>
    /// <param name="body">The posted body.</param>
    /// <returns>The submission.</returns>
    /// <remarks>
    /// Filled by walking the type's members and matching each one to a key
    /// ignoring case, which is how the binder matches them, rather than by
    /// naming three of them here. A member the form has no control for is left
    /// unset, which is the state the post refuses, so this cannot quietly supply
    /// what the page did not.
    /// </remarks>
    private static SetupSubmission Bound(IDictionary<string, StringValues> body)
    {
        var submission = new SetupSubmission();
        foreach (var member in typeof(SetupSubmission).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var found = body.Keys.FirstOrDefault(
                key => string.Equals(key, member.Name, StringComparison.OrdinalIgnoreCase));
            if (found is not null)
            {
                member.SetValue(submission, body[found].ToString());
            }
        }

        return submission;
    }
}
