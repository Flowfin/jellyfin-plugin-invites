using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Configuration;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Storage;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The grant an invitation carries is a copy taken at minting, which is #61's
/// second rule: the record holds the value, and the name only as what was
/// chosen.
/// </summary>
/// <remarks>
/// <para>
/// Every fact here is asked of the mint and the store rather than of the
/// template type. <c>AccountTemplateTests</c> already holds that a copy of the
/// type survives an edit to the value it was copied from, against two values a
/// test holds in its hands; what is asked here is the same property at the
/// level the issue asks for, with a configured entry an operator would edit on
/// one side and a record on disk on the other.
/// </para>
/// <para>
/// Nothing here sleeps, reads the machine clock, or writes outside a directory
/// the test creates and removes.
/// </para>
/// </remarks>
public class TemplateCopyTests
{
    private const string Configured = "https://media.example.org";

    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _operator = Guid.Parse("11112222-3333-4444-5555-666677778888");

    /// <summary>
    /// A mint against a configured name writes the grant that name stood for
    /// onto the record, and the record on disk carries it back.
    /// </summary>
    [Fact]
    public void AMintCopiesTheConfiguredGrantOntoTheRecord()
    {
        using var directory = new OwnedDirectory();
        var entries = TestTemplates.Configured();
        var operations = OperationsOver(directory, entries);

        var minted = operations.Mint(_operator, "Household", null, null);

        Assert.Equal("Household", minted.Invitation.TemplateLabel);
        Assert.Equal(TemplateSettings.Of(entries[0]!), minted.Invitation.Template);

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(minted.Invitation, stored);
        Assert.Equal(TemplateSettings.Of(entries[0]!), stored.Template);
    }

    /// <summary>
    /// The name is matched ignoring case, and the label the record keeps is
    /// the one the operator typed rather than the one the entry carries.
    /// </summary>
    [Fact]
    public void TheNameIsMatchedIgnoringCaseAndTheRecordKeepsWhatWasTyped()
    {
        using var directory = new OwnedDirectory();
        var operations = OperationsOver(directory, TestTemplates.Configured());

        var minted = operations.Mint(_operator, "hOUSEHOLD", null, null);

        Assert.Equal("hOUSEHOLD", minted.Invitation.TemplateLabel);
        Assert.Equal(TestTemplates.Household, minted.Invitation.Template);
    }

    /// <summary>
    /// Editing the configured template after minting changes what the next
    /// invitation grants and leaves the one already minted exactly as it was,
    /// on the record in memory and on the record read back off disk.
    /// </summary>
    /// <remarks>
    /// This is the fourth clause of #61 at the level the issue asks for. The
    /// entry is edited in place, which is what the configuration page does to
    /// the plugin's own settings, and the edit is one that would widen the
    /// account: a library added and the download permission opened. An
    /// invitation that resolved its name at redemption would pick both up.
    /// </remarks>
    [Fact]
    public void EditingTheConfiguredTemplateLeavesAMintedInvitationUnchanged()
    {
        using var directory = new OwnedDirectory();
        var entries = TestTemplates.Configured();
        var operations = OperationsOver(directory, entries);

        var before = operations.Mint(_operator, "Guest", null, null);
        var copyAtMinting = TestTemplates.Guest;
        Assert.Equal(copyAtMinting, before.Invitation.Template);

        var guest = entries[1]!;
        guest.Libraries = [TestTemplates.Music, TestTemplates.Films];
        guest.MayDownload = true;

        var after = operations.Mint(_operator, "Guest", null, null);

        Assert.NotEqual(copyAtMinting, after.Invitation.Template);
        Assert.Equal(TemplateSettings.Of(guest), after.Invitation.Template);

        var records = new InvitationStore(directory.Path).Read().Invitations;
        Assert.Equal(2, records.Length);
        Assert.Equal(copyAtMinting, records.Single(record => record.Id == before.Invitation.Id).Template);
        Assert.Equal(TemplateSettings.Of(guest), records.Single(record => record.Id == after.Invitation.Id).Template);
    }

