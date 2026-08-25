using System;

namespace Jellyfin.Plugin.Invites.Server;

/// <summary>
/// What version the server this plugin is loaded into reports for itself.
/// </summary>
/// <remarks>
/// <para>
/// One member, and it reads. This is the same shape and the same reason as
/// <see cref="Accounts.IServerAccounts"/>: a seam that asks the server exactly
/// one question, so that the question can be answered by a test without a
/// server and so that nothing in this plugin can grow a second use for the
/// handle it holds.
/// </para>
/// <para>
/// It answers <c>null</c> where the server does not say. A server that will not
/// name itself is not a server this plugin can claim to have been built for, and
/// <see cref="ServerLine"/> refuses on that rather than guessing.
/// </para>
/// </remarks>
public interface IRunningServer
{
    /// <summary>
    /// Gets the version the running server reports, or <c>null</c> where it
    /// reports none.
    /// </summary>
    Version? Version { get; }
}
