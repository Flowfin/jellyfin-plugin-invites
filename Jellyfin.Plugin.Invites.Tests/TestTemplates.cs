using System;
using System.Collections.Immutable;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Configuration;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The grants the suite mints against, in the two shapes a grant has: the
/// configured entry an operator writes, and the value the record carries.
/// </summary>
/// <remarks>
/// <para>
/// One place rather than one per file, because a record carries a copy of a
/// grant since #61 and every test that builds a record by hand has to hand it
/// one. A test that is not about the grant takes <see cref="Household"/> and
/// says nothing further; a test that is about it takes the configured entry,
/// mints against it, and compares.
/// </para>
/// <para>
/// The configured list carries every name a test in this suite mints against,
/// so a stub handed <see cref="AsConfigured"/> answers for all of them. A name
/// added to a test and not here is refused at the mint, which is the mint doing
/// what it does rather than a fixture being incomplete.
/// </para>
/// </remarks>
internal static class TestTemplates
{
    /// <summary>
    /// The library most fixtures grant.
    /// </summary>
    public static readonly Guid Films = Guid.Parse("11111111-1111-4111-8111-111111111111");

    /// <summary>
    /// The second library, so a fixture granting two exists.
    /// </summary>
    public static readonly Guid Music = Guid.Parse("22222222-2222-4222-8222-222222222222");

    /// <summary>
    /// Gets the grant most records in the suite carry: two libraries, the
    /// posture #64 decided, and one ceiling of each kind.
    /// </summary>
    /// <remarks>
    /// It carries a value in every field a stored grant has, including all
    /// three ceilings, so a round trip that lost any member has something to
    /// lose. A fixture with every ceiling absent would round-trip through a
    /// writer that dropped ceilings and never notice.
    /// </remarks>
    public static AccountTemplate Household { get; } = TemplateSettings.Of(HouseholdAsConfigured());

    /// <summary>
    /// Gets a grant that differs from <see cref="Household"/> in one library,
    /// so a test moving the template has a second value to move it to.
    /// </summary>
    public static AccountTemplate Guest { get; } = TemplateSettings.Of(GuestAsConfigured());

    /// <summary>
    /// Gets the configured templates, as a mint reads them: every name a test
    /// in this suite mints against, each a usable entry.
    /// </summary>
    public static IConfiguredTemplates AsConfigured => new StubConfiguredTemplates(Configured());

    /// <summary>
    /// The configured entries behind <see cref="AsConfigured"/>, fresh each
    /// time so a test that edits one edits its own copy.
    /// </summary>
    /// <returns>The entries.</returns>
    public static ConfiguredTemplate?[] Configured()
    {
        return
        [
            HouseholdAsConfigured(),
            GuestAsConfigured(),
            new ConfiguredTemplate { Label = "Old", Libraries = [Films] },
            new ConfiguredTemplate { Label = "Current", Libraries = [Films] },
        ];
    }

    /// <summary>
    /// The household entry as an operator would write it.
    /// </summary>
    /// <returns>A fresh entry.</returns>
    public static ConfiguredTemplate HouseholdAsConfigured()
    {
        return new ConfiguredTemplate
        {
            Label = "Household",
            Libraries = [Films, Music],
            MayDownload = true,
            MayPlayFromOutsideTheNetwork = true,
            MayControlOtherSessions = false,
            MayWatchLiveTelevision = true,
            MayManageLiveTelevision = false,
            MayDeleteContent = false,
            MayManageCollections = false,
            MayManageSubtitles = false,
            MayManageLyrics = false,
            MayChangeItsOwnPreferences = true,
            RemoteBitrateCeiling = 8_000_000,
            SimultaneousStreamCeiling = 2,
            ParentalRatingCeiling = 12,
        };
    }

    /// <summary>
    /// The guest entry as an operator would write it.
    /// </summary>
    /// <returns>A fresh entry.</returns>
    public static ConfiguredTemplate GuestAsConfigured()
    {
        return new ConfiguredTemplate { Label = "Guest", Libraries = [Music] };
    }

    /// <summary>
    /// A stored grant's members, as the writer spells them, for a test that
    /// takes a document apart member by member.
    /// </summary>
    /// <returns>The member names, in the order the writer emits them.</returns>
    public static ImmutableArray<string> StoredMembers()
    {
        return
        [
            "libraries",
            "mayDownload",
            "mayPlayFromOutsideTheNetwork",
            "mayManage",
            "mayControlOtherSessions",
            "mayWatchLiveTelevision",
            "mayManageLiveTelevision",
            "mayDeleteContent",
            "mayManageCollections",
            "mayManageSubtitles",
            "mayManageLyrics",
            "mayChangeItsOwnPreferences",
            "remoteBitrateCeiling",
            "simultaneousStreamCeiling",
            "parentalRatingCeiling",
            "serverDefaultsLeftAlone",
        ];
    }
}
