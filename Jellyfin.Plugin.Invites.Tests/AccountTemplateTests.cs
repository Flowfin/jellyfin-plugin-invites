using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Invites.Accounts;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The template is the shape a grant is carried in, so these are tests of that
/// shape. Nothing here decides what a template should grant: the libraries are
/// #63, the ceilings are #65, and the refusal of an account that manages
/// anything is #62.
/// </summary>
public class AccountTemplateTests
{
    /// <summary>
    /// The grants #61 and #64 name, one per property. A property with no entry
    /// is a grant nobody argued for, and an entry with no property is a grant
    /// an issue asked for and the type does not carry.
    /// </summary>
    private static readonly string[] GrantsTheIssueNames =
    {
        "Libraries",
        "MayDownload",
        "MayPlayFromOutsideTheNetwork",
        "MayManage",
        "MayControlOtherSessions",
        "MayWatchLiveTelevision",
        "MayManageLiveTelevision",
        "MayDeleteContent",
        "MayManageCollections",
        "MayManageSubtitles",
        "MayManageLyrics",
        "MayChangeItsOwnPreferences",
        "RemoteBitrateCeiling",
        "SimultaneousStreamCeiling",
        "ParentalRatingCeiling",
        "ServerDefaultsLeftAlone",
    };

    private static readonly Guid FirstLibrary = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid SecondLibrary = new Guid("aaaaaaaa-0000-0000-0000-000000000002");

    /// <summary>
    /// One template, built the same way every time, so a test that changes a
    /// single grant is changing a single grant.
    /// </summary>
    private static AccountTemplate Baseline(
        ImmutableArray<Guid>? libraries = null,
        bool mayDownload = false,
        bool mayPlayFromOutsideTheNetwork = true,
        bool mayManage = false,
        bool mayControlOtherSessions = false,
        bool mayWatchLiveTelevision = false,
        bool mayManageLiveTelevision = false,
        bool mayDeleteContent = false,
        bool mayManageCollections = false,
        bool mayManageSubtitles = false,
        bool mayManageLyrics = false,
        bool mayChangeItsOwnPreferences = true,
        int? remoteBitrateCeiling = 4_000_000,
        int? simultaneousStreamCeiling = 2,
        int? parentalRatingCeiling = 13,
        ImmutableArray<string>? serverDefaultsLeftAlone = null) =>
        new AccountTemplate(
            libraries ?? ImmutableArray.Create(FirstLibrary, SecondLibrary),
            mayDownload,
            mayPlayFromOutsideTheNetwork,
            mayManage,
            mayControlOtherSessions,
            mayWatchLiveTelevision,
            mayManageLiveTelevision,
            mayDeleteContent,
            mayManageCollections,
            mayManageSubtitles,
            mayManageLyrics,
            mayChangeItsOwnPreferences,
            remoteBitrateCeiling,
            simultaneousStreamCeiling,
            parentalRatingCeiling,
            serverDefaultsLeftAlone ?? ImmutableArray.Create("EnableSyncTranscoding", "MaxParentalSubRating"));

