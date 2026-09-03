using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Configuration;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The rules a configured template is refused on, and the grant one becomes.
/// </summary>
/// <remarks>
/// Every refusal here is asserted twice: once as the sentence the load writes,
/// and once as the refusal of the conversion, so the two cannot disagree about
/// what a grant is. The sentence is also held to naming a position and never a
/// label, because it is written to a log.
/// </remarks>
public class TemplateSettingsTests
{
    private static readonly Guid _films = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid _music = Guid.Parse("22222222-2222-4222-8222-222222222222");

    /// <summary>
    /// No configuration, and a configuration with no template, are both a
    /// fresh install and not a fault, and a name looked up in either finds
    /// nothing rather than failing.
    /// </summary>
    [Fact]
    public void NoTemplatesIsNoFault()
    {
        Assert.Null(TemplateSettings.WhyRefused((IReadOnlyList<ConfiguredTemplate?>?)null));
        Assert.Null(TemplateSettings.WhyRefused(Array.Empty<ConfiguredTemplate>()));
        Assert.Null(TemplateSettings.Named(null, "Household"));
        Assert.Null(TemplateSettings.Named(Array.Empty<ConfiguredTemplate>(), "Household"));
    }

    /// <summary>
    /// A list of grants that each carry a distinct name is not remarked on.
    /// </summary>
    [Fact]
    public void AListOfDistinctGrantsIsNotRefused()
    {
        var templates = new[] { ATemplate("Household"), ATemplate("Guest"), ATemplate("Trial") };

        Assert.Null(TemplateSettings.WhyRefused(templates));
    }