    /// <summary>
    /// A name no configured template carries is refused before anything is
    /// written, and the refusal names the setting.
    /// </summary>
    /// <param name="typed">The name the operator typed.</param>
    [Theory]
    [InlineData("Family")]
    [InlineData("House")]
    [InlineData("Households")]
    public void ANameNoTemplateCarriesIsRefusedAndWritesNothing(string typed)
    {
        using var directory = new OwnedDirectory();
        var operations = OperationsOver(directory, TestTemplates.Configured());

        var refused = Assert.Throws<ArgumentException>(() => operations.Mint(_operator, typed, null, null));

        Assert.Equal("templateLabel", refused.ParamName);
        Assert.Contains(TemplateSettings.SettingName, refused.Message, StringComparison.Ordinal);
        Assert.Empty(new InvitationStore(directory.Path).Read().Invitations);
        Assert.False(File.Exists(new InvitationStore(directory.Path).Path));
    }

    /// <summary>
    /// With no template configured at all, which is a fresh install, every
    /// name is refused and nothing is written.
    /// </summary>
    /// <param name="configured">The list as the plugin reads it.</param>
    [Theory]
    [MemberData(nameof(NothingConfigured))]
    public void WithNothingConfiguredNothingCanBeMinted(ConfiguredTemplate?[]? configured)
    {
        using var directory = new OwnedDirectory();
        var operations = OperationsOver(directory, configured);

        Assert.Throws<ArgumentException>(() => operations.Mint(_operator, "Household", null, null));
        Assert.False(File.Exists(new InvitationStore(directory.Path).Path));
    }

    /// <summary>
    /// The two shapes of nothing configured.
    /// </summary>
    /// <returns>One row per shape.</returns>
    public static TheoryData<ConfiguredTemplate?[]?> NothingConfigured()
    {
        return new TheoryData<ConfiguredTemplate?[]?> { null, Array.Empty<ConfiguredTemplate?>() };
    }

    /// <summary>
    /// A configured list with a fault in one entry answers for no name, the
    /// good entry asked for included, with the sentence the load writes, and
    /// nothing is written.
    /// </summary>
    /// <remarks>
    /// It is its own exception rather than an argument fault because the name
    /// asked for was fine. The controller answers it as a conflict for the
    /// reason the live ceiling is one: the repair is on the configuration page
    /// and not in the request.
    /// </remarks>
    [Fact]
    public void AListWithAFaultRefusesEveryNameWithTheLoadsOwnSentence()
    {
        using var directory = new OwnedDirectory();
        var entries = TestTemplates.Configured();
        entries[2]!.SimultaneousStreamCeiling = -1;
        var operations = OperationsOver(directory, entries);

        var refused = Assert.Throws<ConfiguredTemplatesRefusedException>(
            () => operations.Mint(_operator, "Household", null, null));

        Assert.Contains(TemplateSettings.WhyRefused(entries)!, refused.Message, StringComparison.Ordinal);
        Assert.Contains("position 3", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Old", refused.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(new InvitationStore(directory.Path).Path));
    }

    /// <summary>
    /// The refusal of a name comes before the clock is read, a code is minted
    /// or the store is touched: the directory is left with nothing in it, not
    /// even the hash secret a first mint creates.
    /// </summary>
    [Fact]
    public void ARefusedNameLeavesTheDirectoryUntouched()
    {
        using var directory = new OwnedDirectory();
        var operations = OperationsOver(directory, TestTemplates.Configured());

        Assert.Throws<ArgumentException>(() => operations.Mint(_operator, "Nobody", null, null));

        Assert.False(Directory.Exists(directory.Path) && Directory.EnumerateFileSystemEntries(directory.Path).Any());
    }

    /// <summary>
    /// A record with no grant, which is what a version one document migrates
    /// into, is written back with the member present and null, and reads back
    /// as the same record.
    /// </summary>
    /// <remarks>
    /// The write-back happens on the next revocation or sweep of a store that
    /// was migrated, so this is the round trip such a record makes rather than
    /// a shape anything mints.
    /// </remarks>
    [Fact]
    public void ARecordWithNoGrantRoundTripsThroughTheCurrentShape()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        var withoutAGrant = ARecord(template: null);

        store.Write(new[] { withoutAGrant });

        var record = JsonNode.Parse(File.ReadAllText(store.Path))!["invitations"]!.AsArray()[0]!.AsObject();
        Assert.True(record.ContainsKey("template"));
        Assert.Null(record["template"]);
        Assert.Equal(withoutAGrant, Assert.Single(store.Read().Invitations));
    }

