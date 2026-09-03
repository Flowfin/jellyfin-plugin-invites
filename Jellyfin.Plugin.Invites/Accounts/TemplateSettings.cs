using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using Jellyfin.Plugin.Invites.Configuration;

namespace Jellyfin.Plugin.Invites.Accounts;

/// <summary>
/// The one place a configured template becomes a grant, and the rules a
/// configured template is refused on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the rules are here and not on the configuration type.</b>
/// <see cref="ConfiguredTemplate"/> is the shape the server writes to a file
/// and reads back, and docs/coverage-floors.md leaves that area out of a floor
/// on the argument that it carries values and no decisions. The decisions are
/// here, in an area that is measured and mutated, so a rule that stopped
/// biting is found by the suite rather than by an operator.
/// </para>
/// <para>
/// <b>Refused whole, never thinned.</b> A list with one entry that is no grant
/// is refused as a list. Handing on the entries that pass would be the plugin
/// deciding which of an operator's templates count, and an operator who wrote
/// five and can mint against four has been corrected without being told, which
/// is the silent fallback #86 refuses.
/// </para>
/// <para>
/// <b>What a refusal names, and what it never names.</b> The position of the
/// entry, counted from one, and the rule it missed. Never the label or any
/// other value typed into the file: the sentence is written to a log when the
/// plugin loads, and docs/logging.md admits a value there only where it is a
/// row in the inventory, which a setting is not.
/// </para>
/// <para>
/// <b>What is not judged here.</b> Whether a library identifier names a
/// library the server has, which is #70's, and where each ceiling may sit
/// inside the non-negative numbers, which is #65's. Both are decided where the
/// grant meets a server rather than where it is written down. Nothing here
/// reads the mint or the record: what copies a template out of this list into
/// an invitation is #61's, and this is what it copies from.
/// </para>
/// </remarks>
public static class TemplateSettings
{
    /// <summary>
    /// Gets the name of the setting, as an operator meets it on the
    /// configuration page and in a refusal.
    /// </summary>
    public static string SettingName => nameof(PluginConfiguration.Templates);

    /// <summary>
    /// Says why the configured templates cannot be used, or nothing where every
    /// one of them is a grant and no name is written twice.
    /// </summary>
    /// <param name="templates">The templates as configured. <c>null</c> reads as none configured.</param>
    /// <returns>One sentence naming the position and the rule, or <c>null</c>.</returns>
    /// <remarks>
    /// The first refusal in the list is the one named. An operator repairs it,
    /// loads again and meets the next, which costs a reload per fault and never
    /// hides one behind another.
    /// </remarks>
    public static string? WhyRefused(IReadOnlyList<ConfiguredTemplate?>? templates)
    {
        if (templates is null)
        {
            return null;
        }

        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < templates.Count; index++)
        {
            var template = templates[index];
            var why = WhyRefused(template);
            if (why is not null)
            {
                return AtPosition(index, why);
            }

            if (!labels.Add(template!.Label!))
            {
                return AtPosition(
                    index,
                    "carries a label another template in the list already carries, compared ignoring case, so an operator minting against that name would get whichever of the two the plugin happened to read first.");
            }
        }

