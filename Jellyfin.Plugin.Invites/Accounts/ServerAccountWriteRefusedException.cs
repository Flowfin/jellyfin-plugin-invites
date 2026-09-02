using System;
using System.Globalization;

namespace Jellyfin.Plugin.Invites.Accounts;

/// <summary>
/// Raised where the server this plugin is loaded on does not answer for a write
/// the plugin was built to make.
/// </summary>
/// <remarks>
/// <para>
/// It is the write side of what <see cref="ServerAccounts"/> reports by handing
/// back nothing: a user manager whose members are not the ones this plugin was
/// built for. On the read side the caller can say so and carry on, because the
/// question was a comparison. Here it cannot: the account is half made, and
/// carrying on would leave exactly the state the ordering in
/// <see cref="AccountCreation"/> exists to prevent.
/// </para>
/// <para>
/// So this stops the routine where it is and says which member was looked for.
/// What is left behind is disclosed rather than repaired: nothing here deletes
/// an account, and a plugin that cannot set a credential is not one that should
/// be trusted to remove a user.
/// </para>
/// </remarks>
public class ServerAccountWriteRefusedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServerAccountWriteRefusedException"/> class.
    /// </summary>
    public ServerAccountWriteRefusedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerAccountWriteRefusedException"/> class.
    /// </summary>
    /// <param name="message">What went wrong.</param>
    public ServerAccountWriteRefusedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerAccountWriteRefusedException"/> class.
    /// </summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">What it happened during.</param>
    public ServerAccountWriteRefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// The refusal for a member this plugin looks up by name and does not find
    /// in a shape it knows.
    /// </summary>
    /// <param name="member">The member that was looked for.</param>
    /// <param name="shapes">The shapes this plugin was built to call it in.</param>
    /// <returns>The refusal, naming the member and both shapes.</returns>
    public static ServerAccountWriteRefusedException NoSuchMember(string member, string shapes)
    {
        return new ServerAccountWriteRefusedException(string.Format(
            CultureInfo.InvariantCulture,
            "The server's user manager carries no {0} this plugin can call. It was looked for as {1}. This is a server outside the line the manifest declares, and reporting that is better than guessing at a call.",
            member,
            shapes));
    }

    /// <summary>
    /// The refusal for a call that answered something the routine cannot use.
    /// </summary>
    /// <param name="member">The member that was called.</param>
    /// <param name="wanted">What the routine needed back from it.</param>
    /// <returns>The refusal, naming the member and what was wanted.</returns>
    public static ServerAccountWriteRefusedException AnsweredNothingUsable(string member, string wanted)
    {
        return new ServerAccountWriteRefusedException(string.Format(
            CultureInfo.InvariantCulture,
            "{0} answered without {1}. The routine has nothing to address the account it just asked for, so it stops here rather than carrying on against an identifier it made up.",
            member,
            wanted));
    }
}
