using System;

namespace Jellyfin.Plugin.Invites.Server;

/// <summary>
/// What the start-up comparison between the declared line and the running
/// server found, and the sentence an operator is given for it.
/// </summary>
/// <remarks>
/// The message is built once here rather than at each place that reports it.
/// #97 asks that a mismatch be reported with a message naming both versions,
/// and the plugin has three places that report one: the log at start-up, the
/// refusal every route answers with, and a test. Three sentences for one fact
/// is two of them going stale.
/// </remarks>
public sealed class ServerLineVerdict
{
    private ServerLineVerdict(bool matches, string declared, string running, string message)
    {
        Matches = matches;
        Declared = declared;
        Running = running;
        Message = message;
    }

    /// <summary>
    /// Gets a value indicating whether the running server is on the declared line.
    /// </summary>
    public bool Matches { get; }

    /// <summary>
    /// Gets the line this plugin was built for, as <c>major.minor</c>.
    /// </summary>
    public string Declared { get; }

    /// <summary>
    /// Gets what the running server reported, verbatim, or a sentence fragment
    /// saying it reported nothing.
    /// </summary>
    public string Running { get; }

    /// <summary>
    /// Gets the sentence naming both versions and what follows from them.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// The verdict for a server on the declared line.
    /// </summary>
    /// <param name="declared">The declared line.</param>
    /// <param name="running">What the running server reported.</param>
    /// <returns>The verdict.</returns>
    public static ServerLineVerdict Agreeing(string declared, Version running)
    {
        ArgumentNullException.ThrowIfNull(running);

        var reported = running.ToString();
        return new ServerLineVerdict(
            true,
            declared,
            reported,
            "This plugin is built for the Jellyfin " + declared + " line and this server reports " + reported + ", which is on it.");
    }

    /// <summary>
    /// The verdict for a server on some other line.
    /// </summary>
    /// <param name="declared">The declared line.</param>
    /// <param name="running">What the running server reported.</param>
    /// <returns>The verdict.</returns>
    public static ServerLineVerdict Disagreeing(string declared, Version running)
    {
        ArgumentNullException.ThrowIfNull(running);

        var reported = running.ToString();
        return new ServerLineVerdict(
            false,
            declared,
            reported,
            "This plugin is built for the Jellyfin "
            + declared
            + " line and this server reports "
            + reported
            + ". It does nothing on this server rather than working in part: it reaches server interfaces that move between lines, so a plugin that carried on would fail somewhere further in, at a moment chosen by whoever presented an invitation. Install the build for the line this server runs. Invitations already sent are unaffected, because an expiry is an absolute instant and keeps running while this plugin does not.");
    }

    /// <summary>
    /// The verdict for a server that reports no version at all.
    /// </summary>
    /// <param name="declared">The declared line.</param>
    /// <returns>The verdict.</returns>
    /// <remarks>
    /// This refuses, and the direction is deliberate. A server that will not say
    /// what it is has not been shown to be on the declared line, and treating an
    /// unanswered question as agreement is how a comparison ends up passing
    /// everything the day the member it reads is renamed.
    /// </remarks>
    public static ServerLineVerdict Unanswered(string declared)
    {
        return new ServerLineVerdict(
            false,
            declared,
            "nothing",
            "This plugin is built for the Jellyfin "
            + declared
            + " line and this server reports no version at all, so it has not been shown to be on that line. It does nothing on this server rather than working in part. A server this plugin knows answers for its own version; one that does not is one this plugin has not been built for, and saying so is better than assuming.");
    }
}
