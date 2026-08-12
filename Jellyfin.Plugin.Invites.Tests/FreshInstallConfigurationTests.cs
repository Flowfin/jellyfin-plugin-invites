using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Invites.Configuration;
using MediaBrowser.Model.Plugins;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A configuration type declared by the suite and never by the plugin, so the
/// scan below can be shown to see a setting when there is one. An empty result
/// over the real configuration type has to mean the type carries no settings,
/// not that the scan went blind.
/// </summary>
public sealed class ProbeConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets a setting written the ordinary way. Nothing reads it.
    /// </summary>
    public bool AllowEveryLibrary { get; set; }
}

/// <summary>
/// What a setting is worth on a server where nobody has opened the
/// configuration page.
/// </summary>
/// <param name="Value">
/// The value a fresh install carries, compared against the one the type
/// actually produces.
/// </param>
/// <param name="Source">
/// Why that value is the closed one, or why it is not and what was traded for
/// it. A sentence rather than a word, because this is the column somebody has
/// to disagree with in review before a permissive default ships.
/// </param>
public sealed record FreshInstallValue(object? Value, string Source);

/// <summary>
/// <para>
/// A fresh install is a server that has this plugin and has never had its
/// configuration page opened, which is the state every install passes through
/// and most of them stay in. What it is worth defending against is not an
/// operator changing a default; it is a default arriving that nobody decided,
/// in a change about something else, and shipping because no test named it.
/// </para>
/// <para>
/// So the assertion is not a list of expected values. A list of expected values
/// passes happily when a new setting appears beside the ones it names, which is
/// the case that actually happens. It is a table every setting has to be in,
/// and a setting in neither the table nor the type turns this red in whichever
/// direction it went missing.
/// </para>
/// <para>
/// The table is empty today, because the configuration type carries no settings
/// at all, and it is landed empty on purpose. Written now, the first setting
/// added to the plugin turns this red and its author has to state the
/// fresh-install value and the reason it is the closed one in the same change.
/// Written afterwards, it starts as a snapshot of whatever the defaults happened
/// to be, which is the thing it exists to refuse.
/// </para>
/// <para>
/// This does not replace <c>.github/lint/configuration-reference.sh</c> and
/// does not repeat it. That check holds the type to the rows of
/// docs/configuration.md, so an operator meeting a setting finds it explained;
/// this one holds the type to a decided value, so a setting nobody decided
/// cannot ship quietly. The two use the same definition of a setting, a public
/// instance property with a setter, and reach it differently: the lint matches
/// the spelling of a declaration in the source and says so, and the scan below
/// reads the compiled type, so a property spelled across several lines or
/// produced by a source generator is invisible to the lint and visible here.
/// </para>
/// </summary>
public class FreshInstallConfigurationTests
{
    /// <summary>
    /// Every setting of <see cref="PluginConfiguration"/>, with what a fresh
    /// install is worth and why that is the closed answer.
    /// </summary>
    /// <remarks>
    /// One row, since #50 landed the first setting. The table was landed empty
    /// before that on purpose, so the first setting to arrive turned this red
    /// and its author had to state the fresh-install value and the reason it is
    /// the closed one in the same change.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, FreshInstallValue> FreshInstall =
        new Dictionary<string, FreshInstallValue>(StringComparer.Ordinal)
        {
            [nameof(PluginConfiguration.PublicBaseUrl)] = new FreshInstallValue(
                string.Empty,
                "Empty, which builds no invitation link at all. The closed answer here is not a safe address, it is no address: anything this plugin could pick without being told would be right on some networks and wrong on the operator's, and a link that resolves somewhere unintended is the failure #50 exists against. An unset address refuses at the moment a link is asked for, naming the setting, which is a support question with an answer."),
        };

