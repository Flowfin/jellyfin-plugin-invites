using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.Invites.Accounts;
using MediaBrowser.Model.Users;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// Every field of the server's user policy, against where its value on an
/// invited account comes from.
/// </summary>
/// <remarks>
/// <para>
/// #69 asks for a table over the whole policy rather than over the fields a
/// template happens to mention: one row per field, with the source it takes its
/// value from, so that a field appearing in no column is a field nobody
/// decided. The failure it is written against is quiet. The server's policy
/// gains a field in a later version, the routine that applies a template does
/// not write it, and an invited account carries whatever that version chose as
/// the default for every account it creates. Nothing in a build notices, because
/// the plugin still compiles and every existing assertion still passes.
/// </para>
/// <para>
/// The three sources are the ones #69 names. A field is written from a grant the
/// template carries; or it is the ceiling, meaning this plugin refuses to write
/// it at all and a rule in <c>.github/lint/invariants.sh</c> refuses the write
/// by name; or this plugin writes nothing there and the account keeps what the
/// server set when it created the user.
/// </para>
/// <para>
/// <b>What the third column does not say.</b> It says this plugin writes nothing
/// to that field. It does not say that somebody weighed the field and chose to
/// leave it, and reading it that way is the mistake this paragraph exists to
/// stop: a field sits there because no grant names it, which is the state #64
/// moves a field out of when it decides one. A template that wants a particular
/// omission on the record names it in
/// <see cref="AccountTemplate.ServerDefaultsLeftAlone"/>, and that is a
/// per-template value rather than anything this table holds.
/// </para>
/// <para>
/// <b>What is not covered here.</b> The value an invited account ends up
/// carrying in each field. That needs a routine that creates an account and
/// applies a template, and this plugin has neither, so the whole of #69's
/// expected-value column is absent rather than asserted. What this file holds is
/// the source column, which is the half that can be held before the routine
/// exists and the half that reds when the server's policy grows.
/// </para>
/// <para>
/// <b>What was measured and what was not.</b> That a field exists on the policy
/// type, read off the package this project restores. That the server acts on any
/// of it was not measured: nothing here runs a server.
/// </para>
/// </remarks>
public class PolicyFieldSourceTests
{
    /// <summary>
    /// The grants an account template carries, against the fields of
    /// <see cref="UserPolicy"/> each one is handed to.
    /// </summary>
    /// <remarks>
    /// One grant reaches two fields. A template's libraries are a resolved list
    /// and the server keeps two of them side by side, which is written where the
    /// rule refusing the flag version of the same grant is argued, in
    /// <c>.github/lint/invariants.sh</c> under
    /// <c>server-wide-grant-flag-set</c>.
    /// </remarks>
    private static readonly Dictionary<string, string[]> HandedTo =
        new(StringComparer.Ordinal)
        {
            ["Libraries"] = new[] { "EnabledFolders", "EnabledChannels" },
            ["MayDownload"] = new[] { "EnableContentDownloading" },
            ["MayPlayFromOutsideTheNetwork"] = new[] { "EnableRemoteAccess" },
            ["MayControlOtherSessions"] = new[] { "EnableRemoteControlOfOtherUsers" },
            ["MayWatchLiveTelevision"] = new[] { "EnableLiveTvAccess" },
            ["MayManageLiveTelevision"] = new[] { "EnableLiveTvManagement" },
            ["MayDeleteContent"] = new[] { "EnableContentDeletion" },
            ["MayManageCollections"] = new[] { "EnableCollectionManagement" },
            ["MayManageSubtitles"] = new[] { "EnableSubtitleManagement" },
            ["MayManageLyrics"] = new[] { "EnableLyricManagement" },
            ["MayChangeItsOwnPreferences"] = new[] { "EnableUserPreferenceAccess" },
            ["RemoteBitrateCeiling"] = new[] { "RemoteClientBitrateLimit" },
            ["SimultaneousStreamCeiling"] = new[] { "MaxActiveSessions" },
            ["ParentalRatingCeiling"] = new[] { "MaxParentalRating" },
        };

