using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Invites.Configuration;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// <para>
/// The configuration page against the configuration type, which is #462: a
/// setting an operator cannot reach from the page is one they can only change
/// by editing a file on the server, and nothing said so when the three numbers
/// landed under #86.
/// </para>
/// <para>
/// <b>This is the third reading of the same type and it asks a different
/// question from the other two.</b>
/// <c>.github/lint/configuration-reference.sh</c> asks whether a setting is
/// explained, <see cref="FreshInstallConfigurationTests"/> asks what it is worth
/// on a server nobody configured, and this asks whether anybody can change it.
/// A setting can pass both of the others and still be unreachable.
/// </para>
/// <para>
/// The bounds a field declares are read off the field and compared against the
/// source of the rule rather than against a number written here, so a range that
/// moves in <see cref="NumberSettings"/> and not on the page reds this rather
/// than shipping a form that promises what the plugin refuses.
/// </para>
/// </summary>
public class EverySettingHasAControlTests
{
    private const string PageResource = "Jellyfin.Plugin.Invites.Configuration.configPage.html";

    /// <summary>
    /// The settings the page reaches by hand rather than through a
    /// <c>data-setting</c> field, with the reason for each.
    /// </summary>
    /// <remarks>
    /// One entry. The template list is not a value a single control can carry:
    /// the page builds a block of controls per entry and reads them back into a
    /// list, which is what #435 landed, so it is reachable and it is reachable
    /// some other way. A second entry added here without a reason is the shape
    /// this table exists to make somebody argue for.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> ReachedByHand =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(PluginConfiguration.Templates)] =
                "A list rather than a value. The page renders one block of controls per entry and reads them back into a list, under #435, so it is edited on the page and not through a single field.",
        };

    /// <summary>
    /// The bounds each number field must declare, taken from the routine that
    /// refuses a value outside them rather than from a literal here.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, (int Least, int Most)> Ranges =
        new Dictionary<string, (int, int)>(StringComparer.Ordinal)
        {
            [nameof(PluginConfiguration.RecordRetentionDays)] =
                (NumberSettings.FewestRetentionDays, NumberSettings.MostRetentionDays),
            [nameof(PluginConfiguration.RedemptionAttemptsPerAddressInAnHour)] =
                (NumberSettings.FewestAttempts, NumberSettings.MostAttemptsPerAddressInAnHour),
            [nameof(PluginConfiguration.RedemptionAttemptsPerSecond)] =
                (NumberSettings.FewestAttempts, NumberSettings.MostAttemptsPerSecond),
        };

    private static readonly Regex _settingField = new(
        @"data-setting=""([A-Za-z_][A-Za-z0-9_]*)""",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The scan finds a field when there is one. Without this, a page whose
    /// attribute spelling changed and a page with no fields at all report the
    /// same silence, and every assertion below compares two empty sets.
    /// </summary>
    [Fact]
    public void TheScanFindsAFieldWhenThereIsOne()
    {
        Assert.NotEmpty(FieldsOnThePage());
        Assert.NotEmpty(SettingsOnTheType());
    }

    /// <summary>
    /// Every setting an operator can configure has a control on the page, or is
    /// named above as reached some other way with the reason written down.
    /// </summary>
    [Fact]
    public void EverySettingIsReachableFromThePage()
    {
        var fields = FieldsOnThePage();

        var unreachable = SettingsOnTheType()
            .Where(setting => !fields.Contains(setting) && !ReachedByHand.ContainsKey(setting))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            unreachable.Count == 0,
            "These settings have no control on the configuration page and no entry saying how else they are reached: "
            + string.Join(", ", unreachable)
            + ". An operator who only ever opens the page cannot change them, so the setting is one they would have to edit a file on the server for.");
    }

    /// <summary>
    /// And every field on the page names a setting the type has. A field left
    /// behind by a removed or renamed setting writes a member the plugin does
    /// not read, and an operator watches it save and do nothing.
    /// </summary>
    [Fact]
    public void EveryFieldOnThePageNamesASetting()
    {
        var settings = SettingsOnTheType();

        var orphaned = FieldsOnThePage()
            .Where(field => !settings.Contains(field))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            orphaned.Count == 0,
            "These fields on the configuration page name no setting on the type: "
            + string.Join(", ", orphaned)
            + ". Remove the field in the change that removes or renames the setting.");
    }

    /// <summary>
    /// And every name in the table above is still a setting, so a reason kept
    /// after its setting was removed does not make the table read as covering
    /// more than it does.
    /// </summary>
    [Fact]
    public void EveryReasonNamesASettingThatStillExists()
    {
        var settings = SettingsOnTheType();

        Assert.All(
            ReachedByHand,
            entry =>
            {
                Assert.Contains(entry.Key, settings);
                Assert.False(
                    string.IsNullOrWhiteSpace(entry.Value),
                    "The entry for " + entry.Key + " carries no reason, and a reason is the whole of what this table is.");
            });
    }

    /// <summary>
    /// Each of the three numbers declares the range its own rule enforces. A
    /// field that let an operator type a larger number than the plugin will
    /// accept is a form promising something the load refuses.
    /// </summary>
    /// <param name="setting">The setting whose field is read.</param>
    [Theory]
    [InlineData(nameof(PluginConfiguration.RecordRetentionDays))]
    [InlineData(nameof(PluginConfiguration.RedemptionAttemptsPerAddressInAnHour))]
    [InlineData(nameof(PluginConfiguration.RedemptionAttemptsPerSecond))]
    public void ANumberFieldDeclaresTheRangeItsRuleEnforces(string setting)
    {
        var page = ReadPage();
        var range = Ranges[setting];
        var field = FieldFor(page, setting);

        Assert.Contains("type=\"number\"", field, StringComparison.Ordinal);
        Assert.Contains(
            "min=\"" + range.Least.ToString(CultureInfo.InvariantCulture) + "\"",
            field,
            StringComparison.Ordinal);
        Assert.Contains(
            "max=\"" + range.Most.ToString(CultureInfo.InvariantCulture) + "\"",
            field,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The page sends a number rather than the string a text field would hand
    /// back. The plugin reads these three as whole numbers, and a string is
    /// refused by a deserialiser rather than by anything an operator can see.
    /// </summary>
    [Fact]
    public void ANumberFieldIsSentAsANumber()
    {
        var page = ReadPage();
        var script = page[page.IndexOf("<script", StringComparison.Ordinal)..];

        Assert.Contains("field.type === \"number\" ? Number(field.value)", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// A value the settings would refuse is refused by the page rather than
    /// saved, and it is refused rather than corrected. Nudging it into range
    /// here would be the silent correction the whole arrangement exists
    /// against, one step earlier than the load.
    /// </summary>
    [Fact]
    public void APageThatWouldSaveARefusedNumberRefusesFirst()
    {
        var page = ReadPage();
        var script = page[page.IndexOf("<script", StringComparison.Ordinal)..];

        Assert.Contains("invitesWhyTheNumbersCannotBeSaved", script, StringComparison.Ordinal);

        var refusal = script.IndexOf("var refusal = invitesWhyTheNumbersCannotBeSaved();", StringComparison.Ordinal);
        var save = script.IndexOf("ApiClient.updatePluginConfiguration(", StringComparison.Ordinal);

        Assert.True(
            refusal >= 0 && save > refusal,
            "The refusal is read at " + refusal + " and the save is made at " + save
            + ". A page that asked afterwards would have written the value it then complained about.");

        // The correction this page must not make. A clamp is the one repair
        // somebody reaches for on a form with a range, and it is exactly the
        // silent fallback docs/configuration.md refuses.
        Assert.DoesNotContain("Math.min(", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.max(", script, StringComparison.Ordinal);
    }

    private static string FieldFor(string page, string setting)
    {
        var at = page.IndexOf("data-setting=\"" + setting + "\"", StringComparison.Ordinal);
        Assert.True(at >= 0, "The page has no field for " + setting + ".");

        var opens = page.LastIndexOf('<', at);
        var closes = page.IndexOf('>', at);

        return page[opens..closes];
    }

    private static IReadOnlyCollection<string> FieldsOnThePage() =>
        new HashSet<string>(
            _settingField.Matches(ReadPage()).Select(match => match.Groups[1].Value),
            StringComparer.Ordinal);

    /// <summary>
    /// The settings the type declares, by the same definition
    /// <c>.github/lint/configuration-reference.sh</c> and
    /// <see cref="FreshInstallConfigurationTests"/> use: a public instance
    /// property with a public setter, stopping at the framework base.
    /// </summary>
    /// <returns>The names.</returns>
    private static IReadOnlyCollection<string> SettingsOnTheType()
    {
        var declared = new List<PropertyInfo>();
        for (var type = typeof(PluginConfiguration);
             type is not null && type != typeof(MediaBrowser.Model.Plugins.BasePluginConfiguration);
             type = type.BaseType)
        {
            declared.AddRange(type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        }

        return new HashSet<string>(
            declared
                .Where(property => property.SetMethod is not null && property.SetMethod.IsPublic)
                .Select(property => property.Name),
            StringComparer.Ordinal);
    }

    private static string ReadPage()
    {
        using var stream = typeof(Plugin).Assembly.GetManifestResourceStream(PageResource)
            ?? throw new InvalidOperationException(PageResource + " is not an embedded resource of the plugin assembly.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
