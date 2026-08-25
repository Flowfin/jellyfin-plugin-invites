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
/// revoke one, and rotate the hash secret.
/// </summary>
/// <remarks>
/// <para>
/// <b>Five operations and no more, and it was four.</b> There is no route that
/// deletes a record, because removal is retention rather than a button; no
/// route that creates an account, because an operator who wants one has the
/// server's own user editor; and no route that returns a code after minting.
/// Those absences are decisions docs/api.md holds, and they are invisible to
/// somebody reading a list of routes, which is why they are written here as
/// well.
/// </para>
/// <para>
/// <b>Why the fifth arrived, written here rather than left to a commit
/// message.</b> A rule that quietly grew by one is a rule nobody trusts the
/// next time. <see cref="Storage.HashSecretRotation"/> already counts what a
/// rotation would invalidate and already refuses a confirmation made against a
/// store that has moved, and with no route none of that reached an operator: a
/// mechanism that exists and cannot be reached is the same as an absent one
/// while looking like a present one. Keeping the surface at four would have
/// made rotation an offline edit of a key file, and then the counter and the
/// refusal serve nobody. #30 is where that was decided.
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
    private readonly IServerAccounts _accounts;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvitesController"/> class.
    /// </summary>
    /// <param name="operations">The operations the routes translate.</param>
    /// <param name="caller">Where the calling operator's identity comes from.</param>
    /// <param name="accounts">
    /// The read seam over the server's own accounts. Every route that hands back
    /// a record asks it what became of the accounts that record claims, which is
    /// #45: the pointer at a deleted account is kept, so something has to say
    /// that it is now a pointer at nothing.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public InvitesController(
        InvitationOperations operations,
        IOperatorIdentity caller,
        IServerAccounts accounts)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(accounts);

        _operations = operations;
        _caller = caller;
        _accounts = accounts;
    }

    /// <summary>
    /// Mints one invitation and returns its code exactly once.
    /// </summary>
    /// <param name="request">The template, the validity and the use count.</param>
    /// <response code="200">Minted. The body carries the code, and nothing returns it again.</response>
    /// <response code="400">The template is missing, or the validity or the use count is outside its ceiling.</response>
    /// <response code="409">This server already holds as many live invitations as the plugin allows. Nothing was written.</response>
    /// <response code="503">This plugin has no data directory, so there is no store to write to.</response>
    /// <returns>The code and the record.</returns>
    [Authorize(Policy = "RequiresElevation")]
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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
        catch (LiveCeilingReachedException refused)
        {
            // A different code because it is a different kind of refusal. The
            // request was acceptable and the store's state was not, so the
            // operator's repair is to revoke an invitation rather than to change
            // what they asked for, and 400 would send them at the wrong one.
            return Conflict(refused.Message);
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

        return Ok(InvitationView.Of(_operations.All(), _accounts.Identifiers));
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

        return found is null ? NotFound() : Ok(InvitationView.Of(found, _accounts.Identifiers));
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

        return revoked is null ? NotFound() : Ok(InvitationView.Of(revoked, _accounts.Identifiers));
    }

    /// <summary>
    /// Says what rotating the hash secret would cost, and rotates it when the
    /// caller sends that cost back.
    /// </summary>
    /// <param name="request">
    /// Empty to ask what a rotation would cost. Carrying the count from such an
    /// answer to rotate against it.
    /// </param>
    /// <response code="200">
    /// The plan, or the receipt. <c>Rotated</c> says which.
    /// </response>
    /// <response code="409">
    /// The store holds a different number of records than the caller
    /// confirmed. Nothing was written.
    /// </response>
    /// <response code="503">This plugin has no data directory.</response>
    /// <returns>What the rotation costs, or cost.</returns>
    /// <remarks>
    /// <para>
    /// <b>The first call cannot be skipped.</b> The only way to rotate is to
    /// send back a number this route gave out, so an interface cannot rotate
    /// without having put the cost in front of somebody, and #30's clause about
    /// saying what it will do before it does it is held by the shape rather
    /// than by whoever writes the page.
    /// </para>
    /// <para>
    /// <b>A conflict rather than a bad request when the count has moved.</b>
    /// The caller sent a number that was right when they were shown it, so
    /// nothing about the request is malformed: the store changed underneath.
    /// The repair is to ask again, which the message says.
    /// </para>
    /// <para>
    /// Rotation touches no account and removes no record. It makes every stored
    /// hash unverifiable, which is what makes it the operator's answer to a
    /// leaked key, and docs/api.md carries the same sentence.
    /// </para>
    /// </remarks>
    [Authorize(Policy = "RequiresElevation")]
    [HttpPost("HashSecret/Rotate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<RotationView> Rotate([FromBody] RotateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_operations.StoreIsAvailable)
        {
            return NoStore();
        }

        if (request.Invalidates is null)
        {
            return Ok(RotationView.Planned(_operations.PlanRotation()));
        }

        try
        {
            return Ok(RotationView.Done(_operations.Rotate(request.Invalidates.Value)));
        }
        catch (InvalidOperationException refused)
        {
            // The refusal is the routine's, and its message carries both counts
            // and what to do. Replacing it here would be a second opinion about
            // an event this type cannot see.
            return Conflict(refused.Message);
        }
    }

    private ObjectResult NoStore() => StatusCode(
        StatusCodes.Status503ServiceUnavailable,
        "This plugin has no data directory on this server, so there is no invitation store to read or write.");
}