    /// <summary>
    /// The template's own properties that are not grants handed to a policy
    /// field.
    /// </summary>
    /// <remarks>
    /// Two, and they are two different reasons.
    /// <see cref="AccountTemplate.ServerDefaultsLeftAlone"/> is a list of field
    /// names rather than a value written to one.
    /// <see cref="AccountTemplate.MayManage"/> is the open one: it is a single
    /// flag and the server carries no single field for it, so which fields it
    /// would be handed to is undecided. It is named here rather than left out,
    /// so that giving it a field is a change to this list and not something that
    /// happens quietly.
    /// </remarks>
    private static readonly string[] GrantsHandedToNoField =
        new[] { "MayManage", "ServerDefaultsLeftAlone" };

    /// <summary>
    /// How many fields of the server's user policy this plugin writes nothing
    /// to, on the line it compiles against.
    /// </summary>
    /// <remarks>
    /// A count rather than a list, because a list here would be a second copy of
    /// the two columns above with the arithmetic done by hand. The count is what
    /// makes the third column bite: a field arriving on the policy joins it
    /// silently otherwise, and inheriting a later server line's default for an
    /// invited account is what #69 asks somebody to decide instead.
    /// </remarks>
    private const int FieldsThisPluginWritesNothingTo = 26;

    /// <summary>
    /// Every field of the server's user policy is in exactly one column, and
    /// every name in a column is a field the server carries.
    /// </summary>
    /// <remarks>
    /// This is #69's fourth clause. A field arriving on the policy in a later
    /// server line lands in no column and reds this, which is the point:
    /// somebody has to say whether it is a grant, a refusal, or a field this
    /// plugin leaves alone, and until they do the suite says so.
    /// </remarks>
    [Fact]
    public void EveryFieldOfTheServersPolicyIsInExactlyOneColumn()
    {
        var onTheServer = PolicyFieldNames();

        var fromTheTemplate = HandedTo.Values.SelectMany(fields => fields).ToList();
        var theCeiling = FieldsNamedByTheInvariantRules().ToList();

        var decided = fromTheTemplate.Concat(theCeiling).ToList();

        Assert.Equal(decided.Count, decided.Distinct(StringComparer.Ordinal).Count());

        var namingNothing = decided.Except(onTheServer, StringComparer.Ordinal).ToList();

        Assert.True(
            namingNothing.Count == 0,
            "These names are in a column and are not fields of the server's user policy: "
            + string.Join(", ", namingNothing)
            + ". A column naming a field the server does not carry is a mapping of nothing.");

        var placedNowhere = onTheServer.Except(decided, StringComparer.Ordinal).ToList();

        Assert.True(
            placedNowhere.Count == FieldsThisPluginWritesNothingTo,
            "The server's user policy carries "
            + placedNowhere.Count
            + " fields no grant names and no rule refuses, and this table is written for "
            + FieldsThisPluginWritesNothingTo
            + ". The difference is: "
            + string.Join(", ", placedNowhere.OrderBy(name => name, StringComparer.Ordinal))
            + ". A field here takes whatever the server sets when it creates the account, so say which column it belongs in.");
    }

    /// <summary>
    /// Every grant on the template is handed to a field the server's policy
    /// carries, or is named as one that is handed to none.
    /// </summary>
    /// <remarks>
    /// <see cref="AccountTemplate"/> states, in the remarks on
    /// <see cref="AccountTemplate.ServerDefaultsLeftAlone"/>, that whether each
    /// name is a field the server actually carries is checked where the policy
    /// is applied, which is #69. This is that check for the grants. A grant added
    /// to the template without a row here reds, which is the near miss: eight
    /// fields arrived on that type at once, and each of them named its server
    /// field in prose that nothing read.
    /// </remarks>
    [Fact]
    public void EveryGrantOnTheTemplateNamesAFieldTheServerCarriesOrIsNamedAsHandedToNone()
    {
        var grants = typeof(AccountTemplate)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var accountedFor = HandedTo.Keys
            .Concat(GrantsHandedToNoField)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(accountedFor, grants);

        var onTheServer = PolicyFieldNames();
        foreach (var row in HandedTo)
        {
            foreach (var field in row.Value)
            {
                Assert.True(
                    onTheServer.Contains(field),
                    row.Key
                    + " is written as handed to "
                    + field
                    + ", which the server's user policy does not carry.");
            }
        }
    }

