namespace Jellyfin.Plugin.Invites.Controllers;

/// <summary>
/// What became of an account an invitation claims to have created, as a route
/// reports it.
/// </summary>
/// <remarks>
/// <para>
/// #45 decided that a record keeps its pointer at an account that has been
/// deleted rather than clearing it, on the argument that clearing it loses the
/// answer to the question an operator is actually asking. That decision is only
/// worth anything if the absence is then said out loud: a deleted account
/// rendered as an identifier looks exactly like a live one, which is the blank
/// the decision refuses.
/// </para>
/// <para>
/// <b>There are three states and not two, and the third is the one that matters.</b>
/// <see cref="Jellyfin.Plugin.Invites.Accounts.IServerAccounts"/> answers
/// <c>null</c> where this server does not report its accounts in a shape this
/// plugin knows, and a route that read that as an empty list would tell an
/// operator that every account the plugin ever created has been deleted. So the
/// unanswered case is its own value rather than being folded into either
/// answer.
/// </para>
/// </remarks>
public enum AccountPresence
{
    /// <summary>
    /// The server was not asked in a shape that answers, so nothing is claimed
    /// in either direction. This is what a caller gets when the server does not
    /// report its accounts.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The server has an account with this identifier.
    /// </summary>
    Present = 1,

    /// <summary>
    /// The server does not have an account with this identifier. The record
    /// keeps its pointer and this is what the pointer now means.
    /// </summary>
    Gone = 2,
}