    /// <summary>
    /// The settings a configuration type declares.
    /// <para>
    /// A setting is a public instance property with a setter, which is the same
    /// definition <c>.github/lint/configuration-reference.sh</c> uses and for
    /// the same reason: the server deserialises the stored configuration file
    /// into the setters, so a property nothing can write is not part of what an
    /// operator configures.
    /// </para>
    /// <para>
    /// The walk stops at <see cref="BasePluginConfiguration"/> rather than at
    /// the assembly boundary. What the framework declares on that base is part
    /// of every plugin's configuration and is not this plugin's decision to
    /// state a value for, and naming the boundary type says which line was
    /// drawn instead of leaving it to where the code happens to live.
    /// </para>
    /// </summary>
    /// <param name="configuration">The configuration type to read.</param>
    /// <returns>The names of the settings it declares, ordered.</returns>
    private static IReadOnlyList<string> SettingsDeclaredBy(Type configuration)
    {
        var declared = new List<PropertyInfo>();
        for (var type = configuration; type is not null && type != typeof(BasePluginConfiguration); type = type.BaseType)
        {
            declared.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        }

        return declared
            .Where(property => property.SetMethod is not null && property.SetMethod.IsPublic)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The scan sees a setting when there is one. Without this, the assertions
    /// below report the same thing for a configuration type with no settings
    /// and for a scan that stopped working, and only one of those is worth a
    /// green mark.
    /// </summary>
    [Fact]
    public void TheScanFindsASettingWhenThereIsOne()
    {
        var found = SettingsDeclaredBy(typeof(ProbeConfiguration));

        Assert.Contains(nameof(ProbeConfiguration.AllowEveryLibrary), found, StringComparer.Ordinal);
    }

    /// <summary>
    /// And it reports nothing but the setting the type itself declares. A scan
    /// that swept up whatever the framework base carries would demand a
    /// fresh-install value for a field this plugin does not own, and the first
    /// person to meet that red mark would widen the table rather than the scan.
    /// </summary>
    [Fact]
    public void TheScanStopsAtTheFrameworkBase()
    {
        Assert.Equal(
            new[] { nameof(ProbeConfiguration.AllowEveryLibrary) },
            SettingsDeclaredBy(typeof(ProbeConfiguration)));
    }

    /// <summary>
    /// Every setting the plugin declares is in the table. A setting that is not
    /// is not a setting somebody forgot to document; it is one whose value on a
    /// server nobody has configured was never decided by anybody.
    /// </summary>
    [Fact]
    public void EverySettingHasADecidedFreshInstallValue()
    {
        var undecided = SettingsDeclaredBy(typeof(PluginConfiguration))
            .Where(setting => !FreshInstall.ContainsKey(setting))
            .ToList();

        Assert.True(
            undecided.Count == 0,
            "These settings have no fresh-install value in "
            + nameof(FreshInstallConfigurationTests)
            + ": "
            + string.Join(", ", undecided)
            + ". State what a server that never opened the configuration page is worth for each, and why that is the closed answer, in the change that adds the setting.");
    }

    /// <summary>
    /// And every row in the table is still a setting. A row left behind by a
    /// removed or renamed setting is a decision about a field nobody can set,
    /// and it makes the table read as covering more than it does.
    /// </summary>
    [Fact]
    public void EveryDecidedValueIsStillASetting()
    {
        var declared = SettingsDeclaredBy(typeof(PluginConfiguration));

        var orphaned = FreshInstall.Keys
            .Where(setting => !declared.Contains(setting, StringComparer.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            orphaned.Count == 0,
            "These names have a fresh-install value and are settings of nothing: "
            + string.Join(", ", orphaned)
            + ". Remove the row in the change that removes or renames the setting.");
    }

    /// <summary>
    /// What the type produces with nothing stored is what the table says it is.
    /// A fresh install is exactly this: the server finds no configuration file
    /// and constructs the type, so the values a bare instance carries are the
    /// ones an unconfigured server runs on.
    /// </summary>
    [Fact]
    public void AFreshInstallCarriesTheDecidedValues()
    {
        var fresh = new PluginConfiguration();

        foreach (var (setting, decided) in FreshInstall)
        {
            var property = typeof(PluginConfiguration).GetProperty(setting, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(property);

            Assert.Equal(decided.Value, property!.GetValue(fresh));
            Assert.False(
                string.IsNullOrWhiteSpace(decided.Source),
                "The fresh-install value of " + setting + " carries no reason. The reason is the column somebody argues with before a permissive default ships.");
        }
    }
}