    /// <summary>
    /// A record in the current shape that leaves the grant member out
    /// altogether is refused rather than read as a record with no grant.
    /// </summary>
    /// <remarks>
    /// The difference between absent and null is the difference between a
    /// document this build did not write and one it did. The migration writes
    /// null; nothing writes absence.
    /// </remarks>
    [Fact]
    public void ARecordMissingTheGrantMemberIsRefused()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write(new[] { ARecord(TestTemplates.Household) });

        var document = JsonNode.Parse(File.ReadAllText(store.Path))!;
        var record = document["invitations"]!.AsArray()[0]!.AsObject();
        Assert.True(record.Remove("template"));
        File.WriteAllText(store.Path, document.ToJsonString());

        var refused = Assert.Throws<JsonException>(() => store.Read());

        Assert.Contains("template", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A grant on disk with any one of its members missing is refused rather
    /// than read with that member at its default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// #93's per-member rule applied to the nested shape. A ceiling read at its
    /// default is no ceiling, which grants the account more than the template
    /// allowed; a permission read at its default is closed, which grants less.
    /// Rather than argue each member, the grant is read whole or not at all,
    /// and this asks that of every member the writer emits, by taking each one
    /// out in turn.
    /// </para>
    /// <para>
    /// The list of members is the writer's own output rather than one kept
    /// here, and it is asserted against the list the test fixture carries so a
    /// member added to the stored grant reaches both this loop and the fixture
    /// that names it.
    /// </para>
    /// </remarks>
    [Fact]
    public void AGrantMissingAnyMemberIsRefused()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write(new[] { ARecord(TestTemplates.Household) });
        var whole = File.ReadAllText(store.Path);

        var template = JsonNode.Parse(whole)!["invitations"]!.AsArray()[0]!["template"]!.AsObject();
        var members = template.Select(member => member.Key).ToArray();
        Assert.Equal(TestTemplates.StoredMembers(), members);

        foreach (var member in members)
        {
            var document = JsonNode.Parse(whole)!;
            var grant = document["invitations"]!.AsArray()[0]!["template"]!.AsObject();
            Assert.True(grant.Remove(member));
            File.WriteAllText(store.Path, document.ToJsonString());

            var refused = Assert.Throws<JsonException>(() => store.Read());
            Assert.Contains(member, refused.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A revocation carries the grant across unchanged, so the account an
    /// operator stopped is still recorded with what it would have been worth.
    /// </summary>
    [Fact]
    public void ARevocationCarriesTheGrantAcross()
    {
        var minted = ARecord(TestTemplates.Household);

        var revoked = Revocation.Of(minted, _operator, _minted.AddHours(1));

        Assert.True(revoked.IsRevoked);
        Assert.Equal(TestTemplates.Household, revoked.Template);
    }

    /// <summary>
    /// The routine that mints a record refuses to mint one with no grant, so
    /// no record leaves the mint carrying a name and nothing behind it.
    /// </summary>
    [Fact]
    public void TheMintRefusesToWriteARecordWithNoGrant()
    {
        var refusal = Assert.Throws<ArgumentNullException>(() => InvitationMint.Mint(
            id: Guid.NewGuid(),
            codeHash: ImmutableArray.Create<byte>(1, 2, 3, 4),
            mintedBy: _operator,
            mintedAt: _minted,
            expiresAt: _minted.AddDays(7),
            uses: 1,
            templateLabel: "Household",
            template: null!));

        Assert.Equal("template", refusal.ParamName);
    }

    private static InvitationOperations OperationsOver(OwnedDirectory directory, ConfiguredTemplate?[]? configured)
    {
        return new InvitationOperations(
            new StubStoreDirectory(directory.Path),
            new TestClock(_minted),
            new StubPublicAddress(Configured),
            new StubConfiguredTemplates(configured));
    }

    private static Invitation ARecord(AccountTemplate? template)
    {
        return new Invitation(
            id: Guid.Parse("6f0a2d1e-6b3c-4f8a-9f1d-2a7c5e8b0d34"),
            codeHash: ImmutableArray.Create<byte>(0x01, 0x7f, 0x80, 0xff, 0x00, 0x42),
            mintedBy: _operator,
            mintedAt: _minted,
            expiresAt: _minted.AddDays(7),
            usesGranted: 3,
            usesRemaining: 1,
            revokedAt: null,
            revokedBy: null,
            templateLabel: "Household",
            template: template,
            accountsProduced: ImmutableArray<Guid>.Empty);
    }
}
