using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Invites.Invitations;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The record is the shape every later rule constrains, so these are tests of
/// the shape rather than of any rule judged against it. Nothing here decides
/// whether an invitation may be redeemed: that is one routine and it is not
/// written.
/// </summary>
public class InvitationRecordTests
{
    /// <summary>
    /// Every public member of <see cref="Invitation"/>, against the row of the
    /// invitation record table in docs/personal-data.md it implements. That
    /// page is the authority for what this plugin holds about a person, so a
    /// member with no row here is a field nobody argued for, and the argument
    /// is the only thing standing between this record and somebody's full name
    /// being stored in it.
    /// </summary>
    private static readonly Dictionary<string, string> InventoryRows = new(StringComparer.Ordinal)
    {
        ["Id"] = "Invitation identifier",
        ["CodeHash"] = "Keyed hash of the code",
        ["MintedBy"] = "Minted by",
        ["MintedAt"] = "Minted at",
        ["ExpiresAt"] = "Expires at",
        ["UsesGranted"] = "Uses granted, uses remaining",
        ["UsesRemaining"] = "Uses granted, uses remaining",

        // Two rows, one stored field. The inventory names revoked and revoked
        // at; IsRevoked is derived from RevokedAt rather than stored beside it,
        // so the record cannot say it is revoked and not say when.
        ["RevokedAt"] = "Revoked, revoked at",
        ["IsRevoked"] = "Revoked, revoked at",

        ["TemplateLabel"] = "Template name",
        ["AccountsProduced"] = "Accounts produced",
    };

    /// <summary>
    /// A hash-shaped value a test can hold. It is not a keyed hash of anything:
    /// the hash and the secret it is keyed with are #29 and do not exist, and a
    /// test that invented one would be asserting against its own invention.
    /// What these bytes stand for is that the field holds bytes.
    /// </summary>
    private static ImmutableArray<byte> SomeHashBytes(byte seed) =>
        ImmutableArray.Create(seed, (byte)(seed + 1), (byte)(seed + 2), (byte)(seed + 3));

    /// <summary>
    /// One record, built the same way every time so a test that changes a
    /// single field is changing a single field.
    /// </summary>
    private static Invitation Baseline() => new Invitation(
        id: new Guid("11111111-1111-1111-1111-111111111111"),
        codeHash: SomeHashBytes(0x10),
        mintedBy: new Guid("22222222-2222-2222-2222-222222222222"),
        mintedAt: new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
        expiresAt: new DateTimeOffset(2026, 3, 8, 9, 0, 0, TimeSpan.Zero),
        usesGranted: 1,
        usesRemaining: 1,
        revokedAt: null,
        templateLabel: "Household",
        accountsProduced: ImmutableArray.Create(new Guid("55555555-5555-5555-5555-555555555555")));

    /// <summary>
    /// The type carries the inventory and nothing else. A field added here
    /// without a row in docs/personal-data.md reds this, which is the point:
    /// the page says a field that is not in the inventory does not go in the
    /// record, and this is the only thing that makes that sentence cost
    /// anything. It reds in the other direction too, so a row deleted from the
    /// dictionary without the member going with it is caught rather than
    /// quietly narrowing what the test covers.
    /// </summary>
    [Fact]
    public void EveryPublicMemberOfTheRecordIsARowInThePersonalDataInventory()
    {
        var members = typeof(Invitation)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var expected = InventoryRows.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(expected, members);
    }

    /// <summary>
    /// The scan above reads properties, so a public field would be a member it
    /// never sees. There are none, and a change that adds one has to notice
    /// this line rather than slipping a value past the inventory.
    /// </summary>
    [Fact]
    public void TheRecordExposesNoPublicField()
    {
        var fields = typeof(Invitation)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(field => field.Name)
            .ToList();

        Assert.Empty(fields);
    }

    /// <summary>
    /// Nothing on the record is the code or is shaped like one. The inventory
    /// scan above is what refuses a field called something else that holds one;
    /// this is the narrower statement that no member today hands back a value a
    /// canonical code could be read out of, checked by minting one and asking
    /// the type for it.
    /// </summary>
    [Fact]
    public void NoMemberOfTheRecordHandsBackSomethingShapedLikeACode()
    {
        var record = Baseline();

        var rendered = typeof(Invitation)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.GetValue(record))
            .Select(value => value?.ToString() ?? string.Empty)
            .ToList();

