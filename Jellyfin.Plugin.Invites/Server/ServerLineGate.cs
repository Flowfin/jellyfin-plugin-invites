namespace Jellyfin.Plugin.Invites.Server;

/// <summary>
/// The one verdict this plugin runs on, computed once when the server starts.
/// </summary>
/// <remarks>
/// <para>
/// <b>Once, not per request.</b> A server does not change version underneath a
/// running process, so asking again on every request would be the same answer
/// bought repeatedly. More than that, it would be a rule that could answer two
/// different things inside one server's lifetime, and the refusal this gate
/// carries is meant to be a property of the installation rather than of the
/// moment a request arrived.
/// </para>
/// <para>
/// <b>It is a registered singleton so there is one of it.</b> Every route reads
/// this, the start-up load reads this, and the log line naming both versions is
/// written from this. A second place that compared for itself would be a second
/// authority for whether the plugin runs, and the two would disagree the first
/// time one of them was changed.
/// </para>
/// </remarks>
public sealed class ServerLineGate
{
    private readonly ServerLineVerdict _verdict;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerLineGate"/> class.
    /// </summary>
    /// <param name="server">The running server.</param>
    public ServerLineGate(IRunningServer server)
        : this(DeclaredLine.Value, server)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerLineGate"/> class
    /// against a stated line.
    /// </summary>
    /// <param name="declared">The line to judge against.</param>
    /// <param name="server">The running server.</param>
    /// <remarks>
    /// The line is a parameter here so a test can drive both sides of the
    /// comparison. Nothing in the plugin calls this: the server builds the
    /// constructor above, which reads the declared line off the assembly.
    /// </remarks>
    public ServerLineGate(string declared, IRunningServer server)
    {
        _verdict = ServerLine.Judge(declared, server is null ? null : server.Version);
    }

    /// <summary>
    /// Gets the verdict, naming both versions.
    /// </summary>
    public ServerLineVerdict Verdict => _verdict;

    /// <summary>
    /// Gets a value indicating whether this plugin may do anything at all on
    /// this server.
    /// </summary>
    public bool MayRun => _verdict.Matches;
}
