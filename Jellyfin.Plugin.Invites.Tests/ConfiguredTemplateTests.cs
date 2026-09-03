using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Configuration;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The stored shape of a template, held to the grant it stands for and to the
/// two readers the server puts it through.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here has run against a server. What is exercised is the shape the
/// server's own configuration mechanism writes and reads, through the same two
/// serializers it uses: the XML one that writes the file, and the JSON one
/// behind the configuration page. That a server does put this type through
/// them is a property of the base class the type inherits and is not asserted
/// here.
/// </para>
/// <para>
/// The first assertion is the one to read. It holds the stored shape to the
/// grant member by member, so a grant that grows a field without a stored
/// counterpart reds here and somebody decides whether an operator may write
/// it, rather than the field arriving on every configured template at whatever
/// the grant's constructor was handed.
/// </para>
/// </remarks>
public class ConfiguredTemplateTests
{
    private static readonly Guid _films = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid _music = Guid.Parse("22222222-2222-4222-8222-222222222222");

    /// <summary>
    /// The two members the stored shape may not carry: the administrator
    /// question, which #62 refuses everywhere and which a configuration file
    /// must have no spelling for, and the list of policy fields left alone,
    /// which names the server's own policy and is nothing an operator can
    /// write.
    /// </summary>
    private static readonly string[] _keptOffTheStoredShape = ["MayManage", "ServerDefaultsLeftAlone"];

    /// <summary>
    /// Every settable member of the stored shape is a member of the grant, and
    /// every member of the grant is a settable member of the stored shape,
    /// except the two named above and the label that names the template.
    /// </summary>
    [Fact]
    public void TheStoredShapeMirrorsTheGrantExceptForTheTwoItMayNotCarry()
    {
        var stored = typeof(ConfiguredTemplate)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is not null && property.SetMethod.IsPublic)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var expected = typeof(AccountTemplate)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Where(name => !_keptOffTheStoredShape.Contains(name, StringComparer.Ordinal))
            .Append(nameof(ConfiguredTemplate.Label))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, stored);
    }

    /// <summary>
    /// No spelling in the configuration file asks for an account that manages
    /// the server, because the stored shape has no member for it, and the grant
    /// handed on has it closed whatever else was written.
    /// </summary>
    [Fact]
    public void NoMemberOfTheStoredShapeCanAskForAnAccountThatManagesTheServer()
    {
        Assert.Null(typeof(ConfiguredTemplate).GetProperty("MayManage", BindingFlags.Public | BindingFlags.Instance));

        Assert.False(TemplateSettings.Of(AGenerousTemplate("Everything open")).MayManage);
    }

    /// <summary>
    /// A member left out of the file is worth the posture #64 decided: every
    /// permission closed except the two that reach nothing beyond the invited
    /// person, no library, no ceiling, and no name.
    /// </summary>
    [Fact]
    public void AMemberLeftOutOfTheFileIsWorthThePostureDecided()
    {
        var fresh = new ConfiguredTemplate();
        var open = new[]
        {
            nameof(ConfiguredTemplate.MayPlayFromOutsideTheNetwork),
            nameof(ConfiguredTemplate.MayChangeItsOwnPreferences),
        };

        Assert.Equal(string.Empty, fresh.Label);
        var libraries = fresh.Libraries;
        Assert.NotNull(libraries);
        Assert.Empty(libraries);
        Assert.Null(fresh.RemoteBitrateCeiling);
        Assert.Null(fresh.SimultaneousStreamCeiling);
        Assert.Null(fresh.ParentalRatingCeiling);

        var permissions = typeof(ConfiguredTemplate)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(bool))
            .ToList();
        Assert.Equal(10, permissions.Count);
        foreach (var permission in permissions)
        {
            Assert.Equal(open.Contains(permission.Name, StringComparer.Ordinal), (bool)permission.GetValue(fresh)!);
        }
    }

    /// <summary>
    /// The shape survives the XML reader the server writes the configuration
    /// file with and reads it back through, member by member, for a template
    /// with every value moved off its default and one with none.
    /// </summary>
    [Fact]
    public void TheShapeSurvivesTheServersXmlReader()
    {
        var written = AConfiguration();
        var serializer = new XmlSerializer(typeof(PluginConfiguration));

        using var stream = new MemoryStream();
        serializer.Serialize(stream, written);
        stream.Position = 0;
        var read = (PluginConfiguration)serializer.Deserialize(stream)!;

        AssertTheSameTemplates(written, read);
    }

    /// <summary>
    /// And the JSON reader behind the configuration page, which is the route a
    /// save from that page takes: the page reads the whole configuration,
    /// changes the fields it shows, and sends the whole configuration back.
    /// A shape this reader dropped would be dropped on the first save of an
    /// unrelated setting.
    /// </summary>
    [Fact]
    public void TheShapeSurvivesTheJsonRoundTripTheConfigurationPageMakes()
    {
        var written = AConfiguration();

        var read = JsonSerializer.Deserialize<PluginConfiguration>(JsonSerializer.Serialize(written))!;

        AssertTheSameTemplates(written, read);
    }

    /// <summary>
    /// A configuration file written before this setting existed carries no
    /// element for it, and reads as no templates rather than as a null an
    /// operator never wrote.
    /// </summary>
    [Fact]
    public void AFileWithNoTemplatesElementReadsAsNoTemplates()
    {
        const string before = "<?xml version=\"1.0\"?><PluginConfiguration><PublicBaseUrl>https://media.example.org</PublicBaseUrl></PluginConfiguration>";
        var serializer = new XmlSerializer(typeof(PluginConfiguration));

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(before));
        var read = (PluginConfiguration)serializer.Deserialize(stream)!;

        Assert.Equal("https://media.example.org", read.PublicBaseUrl);
        var templates = read.Templates;
        Assert.NotNull(templates);
        Assert.Empty(templates);
    }

    /// <summary>
    /// Two configurations carry the same templates when each pair has the same
    /// label and stands for the same grant.
    /// </summary>
    /// <param name="written">What was serialised.</param>
    /// <param name="read">What came back.</param>
    private static void AssertTheSameTemplates(PluginConfiguration written, PluginConfiguration read)
    {
        var before = written.Templates;
        var after = read.Templates;
        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.Equal(before.Length, after.Length);
        for (var index = 0; index < before.Length; index++)
        {
            Assert.Equal(before[index].Label, after[index].Label);
            Assert.Equal(TemplateSettings.Of(before[index]), TemplateSettings.Of(after[index]));
        }
    }

    /// <summary>
    /// A configuration holding one template with every value moved off its
    /// default and one with nothing but a name.
    /// </summary>
    /// <returns>The configuration.</returns>
    private static PluginConfiguration AConfiguration()
    {
        return new PluginConfiguration
        {
            PublicBaseUrl = "https://media.example.org",
            Templates =
            [
                AGenerousTemplate("Household"),
                new ConfiguredTemplate { Label = "Guest" },
            ],
        };
    }

    /// <summary>
    /// A template with every permission open, two libraries and all three
    /// ceilings set, so a round trip that lost any one value would show it.
    /// </summary>
    /// <param name="label">The name.</param>
    /// <returns>The template.</returns>
    private static ConfiguredTemplate AGenerousTemplate(string label)
    {
        return new ConfiguredTemplate
        {
            Label = label,
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
    }
}
