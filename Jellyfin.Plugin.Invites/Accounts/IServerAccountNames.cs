namespace Jellyfin.Plugin.Invites.Accounts;

/// <summary>
/// Whether the server already holds an account under a name.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is a seam of its own rather than a member on
/// <see cref="IServerAccounts"/>.</b> That seam hands back identifiers and takes
/// nothing, and <c>AccountsAreNeverWrittenTests</c> refuses a method on it by
/// name, because a method taking an argument is the shape a create, a disable or
/// a delete arrives in. The question here cannot be asked without an argument,
/// so asking it there would mean relaxing that refusal for every future member.
/// A second interface keeps the refusal exact and puts this one under a guard of
/// its own.
/// </para>
/// <para>
/// <b>It asks about one name and never for a list.</b> The plugin does not need
/// to see the server's account names to answer this, and a member handing them
/// all back would be a route into every name on the server for the sake of one
/// comparison. What is seen is one boolean per attempted redemption.
/// docs/personal-data.md carries that as its own row.
/// </para>
/// <para>
/// <b>It is not a second authority on which names are allowed.</b>
/// <see cref="Setup.UsernameRules"/> holds the shape and this holds the
/// collision, and they are asked in that order: a name the shape refuses is
/// never presented here.
/// </para>
/// </remarks>
public interface IServerAccountNames
{
    /// <summary>
    /// Says whether the server already holds an account under this name.
    /// </summary>
    /// <param name="username">The name the person asked for.</param>
    /// <returns>
    /// <c>true</c> where the server holds an account whose name collides with
    /// this one, and <c>false</c> otherwise.
    /// </returns>
    /// <remarks>
    /// The comparison is the server's own and is case-insensitive at both ends
    /// of the line this plugin loads on, which is what makes the answer the same
    /// question the creation call will ask rather than an approximation of it.
    /// </remarks>
    bool IsTaken(string? username);
}