    /// <summary>
    /// A template nobody can name is one nobody can mint against.
    /// </summary>
    /// <param name="label">The label as written.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ATemplateWithNoLabelIsRefused(string? label)
    {
        var template = ATemplate("Household");
        template.Label = label;

        var why = TemplateSettings.WhyRefused(new[] { template });

        Assert.NotNull(why);
        Assert.StartsWith("The template at position 1 of Templates has no label", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// A label padded on either side is a name nobody will type on the mint
    /// form, so it names nothing anyone can reach.
    /// </summary>
    /// <param name="label">The label as written.</param>
    [Theory]
    [InlineData(" Household")]
    [InlineData("Household ")]
    [InlineData("\tHousehold")]
    public void APaddedLabelIsRefused(string label)
    {
        var why = TemplateSettings.WhyRefused(new[] { ATemplate(label) });

        Assert.NotNull(why);
        Assert.Contains("padded with whitespace", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// A label with a space inside it is a name, and is not the padding the
    /// rule above refuses.
    /// </summary>
    [Fact]
    public void ALabelWithASpaceInsideItIsAName()
    {
        Assert.Null(TemplateSettings.WhyRefused(new[] { ATemplate("Kitchen guests") }));
    }

    /// <summary>
    /// Two labels that differ only in case are one name written twice, and the
    /// second is the one named.
    /// </summary>
    [Fact]
    public void TwoLabelsDifferingOnlyInCaseAreRefusedTogether()
    {
        var why = TemplateSettings.WhyRefused(new[] { ATemplate("Household"), ATemplate("household") });

        Assert.NotNull(why);
        Assert.StartsWith("The template at position 2 of Templates carries a label another template", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// The sentence names the position and the setting and never the label,
    /// because it is written to a log and a label is a value an operator typed.
    /// </summary>
    [Fact]
    public void TheRefusalNamesThePositionAndNeverTheLabel()
    {
        var why = TemplateSettings.WhyRefused(new[] { ATemplate("Kitchen guests"), ATemplate("KITCHEN GUESTS") });

        Assert.NotNull(why);
        Assert.Contains("position 2", why, StringComparison.Ordinal);
        Assert.Contains(TemplateSettings.SettingName, why, StringComparison.Ordinal);
        Assert.DoesNotContain("kitchen", why, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The first fault in the list is the one named, counted from one, so a
    /// fault in the third entry is reported as the third and not the second.
    /// </summary>
    [Fact]
    public void TheFirstFaultInTheListIsTheOneNamedByItsPosition()
    {
        var third = ATemplate("Trial");
        third.SimultaneousStreamCeiling = -1;
        var fourth = ATemplate("Trial");

        var why = TemplateSettings.WhyRefused(new[] { ATemplate("Household"), ATemplate("Guest"), third, fourth });

        Assert.NotNull(why);
        Assert.StartsWith("The template at position 3 of Templates has a negative simultaneous stream ceiling", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// An entry the reader left empty is an entry somebody started and nobody
    /// finished.
    /// </summary>
    [Fact]
    public void AnEmptyEntryIsRefused()
    {
        var why = TemplateSettings.WhyRefused(new ConfiguredTemplate?[] { ATemplate("Household"), null });

        Assert.NotNull(why);
        Assert.StartsWith("The template at position 2 of Templates is empty", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// The all-zero identifier names no library.
    /// </summary>
    [Fact]
    public void AnAllZeroLibraryIdentifierIsRefused()
    {
        var template = ATemplate("Household");
        template.Libraries = [_films, Guid.Empty];

        var why = TemplateSettings.WhyRefused(template);

        Assert.NotNull(why);
        Assert.StartsWith("names a library by the all-zero identifier", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// A library named twice is a list nobody meant to write.
    /// </summary>
    [Fact]
    public void ALibraryNamedTwiceIsRefused()
    {
        var template = ATemplate("Household");
        template.Libraries = [_films, _music, _films];

        var why = TemplateSettings.WhyRefused(template);

        Assert.NotNull(why);
        Assert.StartsWith("names one library twice", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// A ceiling below zero is no ceiling at all, and each of the three is
    /// named as the one it is.
    /// </summary>
    /// <param name="which">Which ceiling is negative.</param>
    /// <param name="named">How the refusal names it.</param>
    [Theory]
    [InlineData("remote", "remote bitrate ceiling")]
    [InlineData("streams", "simultaneous stream ceiling")]
    [InlineData("rating", "parental rating ceiling")]
    public void ANegativeCeilingIsRefused(string which, string named)
    {
        var template = ATemplate("Household");
        template.RemoteBitrateCeiling = which == "remote" ? -1 : 8_000_000;
        template.SimultaneousStreamCeiling = which == "streams" ? -1 : 2;
        template.ParentalRatingCeiling = which == "rating" ? -1 : 12;

        var why = TemplateSettings.WhyRefused(template);

        Assert.NotNull(why);
        Assert.StartsWith("has a negative " + named, why, StringComparison.Ordinal);
    }

    /// <summary>
    /// Zero is a ceiling, absent is no ceiling, and a positive number is a
    /// ceiling; none of the three is refused.
    /// </summary>
    /// <param name="ceiling">The ceiling as written.</param>
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(1)]
    public void ACeilingAtOrAboveZeroIsAGrant(int? ceiling)
    {
        var template = ATemplate("Household");
        template.RemoteBitrateCeiling = ceiling;
        template.SimultaneousStreamCeiling = ceiling;
        template.ParentalRatingCeiling = ceiling;

        Assert.Null(TemplateSettings.WhyRefused(template));

        var grant = TemplateSettings.Of(template);
        Assert.Equal(ceiling, grant.RemoteBitrateCeiling);
        Assert.Equal(ceiling, grant.SimultaneousStreamCeiling);
        Assert.Equal(ceiling, grant.ParentalRatingCeiling);
    }

    /// <summary>
    /// The grant carries every value the template wrote, in the field it was
    /// written for, and a value the stored shape cannot carry arrives closed.
    /// </summary>
    [Fact]
    public void TheGrantCarriesEveryValueTheTemplateWrote()
    {
        var template = new ConfiguredTemplate
        {
            Label = "Household",
            Libraries = [_films, _music],
            MayDownload = true,
            MayPlayFromOutsideTheNetwork = false,
            MayControlOtherSessions = true,
            MayWatchLiveTelevision = true,
            MayManageLiveTelevision = true,
            MayDeleteContent = true,
            MayManageCollections = true,
            MayManageSubtitles = true,
            MayManageLyrics = true,
            MayChangeItsOwnPreferences = false,
            RemoteBitrateCeiling = 8_000_000,
            SimultaneousStreamCeiling = 2,
            ParentalRatingCeiling = 12,
        };

        var grant = TemplateSettings.Of(template);

        Assert.Equal(
            new AccountTemplate(
                libraries: ImmutableArray.Create(_films, _music),
                mayDownload: true,
                mayPlayFromOutsideTheNetwork: false,
                mayManage: false,
                mayControlOtherSessions: true,
                mayWatchLiveTelevision: true,
                mayManageLiveTelevision: true,
                mayDeleteContent: true,
                mayManageCollections: true,
                mayManageSubtitles: true,
                mayManageLyrics: true,
                mayChangeItsOwnPreferences: false,
                remoteBitrateCeiling: 8_000_000,
                simultaneousStreamCeiling: 2,
                parentalRatingCeiling: 12,
                serverDefaultsLeftAlone: ImmutableArray<string>.Empty),
            grant);
    }

    /// <summary>
    /// The grant the other way round, so a value passed straight through in
    /// one direction cannot be a value quietly inverted in the other.
    /// </summary>
    [Fact]
    public void TheGrantCarriesTheClosedValuesTheTemplateWroteAsWell()
    {
        var template = ATemplate("Household");
        template.Libraries = null;
        template.MayPlayFromOutsideTheNetwork = true;
        template.MayChangeItsOwnPreferences = true;

        var grant = TemplateSettings.Of(template);

        Assert.Empty(grant.Libraries);
        Assert.False(grant.MayDownload);
        Assert.True(grant.MayPlayFromOutsideTheNetwork);
        Assert.False(grant.MayManage);
        Assert.False(grant.MayControlOtherSessions);
        Assert.False(grant.MayWatchLiveTelevision);
        Assert.False(grant.MayManageLiveTelevision);
        Assert.False(grant.MayDeleteContent);
        Assert.False(grant.MayManageCollections);
        Assert.False(grant.MayManageSubtitles);
        Assert.False(grant.MayManageLyrics);
        Assert.True(grant.MayChangeItsOwnPreferences);
        Assert.Null(grant.RemoteBitrateCeiling);
        Assert.Null(grant.SimultaneousStreamCeiling);
        Assert.Null(grant.ParentalRatingCeiling);
        Assert.Empty(grant.ServerDefaultsLeftAlone);
    }

    /// <summary>
    /// The conversion refuses exactly what the sentence refuses, with the same
    /// words, so the load and the mint cannot disagree about what a grant is.
    /// </summary>
    /// <param name="fault">Which fault the template carries.</param>
    [Theory]
    [InlineData("label")]
    [InlineData("padding")]
    [InlineData("library")]
    [InlineData("ceiling")]
    public void TheConversionRefusesWhatTheSentenceRefusesWithTheSameWords(string fault)
    {
        var template = ATemplate("Household");
        switch (fault)
        {
            case "label":
                template.Label = " ";
                break;
            case "padding":
                template.Label = "Household ";
                break;
            case "library":
                template.Libraries = [Guid.Empty];
                break;
            default:
                template.ParentalRatingCeiling = -3;
                break;
        }

        var why = TemplateSettings.WhyRefused(template);
        var refused = Assert.Throws<ArgumentException>(() => TemplateSettings.Of(template));

        Assert.NotNull(why);
        Assert.Contains(why, refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A conversion handed nothing refuses before it reads anything.
    /// </summary>
    [Fact]
    public void TheConversionRefusesNothing()
    {
        Assert.Throws<ArgumentNullException>(() => TemplateSettings.Of(null!));
        Assert.Throws<ArgumentNullException>(() => TemplateSettings.Named(Array.Empty<ConfiguredTemplate>(), null!));
    }

    /// <summary>
    /// A name finds its template ignoring case, and finds the grant that
    /// template stands for rather than a neighbour's.
    /// </summary>
    /// <param name="typed">What the operator typed on the mint form.</param>
    [Theory]
    [InlineData("Guest")]
    [InlineData("guest")]
    [InlineData("GUEST")]
    public void ANameFindsItsTemplateIgnoringCase(string typed)
    {
        var guest = ATemplate("Guest");
        guest.Libraries = [_music];
        var templates = new[] { ATemplate("Household"), guest, ATemplate("Trial") };

        var grant = TemplateSettings.Named(templates, typed);

        Assert.NotNull(grant);
        Assert.Equal(TemplateSettings.Of(guest), grant);
        Assert.Equal(_music, Assert.Single(grant.Libraries));
    }

    /// <summary>
    /// A name no template carries finds nothing, and a name that is only a
    /// part of one is not that one.
    /// </summary>
    /// <param name="typed">What the operator typed on the mint form.</param>
    [Theory]
    [InlineData("Family")]
    [InlineData("Guests")]
    [InlineData("Gues")]
    [InlineData("")]
    public void ANameNoTemplateCarriesFindsNothing(string typed)
    {
        var templates = new[] { ATemplate("Household"), ATemplate("Guest") };

        Assert.Null(TemplateSettings.Named(templates, typed));
    }

    /// <summary>
    /// A list with a fault in one entry answers for no entry, including the
    /// entry that was asked for and is itself a grant.
    /// </summary>
    [Fact]
    public void AListWithAFaultAnswersForNoName()
    {
        var broken = ATemplate("Trial");
        broken.RemoteBitrateCeiling = -1;
        var templates = new[] { ATemplate("Household"), broken };

        var refused = Assert.Throws<ArgumentException>(() => TemplateSettings.Named(templates, "Household"));

        Assert.Contains("position 2", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A template with a name and the values a fresh entry carries.
    /// </summary>
    /// <param name="label">The name.</param>
    /// <returns>The template.</returns>
    private static ConfiguredTemplate ATemplate(string label)
    {
        return new ConfiguredTemplate { Label = label, Libraries = [_films] };
    }
}
