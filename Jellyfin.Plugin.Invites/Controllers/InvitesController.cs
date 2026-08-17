using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Invitations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Invites.Controllers;

/// <summary>
/// The administrator surface: mint one invitation, list them, look at one,
/// revoke one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Four operations and no more.</b> There is no route that deletes a record,
/// because removal is retention rather than a button; no route that creates an
/// account, because an operator who wants one has the server's own user editor;
/// and no route that returns a code after minting. Those absences are decisions
/// docs/api.md holds, and they are invisible to somebody reading a list of
/// routes, which is why they are written here as well.
/// </para>
/// <para>
/// <b>Nothing here decides anything about a record.</b> Expiry, the use count and
/// revocation are judged in one place by decision, and this type has no clock and
/// no store on it to judge them with. What it has is
/// <see cref="InvitationOperations"/>, and every action validates its input,
/// calls one operation and translates what comes back.
/// </para>
/// <para>
/// <b>Every action carries its own authorization requirement.</b> A requirement
/// satisfied by an attribute on the class is one deletion away from being gone
/// while every action under it keeps answering, and <c>RouteInventoryTests</c>
/// refuses that shape by reading the action's own attributes rather than the
/// type's.
/// </para>
/// <para>
/// <b>What has not been measured.</b> Nothing here has run against a server.
/// Where the server mounts a plugin's controllers, and that the operator's
/// identity reaches <see cref="IOperatorIdentity"/>, are read off the assembly
/// this plugin compiles against rather than observed.
/// </para>
/// </remarks>
[ApiController]
[Route("Invites")]
[Produces("application/json")]
public sealed class InvitesController : ControllerBase
{
    private readonly InvitationOperations _operations;
    private readonly IOperatorIdentity _caller;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvitesController"/> class.
    /// </summary>
    /// <param name="operations">The four operations.</param>
    /// <param name="caller">Where the calling operator's identity comes from.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public InvitesController(InvitationOperations operations, IOperatorIdentity caller)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(caller);

        _operations = operations;
        _caller = caller;
    }

    /// <summary>
    /// Mints one invitation and returns its code exactly once.
    /// </summary>
    /// <param name="request">The template, the validity and the use count.</param>
    /// <response code="200">Minted. The body carries the code, and nothing returns it again.</response>
    /// <response code="400">The template is missing, or the validity or the use count is outside its ceiling.</response>
    /// <response code="503">This plugin has no data directory, so there is no store to write to.</response>
    /// <returns>The code and the record.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MintedInvitation>> Mint([FromBody] MintRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_operations.StoreIsAvailable)
        {
            return NoStore();
        }

        var mintedBy = await _caller.OfAsync(HttpContext).ConfigureAwait(false);

        try
        {
            var minting = _operations.Mint(
                mintedBy,
                request.Template!,
                request.ValidityDays is null ? null : TimeSpan.FromDays(request.ValidityDays.Value),
                request.Uses);

            return Ok(MintedInvitation.Of(minting));
        }
        catch (ArgumentException refused)
        {
            // The ceilings live on the operations rather than here, so this is
            // the translation and not a second opinion about them. The message
            // is the one the operation wrote, because an operator told "invalid
            // request" learns nothing about which ceiling they met.
            return BadRequest(refused.Message);
        }
    }

    /// <summary>
    /// Lists invitations, without codes and without hashes.
    /// </summary>
    /// <response code="200">The records the store holds.</response>
    /// <response code="503">This plugin has no data directory.</response>
    /// <returns>The records.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<IReadOnlyList<InvitationView>> List()
    {
        if (!_operations.StoreIsAvailable)
        {
            return NoStore();
        }

        return Ok(InvitationView.Of(_operations.All()));
    }

    /// <summary>
    /// Returns one invitation.
    /// </summary>
    /// <param name="id">The non-secret invitation identifier.</param>
    /// <response code="200">The record, in the same shape as one row of the list.</response>
    /// <response code="404">The store holds no invitation with that identifier.</response>
    /// <response code="503">This plugin has no data directory.</response>
    /// <returns>One record, without its code and without its hash.</returns>
    /// <remarks>
    /// The identifier here is the non-secret one and never a code, so a caller
    /// guessing at this route learns which identifiers exist and nothing that
    /// would let them redeem anything. That is why this route may say plainly
    /// that it found nothing while the redemption route may not.
    /// </remarks>
    [Authorize(Policy = "RequiresElevation")]
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<InvitationView> One([FromRoute] Guid id)
    {
        if (!_operations.StoreIsAvailable)
        {
            return NoStore();
        }

        var found = _operations.One(id);

        return found is null ? NotFound() : Ok(InvitationView.Of(found));
    }

    /// <summary>
    /// Revokes one invitation.
    /// </summary>
    /// <param name="id">The non-secret invitation identifier.</param>
    /// <response code="200">Revoked, or already revoked, which is the same answer.</response>
    /// <response code="404">The store holds no invitation with that identifier.</response>
    /// <response code="503">This plugin has no data directory.</response>
    /// <returns>The record as it now stands.</returns>
    /// <remarks>
    /// A <c>POST</c> to a named operation rather than a <c>DELETE</c> of the
    /// record. The difference is not cosmetic: this plugin offers no way to
    /// delete a record, so a <c>DELETE</c> on this path would name an operation
    /// that does not exist and the first person to try it would learn that from
    /// a status code.
    /// </remarks>
    [Authorize(Policy = "RequiresElevation")]
    [HttpPost("{id}/Revoke")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<InvitationView>> Revoke([FromRoute] Guid id)
    {
        if (!_operations.StoreIsAvailable)
        {
            return NoStore();
        }

        var revokedBy = await _caller.OfAsync(HttpContext).ConfigureAwait(false);
        var revoked = _operations.Revoke(id, revokedBy);

        return revoked is null ? NotFound() : Ok(InvitationView.Of(revoked));
    }

    private ObjectResult NoStore() => StatusCode(
        StatusCodes.Status503ServiceUnavailable,
        "This plugin has no data directory on this server, so there is no invitation store to read or write.");
}
