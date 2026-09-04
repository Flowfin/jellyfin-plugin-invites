using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Invites.Accounts;
using MediaBrowser.Model.Users;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The policy an invited account is left with, asserted field by field against
/// where each field's value came from.
/// </summary>
/// <remarks>
/// <para>
/// #69's third clause. <c>PolicyFieldSourceTests</c> holds the source column -
/// which of the three places each field of the server's policy is decided in -
/// and says in as many words that the expected-value column needs a routine
/// that applies a template. <see cref="AccountTemplateApplication"/> is that
/// routine and this is that column.
/// </para>
/// <para>
/// <b>The assertion is over every field of the policy, not over the fifteen the
/// routine writes.</b> The failure #69 is written against is a field nobody
/// decided, and a test that only looks at what the routine touches cannot see
/// one. So every property of <see cref="UserPolicy"/> is given a value standing
/// for what the server set when it created the user, the routine runs, and each
/// field is then either the template's grant or exactly the value it had.
/// </para>
/// <para>
/// <b>Why it runs twice.</b> The marker each untouched field carries is the
/// opposite in the two runs. A routine that wrote a constant into a field it
/// should leave alone - an administrator flag set true, a library flag left on -
/// agrees with one of the two markers and disagrees with the other, so it is
/// caught in one of the two runs rather than in neither. A single run over one
/// set of markers is the shape that would let exactly the write this plugin may
/// never make pass green.
/// </para>
/// <para>
/// <b>What is not measured here.</b> What the server actually sets when it
/// creates a user. Those defaults are added by the server's own user manager,
/// in an assembly this plugin does not reference, and nothing in this repository
/// runs a server. The markers below stand for that state and are not a claim
/// about it: what is asserted is that the routine leaves whatever was there
/// alone, which is the property #69 names. Whether an account carrying these
/// values behaves as the template intends is a claim about a running server and
/// is made nowhere.
/// </para>
/// </remarks>
public class AccountTemplateApplicationTests
{
    /// <summary>
    /// A library identifier no template below names, used as the marker on the
    /// two fields the libraries are written to.
    /// </summary>
    private static readonly Guid _aLibraryNoTemplateNames = new("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Every field of the server's user policy is either written from the
    /// template or left exactly as it was.
    /// </summary>
    /// <param name="generous">
    /// Which way the template's grants and the markers point. Both runs assert
    /// the same property; what moves is the value a careless write would have
    /// to agree with.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryFieldIsTheTemplatesGrantOrTheValueTheServerHadSet(bool generous)
    {
        var template = ATemplate(generous);
        var expected = WhatTheTemplateGrants(template);

        var policy = new UserPolicy();
        foreach (var field in WritableFields())
        {
            field.SetValue(policy, AMarkerFor(field, expected, generous));
        }

        var before = WritableFields().ToDictionary(
            field => field.Name,
            field => field.GetValue(policy),
            StringComparer.Ordinal);

        AccountTemplateApplication.ApplyTo(policy, template);

        foreach (var field in WritableFields())
        {
            if (expected.TryGetValue(field.Name, out var granted))
            {
                AssertTheGrantIsOnTheField(field, policy, granted);
                continue;
            }

            Assert.True(
                Equal(before[field.Name], field.GetValue(policy)),
                field.Name
                + " is a field this plugin writes nothing to, and applying a template moved it. Whatever the server set when it created the account has to survive, or the account carries a grant nobody decided.");
        }
    }

    /// <summary>
    /// Moving one grant moves exactly the policy fields that grant is written
    /// to, and no others.
    /// </summary>
    /// <param name="which">
    /// Which grant moves. Nought to ten are the eleven permissions in the order
    /// the template's constructor takes them, eleven to thirteen are the three
    /// ceilings, and fourteen is the libraries.
    /// </param>
    /// <remarks>
    /// <para>
    /// The run above cannot see two grants swapped between their fields, and
    /// this is the leg that can. Every permission on a template points the same
    /// way there, so a routine handing the subtitle grant to the lyric field and
    /// the lyric grant to the subtitle field produces the same policy and passes.
    /// That fault was applied and watched to pass before this leg was written.
    /// </para>
    /// <para>
    /// Here one grant moves at a time and what is compared is which fields moved
    /// with it. The expectation is derived from
    /// <see cref="WhatTheTemplateGrants"/> rather than from a second mapping, so
    /// there is one table for a reader to check and the routine is judged
    /// against it in both directions: a field that should have moved and did
    /// not, and a field that moved and should not have.
    /// </para>
    /// <para>
    /// <b>The case worth reading is eleven, which expects nothing to move.</b>
    /// <see cref="AccountTemplate.MayManage"/> is handed to no field of the
    /// server's policy, so a template that grants it produces exactly the same
    /// account as one that does not. That is the state #62 owns rather than a
    /// defect here, and asserting it means the day somebody gives that grant a
    /// field is the day this case reds.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    public void MovingOneGrantMovesExactlyTheFieldsItIsWrittenTo(int which)
    {
        var baseline = ABaseline();
        var moved = ABaselineWithOneGrantMoved(which);

        var fromBaseline = new UserPolicy();
        AccountTemplateApplication.ApplyTo(fromBaseline, baseline);

        var fromMoved = new UserPolicy();
        AccountTemplateApplication.ApplyTo(fromMoved, moved);

        var grantedBefore = WhatTheTemplateGrants(baseline);
        var grantedAfter = WhatTheTemplateGrants(moved);

        var shouldMove = grantedBefore.Keys
            .Where(name => !Equal(grantedBefore[name], grantedAfter[name]))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var didMove = WritableFields()
            .Where(field => !Equal(field.GetValue(fromBaseline), field.GetValue(fromMoved)))
            .Select(field => field.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(shouldMove, didMove);
    }

    /// <summary>
    /// The fields the routine says it writes are exactly the fields this table
    /// carries an expected value for.
    /// </summary>
    /// <remarks>
    /// The two lists are written in two places on purpose - one beside the
    /// assignments and one beside the expectations - and this is what makes the
    /// pair a check rather than two copies. A grant added to the routine and not
    /// to the table lands here, and so does the reverse.
    /// </remarks>
    [Fact]
    public void TheRoutineWritesTheFieldsThisTableExpectsAndNoOthers()
    {
        var expected = WhatTheTemplateGrants(ATemplate(true))
            .Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var written = AccountTemplateApplication.WrittenFields
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, written);
    }

    /// <summary>
    /// Every field the routine says it writes is a field the server's policy
    /// carries.
    /// </summary>
    /// <remarks>
    /// The list is a list of names, so a misspelt one is a field that is silently
    /// never written and a template grant that reaches nothing. Reflecting over
    /// the package this project restores is the one reading that catches it.
    /// </remarks>
    [Fact]
    public void EveryFieldTheRoutineWritesIsOneTheServerCarries()
    {
        var onTheServer = WritableFields().Select(field => field.Name).ToList();

        foreach (var name in AccountTemplateApplication.WrittenFields)
        {
            Assert.Contains(name, onTheServer, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// A template naming a left-alone field the server's policy does not carry
    /// is refused, and nothing is written before the refusal.
    /// </summary>
    /// <remarks>
    /// <see cref="AccountTemplate"/> says of
    /// <see cref="AccountTemplate.ServerDefaultsLeftAlone"/> that whether each
    /// name is a field the server carries is checked where the policy is
    /// applied. This is that check. A name that is not a field records an
    /// omission as considered when nothing on the account corresponds to it.
    /// </remarks>
    [Fact]
    public void ALeftAloneFieldTheServerDoesNotCarryIsRefusedAndWritesNothing()
    {
        var template = ATemplate(true, leftAlone: ImmutableArray.Create("EnableAllTheThings"));
        var policy = new UserPolicy();
        var before = WritableFields().ToDictionary(
            field => field.Name,
            field => field.GetValue(policy),
            StringComparer.Ordinal);

        var refused = Assert.Throws<ArgumentException>(() => AccountTemplateApplication.ApplyTo(policy, template));

        Assert.Contains("EnableAllTheThings", refused.Message, StringComparison.Ordinal);
        AssertNothingMoved(before, policy);
    }

    /// <summary>
    /// A template naming a left-alone field that the routine writes is refused,
    /// and nothing is written before the refusal.
    /// </summary>
    /// <remarks>
    /// One field cannot be both left alone and granted. The refusal is here
    /// rather than on the template because the template does not know which
    /// fields a grant is handed to, and putting the comparison there would be a
    /// second copy of the routine's own list.
    /// </remarks>
    [Fact]
    public void ALeftAloneFieldTheRoutineWritesIsRefusedAndWritesNothing()
    {
        var template = ATemplate(true, leftAlone: ImmutableArray.Create("EnableRemoteAccess"));
        var policy = new UserPolicy();
        var before = WritableFields().ToDictionary(
            field => field.Name,
            field => field.GetValue(policy),
            StringComparer.Ordinal);

        var refused = Assert.Throws<ArgumentException>(() => AccountTemplateApplication.ApplyTo(policy, template));

        Assert.Contains("EnableRemoteAccess", refused.Message, StringComparison.Ordinal);
        AssertNothingMoved(before, policy);
    }

    /// <summary>
    /// A left-alone field the plugin is refused from writing at all is accepted.
    /// </summary>
    /// <remarks>
    /// The near miss for the refusal above. <c>EnableAllFolders</c> is a field
    /// of the server's policy that this routine does not write, so naming it as
    /// left alone is exactly what a template is supposed to do with it, and a
    /// refusal drawn one name too wide would refuse the honest case. The value
    /// the field carries is untouched, which the field-by-field runs assert.
    /// </remarks>
    [Fact]
    public void ALeftAloneFieldTheLintRefusesIsAcceptedRatherThanRefused()
    {
        var template = ATemplate(true, leftAlone: ImmutableArray.Create("EnableAllFolders", "EnableAllChannels", "IsAdministrator"));

        AccountTemplateApplication.ApplyTo(new UserPolicy(), template);
    }

    /// <summary>
    /// Neither argument may be null.
    /// </summary>
    [Fact]
    public void NeitherThePolicyNorTheTemplateMayBeNull()
    {
        Assert.Throws<ArgumentNullException>(() => AccountTemplateApplication.ApplyTo(null!, ATemplate(true)));
        Assert.Throws<ArgumentNullException>(() => AccountTemplateApplication.ApplyTo(new UserPolicy(), null!));
    }

    /// <summary>
    /// A template granting no ceiling writes the server's unlimited value rather
    /// than leaving the field alone.
    /// </summary>
    /// <remarks>
    /// <see cref="AccountTemplate"/> says that no ceiling is a stated grant of
    /// an unlimited one and is not the server's default creeping back in. The
    /// two look identical on an account whose server default happened to be
    /// unlimited, and different on every other, so this is asserted against
    /// markers that are neither.
    /// </remarks>
    [Fact]
    public void NoCeilingIsWrittenAsTheServersUnlimitedValueRatherThanLeftAlone()
    {
        var template = ATemplate(true, noCeilings: true);
        var policy = new UserPolicy
        {
            RemoteClientBitrateLimit = 4242,
            MaxActiveSessions = 4242,
            MaxParentalRating = 4242,
        };

        AccountTemplateApplication.ApplyTo(policy, template);

        Assert.Equal(0, policy.RemoteClientBitrateLimit);
        Assert.Equal(0, policy.MaxActiveSessions);
        Assert.Null(policy.MaxParentalRating);
    }

    /// <summary>
    /// The libraries reach the two fields the server keeps side by side, and a
    /// template naming none grants none.
    /// </summary>
    [Fact]
    public void TheLibrariesReachBothFieldsAndAnEmptyListGrantsNothing()
    {
        var one = new Guid("22222222-2222-2222-2222-222222222222");
        var granting = ATemplate(true, libraries: ImmutableArray.Create(one));
        var policy = new UserPolicy();

        AccountTemplateApplication.ApplyTo(policy, granting);

        Assert.Equal(new[] { one }, policy.EnabledFolders);
        Assert.Equal(new[] { one }, policy.EnabledChannels);

        AccountTemplateApplication.ApplyTo(policy, ATemplate(true, libraries: ImmutableArray<Guid>.Empty));

        Assert.Empty(policy.EnabledFolders);
        Assert.Empty(policy.EnabledChannels);
    }

    /// <summary>
    /// A library the template does not name is never granted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// #103 asks for a test per refusal and this is that refusal on its own,
    /// rather than as one branch of the field-by-field theory above. The two are
    /// not the same assertion: that theory requires every field to be the grant
    /// or the value the server had, over every field at once, and reads as a
    /// statement about the routine's coverage. This reads as a statement about
    /// one library, which is the sentence somebody would go looking for after an
    /// account saw something it should not have.
    /// </para>
    /// <para>
    /// The policy starts carrying a library no template names, on both fields the
    /// libraries reach, so a routine that added to what was there rather than
    /// replacing it is caught. Adding is the mistake with the plausible motive:
    /// it looks like not taking anything away from an existing account.
    /// </para>
    /// </remarks>
    [Fact]
    public void ALibraryTheTemplateDoesNotNameIsNeverGranted()
    {
        var granted = new Guid("55555555-5555-5555-5555-555555555555");
        var policy = new UserPolicy
        {
            EnabledFolders = [_aLibraryNoTemplateNames],
            EnabledChannels = [_aLibraryNoTemplateNames],
        };

        AccountTemplateApplication.ApplyTo(policy, ATemplate(true, libraries: ImmutableArray.Create(granted)));

        Assert.Equal(new[] { granted }, policy.EnabledFolders);
        Assert.Equal(new[] { granted }, policy.EnabledChannels);
        Assert.DoesNotContain(_aLibraryNoTemplateNames, policy.EnabledFolders);
        Assert.DoesNotContain(_aLibraryNoTemplateNames, policy.EnabledChannels);
    }

    /// <summary>
    /// A permission the template does not name keeps the value the server had
    /// set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other half of #103's refusal list, and the one whose failure is
    /// invisible on the account that meets it: a field this plugin quietly reset
    /// to a type's default looks like a field the server set that way, and the
    /// operator has no reason to look.
    /// </para>
    /// <para>
    /// The field driven is one no grant of an account template reaches, so what
    /// is asserted is not that this field in particular is safe but that a field
    /// outside the grant list is left alone. Which fields the grants reach is
    /// held by the table above, so the two together say every field is either
    /// granted or untouched, one field at a time and once as a set.
    /// </para>
    /// </remarks>
    [Fact]
    public void APermissionTheTemplateDoesNotNameKeepsTheValueTheServerSet()
    {
        // The two values are set against each other so that a routine writing
        // either of them has to disagree with one of these assertions whichever
        // way it writes. A fixture whose value happens to match what a careless
        // write would set is a fixture that passes for the wrong reason, and the
        // first draft of this test was one: it started this field at false, and
        // a break setting it to false left the test green.
        var policy = new UserPolicy { EnableSyncTranscoding = true, EnablePlaybackRemuxing = false };

        AccountTemplateApplication.ApplyTo(policy, ATemplate(true));

        Assert.True(policy.EnableSyncTranscoding);
        Assert.False(policy.EnablePlaybackRemuxing);
    }

    /// <summary>
    /// The policy fields the template's grants are written to, against the value
    /// each one takes for the template handed in.
    /// </summary>
    /// <remarks>
    /// Written out rather than derived from the routine, because a table derived
    /// from the thing it judges asserts that the routine agrees with itself.
    /// Every row names the grant it comes from in the value it computes.
    /// </remarks>
    /// <param name="template">The template.</param>
    /// <returns>The expected value per policy field.</returns>
    private static Dictionary<string, object?> WhatTheTemplateGrants(AccountTemplate template) =>
        new(StringComparer.Ordinal)
        {
            ["EnabledFolders"] = template.Libraries.ToArray(),
            ["EnabledChannels"] = template.Libraries.ToArray(),
            ["EnableContentDownloading"] = template.MayDownload,
            ["EnableRemoteAccess"] = template.MayPlayFromOutsideTheNetwork,
            ["EnableRemoteControlOfOtherUsers"] = template.MayControlOtherSessions,
            ["EnableLiveTvAccess"] = template.MayWatchLiveTelevision,
            ["EnableLiveTvManagement"] = template.MayManageLiveTelevision,
            ["EnableContentDeletion"] = template.MayDeleteContent,
            ["EnableCollectionManagement"] = template.MayManageCollections,
            ["EnableSubtitleManagement"] = template.MayManageSubtitles,
            ["EnableLyricManagement"] = template.MayManageLyrics,
            ["EnableUserPreferenceAccess"] = template.MayChangeItsOwnPreferences,
            ["RemoteClientBitrateLimit"] = template.RemoteBitrateCeiling ?? 0,
            ["MaxActiveSessions"] = template.SimultaneousStreamCeiling ?? 0,
            ["MaxParentalRating"] = template.ParentalRatingCeiling,
        };

    /// <summary>
    /// A template whose grants all point the same way.
    /// </summary>
    /// <param name="generous">Which way every permission points.</param>
    /// <param name="libraries">The libraries, or the default pair.</param>
    /// <param name="leftAlone">The declared omissions, or none.</param>
    /// <param name="noCeilings">Whether the three quotas grant no ceiling.</param>
    /// <returns>The template.</returns>
    private static AccountTemplate ATemplate(
        bool generous,
        ImmutableArray<Guid>? libraries = null,
        ImmutableArray<string>? leftAlone = null,
        bool noCeilings = false) =>
        new(
            libraries ?? ImmutableArray.Create(
                new Guid("33333333-3333-3333-3333-333333333333"),
                new Guid("44444444-4444-4444-4444-444444444444")),
            generous,
            generous,
            generous,
            generous,
            generous,
            generous,
            generous,
            generous,
            generous,
            generous,
            generous,
            noCeilings ? null : 3_000_000,
            noCeilings ? null : 2,
            noCeilings ? null : 13,
            leftAlone ?? ImmutableArray<string>.Empty);

    /// <summary>
    /// A template built out of its parts, so that one of them can be moved and
    /// nothing else.
    /// </summary>
    /// <param name="permissions">
    /// The eleven permissions, in the order the constructor takes them.
    /// </param>
    /// <param name="libraries">The libraries.</param>
    /// <param name="bitrate">The remote bitrate ceiling.</param>
    /// <param name="sessions">The simultaneous stream ceiling.</param>
    /// <param name="rating">The parental rating ceiling.</param>
    /// <returns>The template.</returns>
    private static AccountTemplate ATemplateOf(
        bool[] permissions,
        ImmutableArray<Guid> libraries,
        int? bitrate,
        int? sessions,
        int? rating) =>
        new(
            libraries,
            permissions[0],
            permissions[1],
            permissions[2],
            permissions[3],
            permissions[4],
            permissions[5],
            permissions[6],
            permissions[7],
            permissions[8],
            permissions[9],
            permissions[10],
            bitrate,
            sessions,
            rating,
            ImmutableArray<string>.Empty);

    /// <summary>
    /// The template every one-grant run starts from.
    /// </summary>
    /// <returns>The template.</returns>
    private static AccountTemplate ABaseline() =>
        ATemplateOf(
            new bool[11],
            ImmutableArray.Create(new Guid("55555555-5555-5555-5555-555555555555")),
            1_000_000,
            1,
            5);

    /// <summary>
    /// The baseline with exactly one of its grants moved.
    /// </summary>
    /// <param name="which">
    /// Which grant moves. Nought to ten are the permissions in the order the
    /// constructor takes them, then the three ceilings, then the libraries.
    /// </param>
    /// <returns>The template.</returns>
    private static AccountTemplate ABaselineWithOneGrantMoved(int which)
    {
        var permissions = new bool[11];
        var libraries = ImmutableArray.Create(new Guid("55555555-5555-5555-5555-555555555555"));
        int? bitrate = 1_000_000;
        int? sessions = 1;
        int? rating = 5;

        switch (which)
        {
            case 11: bitrate = 2_000_000; break;
            case 12: sessions = 2; break;
            case 13: rating = 6; break;
            case 14: libraries = ImmutableArray.Create(new Guid("66666666-6666-6666-6666-666666666666")); break;
            default: permissions[which] = true; break;
        }

        return ATemplateOf(permissions, libraries, bitrate, sessions, rating);
    }

    /// <summary>
    /// Every writable property of the server's user policy.
    /// </summary>
    /// <returns>The properties.</returns>
    private static IEnumerable<PropertyInfo> WritableFields() =>
        typeof(UserPolicy)
            .GetProperties()
            .Where(property => property.CanWrite && property.CanRead)
            .OrderBy(property => property.Name, StringComparer.Ordinal);

    /// <summary>
    /// The value a field carries before the routine runs.
    /// </summary>
    /// <param name="field">The policy field.</param>
    /// <param name="expected">What the routine is expected to write, by field.</param>
    /// <param name="generous">Which way the markers point on this run.</param>
    /// <returns>The marker.</returns>
    /// <remarks>
    /// A field the routine writes gets a marker that disagrees with what it will
    /// be written, or the assertion cannot tell a write from an omission. Every
    /// other field gets a marker of its own type, and a type this method has no
    /// marker for is refused rather than skipped: a field arriving on the policy
    /// in a later server line has to be placed by somebody, and passing over it
    /// silently is the failure #69 exists against.
    /// </remarks>
    private static object? AMarkerFor(PropertyInfo field, IReadOnlyDictionary<string, object?> expected, bool generous)
    {
        if (expected.TryGetValue(field.Name, out var granted))
        {
            return granted switch
            {
                bool value => !value,
                int value => value + 7,
                Guid[] => new[] { _aLibraryNoTemplateNames },
                null => 7,
                _ => throw new NotSupportedException(
                    field.Name + " is granted as a " + granted.GetType() + ", which this table has no disagreeing marker for."),
            };
        }

        var type = Nullable.GetUnderlyingType(field.PropertyType) ?? field.PropertyType;

        if (type == typeof(bool))
        {
            return generous;
        }

        if (type == typeof(int))
        {
            return generous ? 4242 : 909;
        }

        if (type == typeof(string))
        {
            return generous ? "an-invites-marker" : "another-invites-marker";
        }

        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            return values.GetValue(generous ? 0 : values.Length - 1);
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType()!, 0);
        }

        throw new NotSupportedException(
            field.Name
            + " is a "
            + field.PropertyType
            + " on the server's user policy and this table has no marker for that type, so what the routine does to it was not asserted. Give it one, and say in PolicyFieldSourceTests which column the field belongs in.");
    }

    /// <summary>
    /// Asserts a field carries the grant the template made.
    /// </summary>
    /// <param name="field">The policy field.</param>
    /// <param name="policy">The policy after the routine ran.</param>
    /// <param name="granted">What the template grants.</param>
    private static void AssertTheGrantIsOnTheField(PropertyInfo field, UserPolicy policy, object? granted)
    {
        Assert.True(
            Equal(granted, field.GetValue(policy)),
            field.Name
            + " is written from a grant the template carries and it does not hold what the template granted.");
    }

    /// <summary>
    /// Asserts no field of the policy moved.
    /// </summary>
    /// <param name="before">The values before.</param>
    /// <param name="policy">The policy after.</param>
    private static void AssertNothingMoved(IReadOnlyDictionary<string, object?> before, UserPolicy policy)
    {
        foreach (var field in WritableFields())
        {
            Assert.True(
                Equal(before[field.Name], field.GetValue(policy)),
                field.Name
                + " moved on a template that was refused. A refusal that has already written part of a grant leaves an account nobody decided.");
        }
    }

    /// <summary>
    /// Two policy field values, compared by what they hold rather than by
    /// reference.
    /// </summary>
    /// <param name="left">One value.</param>
    /// <param name="right">The other.</param>
    /// <returns>Whether they hold the same thing.</returns>
    /// <remarks>
    /// The arrays on this type are compared element by element, because the
    /// routine hands the libraries over as a fresh array and a reference
    /// comparison would read that as a change on a field it did not touch.
    /// </remarks>
    private static bool Equal(object? left, object? right)
    {
        if (left is Array one && right is Array other)
        {
            return one.Length == other.Length
                && one.Cast<object>().SequenceEqual(other.Cast<object>());
        }

        return Equals(left, right);
    }
}
