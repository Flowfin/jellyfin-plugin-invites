using System;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Invites.Accounts;

/// <summary>
/// Whether a name is taken, asked of the server's own user manager.
/// </summary>
/// <remarks>
/// <para>
/// <b>It asks the member the creation call asks, so the two cannot disagree.</b>
/// The server refuses a colliding name inside <c>CreateUserAsync</c>, and the
/// comparison it makes there is the one <c>GetUserByName</c> makes. Read off the
/// server's own source at the two tags this build resolves, the floor the
/// manifest declares and the shipping version:
/// </para>
/// <para>
/// At <c>v10.11.0</c> the creation call refuses on
/// <c>Users.Any(u =&gt; u.Username.Equals(name, StringComparison.OrdinalIgnoreCase))</c>
/// and <c>GetUserByName</c> answers with
/// <c>_users.Values.FirstOrDefault(u =&gt; string.Equals(u.Username, name, StringComparison.OrdinalIgnoreCase))</c>.
/// At <c>v10.11.11</c> the creation call refuses on
/// <c>NormalizedUsername == name.ToUpperInvariant()</c> and <c>GetUserByName</c>
/// answers on the same expression. Both are case-insensitive and both are the
/// same comparison as the create at their own version.
/// </para>
/// <para>
/// <b>This one does not bind late, and that is a reading rather than an
/// assumption.</b> <see cref="ServerAccounts"/> exists because the member
/// answering it is a property on the floor and a method on the shipping version.
/// <c>GetUserByName(string)</c> is declared on <c>IUserManager</c> at both tags,
/// in the same shape, so an ordinary call compiles against the floor and runs on
/// both. If that ever stops being true the failure is a missing method at run
/// time, which is the same failure the other seam was built to avoid, and the
/// repair is the same reflection it uses.
/// </para>
/// <para>
/// <b>What it does with a name the shape rule would refuse.</b> Nothing: it
/// answers that the name is not taken. The server throws on a null or blank name
/// rather than answering, and turning that into an exception on the redemption
/// route would answer a probe with a stack rather than with the refusal it has
/// already earned. <see cref="Setup.UsernameRules"/> refuses such a name before
/// this is asked, and this is not a second authority on shapes.
/// </para>
/// </remarks>
public sealed class ServerAccountNames : IServerAccountNames
{
    /// <summary>
    /// The member name this asks the server for, held as a constant so a guard
    /// can read it rather than being told it.
    /// </summary>
    public const string TheMember = "GetUserByName";

    private readonly IUserManager _users;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerAccountNames"/> class.
    /// </summary>
    /// <param name="users">The server's user manager.</param>
    /// <exception cref="ArgumentNullException">The user manager is null.</exception>
    public ServerAccountNames(IUserManager users)
    {
        ArgumentNullException.ThrowIfNull(users);

        _users = users;
    }

    /// <inheritdoc />
    public bool IsTaken(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        return _users.GetUserByName(username) is not null;
    }
}
