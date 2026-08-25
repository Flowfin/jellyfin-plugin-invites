using System;

namespace Jellyfin.Plugin.Invites.Server;

/// <summary>
/// The comparison between the line this plugin declares and the server it finds
/// itself running on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Equality on the major and minor parts, not a floor.</b> #97 took that on
/// 2026-08-20 and it is the whole of the rule. <c>targetAbi</c> is the oldest
/// server of the declared line, and read as a floor alone it lets a server two
/// lines newer load this plugin and find out what breaks at run time. Equality
/// turns that into a refusal at start-up.
/// </para>
/// <para>
/// <b>The price, which is not softened here.</b> An operator who upgrades to the
/// next line has a plugin that stops rather than one that half works, so
/// invitations already sent cannot be redeemed until a build for that line is
/// installed. #97 accepts that: a plugin that half works on an authentication
/// surface is the worse of the two, and a claim to support a line the plugin has
/// never been built against is a claim nobody checked.
/// </para>
/// <para>
/// <b>It is one function so there is one authority.</b> The verdict is computed
/// once at start-up by <see cref="ServerLineGate"/> and read from there by the
/// log line and by every route, rather than each of them comparing for itself.
/// </para>
/// </remarks>
public static class ServerLine
{
    /// <summary>
    /// Judges a running server against a declared line.
    /// </summary>
    /// <param name="declared">The declared line, as <c>major.minor</c>.</param>
    /// <param name="running">What the running server reports, or <c>null</c>.</param>
    /// <returns>The verdict, with the sentence an operator is given.</returns>
    /// <exception cref="ArgumentNullException">The declared line is null.</exception>
    public static ServerLineVerdict Judge(string declared, Version? running)
    {
        ArgumentNullException.ThrowIfNull(declared);

        if (running is null)
        {
            return ServerLineVerdict.Unanswered(declared);
        }

        var reported = running.Major.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "."
            + running.Minor.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return string.Equals(reported, declared, StringComparison.Ordinal)
            ? ServerLineVerdict.Agreeing(declared, running)
            : ServerLineVerdict.Disagreeing(declared, running);
    }
}
