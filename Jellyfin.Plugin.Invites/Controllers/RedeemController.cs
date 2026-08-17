using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Setup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Invites.Controllers;

/// <summary>
/// The public side: the setup page a person following an invitation link is
/// served.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is reachable without authentication, and it says so on the action.</b>
/// The person following the link has no account yet, which is the whole reason
/// the flow exists. <c>RouteInventoryTests</c> holds that sentence against the
/// assembly: this type is the one name in its public list, and a second one
/// there is a second public endpoint and a decision rather than an addition.
/// </para>
/// <para>
/// <b>The action does not take the code.</b> The route carries it, because
/// docs/api.md puts it in the path and <see cref="InvitationLink"/> builds the
/// link that way, and nothing here binds it. A page assembled around a value a
/// stranger chose is a page with an injection surface, and the way to have none
/// is to serve bytes that no request contributed to rather than to escape the
/// value on the way in. The form posts back to the address it was served from,
/// so the code reaches the post without ever being written into the markup.
/// </para>
/// <para>
/// <b>What this route does not do yet.</b> It reads no invitation and decides
/// nothing, so the same page is served for a code that was never minted as for
/// a live one. docs/api.md describes this route as serving the page or the
/// refusal, and the refusal half is #75 and #77: it needs a lookup by code,
/// which nothing in this plugin has, and the single indistinguishable answer
/// #28 decides. Nothing is created, spent or written by a request here.
/// </para>
/// <para>
/// <b>The headers.</b> The policy comes from <see cref="SetupPage"/> and is
/// derived from the page rather than written beside it. The other four are the
/// ones that matter for a form that takes a password on an address that carries
/// a credential in its path: nothing may frame it, nothing may store it, no
/// browser may guess its type, and no address it links to learns the code from a
/// referrer. The anti-forgery token that belongs on the form, and the refusal of
/// a post without one, are #78 and are not here.
/// </para>
/// </remarks>
[Route(InvitationLink.Segment)]
public sealed class RedeemController : ControllerBase
{
    private const string ReferrerPolicy = "Referrer-Policy";

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
        var headers = Response.Headers;
        headers.ContentSecurityPolicy = SetupPage.ContentSecurityPolicy;
        headers.XFrameOptions = "DENY";
        headers.XContentTypeOptions = "nosniff";
        headers.CacheControl = "no-store";
        headers[ReferrerPolicy] = "no-referrer";

        return Content(SetupPage.Html, SetupPage.ContentType);
    }
}
