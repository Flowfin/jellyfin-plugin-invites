using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Jellyfin.Plugin.Invites.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Invites.Controllers;

/// <summary>
/// The public side: the setup page a person following an invitation link is
/// served, and the post that receives what they filled in.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is reachable without authentication, and it says so on each action.</b>
/// The person following the link has no account yet, which is the whole reason
/// the flow exists. <c>RouteInventoryTests</c> holds that sentence against the
/// assembly: this type is the one name in its public list, and a second one
/// there is a second public endpoint and a decision rather than an addition.
/// </para>
/// <para>
/// <b>The page does not take the code and the post does.</b> A page assembled
/// around a value a stranger chose is a page with an injection surface, and the
/// way to have none is to serve bytes no request contributed to. The post has to
/// have the code, because there is nothing to judge without it, and it reaches
/// no markup: it is canonicalised, hashed and compared.
/// </para>
/// <para>
/// <b>The one thing the page is not the same bytes for every caller in.</b> This
/// paragraph said both pages this route serves are, and one of them stopped
/// being so when the anti-forgery token landed: a token that did not differ per
/// page view would be one a forged form could carry. What still holds, and is
/// the half the sentence was written for, is that nothing a REQUEST carried
/// reaches the markup. The token is minted here, refused by
/// <see cref="Setup.FormToken.IsWellFormed"/> unless it is hexadecimal, and
/// written in by <see cref="SetupPage.For"/>. The refusal page is untouched and
/// is still the same bytes for everybody.
/// </para>
/// <para>
/// <b>The post decides nothing itself.</b> Whether a limit was reached is
/// <see cref="AttemptLimiter"/>'s, whether this plugin may create another
/// account at all is <see cref="CreationCeiling"/>'s, whether the code may be
/// honoured is
/// <see cref="RedemptionDecision"/>'s, taking the use is
/// <see cref="InvitationOperations.Reserve"/>'s under the store's own monitor,
/// and creating the account is <see cref="AccountCreation"/>'s. This type reads
/// the request, calls them in one order, and translates what comes back.
/// </para>
/// <para>
/// <b>Every refusal of a presented code is the same response.</b>
/// docs/refusal-response.md fixes the page, the case list and the list the
/// responses are compared on, and the reason is that a caller able to tell two
/// refusals apart can ask this route which codes exist. The status code is
/// <see cref="StatusCodes.Status403Forbidden"/>, picked here because that page
/// named this route as the one that owes it: it is true of every case without
/// narrowing any of them, and a not-found would be a claim about the address,
/// which is served, rather than about the invitation, which is what was refused.
/// </para>
/// <para>
/// <b>What the post does not do yet.</b> It cannot tell a name already taken from one
/// the server refuses, which is #67's. The
/// completion address a finished redemption is sent to is fixed by docs/api.md
/// and is served by nothing until #79 lands, so a person who finishes today has
/// an account and meets the server's own not-found page.
/// </para>
/// <para>
/// <b>The headers.</b> Every response this route sends carries the same five,
/// set in one place, so an action added beside these two carries them by calling
/// that place rather than by its author remembering four names. Four are
/// constant and the fifth is the policy of what is being served, derived from
/// the page where there is one. The constant four are the ones that matter for a
/// form that takes a password on an address carrying a credential in its path:
/// nothing may frame it, nothing may store it, no browser may guess its type,
/// and no address it links to learns the code from a referrer. The last is why
/// the redirect carries them too, and it is not a formality: without it the
/// browser hands the completion address the invitation code in a referrer
/// header.
/// </para>
/// </remarks>
[Route(InvitationLink.Segment)]
public sealed class RedeemController : ControllerBase
{
    private const string ReferrerPolicy = "Referrer-Policy";

    /// <summary>
    /// The policy a response with no document is sent under. The redirect that
    /// ends a redemption carries no bytes for a browser to render, so the policy
    /// that describes it is the one that permits nothing, and every response
    /// this route sends carries all five headers rather than four with an
    /// exception a reader has to know about.
    /// </summary>
    private const string NoDocument = "default-src 'none'";

