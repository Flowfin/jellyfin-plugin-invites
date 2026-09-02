using System;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Invites.Accounts;

/// <summary>
/// The three things this plugin may do to the server's user table, and nothing
/// else.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is separate from <see cref="IServerAccounts"/> on purpose.</b> That
/// seam reads and its own suite refuses a member on it that takes an argument
/// or hands nothing back, which is what a write looks like. Widening it would
/// delete that refusal in the same edit that made it wrong. So the write side
/// is a second interface whose surface is the whole of what a redemption is
/// allowed to do, and a fourth member here is a change somebody argues rather
/// than one that arrives beside a read.
/// </para>
/// <para>
/// <b>What is deliberately absent.</b> There is no delete, no disable, no
/// rename and no way to change an account that already exists. #91 asks that
/// removing this plugin touch no account, and the reason that holds is that
/// nothing here can reach one: every member below is about the account this
/// redemption is creating, addressed by the identifier the creation handed
/// back.
/// </para>
/// <para>
/// <b>No user policy is named here.</b> The template is what a caller hands in,
/// and turning a template into a policy is #69's one routine. A policy on this
/// surface would be a second place a grant can be built and travel, which is
/// what <c>OneRoutineNamesAUserPolicyTests</c> refuses.
/// </para>
/// <para>
/// The members are ordered the way <see cref="AccountCreation"/> calls them,
/// and that order is the security property rather than a convenience: an
/// account that exists with no credential and the server's default policy is a
/// window somebody can sign in through.
/// </para>
/// </remarks>
public interface IServerAccountWrites
{
    /// <summary>
    /// Creates an account with the given name and hands back its identifier.
    /// </summary>
    /// <param name="username">The name the account is created under.</param>
    /// <returns>The identifier the server gave the new account.</returns>
    /// <remarks>
    /// It hands back an identifier rather than the server's own account object,
    /// so that nothing downstream of it holds a value it could write a field
    /// onto.
    /// </remarks>
    Task<Guid> CreateAccountAsync(string username);

    /// <summary>
    /// Sets the credential of an account this redemption created.
    /// </summary>
    /// <param name="account">The identifier the creation handed back.</param>
    /// <param name="password">The credential the person chose.</param>
    /// <returns>A task that completes when the server has taken it.</returns>
    Task SetCredentialAsync(Guid account, string password);

    /// <summary>
    /// Writes an account template onto the policy the server gave an account
    /// this redemption created.
    /// </summary>
    /// <param name="account">The identifier the creation handed back.</param>
    /// <param name="template">The grant the invitation carried.</param>
    /// <returns>A task that completes when the server has taken the policy.</returns>
    /// <remarks>
    /// The template goes in and no policy comes out. What happens to the policy
    /// in between is <see cref="AccountTemplateApplication"/>'s, and a caller
    /// that wanted to see the result would be a second reader of a grant.
    /// </remarks>
    Task ApplyTemplateAsync(Guid account, AccountTemplate template);
}
