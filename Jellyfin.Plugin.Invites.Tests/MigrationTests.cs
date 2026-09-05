using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Jellyfin.Plugin.Invites.Configuration;
using Jellyfin.Plugin.Invites.Server;
using Jellyfin.Plugin.Invites.Startup;
using Jellyfin.Plugin.Invites.Storage;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// <para>
/// The rules a migration follows, one test per rule, which is what #92 asks
/// for. `docs/migrations.md` is where each rule is argued and this file is
/// where it is held.
/// </para>
/// <para>
/// The store's transitions are version one to the current shape and version
/// two to it, and each already has a committed document read through the
/// current reader, in <see cref="StoreShapeTests"/>. Nothing here repeats
/// that. What is here is what that file does not assert: that a migration
/// widens nothing, that it says what it did, and that the saying carries
/// nothing out of a record.
/// </para>
/// <para>
/// The configuration half has no transition, because no shipped version has
/// declared a different shape of it. What it has is three cases, and the two
/// that are the framework's behaviour rather than this plugin's are asserted
/// here rather than described, because a behaviour a document describes and
/// nothing drives is a behaviour that changes under the document.
/// </para>
/// </summary>
public class MigrationTests
{
    private static readonly DateTimeOffset _started = new(2026, 5, 1, 8, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A version one document is read forward and says so, naming the version
    /// it came from, the version it was read to, and how many records came
    /// forward without a grant.
    /// </summary>
    [Fact]
    public void AVersionOneStoreIsReadForwardAndTheReadSaysSo()
    {
        using var directory = new OwnedDirectory();
        CopyTheShape("version-1.json", directory.Path);

        var contents = new InvitationStore(directory.Path).Read();

        var migration = contents.Migration;
        Assert.NotNull(migration);
        Assert.Equal(InvitationStore.VersionWithoutAGrant, migration!.From);
        Assert.Equal(InvitationStore.Version, migration.To);
        Assert.Equal(contents.Invitations.Length, migration.RecordsWithoutAGrant);
        Assert.NotEqual(0, migration.RecordsWithoutAGrant);
        Assert.Equal(
            contents.Invitations.Sum(record => record.AccountsProduced.Length),
            migration.AccountsWithoutAnExpiry);
        Assert.NotEqual(0, migration.AccountsWithoutAnExpiry);
    }

    /// <summary>
    /// A version two document is read forward and says so, naming the version
    /// it came from, the version it was read to, and how many account claims
    /// came forward without an expiry.
    /// </summary>
    /// <remarks>
    /// The count is of claims and not of records, because the expiry belongs
    /// to an account and one record can claim several. The document here
    /// claims three accounts across two records, so a count that had been
    /// written per record would answer two and disagree.
    /// </remarks>
    [Fact]
    public void AVersionTwoStoreIsReadForwardAndTheReadSaysSo()
    {
        using var directory = new OwnedDirectory();
        CopyTheShape("version-2.json", directory.Path);

        var contents = new InvitationStore(directory.Path).Read();

        var migration = contents.Migration;
        Assert.NotNull(migration);
        Assert.Equal(InvitationStore.VersionWithoutAnAccountExpiry, migration!.From);
        Assert.Equal(InvitationStore.Version, migration.To);
        Assert.Equal(0, migration.RecordsWithoutAGrant);
        Assert.Equal(
            contents.Invitations.Sum(record => record.AccountsProduced.Length),
            migration.AccountsWithoutAnExpiry);
        Assert.NotEqual(contents.Invitations.Length, migration.AccountsWithoutAnExpiry);
    }

    /// <summary>
    /// The migration invents no expiry, and the absence is what grants this
    /// plugin least: an account with no expiry is one nothing here disables.
    /// </summary>
    [Fact]
    public void TheMigrationInventsNoAccountExpiry()
    {
        using var directory = new OwnedDirectory();
        CopyTheShape("version-2.json", directory.Path);

        var records = new InvitationStore(directory.Path).Read().Invitations;
        var claims = records.SelectMany(record => record.AccountsProduced).ToArray();

        Assert.NotEmpty(claims);
        Assert.All(claims, claim => Assert.Null(claim.ExpiresAt));
    }

    /// <summary>
    /// And a document already in the shape this build writes is not a
    /// migration. Without this the observation would be set on every read and
    /// the line below would be written on every start, which is a line an
    /// operator stops reading.
    /// </summary>
    [Fact]
    public void AStoreInTheCurrentShapeIsNoMigration()
    {
        using var directory = new OwnedDirectory();
        CopyTheShape("version-3.json", directory.Path);

        Assert.Null(new InvitationStore(directory.Path).Read().Migration);
    }

    /// <summary>
    /// A store that is not there is not a migration either. A fresh install
    /// reads no file at all, and reporting one would tell an operator their
    /// store was written by an older build on the day they installed it.
    /// </summary>
    [Fact]
    public void AnAbsentStoreIsNoMigration()
    {
        using var directory = new OwnedDirectory();

        Assert.Null(new InvitationStore(directory.Path).Read().Migration);
    }

    /// <summary>
    /// The migration widens nothing, and the strictest value for a grant is its
    /// absence. Every record that comes forward carries no grant, so there is
    /// no permission, no library and no quota on any of them for a migration to
    /// have chosen.
    /// </summary>
    [Fact]
    public void TheMigrationWidensNothingBecauseItGrantsNothing()
    {
        using var directory = new OwnedDirectory();
        CopyTheShape("version-1.json", directory.Path);

        var records = new InvitationStore(directory.Path).Read().Invitations;

        Assert.NotEmpty(records);
        Assert.All(records, record => Assert.Null(record.Template));
    }

    /// <summary>
    /// The sentence an operator is shown carries the two versions and the
    /// count, and nothing out of a record. `docs/logging.md` admits a value in
    /// a log line only where it is a row in `docs/personal-data.md`, and this
    /// is that rule held over the one line a migration produces.
    /// </summary>
    [Fact]
    public void TheSentenceCarriesNothingOutOfARecord()
    {
        using var directory = new OwnedDirectory();
        CopyTheShape("version-1.json", directory.Path);

        var contents = new InvitationStore(directory.Path).Read();
        var summary = contents.Migration!.Summary;

        foreach (var record in contents.Invitations)
        {
            Assert.DoesNotContain(record.Id.ToString(), summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(record.TemplateLabel, summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(record.MintedBy.ToString(), summary, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Convert.ToBase64String(record.CodeHash.ToArray()), summary, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The plugin says what it did when the server starts. This is the clause
    /// of #92 that asks for the strict option AND a message rather than a
    /// silent choice.
    /// </summary>
    /// <returns>The started load.</returns>
    [Fact]
    public async Task TheLoadSaysAStoreWasReadForward()
    {
        using var directory = new OwnedDirectory();
        CopyTheShape("version-1.json", directory.Path);
        var logger = new RecordingLogger<LoadOnStart>();

        using var load = ALoad(directory.Path, logger);
        await load.StartAsync(CancellationToken.None);

        var line = Assert.Single(logger.Lines, entry => entry.Message.Contains("read forward", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Contains("no grant", line.Message, StringComparison.Ordinal);
        Assert.Contains("no expiry", line.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// And it says nothing where the store is already the shape this build
    /// writes.
    /// </summary>
    /// <returns>The started load.</returns>
    [Fact]
    public async Task TheLoadSaysNothingWhereNothingWasMigrated()
    {
        using var directory = new OwnedDirectory();
        CopyTheShape("version-3.json", directory.Path);
        var logger = new RecordingLogger<LoadOnStart>();

        using var load = ALoad(directory.Path, logger);
        await load.StartAsync(CancellationToken.None);

        Assert.DoesNotContain(logger.Lines, entry => entry.Message.Contains("read forward", StringComparison.Ordinal));
    }

    /// <summary>
    /// A configuration file written before a setting existed carries no element
    /// for it, and the setting arrives at the value the type gives it rather
    /// than at zero. That is the second of the three cases in
    /// `docs/migrations.md`, and it is the one an upgrade takes on every
    /// installation at once.
    /// </summary>
    [Fact]
    public void ASettingAbsentFromAnOlderFileArrivesAtItsFreshInstallValue()
    {
        const string before = "<?xml version=\"1.0\"?><PluginConfiguration>"
            + "<PublicBaseUrl>https://media.example.org</PublicBaseUrl>"
            + "</PluginConfiguration>";

        var read = ReadTheConfiguration(before);
        var fresh = new PluginConfiguration();

        Assert.Equal("https://media.example.org", read.PublicBaseUrl);
        Assert.Equal(fresh.RecordRetentionDays, read.RecordRetentionDays);
        Assert.Equal(fresh.RedemptionAttemptsPerAddressInAnHour, read.RedemptionAttemptsPerAddressInAnHour);
        Assert.Equal(fresh.RedemptionAttemptsPerSecond, read.RedemptionAttemptsPerSecond);
    }

    /// <summary>
    /// An element the type does not declare is dropped rather than absorbed,
    /// which is the first of the three cases: a setting removed in a later
    /// version is read once by the build that still declares it and by nothing
    /// afterwards. The element here is named after a setting that never
    /// existed, so a reader that started accepting unknown members would have
    /// somewhere to put it.
    /// </summary>
    [Fact]
    public void AnElementTheTypeDoesNotDeclareIsDropped()
    {
        const string before = "<?xml version=\"1.0\"?><PluginConfiguration>"
            + "<PublicBaseUrl>https://media.example.org</PublicBaseUrl>"
            + "<AllowEveryLibrary>true</AllowEveryLibrary>"
            + "</PluginConfiguration>";

        var read = ReadTheConfiguration(before);

        Assert.Equal("https://media.example.org", read.PublicBaseUrl);
        Assert.DoesNotContain(
            typeof(PluginConfiguration).GetProperties(),
            property => property.Name.Equals("AllowEveryLibrary", StringComparison.Ordinal));
    }

    /// <summary>
    /// A store declaring a version newer than this build reads is refused and
    /// the file is left alone, which is the forward-only rule with something
    /// behind it. <see cref="InvitationStoreTests"/> holds the refusal itself;
    /// what is asserted here is that the refusal is what a load meets, so a
    /// downgrade is never attempted through the path the server takes.
    /// </summary>
    [Fact]
    public void ADowngradeIsRefusedRatherThanAttempted()
    {
        using var directory = new OwnedDirectory();
        Directory.CreateDirectory(directory.Path);
        var path = Path.Combine(directory.Path, InvitationStore.FileName);
        var newer = "{\"version\":" + (InvitationStore.Version + 1) + ",\"invitations\":[]}";
        File.WriteAllText(path, newer);

        Assert.Throws<StoreVersionRefusedException>(() => new InvitationStore(directory.Path).Read());
        Assert.Equal(newer, File.ReadAllText(path));
    }

    private static PluginConfiguration ReadTheConfiguration(string xml)
    {
        var serializer = new XmlSerializer(typeof(PluginConfiguration));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

        return (PluginConfiguration)serializer.Deserialize(stream)!;
    }

    private static void CopyTheShape(string name, string directory)
    {
        Directory.CreateDirectory(directory);
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "StoreShapes", name),
            Path.Combine(directory, InvitationStore.FileName));
    }

    private static LoadOnStart ALoad(string directory, RecordingLogger<LoadOnStart> logger)
    {
        return new LoadOnStart(
            new ServerLineGate("42.7", new StubRunningServer(new Version(42, 7, 3))),
            new StubStoreDirectory(directory),
            new StubPublicAddress(null),
            new StubConfiguredTemplates([]),
            new StubServerAccounts([]),
            new TestClock(_started),
            logger);
    }
}