    /// <summary>
    /// No two grants are handed to the same field.
    /// </summary>
    /// <remarks>
    /// Two grants writing one field is two answers to one question, and the one
    /// that stands is whichever the routine applies last. It is worth refusing
    /// here rather than in the routine, because the routine does not exist yet
    /// and this is the table it will be written from.
    /// </remarks>
    [Fact]
    public void NoFieldIsWrittenByTwoGrants()
    {
        var written = HandedTo.Values.SelectMany(fields => fields).ToList();

        Assert.Equal(written.Count, written.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The ceiling column is exactly the policy fields the invariant lint
    /// refuses a write to, and it is not empty.
    /// </summary>
    /// <remarks>
    /// The emptiness check is the one that matters. The column is derived by
    /// reading a file, and a read that found nothing would leave every ceiling
    /// field in the third column with this suite green, which is the failure
    /// mode of every derived list.
    /// </remarks>
    [Fact]
    public void TheCeilingIsWhatTheInvariantLintRefusesAndIsNotEmpty()
    {
        var ceiling = FieldsNamedByTheInvariantRules()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(ceiling);
        Assert.Equal(
            new[] { "EnableAllChannels", "EnableAllFolders", "IsAdministrator" },
            ceiling);
    }

    /// <summary>
    /// The names of every property on the server's user policy.
    /// </summary>
    /// <returns>The names.</returns>
    private static HashSet<string> PolicyFieldNames() =>
        typeof(UserPolicy)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// The user-policy fields named inside a rule of the invariant lint.
    /// </summary>
    /// <remarks>
    /// The rules are the lines of the <c>RULES</c> array, which are the only
    /// lines in that file that refuse anything. A field named in the prose
    /// around them is deliberately not read: the file argues about
    /// <c>EnabledFolders</c> at length and refuses nothing about it.
    /// </remarks>
    /// <remarks>
    /// <b>Its bound, measured rather than supposed.</b> The match is a substring
    /// of the rule line, so a rule renaming <c>IsAdministrator</c> to
    /// <c>IsAdministratorX</c> still reads as naming the field and this stays
    /// green. That was found by applying exactly that fault and watching nothing
    /// go red. A rule name that no longer contains the field at all is caught,
    /// which is the mistake worth catching, and matching on a word boundary
    /// instead would be a rule about how a regular expression is spelt.
    /// </remarks>
    /// <returns>The field names.</returns>
    private static HashSet<string> FieldsNamedByTheInvariantRules()
    {
        var rules = InvariantLint()
            .Split('\n')
            .SkipWhile(line => !line.StartsWith("RULES=(", StringComparison.Ordinal))
            .Skip(1)
            .TakeWhile(line => !line.StartsWith(")", StringComparison.Ordinal))
            .ToList();

        return PolicyFieldNames()
            .Where(field => rules.Exists(rule => rule.Contains(field, StringComparison.Ordinal)))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The invariant lint, read off the tree.
    /// </summary>
    /// <remarks>
    /// Found by walking up from the test binary until a directory holds both the
    /// solution and the script, which is how the other legs over a tracked file
    /// find one: the number of levels under the binary moves with the
    /// configuration and the target framework, and the marker does not. Nothing
    /// is written and nothing outside the repository is read.
    /// </remarks>
    /// <returns>The text of the script.</returns>
    private static string InvariantLint()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var script = Path.Combine(directory.FullName, ".github", "lint", "invariants.sh");
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            if (File.Exists(script) && File.Exists(solution))
            {
                return File.ReadAllText(script);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds both Jellyfin.Plugin.Invites.sln and .github/lint/invariants.sh, so the ceiling column was read from nothing. Failing rather than passing over an empty list.");
    }
}