    /// <summary>
    /// The type carries the grants #61 names and nothing else. It reds in both
    /// directions, so a grant quietly added and a grant quietly dropped are
    /// both caught rather than only the first.
    /// </summary>
    [Fact]
    public void EveryGrantTheIssueNamesIsAPropertyAndThereAreNoOthers()
    {
        var properties = typeof(AccountTemplate)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var expected = GrantsTheIssueNames
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, properties);
    }

    /// <summary>
    /// Nothing on a built template can be changed. This is what makes the copy
    /// an invitation carries worth carrying: there is no edit anywhere that can
    /// reach a template somebody already holds.
    /// </summary>
    [Fact]
    public void NothingOnTheTemplateCanBeSetAfterItIsBuilt()
    {
        var settable = typeof(AccountTemplate)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .Select(property => property.Name)
            .ToList();

        Assert.Empty(settable);
    }

    /// <summary>
    /// The scan above reads properties, so a public field would be a member it
    /// never sees, and a public field is settable unless somebody remembered to
    /// say otherwise.
    /// </summary>
    [Fact]
    public void TheTemplateExposesNoPublicField()
    {
        var fields = typeof(AccountTemplate)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(field => field.Name)
            .ToList();

        Assert.Empty(fields);
    }

    /// <summary>
    /// Every grant arrives through the constructor and none of the parameters
    /// has a default value. That is the whole of "every field is explicit": a
    /// parameter with a default is a field that arrives unset at every call
    /// site written before it existed, which is the server-default problem
    /// moved inside the plugin.
    /// </summary>
    [Fact]
    public void EveryGrantArrivesThroughTheConstructorAndNoParameterHasADefault()
    {
        var constructor = Assert.Single(typeof(AccountTemplate).GetConstructors());

        var parameters = constructor.GetParameters()
            .Select(parameter => parameter.Name ?? string.Empty)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var expected = GrantsTheIssueNames
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Equal(expected, parameters, StringComparer.OrdinalIgnoreCase);
        Assert.All(constructor.GetParameters(), parameter => Assert.False(parameter.HasDefaultValue));
    }

    /// <summary>
    /// A library list nobody decided is refused rather than read as a grant of
    /// nothing. Delete the <c>IsDefault</c> guard on the libraries and this
    /// goes green on a template that granted whatever the caller forgot.
    /// </summary>
    [Fact]
    public void ALibraryListNobodyDecidedIsRefused()
    {
        var refusal = Assert.Throws<ArgumentException>(() => Baseline(libraries: default(ImmutableArray<Guid>)));

        Assert.Equal("libraries", refusal.ParamName);
    }

    /// <summary>
    /// The other direction of the same guard. Granting no library is a template
    /// somebody chose and it is kept as one.
    /// </summary>
    [Fact]
    public void AGrantOfNoLibraryIsKeptAsOne()
    {
        var template = Baseline(libraries: ImmutableArray<Guid>.Empty);

        Assert.Empty(template.Libraries);
    }

    /// <summary>
    /// A library named twice makes the count of what a template grants disagree
    /// with the count of what is written in it. Delete the repeat guard and
    /// this goes red.
    /// </summary>
    [Fact]
    public void ALibraryNamedTwiceIsRefused()
    {
        var refusal = Assert.Throws<ArgumentException>(
            () => Baseline(libraries: ImmutableArray.Create(FirstLibrary, FirstLibrary)));

        Assert.Equal("libraries", refusal.ParamName);
    }

    /// <summary>
    /// The same distinction on the fields left alone. An uninitialized list is
    /// nobody having decided; an empty one is a template that says it sets
    /// everything it touches.
    /// </summary>
    [Fact]
    public void AListOfFieldsLeftAloneThatNobodyDecidedIsRefused()
    {
        var refusal = Assert.Throws<ArgumentException>(
            () => Baseline(serverDefaultsLeftAlone: default(ImmutableArray<string>)));

        Assert.Equal("serverDefaultsLeftAlone", refusal.ParamName);
    }

    /// <summary>
    /// A blank entry names no field, so it cannot be told from a field nobody
    /// wrote down, which is the one thing the list exists to prevent.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankFieldLeftAloneIsRefused(string blank)
    {
        var refusal = Assert.Throws<ArgumentException>(
            () => Baseline(serverDefaultsLeftAlone: ImmutableArray.Create("EnableSyncTranscoding", blank)));

        Assert.Equal("serverDefaultsLeftAlone", refusal.ParamName);
    }

    /// <summary>
    /// A field left alone twice, refused for the reason a library named twice
    /// is.
    /// </summary>
    [Fact]
    public void AFieldLeftAloneTwiceIsRefused()
    {
        var refusal = Assert.Throws<ArgumentException>(
            () => Baseline(serverDefaultsLeftAlone: ImmutableArray.Create("EnableSyncTranscoding", "EnableSyncTranscoding")));

        Assert.Equal("serverDefaultsLeftAlone", refusal.ParamName);
    }

    /// <summary>
    /// A ceiling below zero is not a smaller allowance. Every ceiling is
    /// driven, because a guard written once and called three times is a guard
    /// that can lose one of its three call sites without any single test
    /// noticing.
    /// </summary>
    [Theory]
    [InlineData("remoteBitrateCeiling")]
    [InlineData("simultaneousStreamCeiling")]
    [InlineData("parentalRatingCeiling")]
    public void ANegativeCeilingIsRefused(string which)
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => which switch
        {
            "remoteBitrateCeiling" => Baseline(remoteBitrateCeiling: -1),
            "simultaneousStreamCeiling" => Baseline(simultaneousStreamCeiling: -1),
            _ => Baseline(parentalRatingCeiling: -1),
        });

        Assert.Equal(which, refusal.ParamName);
    }

    /// <summary>
    /// No ceiling at all is a grant somebody made rather than a field left out.
    /// It survives construction as itself, which is what lets #103 name this
    /// template as the source of an unlimited policy field instead of guessing.
    /// </summary>
    [Fact]
    public void NoCeilingIsAStatedGrantRatherThanAnAbsentOne()
    {
        var template = Baseline(
            remoteBitrateCeiling: null,
            simultaneousStreamCeiling: null,
            parentalRatingCeiling: null);

        Assert.Null(template.RemoteBitrateCeiling);
        Assert.Null(template.SimultaneousStreamCeiling);
        Assert.Null(template.ParentalRatingCeiling);
        Assert.Empty(template.ServerDefaultsLeftAlone.Where(name => name.Contains("Ceiling", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Two templates granting the same things are equal, by their contents and
    /// not by which arrays they were built from. Without this, a template read
    /// back off disk could never be shown to be the one that was written.
    /// </summary>
    [Fact]
    public void TwoTemplatesGrantingTheSameThingsAreEqual()
    {
        var one = Baseline();
        var another = Baseline(
            libraries: ImmutableArray.Create(FirstLibrary, SecondLibrary),
            serverDefaultsLeftAlone: ImmutableArray.Create("EnableSyncTranscoding", "MaxParentalSubRating"));

        Assert.False(ReferenceEquals(one, another));
        Assert.Equal(one, another);
        Assert.Equal(one.GetHashCode(), another.GetHashCode());
    }

    /// <summary>
    /// A template differing in one grant is a different template. Every grant
    /// is driven separately, because an equality that forgot one field would be
    /// an equality that says two different grants are the same, and the test
    /// below leans on it.
    /// </summary>
    [Theory]
    [InlineData("Libraries")]
    [InlineData("MayDownload")]
    [InlineData("MayPlayFromOutsideTheNetwork")]
    [InlineData("MayManage")]
    [InlineData("MayControlOtherSessions")]
    [InlineData("MayWatchLiveTelevision")]
    [InlineData("MayManageLiveTelevision")]
    [InlineData("MayDeleteContent")]
    [InlineData("MayManageCollections")]
    [InlineData("MayManageSubtitles")]
    [InlineData("MayManageLyrics")]
    [InlineData("MayChangeItsOwnPreferences")]
    [InlineData("RemoteBitrateCeiling")]
    [InlineData("SimultaneousStreamCeiling")]
    [InlineData("ParentalRatingCeiling")]
    [InlineData("ServerDefaultsLeftAlone")]
    public void ATemplateDifferingInOneGrantIsNotEqual(string grant)
    {
        var changed = grant switch
        {
            "Libraries" => Baseline(libraries: ImmutableArray.Create(FirstLibrary)),
            "MayDownload" => Baseline(mayDownload: true),
            "MayPlayFromOutsideTheNetwork" => Baseline(mayPlayFromOutsideTheNetwork: false),
            "MayManage" => Baseline(mayManage: true),
            "MayControlOtherSessions" => Baseline(mayControlOtherSessions: true),
            "MayWatchLiveTelevision" => Baseline(mayWatchLiveTelevision: true),
            "MayManageLiveTelevision" => Baseline(mayManageLiveTelevision: true),
            "MayDeleteContent" => Baseline(mayDeleteContent: true),
            "MayManageCollections" => Baseline(mayManageCollections: true),
            "MayManageSubtitles" => Baseline(mayManageSubtitles: true),
            "MayManageLyrics" => Baseline(mayManageLyrics: true),
            "MayChangeItsOwnPreferences" => Baseline(mayChangeItsOwnPreferences: false),
            "RemoteBitrateCeiling" => Baseline(remoteBitrateCeiling: null),
            "SimultaneousStreamCeiling" => Baseline(simultaneousStreamCeiling: 3),
            "ParentalRatingCeiling" => Baseline(parentalRatingCeiling: 18),
            _ => Baseline(serverDefaultsLeftAlone: ImmutableArray.Create("EnableSyncTranscoding")),
        };

        Assert.NotEqual(Baseline(), changed);
    }

    /// <summary>
    /// The clause #61 asks for, at the level the type can carry it: a copy
    /// taken out of the named templates is unaffected by the name being given a
    /// different template afterwards.
    /// </summary>
    /// <remarks>
    /// The named templates are a dictionary here because the configured ones
    /// are #86 and do not exist. What the test proves is the property that
    /// makes the clause true whatever holds the names: the value is copied at
    /// the moment of minting and the name is never resolved again, so an edit
    /// to the name reaches nothing that was minted before it. Turn the copy
    /// into a lookup of <c>named[label]</c> at the assertion and this goes red.
    /// The other half of the clause, that an invitation record carries the
    /// copy, is not asserted here, because the record does not carry one yet.
    /// </remarks>
    [Fact]
    public void EditingANamedTemplateLeavesACopyTakenEarlierUnchanged()
    {
        const string Label = "Household";
        var named = new Dictionary<string, AccountTemplate>(StringComparer.Ordinal)
        {
            [Label] = Baseline(),
        };

        var carriedByTheInvitation = named[Label];

        named[Label] = Baseline(mayManage: true, mayDownload: true);

        Assert.Equal(Baseline(), carriedByTheInvitation);
        Assert.NotEqual(named[Label], carriedByTheInvitation);
    }
}
