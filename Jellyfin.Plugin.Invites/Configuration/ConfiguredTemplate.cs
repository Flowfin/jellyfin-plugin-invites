using System;

namespace Jellyfin.Plugin.Invites.Configuration;

/// <summary>
/// One named account template, as an operator writes it into this plugin's
/// configuration.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the stored shape of a grant and not the grant.</b> The server's own
/// configuration mechanism writes this plugin's settings to a file and reads
/// them back into public setters, so every member here is a plain settable
/// value and nothing here decides anything. The value the plugin acts on is
/// <see cref="Accounts.AccountTemplate"/>, which refuses the states that are
/// not a grant at all, and <see cref="Accounts.TemplateSettings"/> is the one
/// place that turns one of these into one of those.
/// </para>
/// <para>
/// <b>There is no way to write an account that manages the server.</b> #62
/// asks that a configuration asking for an administrator account be rejected
/// when the plugin loads. It is refused by shape instead of by a check: this
/// type has no member for that grant, so no element of the configuration file
/// spells it, and an element the server's reader does not know is dropped
/// rather than read. Every template that leaves here reaches the account
/// routine with that grant closed, and <c>ConfiguredTemplateTests</c> holds
/// this type to having no such member.
/// </para>
/// <para>
/// <b>What a member left out of the file is worth.</b> The posture #64
/// decided: closed unless the permission reaches nothing beyond the invited
/// person. Two are open on that rule and start open here, playing from outside
/// the network and changing the account's own display preferences, because an
/// invitation sent to somebody outside the household is worth nothing to them
/// without the first, and the second reaches nothing but the account itself.
/// Every other permission is closed until an operator opens it, and every
/// ceiling is absent, which is no ceiling rather than a ceiling of zero.
/// </para>
/// <para>
/// <b>What it does not carry, and why.</b> The server's policy fields a
/// template deliberately leaves alone, which
/// <see cref="Accounts.AccountTemplate.ServerDefaultsLeftAlone"/> exists for.
/// That list names fields of the server's own policy, which an operator
/// writing a template has no way to read, and a name that is wrong there is
/// refused where the template is written onto a created account, which is the
/// worst moment for it: the account exists by then. So a configured template
/// names none, and the field-by-field assertion over a created account derives
/// what was left alone from what the routine writes.
/// </para>
/// <para>
/// <b>Why the lists are arrays.</b> The server's reader fills public setters,
/// arrays included, and a list setting has no other shape both of its readers
/// fill: a settable collection is what one analyzer rule refuses, and a
/// read-only one is what the JSON reader behind the configuration page does
/// not fill unless told to. The rule against array properties is off for this
/// directory alone, in <c>.editorconfig</c>, with the reason beside it.
/// </para>
/// </remarks>
public sealed class ConfiguredTemplate
{
    /// <summary>
    /// Gets or sets the name an operator picks this template by when minting.
    /// </summary>
    /// <remarks>
    /// Names are compared ignoring case, so two templates whose labels differ
    /// only in case are one name written twice and are refused together. The
    /// name is what an operator reads; the value an invitation carries is a
    /// copy of the rest of this template, taken at minting, which is #61's
    /// rule and the reason editing a template afterwards changes no live
    /// invitation.
    /// </remarks>
    public string? Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the libraries the account may see, by identifier.
    /// </summary>
    /// <remarks>
    /// Named one at a time and never as all of them, for the reason on
    /// <see cref="Accounts.AccountTemplate.Libraries"/>. Empty grants no
    /// library, which is a template somebody chose rather than a mistake, and
    /// an absent element reads as empty.
    /// </remarks>
    public Guid[]? Libraries { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the account may download media.
    /// Closed unless opened, because a download cannot be undone by revocation.
    /// </summary>
    public bool MayDownload { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the account may play from
    /// outside the network. Open unless closed, because an invitation sent
    /// outside the household is worth nothing without it.
    /// </summary>
    public bool MayPlayFromOutsideTheNetwork { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the account may drive somebody
    /// else's session. Closed unless opened.
    /// </summary>
    public bool MayControlOtherSessions { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the account may watch live
    /// television. Closed unless opened.
    /// </summary>
    public bool MayWatchLiveTelevision { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the account may schedule and
    /// remove recordings. Closed unless opened.
    /// </summary>
    public bool MayManageLiveTelevision { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the account may delete media
    /// from the server. Closed unless opened.
    /// </summary>
    public bool MayDeleteContent { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the account may edit
    /// collections. Closed unless opened.
    /// </summary>
    public bool MayManageCollections { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the account may fetch and
    /// remove subtitles. Closed unless opened.
    /// </summary>
    public bool MayManageSubtitles { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the account may fetch and
    /// remove lyrics. Closed unless opened.
    /// </summary>
    public bool MayManageLyrics { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the account may change its own
    /// display preferences. Open unless closed, because it reaches nothing but
    /// the account itself.
    /// </summary>
    public bool MayChangeItsOwnPreferences { get; set; } = true;

    /// <summary>
    /// Gets or sets the remote bitrate ceiling in bits a second, or nothing
    /// for no ceiling.
    /// </summary>
    public int? RemoteBitrateCeiling { get; set; }

    /// <summary>
    /// Gets or sets how many streams the account may run at once, or nothing
    /// for no ceiling.
    /// </summary>
    public int? SimultaneousStreamCeiling { get; set; }

    /// <summary>
    /// Gets or sets the parental rating ceiling, or nothing for no ceiling.
    /// </summary>
    public int? ParentalRatingCeiling { get; set; }
}
