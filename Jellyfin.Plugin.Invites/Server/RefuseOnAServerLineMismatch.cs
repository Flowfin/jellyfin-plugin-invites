using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jellyfin.Plugin.Invites.Server;

/// <summary>
/// Answers every action of this plugin with a refusal while the running server
/// is not on the line the plugin was built for.
/// </summary>
/// <remarks>
/// <para>
/// <b>Refusing rather than unregistering, and the difference is stated because
/// #97's wording does not settle it.</b> That issue asks that a mismatch
/// "disable every route". A plugin's controllers are discovered from its
/// assembly by the server's own routing, and nothing a plugin does afterwards
/// takes an address back out of a route table that is already built. So what is
/// available is that every address answers a refusal, and that is what this is.
/// The addresses continue to exist and none of them does anything.
/// </para>
/// <para>
/// <b>It short-circuits before the action runs.</b> The result is set in
/// <see cref="OnActionExecuting"/>, which is what stops the action being
/// entered at all rather than letting it run and discarding its answer.
/// </para>
/// <para>
/// <b>It is one filter and not a line in six actions.</b>
/// <see cref="ThisPluginsControllers"/> attaches it to every controller this
/// assembly holds, so a controller added later carries it without anybody
/// remembering to. A refusal written action by action is a rule in as many
/// places as there are actions, and one of them is the one that gets forgotten.
/// </para>
/// <para>
/// <b>What it says.</b> The message names both versions, which is the clause of
/// #97 this carries, and it is the same sentence the log line at start-up
/// carries because both read it off <see cref="ServerLineVerdict"/>.
/// </para>
/// <para>
/// <b>Why this is not an oracle.</b> The redemption route answers one thing for
/// every presented invitation by design, so that a stranger cannot learn from
/// the response whether an invitation exists. This refusal is louder than that
/// one and tells a stranger something: that the plugin will not run here. It
/// discloses nothing about any invitation, it is identical for every request
/// including one carrying nothing at all, and on a server in this state there is
/// no invitation to disclose anything about, because nothing was minted and
/// nothing can be redeemed.
/// </para>
/// </remarks>
public sealed class RefuseOnAServerLineMismatch : IActionFilter
{
    /// <summary>
    /// The content type the refusal is written as.
    /// </summary>
    /// <remarks>
    /// Plain text rather than the shape of whichever action was asked for. One
    /// of this plugin's routes serves a page and the rest answer with objects,
    /// and a refusal that imitated each of them would be several refusals.
    /// </remarks>
    public const string ContentType = "text/plain; charset=utf-8";

    private readonly ServerLineGate _gate;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefuseOnAServerLineMismatch"/> class.
    /// </summary>
    /// <param name="gate">The verdict taken when the server started.</param>
    public RefuseOnAServerLineMismatch(ServerLineGate gate)
    {
        _gate = gate;
    }

    /// <inheritdoc />
    public void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_gate.MayRun)
        {
            return;
        }

        context.Result = new ContentResult
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable,
            ContentType = ContentType,
            Content = _gate.Verdict.Message,
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// Nothing. An action that was refused never ran, and one that ran was on a
    /// server this plugin is built for.
    /// </remarks>
    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
