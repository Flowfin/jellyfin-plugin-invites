using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MediaBrowser.Model.Users;

namespace Jellyfin.Plugin.Invites.Accounts;

/// <summary>
/// The one routine that writes an account template onto a user policy.
/// </summary>
/// <remarks>
/// <para>
/// #69 asks for one routine that takes a created account and a template and
/// produces the account's final state, and for nothing else in the plugin to
/// write a user policy. This is that routine. The two rules in
/// <c>.github/lint/invariants.sh</c> that carry #69 exempt a path containing
/// <c>AccountTemplate</c>, which is this file and <see cref="AccountTemplate"/>
/// beside it, so a policy write appearing anywhere else is refused by name
/// rather than by review.
/// </para>
/// <para>
/// <b>It takes the policy the server already gave the account and writes over
/// part of it.</b> That is the shape #69 names: the fields the template does
/// not mention have to be exactly what the server set when it created the
/// user, and the only way to keep that promise without knowing what the server
/// set is to leave those fields alone rather than to build a policy from
/// nothing. A routine returning a fresh <see cref="UserPolicy"/> would hand the
/// server this package's own constructor defaults for twenty-nine fields, which
/// is a grant nobody decided and which would move under the plugin on the day
/// the package moved.
/// </para>
/// <para>
/// <b>What it writes is fifteen fields and the list of columns is not here.</b>
/// It is <c>PolicyFieldSourceTests</c> in the test project, which holds every
/// field of the server's policy against the column it belongs in and reds when
/// the server's policy grows a field that is in none. The assignments below are
/// what that table describes; the table is what refuses a field nobody placed.
/// </para>
/// <para>
/// <b>Three fields it may not write, and it is not the author of that
/// refusal.</b> <c>IsAdministrator</c> is #62's, and <c>EnableAllFolders</c>
/// and <c>EnableAllChannels</c> are #63's. Each is refused by a rule in the
/// invariant lint over the whole plugin, this file included: the exemptions the
/// two #69 rules carry do not reach the three rules that name these fields. So
/// the ceiling holds here in the same way it holds everywhere, and this routine
/// has no exception to it.
/// </para>
/// <para>
/// <b>What that costs, written here rather than discovered.</b> The server
/// decides <c>EnableAllFolders</c> when it creates the account and this plugin
/// never writes it. On a server that creates accounts with that flag on, an
/// account carries every library whatever <see cref="AccountTemplate.Libraries"/>
/// says, and the resolved list this routine writes narrows nothing. What the
/// server does there is not measured anywhere in this repository: the defaults
/// are added by the server's own user manager, in an assembly this plugin does
/// not reference, and nothing here runs a server. It belongs to #63's clause
/// about a library created after minting and is named on that issue rather than
/// settled here.
/// </para>
/// <para>
/// <b>One grant is written to nothing.</b>
/// <see cref="AccountTemplate.MayManage"/> is the administrator question, the
/// server carries no single field for it, and #62 owns what an invitation may
/// never mint. This routine writes nothing for it and says so here, so that
/// giving it a field is a change to this file and to the table in the test
/// project rather than something that happens quietly.
/// </para>
/// </remarks>
public static class AccountTemplateApplication
{
    /// <summary>
    /// The policy fields this routine writes, in the order the assignments in
    /// <see cref="ApplyTo"/> make them.
    /// </summary>
    /// <remarks>
    /// It is a value because <see cref="ApplyTo"/> refuses a template that
    /// declares one of these fields left alone, and a refusal comparing against
    /// a list retyped beside the assignments is a refusal that stops agreeing
    /// with them. What keeps the list honest against the server's own policy is
    /// <c>PolicyFieldSourceTests</c>, which reads these names back and every
    /// field of <see cref="UserPolicy"/> beside them.
    /// </remarks>
    private static readonly string[] FieldsWritten =
    {
        "EnabledFolders",
        "EnabledChannels",
        "EnableContentDownloading",
        "EnableRemoteAccess",
        "EnableRemoteControlOfOtherUsers",
        "EnableLiveTvAccess",
        "EnableLiveTvManagement",
        "EnableContentDeletion",
        "EnableCollectionManagement",
        "EnableSubtitleManagement",
        "EnableLyricManagement",
        "EnableUserPreferenceAccess",
        "RemoteClientBitrateLimit",
        "MaxActiveSessions",
        "MaxParentalRating",
    };

    /// <summary>
    /// Gets the names of the policy fields this routine writes.
    /// </summary>
    /// <remarks>
    /// Handed out so that a test asserting the resulting policy field by field
    /// names the source of each field from the routine rather than from a
    /// second copy of the list. What is left alone is the difference against
    /// the server's own policy, which is #69's third column and is not a list
    /// this plugin holds.
    /// </remarks>
    public static IReadOnlyList<string> WrittenFields => FieldsWritten;