        Assert.All(
            rendered,
            text => Assert.Null(Jellyfin.Plugin.Invites.Codes.InvitationCode.Canonicalise(text)));
    }

    /// <summary>
    /// An invitation with no stored hash is one no presented code can ever be
    /// checked against, so it is refused rather than held. Delete the
    /// <c>IsDefaultOrEmpty</c> guard in the constructor and this goes red.
    /// </summary>
    [Fact]
    public void ARecordWithoutAKeyedHashIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new Invitation(
            id: Guid.NewGuid(),
            codeHash: ImmutableArray<byte>.Empty,
            mintedBy: Guid.NewGuid(),
            mintedAt: new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
            expiresAt: new DateTimeOffset(2026, 3, 8, 9, 0, 0, TimeSpan.Zero),
            usesGranted: 1,
            usesRemaining: 1,
            revokedAt: null,
            templateLabel: "Household",
            accountsProduced: ImmutableArray<Guid>.Empty));
    }

    /// <summary>
    /// The remaining uses are a count of the granted ones, so a value above
    /// them or below zero is not a count of anything. Delete the range guard in
    /// the constructor and this goes red. What may be granted in the first
    /// place is #52 and #33 and is deliberately not judged here.
    /// </summary>
    /// <param name="granted">The uses granted.</param>
    /// <param name="remaining">The uses remaining, outside the granted ones.</param>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(1, -1)]
    [InlineData(0, 1)]
    public void ARemainingCountOutsideTheGrantedOnesIsRefused(int granted, int remaining)
    {
        Assert.Throws<ArgumentException>(() => new Invitation(
            id: Guid.NewGuid(),
            codeHash: SomeHashBytes(0x20),
            mintedBy: Guid.NewGuid(),
            mintedAt: new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
            expiresAt: new DateTimeOffset(2026, 3, 8, 9, 0, 0, TimeSpan.Zero),
            usesGranted: granted,
            usesRemaining: remaining,
            revokedAt: null,
            templateLabel: "Household",
            accountsProduced: ImmutableArray<Guid>.Empty));
    }

    /// <summary>
    /// Two records built from equal values are equal, including where the two
    /// sequence fields are backed by different arrays. This is the property a
    /// store round trip needs and the one a synthesised record equality would
    /// not have given, because it compares those two fields by the identity of
    /// their backing arrays.
    /// </summary>
    [Fact]
    public void TwoRecordsBuiltFromEqualValuesAreEqual()
    {
        var accounts = new Guid("33333333-3333-3333-3333-333333333333");

        var written = new Invitation(
            id: new Guid("11111111-1111-1111-1111-111111111111"),
            codeHash: SomeHashBytes(0x10),
            mintedBy: new Guid("22222222-2222-2222-2222-222222222222"),
            mintedAt: new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
            expiresAt: new DateTimeOffset(2026, 3, 8, 9, 0, 0, TimeSpan.Zero),
            usesGranted: 2,
            usesRemaining: 1,
            revokedAt: new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero),
            templateLabel: "Household",
            accountsProduced: ImmutableArray.Create(accounts));

        var readBack = new Invitation(
            id: new Guid("11111111-1111-1111-1111-111111111111"),
            codeHash: SomeHashBytes(0x10),
            mintedBy: new Guid("22222222-2222-2222-2222-222222222222"),
            mintedAt: new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
            expiresAt: new DateTimeOffset(2026, 3, 8, 9, 0, 0, TimeSpan.Zero),
            usesGranted: 2,
            usesRemaining: 1,
            revokedAt: new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero),
            templateLabel: "Household",
            accountsProduced: ImmutableArray.Create(accounts));

        Assert.Equal(written, readBack);
        Assert.Equal(written.GetHashCode(), readBack.GetHashCode());
    }

    /// <summary>
    /// Moving any one field moves the equality. Without this, an equality that
    /// forgot a field would still pass the test above, and a store that dropped
    /// exactly that field on the way back would pass its round trip. The cases
    /// are one per field, which is why they are named rather than numbered.
    /// </summary>
    /// <param name="field">The field the variant moves.</param>
    [Theory]
    [InlineData("Id")]
    [InlineData("CodeHash")]
    [InlineData("MintedBy")]
    [InlineData("MintedAt")]
    [InlineData("ExpiresAt")]
    [InlineData("UsesGranted")]
    [InlineData("UsesRemaining")]
    [InlineData("RevokedAt")]
    [InlineData("TemplateLabel")]
    [InlineData("AccountsProduced")]
    public void ARecordDifferingInOneFieldIsNotEqual(string field)
    {
        var other = new Guid("44444444-4444-4444-4444-444444444444");
        var later = new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);

        var variant = new Invitation(
            id: field == "Id" ? other : new Guid("11111111-1111-1111-1111-111111111111"),
            codeHash: field == "CodeHash" ? SomeHashBytes(0x70) : SomeHashBytes(0x10),
            mintedBy: field == "MintedBy" ? other : new Guid("22222222-2222-2222-2222-222222222222"),
            mintedAt: field == "MintedAt" ? later : new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
            expiresAt: field == "ExpiresAt" ? later : new DateTimeOffset(2026, 3, 8, 9, 0, 0, TimeSpan.Zero),
            usesGranted: field == "UsesGranted" ? 2 : 1,
            usesRemaining: field == "UsesRemaining" ? 0 : 1,
            revokedAt: field == "RevokedAt" ? later : null,
            templateLabel: field == "TemplateLabel" ? "Friends" : "Household",
            // Same length as the baseline's and a different value in it, so an
            // equality comparing these two fields by their length rather than
            // by their contents fails here instead of passing.
            accountsProduced: field == "AccountsProduced"
                ? ImmutableArray.Create(other)
                : ImmutableArray.Create(new Guid("55555555-5555-5555-5555-555555555555")));

        Assert.NotEqual(Baseline(), variant);
    }

    /// <summary>
    /// A keyed hash that is a prefix of the other one is not equal to it. The
    /// case above moves a byte and leaves the length alone, so this is the one
    /// where the two hashes are different lengths, and it is the only place a
    /// fixed-time comparison could have behaved differently from the sequence
    /// comparison it replaced. Both answer false; this says so rather than
    /// leaving it as a reading of the framework's documentation.
    /// </summary>
    [Fact]
    public void AKeyedHashThatIsAPrefixOfTheOtherIsNotEqual()
    {
        var full = SomeHashBytes(0x10);
        var prefix = ImmutableArray.Create(full[0], full[1], full[2]);

        var truncated = new Invitation(
            id: new Guid("11111111-1111-1111-1111-111111111111"),
            codeHash: prefix,
            mintedBy: new Guid("22222222-2222-2222-2222-222222222222"),
            mintedAt: new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
            expiresAt: new DateTimeOffset(2026, 3, 8, 9, 0, 0, TimeSpan.Zero),
            usesGranted: 1,
            usesRemaining: 1,
            revokedAt: null,
            templateLabel: "Household",
            accountsProduced: ImmutableArray.Create(new Guid("55555555-5555-5555-5555-555555555555")));

        Assert.NotEqual(Baseline(), truncated);
        Assert.NotEqual(truncated, Baseline());
    }

    /// <summary>
    /// Revoked and revoked-at cannot disagree, because there is one of them.
    /// The inventory names two rows and a record holding two independent fields
    /// can say it is revoked and not say when, which a partial write and a
    /// restored store both produce.
    /// </summary>
    [Fact]
    public void WhetherARecordIsRevokedFollowsTheOneStoredTime()
    {
        var revokedAt = new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero);

        var live = Baseline();
        var revoked = new Invitation(
            id: live.Id,
            codeHash: live.CodeHash,
            mintedBy: live.MintedBy,
            mintedAt: live.MintedAt,
            expiresAt: live.ExpiresAt,
            usesGranted: live.UsesGranted,
            usesRemaining: live.UsesRemaining,
            revokedAt: revokedAt,
            templateLabel: live.TemplateLabel,
            accountsProduced: live.AccountsProduced);

        Assert.False(live.IsRevoked);
        Assert.True(revoked.IsRevoked);
        Assert.Equal(revokedAt, revoked.RevokedAt);
    }
}
