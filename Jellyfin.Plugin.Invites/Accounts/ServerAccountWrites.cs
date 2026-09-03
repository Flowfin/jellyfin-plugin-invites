using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Invites.Accounts;

/// <summary>
/// The write side of this plugin's contact with the server's user table.
/// </summary>
/// <remarks>
/// <para>
/// <b>One of the arms binds late and the others do not, and that was measured
/// rather than decided.</b> Read off both ends of the line: the floor the
/// plugin manifest's target ABI names, which this build resolves, and the
/// newest release, which it resolved too until #155 moved it to the floor.
/// </para>
/// <para>
/// <c>CreateUserAsync(name)</c>, <c>GetUserById(identifier)</c>,
/// <c>GetUserDto(account)</c> and <c>UpdatePolicyAsync(identifier, policy)</c>
/// are one name and one signature at both ends, so they are called directly and
/// the compiler and the ABI floor build are what judge them.
/// </para>
/// <para>
/// <c>ChangePassword</c> is not. It takes the account entity on the floor and
/// the account identifier on the shipping version, so no source form compiles
/// against both and a call compiled against either throws on the other at run
/// time. That is the same shape <see cref="ServerAccounts"/> exists for, and it
/// is the arm that carries the password.
/// </para>
/// <para>
/// <b>What the ABI floor build covers here, and what it does not.</b> It
/// compiles this file against the floor, so a direct call whose member moved
/// reds the build instead of a server. It says nothing about
/// <see cref="SetCredentialOn"/>, which reaches its member by name: that arm is
/// judged by the two stand-ins in the suite, one carrying each shape.
/// </para>
/// <para>
/// <b>Why no test drives the direct arms.</b> A fake user manager would have to
/// implement <see cref="IUserManager"/>, and <c>ChangePassword</c> is a member
/// of it, so such a fake compiles against exactly one end of the declared line
/// and reds the floor build at the other. That is a property of the interface
/// rather than a gap somebody chose, it is why the reflective arm takes an
/// <see cref="object"/>, and the direct arms are held by the compiler, by the
/// floor build and by a reader instead.
/// </para>
/// <para>
/// <b>Where the policy comes from.</b> Nothing on the user manager and nothing
/// on the account entity hands back the policy the server made. The one route
/// is the account view, whose policy field is what
/// <see cref="AccountTemplateApplication"/> is handed and what goes back to the
/// server afterwards. So this file names no policy on any member it declares
/// and builds none: it carries the server's own between two calls.
/// </para>
/// </remarks>
public sealed class ServerAccountWrites : IServerAccountWrites
{
    /// <summary>
    /// The member reached by name.
    /// </summary>
    /// <remarks>
    /// It is a constant so a test can read back what this binds to rather than a
    /// second copy of the name, which is the shape <see cref="ServerAccounts"/>
    /// already uses.
    /// </remarks>
    private const string TheCredential = "ChangePassword";

    /// <summary>
    /// The member the credential arm looks up where the server wants the account
    /// entity rather than its identifier.
    /// </summary>
    private const string TheLookup = "GetUserById";

    private readonly IUserManager _users;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerAccountWrites"/> class.
    /// </summary>
    /// <param name="users">The server's user manager.</param>
    public ServerAccountWrites(IUserManager users)
    {
        _users = users;
    }

    /// <summary>
    /// What this plugin was built to call, named so a refusal can say what it
    /// was looking for.
    /// </summary>
    /// <returns>The two shapes of the credential member, as a sentence fragment.</returns>
    public static string WhatWasLookedFor()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}(account, password) or {0}(identifier, password)",
            TheCredential);
    }

    /// <summary>
    /// Sets a credential on whatever was handed in, in whichever of the two
    /// shapes it carries.
    /// </summary>
    /// <param name="users">The user manager.</param>
    /// <param name="account">The identifier the creation handed back.</param>
    /// <param name="password">The credential the person chose.</param>
    /// <returns>A task that completes when the server has taken it.</returns>
    /// <remarks>
    /// It takes an <see cref="object"/> for the reason
    /// <see cref="ServerAccounts.Of"/> does: the whole question is what a member
    /// reflection finds, and a stand-in carrying one shape asks it exactly. A
    /// fake of the server's own interface could not carry both.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The user manager is null.</exception>
    /// <exception cref="ServerAccountWriteRefusedException">
    /// The user manager carries the member in neither shape, or the account
    /// lookup the older shape needs answers nothing.
    /// </exception>
    public static async Task SetCredentialOn(object users, Guid account, string password)
    {
        ArgumentNullException.ThrowIfNull(users);

        var method = Array.Find(
            users.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance),
            candidate => string.Equals(candidate.Name, TheCredential, StringComparison.Ordinal)
                && candidate.GetParameters().Length == 2
                && candidate.GetParameters()[1].ParameterType == typeof(string));

        if (method is null)
        {
            throw ServerAccountWriteRefusedException.NoSuchMember(TheCredential, WhatWasLookedFor());
        }

        var addressed = method.GetParameters()[0].ParameterType == typeof(Guid)
            ? account
            : TheAccountItself(users, account);

        await Completed(method.Invoke(users, new[] { addressed, password })).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Guid> CreateAccountAsync(string username)
    {
        var created = await _users.CreateUserAsync(username).ConfigureAwait(false);

        return created is null
            ? throw ServerAccountWriteRefusedException.AnsweredNothingUsable("CreateUserAsync", "an account")
            : created.Id;
    }

    /// <inheritdoc />
    public Task SetCredentialAsync(Guid account, string password)
    {
        return SetCredentialOn(_users, account, password);
    }

    /// <inheritdoc />
    public async Task ApplyTemplateAsync(Guid account, AccountTemplate template)
    {
        var created = _users.GetUserById(account)
            ?? throw ServerAccountWriteRefusedException.AnsweredNothingUsable(TheLookup, "the account it just created");

        var granted = _users.GetUserDto(created)?.Policy
            ?? throw ServerAccountWriteRefusedException.AnsweredNothingUsable("GetUserDto", "the policy the server made");

        AccountTemplateApplication.ApplyTo(granted, template);

        await _users.UpdatePolicyAsync(account, granted).ConfigureAwait(false);
    }

    /// <summary>
    /// The account entity, for the shape of the credential member that wants one
    /// instead of an identifier.
    /// </summary>
    /// <param name="users">The user manager.</param>
    /// <param name="account">The identifier the creation handed back.</param>
    /// <returns>Whatever the lookup answered.</returns>
    private static object TheAccountItself(object users, Guid account)
    {
        var lookup = users.GetType().GetMethod(
            TheLookup,
            BindingFlags.Public | BindingFlags.Instance,
            new[] { typeof(Guid) });

        if (lookup is null)
        {
            throw ServerAccountWriteRefusedException.NoSuchMember(TheLookup, TheLookup + "(identifier)");
        }

        return lookup.Invoke(users, new object[] { account })
            ?? throw ServerAccountWriteRefusedException.AnsweredNothingUsable(TheLookup, "the account it just created");
    }

    /// <summary>
    /// Waits for whatever a looked-up member answered, where that is something
    /// to wait for.
    /// </summary>
    /// <param name="answered">What the call handed back.</param>
    /// <returns>A task that completes when it has.</returns>
    private static async Task Completed(object? answered)
    {
        if (answered is Task running)
        {
            await running.ConfigureAwait(false);
        }
    }
}