    /// <summary>
    /// Writes the template's grants onto the policy the server gave a created
    /// account, and leaves every other field of it as it was.
    /// </summary>
    /// <param name="policy">
    /// The account's policy as the server created it. It is written in place,
    /// because the caller hands this same value back to the server and a copy
    /// returned beside it would be two policies with one of them stale.
    /// </param>
    /// <param name="template">The grant the invitation carried.</param>
    /// <exception cref="ArgumentNullException">
    /// The policy or the template is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The template declares a policy field left alone that the server's policy
    /// does not carry, or one that this routine writes.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>The two refusals are the promise <see cref="AccountTemplate"/> makes
    /// and cannot keep on its own.</b> Its remarks on
    /// <see cref="AccountTemplate.ServerDefaultsLeftAlone"/> say that whether
    /// each name is a field the server actually carries is checked where the
    /// policy is applied, and this is that place. A misspelt name there is a
    /// field an operator believes was considered and that nothing on the
    /// account corresponds to, which is worse than an unnamed field: the
    /// omission is recorded as deliberate and is not.
    /// </para>
    /// <para>
    /// The second refusal is the contradiction. A field named as left alone and
    /// then written by one of the lines below is a template saying both things
    /// about one field, and whichever a reader believes, the other is false.
    /// </para>
    /// <para>
    /// <b>Two of the three quotas write no ceiling as zero.</b>
    /// <c>RemoteClientBitrateLimit</c> and <c>MaxActiveSessions</c> are
    /// <c>Int32</c> on the server's policy and zero is their unlimited value,
    /// so a template granting no ceiling arrives there as zero.
    /// <c>MaxParentalRating</c> is nullable at both ends and travels unchanged.
    /// Which value each of the three may take is #65's.
    /// </para>
    /// <para>
    /// <b>The libraries are handed to two fields and it is one grant.</b> The
    /// server keeps folders and channels side by side, which is argued where
    /// the rule refusing the flag version of the same grant is argued, in
    /// <c>.github/lint/invariants.sh</c>. A library identifier is not a channel
    /// identifier, so a template naming only libraries grants no channel, which
    /// is the closed direction and is the one an operator who named no channel
    /// meant.
    /// </para>
    /// </remarks>
    public static void ApplyTo(UserPolicy policy, AccountTemplate template)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(template);

        RefuseAnOmissionThatCannotBeKept(template);

        var libraries = template.Libraries.ToArray();

        policy.EnabledFolders = libraries;
        policy.EnabledChannels = libraries;
        policy.EnableContentDownloading = template.MayDownload;
        policy.EnableRemoteAccess = template.MayPlayFromOutsideTheNetwork;
        policy.EnableRemoteControlOfOtherUsers = template.MayControlOtherSessions;
        policy.EnableLiveTvAccess = template.MayWatchLiveTelevision;
        policy.EnableLiveTvManagement = template.MayManageLiveTelevision;
        policy.EnableContentDeletion = template.MayDeleteContent;
        policy.EnableCollectionManagement = template.MayManageCollections;
        policy.EnableSubtitleManagement = template.MayManageSubtitles;
        policy.EnableLyricManagement = template.MayManageLyrics;
        policy.EnableUserPreferenceAccess = template.MayChangeItsOwnPreferences;
        policy.RemoteClientBitrateLimit = template.RemoteBitrateCeiling ?? 0;
        policy.MaxActiveSessions = template.SimultaneousStreamCeiling ?? 0;
        policy.MaxParentalRating = template.ParentalRatingCeiling;
    }

    /// <summary>
    /// Refuses a template whose declared omissions cannot be kept.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <remarks>
    /// The field names of the server's policy are read off the type rather than
    /// listed here, so the comparison moves with the package this project
    /// restores instead of against a list somebody has to remember to edit.
    /// </remarks>
    private static void RefuseAnOmissionThatCannotBeKept(AccountTemplate template)
    {
        var onTheServer = new HashSet<string>(
            typeof(UserPolicy).GetProperties().Select(property => property.Name),
            StringComparer.Ordinal);

        foreach (var name in template.ServerDefaultsLeftAlone)
        {
            if (!onTheServer.Contains(name))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The template names {0} among the policy fields it deliberately leaves alone, and the server's user policy carries no such field. An omission recorded against a field that does not exist reads as a decision somebody took.",
                        name),
                    nameof(template));
            }

            if (Array.Exists(FieldsWritten, written => string.Equals(written, name, StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The template names {0} among the policy fields it deliberately leaves alone, and a grant it carries is written to that field. One field cannot be both left alone and granted, and whichever a reader believes, the other is false.",
                        name),
                    nameof(template));
            }
        }
    }
}
