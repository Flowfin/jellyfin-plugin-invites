using System;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// Raised when minting cannot copy a grant because the configured templates,
/// as a list, are ones the plugin refuses.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is not an argument fault, and that is why it is not an
/// <see cref="ArgumentException"/>.</b> Every value the caller sent may be
/// acceptable, the name they typed included; what refuses the mint is the
/// state of the plugin's own configuration, which
/// <see cref="Accounts.TemplateSettings"/> judges whole and never thins. The
/// same call made after an operator repairs the entry the message names
/// succeeds unchanged, and a refusal that told them to fix their request would
/// be pointing at the one thing that is not wrong. A name that matches no
/// configured template is the other case and stays an argument fault, because
/// the repair for that one is in what was asked.
/// </para>
/// <para>
/// The message is the sentence the load writes when the plugin starts, so an
/// operator meets one wording in the log and at the mint rather than two
/// descriptions of one fault. It names the setting, the position of the entry
/// and the rule it missed, and no label, for the reason that type gives.
/// </para>
/// </remarks>
public sealed class ConfiguredTemplatesRefusedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfiguredTemplatesRefusedException"/> class.
    /// </summary>
    public ConfiguredTemplatesRefusedException()
        : base("The account templates this plugin is configured with cannot be used as they stand, so no grant can be copied onto an invitation and nothing was minted.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfiguredTemplatesRefusedException"/> class.
    /// </summary>
    /// <param name="message">The refusal, as <see cref="Accounts.TemplateSettings"/> wrote it.</param>
    public ConfiguredTemplatesRefusedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfiguredTemplatesRefusedException"/> class.
    /// </summary>
    /// <param name="message">The refusal.</param>
    /// <param name="innerException">What went wrong underneath.</param>
    public ConfiguredTemplatesRefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
