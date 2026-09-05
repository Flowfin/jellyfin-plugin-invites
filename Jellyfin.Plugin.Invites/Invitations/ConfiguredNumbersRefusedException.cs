using System;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// A configured number cannot be used, so the routine that asked for it has
/// nothing to work from.
/// </summary>
/// <remarks>
/// The same shape and the same reason as
/// <see cref="Invitations.ConfiguredTemplatesRefusedException"/>: a setting
/// outside its range is refused rather than replaced with the value the plugin
/// would have used, because a silent fallback on a bound is the bound gone. The
/// message is the one <see cref="NumberSettings"/> wrote, naming the setting and
/// the rule it missed, so whoever meets the refusal is told what to repair.
/// </remarks>
public sealed class ConfiguredNumbersRefusedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfiguredNumbersRefusedException"/> class.
    /// </summary>
    public ConfiguredNumbersRefusedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfiguredNumbersRefusedException"/> class.
    /// </summary>
    /// <param name="message">The refusal, as <see cref="NumberSettings"/> wrote it.</param>
    public ConfiguredNumbersRefusedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfiguredNumbersRefusedException"/> class.
    /// </summary>
    /// <param name="message">The refusal, as <see cref="NumberSettings"/> wrote it.</param>
    /// <param name="innerException">The exception underneath.</param>
    public ConfiguredNumbersRefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