    /// <summary>
    /// Where a finished redemption is sent. docs/api.md fixes the address and
    /// #79 owns what answers there.
    /// </summary>
    private const string Completion = "/" + InvitationLink.Segment + "/done";

    private readonly InvitationOperations _operations;
    private readonly AttemptLimiter _limiter;
    private readonly CreationCeiling _ceiling;
    private readonly IServerAccountWrites _accounts;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedeemController"/> class.
    /// </summary>
    /// <param name="operations">The operations the post translates.</param>
    /// <param name="limiter">
    /// The limiter, asked before a presented code is judged. It is one instance
    /// for the process, because a limiter handed out per request would give
    /// every attempt an empty counter.
    /// </param>
    /// <param name="ceiling">
    /// How many accounts this plugin may create in a window, asked before the
    /// use is taken. One instance for the process, for the reason the limiter is
    /// one: a ceiling handed out per request counts to one and bounds nothing.
    /// </param>
    /// <param name="accounts">The write seam over the server's user table.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public RedeemController(
        InvitationOperations operations,
        AttemptLimiter limiter,
        CreationCeiling ceiling,
        IServerAccountWrites accounts)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentNullException.ThrowIfNull(ceiling);
        ArgumentNullException.ThrowIfNull(accounts);

        _operations = operations;
        _limiter = limiter;
        _ceiling = ceiling;
        _accounts = accounts;
    }

    /// <summary>
    /// Serves the setup page.
    /// </summary>
    /// <response code="200">The page. It is the same bytes for every code.</response>
    /// <returns>The page, as HTML.</returns>
    [AllowAnonymous]
    [HttpGet("{code}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ContentResult Page()
    {
        Secure(SetupPage.ContentSecurityPolicy);

        // Both halves are written on this one response and they are the same
        // value: the cookie the browser will send back by itself, and the
        // control on the form, which a page on another site cannot read in
        // order to forge. Minted per page view, because a token that did not
        // move would be one a forged form could simply carry.
        var minted = FormToken.Fresh();
        Response.Cookies.Append(
            FormToken.CookieName,
            minted,
            FormToken.OptionsFor(Request.IsHttps));

        return Content(SetupPage.For(minted), SetupPage.ContentType);
    }

    /// <summary>
    /// Receives the setup form, judges the code the page was served for, and
    /// creates the account where the code is honoured.
    /// </summary>
    /// <param name="code">The code the page was served for, from the path.</param>
    /// <param name="submission">The three fields the form defines.</param>
    /// <response code="303">
    /// The account exists. The person is sent to the completion address.
    /// </response>
    /// <response code="400">
    /// The post did not carry the fields the form defines. Nothing was looked
    /// up, so this answer says nothing about the code.
    /// </response>
    /// <response code="403">
    /// The single refusal, byte for byte the same in every case
    /// docs/refusal-response.md lists.
    /// </response>
    /// <returns>The redirect, the refusal, or the bad request.</returns>
    /// <remarks>
    /// <para>
    /// <b>The order is most of what this action is.</b> The anti-forgery token
    /// is read first, and then the shape of the request, because both are
    /// answered out of the request alone and therefore disclose nothing about
    /// any code. Both happen before the limiter, so a forged post is refused
    /// without a lookup, without an attempt counted and without a use spent.
    /// Then the
    /// limiter, so an attempt is counted before anything is looked up. Then the
    /// reservation, which reads the records, asks for the verdict and takes the
    /// use inside one monitor. Then the account. Then the record of which
    /// account this invitation produced.
    /// </para>
    /// <para>
    /// <b>A failure after the use was taken is answered with the refusal and the
    /// use stays taken.</b> That is the fail-closed direction and it is stated
    /// rather than hidden: an invitation that produced nothing costs a fresh
    /// mint, and one that produced an account while still reading as unused is a
    /// single-use link that works again. Telling a taken username from a server
    /// that refused the write is #67's and is not done here, so both arrive as
    /// the same refusal.
    /// </para>
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("{code}")]
    [ProducesResponseType(StatusCodes.Status303SeeOther)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Submit(string code, [FromForm] SetupSubmission submission)
    {
        if (!FormToken.Accompanies(Request, submission?.Token))
        {
            // Before everything, so a post from a page on another site costs the
            // invitation nothing: no record is read, no attempt is counted and
            // no use is taken. It is not the single refusal, which
            // docs/refusal-response.md keeps for the cases that would otherwise
            // say something about a code; this one is decided out of the request
            // alone and says nothing about any code, which is exactly what the
            // bad request below already means.
            return Malformed();
        }

        var answers = SetupAnswers.Accept(submission, Request);
        if (answers is null)
        {
            // Read off the request and nothing else, before any code is judged,
            // so this answer is the same whatever the code is worth. Every rule
            // behind it is in SetupAnswers, which is where the argument for each
            // one is; what is here is the order, and the order is the part that
            // keeps a malformed post from telling anybody what its code was worth.
            return Malformed();
        }

        var from = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(from))
        {
            // The limiter refuses to count an attempt naming no address, because
            // counting one as everybody would let a caller the server cannot
            // place spend everybody's allowance. A caller this route cannot
            // place is refused rather than judged uncounted.
            return Refusal();
        }

        if (!_limiter.MayJudge(from) || !_operations.StoreIsAvailable)
        {
            return Refusal();
        }

        // Asked before the use is taken, so a redemption this refuses leaves the
        // invitation exactly as it found it. The person is answered with the same
        // page as every other refusal, which docs/refusal-response.md requires:
        // a caller able to tell a ceiling refusal apart learns something true
        // about the server that refusing them was not meant to disclose.
        if (!_ceiling.MayCreate())
        {
            return Refusal();
        }

        var reservation = _operations.Reserve(code);
        if (!reservation.MayCreateAnAccount)
        {
            return Refusal();
        }

        var reserved = reservation.Reserved!;

        Guid account;
        try
        {
            account = await AccountCreation.CreateAsync(
                _accounts,
                answers.Username,
                answers.Password,
                reserved.Template!).ConfigureAwait(false);
        }
        catch (ServerAccountWriteRefusedException)
        {
            return Refusal();
        }
        catch (ArgumentException)
        {
            return Refusal();
        }

        _operations.RecordAccount(reserved.Id, account);

        return Completed();
    }

    /// <summary>
    /// The answer to a post this route read nothing out of: one that carried no
    /// good anti-forgery token, or not the fields the form defines.
    /// </summary>
    /// <returns>The bad request, under this route's headers.</returns>
    /// <remarks>
    /// It carries no document, so the policy is the one that permits nothing.
    /// The headers are set here for the reason they are set on every other
    /// answer this route gives: the class remarks say every response carries the
    /// same five, and an answer that quietly carried four would make that
    /// sentence false for the one case nothing drives.
    /// </remarks>
    private BadRequestResult Malformed()
    {
        Secure(NoDocument);

        return BadRequest();
    }

    /// <summary>
    /// The single refusal, as the response a caller receives.
    /// </summary>
    /// <returns>The refusal page, under this route's headers.</returns>
    private ContentResult Refusal()
    {
        Secure(RefusalPage.ContentSecurityPolicy);

        var refusal = Content(RefusalPage.Html, RefusalPage.ContentType);
        refusal.StatusCode = StatusCodes.Status403Forbidden;

        return refusal;
    }

    /// <summary>
    /// Sends a finished redemption to the completion address.
    /// </summary>
    /// <returns>The redirect.</returns>
    /// <remarks>
    /// See other rather than found, so what the browser does next is a get of a
    /// page and never the form being sent a second time.
    /// </remarks>
    private StatusCodeResult Completed()
    {
        Secure(NoDocument);
        Response.Headers.Location = Completion;

        return StatusCode(StatusCodes.Status303SeeOther);
    }

    /// <summary>
    /// Sets the five headers every response from this route carries.
    /// </summary>
    /// <param name="policy">
    /// The policy of what is being served, derived from the page where there is
    /// one and <see cref="NoDocument"/> where there is not.
    /// </param>
    private void Secure(string policy)
    {
        var headers = Response.Headers;
        headers.ContentSecurityPolicy = policy;
        headers.XFrameOptions = "DENY";
        headers.XContentTypeOptions = "nosniff";
        headers.CacheControl = "no-store";
        headers[ReferrerPolicy] = "no-referrer";
    }
}