        return null;
    }

    /// <summary>
    /// Says why one configured template is no grant, or nothing where it is.
    /// </summary>
    /// <param name="template">The template as configured.</param>
    /// <returns>The rule it missed, as the rest of a sentence, or <c>null</c>.</returns>
    public static string? WhyRefused(ConfiguredTemplate? template)
    {
        if (template is null)
        {
            return "is empty, which is an entry somebody started and nobody finished.";
        }

        var label = template.Label;
        if (string.IsNullOrWhiteSpace(label))
        {
            return "has no label, and a template nobody can name is one nobody can mint against.";
        }

        if (label.Trim().Length != label.Length)
        {
            return "has a label padded with whitespace, which nobody will type on the mint form, so it names nothing anyone can reach.";
        }

        var seen = new HashSet<Guid>();
        foreach (var library in template.Libraries ?? [])
        {
            if (library == Guid.Empty)
            {
                return "names a library by the all-zero identifier, which is no library.";
            }

            if (!seen.Add(library))
            {
                return "names one library twice, so what it grants and what is written in it disagree.";
            }
        }

        if (template.RemoteBitrateCeiling < 0)
        {
            return "has a negative remote bitrate ceiling, and a ceiling below zero is no ceiling at all. No ceiling is written as an absent value.";
        }

        if (template.SimultaneousStreamCeiling < 0)
        {
            return "has a negative simultaneous stream ceiling, and a ceiling below zero is no ceiling at all. No ceiling is written as an absent value.";
        }

        if (template.ParentalRatingCeiling < 0)
        {
            return "has a negative parental rating ceiling, and a ceiling below zero is no ceiling at all. No ceiling is written as an absent value.";
        }

        return null;
    }

    /// <summary>
    /// Turns a configured template into the grant it describes.
    /// </summary>
    /// <param name="template">The template as configured.</param>
    /// <returns>The grant, which never manages the server and leaves no policy field named as left alone.</returns>
    /// <exception cref="ArgumentNullException">The template is null.</exception>
    /// <exception cref="ArgumentException">
    /// The template is one <see cref="WhyRefused(ConfiguredTemplate)"/> refuses, and the message says why.
    /// </exception>
    public static AccountTemplate Of(ConfiguredTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var why = WhyRefused(template);
        if (why is not null)
        {
            throw new ArgumentException("The template " + why, nameof(template));
        }

        return new AccountTemplate(
            libraries: ImmutableArray.CreateRange(template.Libraries ?? []),
            mayDownload: template.MayDownload,
            mayPlayFromOutsideTheNetwork: template.MayPlayFromOutsideTheNetwork,
            mayManage: false,
            mayControlOtherSessions: template.MayControlOtherSessions,
            mayWatchLiveTelevision: template.MayWatchLiveTelevision,
            mayManageLiveTelevision: template.MayManageLiveTelevision,
            mayDeleteContent: template.MayDeleteContent,
            mayManageCollections: template.MayManageCollections,
            mayManageSubtitles: template.MayManageSubtitles,
            mayManageLyrics: template.MayManageLyrics,
            mayChangeItsOwnPreferences: template.MayChangeItsOwnPreferences,
            remoteBitrateCeiling: template.RemoteBitrateCeiling,
            simultaneousStreamCeiling: template.SimultaneousStreamCeiling,
            parentalRatingCeiling: template.ParentalRatingCeiling,
            serverDefaultsLeftAlone: ImmutableArray<string>.Empty);
    }

    /// <summary>
    /// The grant behind a name, out of a list judged whole first.
    /// </summary>
    /// <param name="templates">The templates as configured. <c>null</c> reads as none configured.</param>
    /// <param name="label">The name an operator typed, compared ignoring case.</param>
    /// <returns>The grant, or <c>null</c> where no template carries that name.</returns>
    /// <exception cref="ArgumentNullException">The label is null.</exception>
    /// <exception cref="ArgumentException">
    /// The list is one <see cref="WhyRefused(IReadOnlyList{ConfiguredTemplate})"/> refuses, and the message says why.
    /// </exception>
    /// <remarks>
    /// The whole list is judged before any name is looked up, so a list with a
    /// fault in one entry answers for no entry. Answering for the good ones
    /// would be the thinning the remarks on this type refuse.
    /// </remarks>
    public static AccountTemplate? Named(IReadOnlyList<ConfiguredTemplate?>? templates, string label)
    {
        ArgumentNullException.ThrowIfNull(label);

        var why = WhyRefused(templates);
        if (why is not null)
        {
            throw new ArgumentException(why, nameof(templates));
        }

        if (templates is null)
        {
            return null;
        }

        foreach (var template in templates)
        {
            if (string.Equals(template!.Label, label, StringComparison.OrdinalIgnoreCase))
            {
                return Of(template);
            }
        }

        return null;
    }

    /// <summary>
    /// The sentence a refusal is written as, naming the entry by position.
    /// </summary>
    /// <param name="index">The entry's index, counted from zero.</param>
    /// <param name="why">The rule it missed, as the rest of a sentence.</param>
    /// <returns>The sentence.</returns>
    private static string AtPosition(int index, string why)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "The template at position {0} of {1} {2}",
            index + 1,
            SettingName,
            why);
    }
}
