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
/// <b>The ceiling is refused here rather than at whoever calls this.</b> #62
/// asks for two refusals inside the creation routine rather than as validation
/// on the way in, so that a later caller which skips the validation still meets
/// them. The first is a template asking for an account that manages the server,
/// and it is refused before anything is created. The second is touching an
/// account that already exists, and it is refused by shape rather than by a
/// test at run time: nothing here takes an account identifier, so there is no
/// account to be pointed at except the one the creation just made.
/// </para>
/// <para>
/// <b>Why the administrator case refuses rather than quietly dropping the
/// grant.</b> <see cref="AccountTemplateApplication"/> writes no field for
/// <see cref="AccountTemplate.MayManage"/>, so a template asking for one would
/// otherwise produce an ordinary account and nobody would be told. An operator
/// who minted that template believes they invited an administrator, and the
/// invitation is worth something different from what they think it is worth
/// until somebody notices. A refusal at the moment of creation is what makes
/// them notice.
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
    /// <exception cref="ArgumentException">
    /// The template says the account may manage the server.
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

        RefuseAnAccountThatWouldManageTheServer(template);

        var account = await server.CreateAccountAsync(username).ConfigureAwait(false);

        await server.SetCredentialAsync(account, password).ConfigureAwait(false);

        await server.ApplyTemplateAsync(account, template).ConfigureAwait(false);

        return account;
    }

    /// <summary>
    /// Refuses a template whose account would manage the server.
    /// </summary>
    /// <param name="template">The grant the invitation carried.</param>
    /// <remarks>
    /// <para>
    /// It is checked before the first call rather than after, so a refused
    /// template leaves nothing on the server. A refusal raised afterwards would
    /// leave an ordinary account behind and would still not have made an
    /// administrator, which is the worst of the three outcomes: the ceiling
    /// held and somebody has an account they were not meant to get.
    /// </para>
    /// <para>
    /// The message says what the template asked for and never what the account
    /// would have been given, because there is nothing to give: no field of the
    /// server's policy carries this grant, which
    /// <see cref="AccountTemplateApplication"/> states from its side.
    /// </para>
    /// </remarks>
    private static void RefuseAnAccountThatWouldManageTheServer(AccountTemplate template)
    {
        if (template.MayManage)
        {
            throw new ArgumentException(
                "The template says the account it creates may manage the server, and no account an invitation creates is an administrator, whatever the template says. Nothing was created. Mint the invitation against a template that manages nothing, and give an administrator an account through the server's own user page.",
                nameof(template));
        }
    }
}
