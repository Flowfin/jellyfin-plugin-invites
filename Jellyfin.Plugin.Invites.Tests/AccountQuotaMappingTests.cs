using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.Invites.Accounts;
using MediaBrowser.Model.Users;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The quotas an account template carries, against the fields of the server's
/// own user policy they are handed to.
/// </summary>
/// <remarks>
/// <para>
/// #65 asks that every quota in the template map to a policy field the server
/// enforces, and that a quota the server cannot be handed is removed from the
/// template rather than faked. A quota with no field behind it would have to be
/// enforced by this plugin at request time, which would put the plugin in the
/// path of every playback on the server, and that is a much larger thing than a
/// quota.
/// </para>
/// <para>
/// The mapping is written here rather than argued in a document, because a
/// mapping in a document drifts against the package the plugin compiles
/// against, and this is the one form of it a server line can break loudly. The
/// version read is the one <c>Directory.Build.props</c> pins and this project
/// restores.
/// </para>
/// <para>
/// <b>What this does not say.</b> That the field exists on the policy is what
/// is checked. That the server acts on it was not measured here: nothing in
/// this repository runs a server, and reading a property off an assembly says
/// what the policy carries and not what reads it. The two are different claims
/// and only the first is made.
/// </para>
/// </remarks>
public class AccountQuotaMappingTests
{
    /// <summary>
    /// Each quota on the template, against the field of
    /// <see cref="UserPolicy"/> it is handed to and the type that field has.
    /// The type is part of the mapping rather than a detail of it: it is what
    /// decides how a template granting no ceiling is written on the other side.
    /// </summary>
    private static readonly Dictionary<string, (string Field, Type Type)> Mapping =
        new(StringComparer.Ordinal)
        {
            ["RemoteBitrateCeiling"] = ("RemoteClientBitrateLimit", typeof(int)),
            ["SimultaneousStreamCeiling"] = ("MaxActiveSessions", typeof(int)),
            ["ParentalRatingCeiling"] = ("MaxParentalRating", typeof(int?)),
        };

    /// <summary>
    /// Every quota the template carries names a field the server's policy has.
    /// Misspell either side of a row and this goes red, which is the whole
    /// reason the mapping is a value here and not a sentence somewhere.
    /// </summary>
    [Theory]
    [InlineData("RemoteBitrateCeiling")]
    [InlineData("SimultaneousStreamCeiling")]
    [InlineData("ParentalRatingCeiling")]
    public void AQuotaOnTheTemplateNamesAFieldTheServerPolicyCarries(string quota)
    {
        var (field, type) = Mapping[quota];

        var property = typeof(UserPolicy).GetProperty(field);

        Assert.NotNull(property);
        Assert.Equal(type, property.PropertyType);
    }

    /// <summary>
    /// The mapping covers the template's quotas and nothing else. A quota added
    /// to the template without a row here is a value with nowhere on the
    /// server to go, which is the case #65 refuses by name, and a row here for
    /// a property the template does not have is a mapping of nothing.
    /// </summary>
    [Fact]
    public void EveryQuotaOnTheTemplateHasARowAndEveryRowIsAQuota()
    {
        var quotasOnTheTemplate = typeof(AccountTemplate)
            .GetProperties()
            .Where(property => property.PropertyType == typeof(int?))
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var mapped = Mapping.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(mapped, quotasOnTheTemplate);
    }

    /// <summary>
    /// Two of the three fields are not nullable on the server, so a template
    /// granting no ceiling cannot be handed a null and the value it is handed
    /// instead has to be one the server reads as unlimited. Recording which of
    /// the three is which is what keeps #69 from writing a zero into the one
    /// field where zero is a real limit.
    /// </summary>
    [Fact]
    public void OnlyTheParentalRatingFieldTakesAnAbsentValueOnTheServer()
    {
        var nullable = Mapping
            .Where(row => row.Value.Type == typeof(int?))
            .Select(row => row.Key)
            .ToList();

        Assert.Equal(new[] { "ParentalRatingCeiling" }, nullable);
    }

    /// <summary>
    /// The access schedule #65 lists as a quota is not on the template, and the
    /// server carries a field for it. This asserts the second half, so the gap
    /// is a measured one rather than an assumption, and it goes red on the day
    /// somebody adds the field to the template without deciding the first half.
    /// </summary>
    /// <remarks>
    /// #65's body names four quotas and the template carries three. The reason
    /// the fourth is absent is that #61 named the template's fields and an
    /// access schedule was not among them, so the two issues disagree and the
    /// disagreement is written on #65 rather than settled here.
    /// </remarks>
    [Fact]
    public void TheAccessScheduleIsAPolicyFieldTheTemplateDoesNotCarry()
    {
        Assert.NotNull(typeof(UserPolicy).GetProperty("AccessSchedules"));

        var onTheTemplate = typeof(AccountTemplate)
            .GetProperties()
            .Select(property => property.Name)
            .ToList();

        Assert.DoesNotContain("AccessSchedules", onTheTemplate);
    }
}
