using System;
using MediaBrowser.Common;

namespace Jellyfin.Plugin.Invites.Server;

/// <summary>
/// The running server's version, read off the host the server hands the plugin.
/// </summary>
/// <remarks>
/// <para>
/// <b>This one does not bind late, and that is a decision rather than an
/// oversight.</b> <see cref="Accounts.ServerAccounts"/> reaches its member by
/// name because that member is a property on the declared floor, which is the
/// version the plugin compiles against, and a method on the newest release of
/// the line, so no source form compiles against both. <c>ApplicationVersion</c>
/// is not in that state: it is the same member on both, which the build against
/// the floor and the server jobs on the newest release hold rather than this
/// sentence.
/// A reflection call here would buy nothing and would put the one thing that
/// decides whether the plugin runs at all behind a string.
/// </para>
/// <para>
/// <b>If it ever does move, this is the first thing that breaks and it breaks
/// loudly.</b> Every build compiles this file against the oldest server the
/// manifest invites somebody to install on, so a member that moved inside the
/// line fails there rather than on somebody's server.
/// </para>
/// </remarks>
public sealed class RunningServer : IRunningServer
{
    private readonly IApplicationHost _host;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunningServer"/> class.
    /// </summary>
    /// <param name="host">The server's own application host.</param>
    public RunningServer(IApplicationHost host)
    {
        _host = host;
    }

    /// <inheritdoc />
    public Version? Version => _host.ApplicationVersion;
}
