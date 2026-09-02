using System;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.Invites.Accounts;

/// <summary>
/// The one routine that turns an honoured redemption into an account on the
/// server.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order is the whole of what this type is.</b> Create the account, set
/// its credential, apply the template. An account that exists with no
/// credential and the server's own default policy is a window somebody can sign
/// in through, and every other order leaves it standing open for as long as the
/// next call takes. So the order is asserted by a test over what the routine
/// asked the server for, rather than by a test over the state at the end, which
/// cannot tell the three orders apart.
/// </para>
/// <para>
/// <b>It writes no policy field.</b> The template goes to the seam and
/// <see cref="AccountTemplateApplication"/> is the one routine that writes a
/// grant, which two rules in the invariant lint refuse a second place for.
/// Nothing here names a user policy at all.
/// </para>
/// <para>
/// <b>What it deliberately does not take.</b> It is handed no invitation. A
/// redemption is honoured by <c>RedemptionDecision</c> before this is reached,
/// and a routine holding an invitation it never reads is a routine that looks
/// as though it checked one. What the invitation contributes is the template,
/// and the template is the parameter.
/// </para>
/// <para>
/// <b>What is not here, named so this is not read as holding it.</b> Spending
/// the use under a lock, the intent written before the account is created and
/// cleared afterwards, and the refusal of a username the server would reject or
/// one that collides, all happen around this routine rather than inside it and
/// belong to the issues that own them. This one does the three acts in the one
/// order that is safe, and its caller is what decides whether they should
/// happen at all.
/// </para>
/// <para>
/// <b>What a failure part-way leaves.</b> Nothing is undone. A refusal from the
/// credential arm leaves an account with no credential and the server's default
/// policy, and a refusal from the template arm leaves one with a credential and
/// that same default policy. Neither is repaired here, because a routine that
/// deleted an account to tidy up would be a plugin that can delete accounts,
/// which is a larger power than the one this issue asked for. The residual is
/// disclosed rather than closed, and the direction of it belongs to the issue
/// that owns the intent record.
/// </para>
/// </remarks>
public static class AccountCreation
{
    /// <summary>
    /// Creates the account an invitation redeems into.
    /// </summary>
    /// <param name="server">The write seam over the server's user table.</param>
    /// <param name="username">The name the person chose.</param>
    /// <param name="password">The credential the person chose.</param>
    /// <param name="template">The grant the invitation carried.</param>
    /// <returns>The identifier of the account that now exists.</returns>
    /// <exception cref="ArgumentNullException">
    /// The seam, the username, the password or the template is null.
    /// </exception>
    /// <remarks>
    /// The identifier comes back because the caller has to record which account
    /// this invitation produced, and reading it back off the server afterwards
    /// would be a second lookup that could answer about somebody else's account
    /// of the same name.
    /// </remarks>
    public static async Task<Guid> CreateAsync(
        IServerAccountWrites server,
        string username,
        string password,
        AccountTemplate template)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(template);

        var account = await server.CreateAccountAsync(username).ConfigureAwait(false);

        await server.SetCredentialAsync(account, password).ConfigureAwait(false);

        await server.ApplyTemplateAsync(account, template).ConfigureAwait(false);

        return account;
    }
}
