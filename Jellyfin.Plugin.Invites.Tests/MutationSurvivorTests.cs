using System;
using System.Collections.Immutable;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// Every test here exists because a mutant survived at the site it asserts.
/// </summary>
/// <remarks>
/// <para>
/// #22 measures the suite by planting a defect and asking whether a test
/// notices. The first run over the scope in stryker-config.json left fourteen
/// mutants alive that were not exception-message text, and each one is a rule
/// the rest of the suite states somewhere and never checks: a refusal that
/// throws nothing, an equality that answers for the wrong reason, a hash that
/// is the same number for every value, a boundary comparison one character
/// wide, and a normalisation the constructor performs and nobody reads back.
/// </para>
/// <para>
/// They are together in one file rather than spread into the files that own
/// each type, because what they have in common is why they exist, and a reader
/// asking why anybody asserts that a hash code differs is owed the paragraph
/// above rather than a guess. docs/mutation-testing.md carries the scope, the
/// threshold and the class of mutant this suite deliberately does not chase.
/// </para>
/// <para>
/// Nothing here reads a clock or touches a file. Every instant is an argument
/// and every value is built in the test.
/// </para>
/// </remarks>
public class MutationSurvivorTests
{
    private static readonly Guid _operator = new Guid("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid _second = new Guid("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _expires = new(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A record built the same way every time, so a test changing one field is
    /// changing one field.
    /// </summary>
    private static Invitation Record(
        Guid? id = null,
        int usesGranted = 3,
        int usesRemaining = 3,
        DateTimeOffset? revokedAt = null,
        Guid? revokedBy = null,
        ImmutableArray<Guid>? accountsProduced = null) =>
        new Invitation(
            id ?? new Guid("cccccccc-0000-0000-0000-000000000001"),
            ImmutableArray.Create(new byte[] { 1, 2, 3, 4 }),
            _operator,
            _minted,
            _expires,
            usesGranted,
            usesRemaining,
            revokedAt,
            revokedBy,
            "the template an operator picked",
            accountsProduced ?? ImmutableArray<Guid>.Empty);

    private static AccountTemplate Template(
        bool mayDownload = false,
        int? simultaneousStreamCeiling = 2) =>
        new AccountTemplate(
            ImmutableArray.Create(new Guid("aaaaaaaa-0000-0000-0000-000000000001")),
            mayDownload,
            true,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            true,
            4_000_000,
            simultaneousStreamCeiling,
            13,
            ImmutableArray.Create("EnableSyncTranscoding"));

    /// <summary>
    /// Both factories refuse a null record, and the refusal is the whole of
    /// what stops a verdict that says it matched something while holding
    /// nothing.
    /// </summary>
    /// <remarks>
    /// The two null checks were the only statements in this type nothing
    /// reached: removing either left the suite green and produced a verdict
    /// whose <c>Invitation</c> is null, which every reader of a verdict treats
    /// as a record.
    /// </remarks>
    [Fact]
    public void AVerdictAgainstAMatchedRecordRefusesANullRecord()
    {
        Assert.Throws<ArgumentNullException>(
            () => RedemptionVerdict.Refused(RedemptionOutcome.Revoked, null!));

        Assert.Throws<ArgumentNullException>(
            () => RedemptionVerdict.Honoured(null!));
    }

    /// <summary>
    /// A ceiling of zero is a grant of nothing and is accepted. Only a ceiling
    /// below zero is refused.
    /// </summary>
    /// <remarks>
    /// The comparison is one character wide and the suite tested it from one
    /// side. <c>ANegativeCeilingIsRefused</c> passes -1, which is refused
    /// whether the comparison reads "below zero" or "zero or below", so it
    /// cannot tell the two apart. Zero is the position where they disagree, and
    /// the type's own refusal says which answer is meant: no ceiling at all is
    /// written as null, so zero has to mean a ceiling of zero rather than an
    /// absent one.
    /// </remarks>
    [Fact]
    public void ACeilingOfZeroIsAGrantOfNothingRatherThanARefusal()
    {
        var template = new AccountTemplate(
            ImmutableArray.Create(new Guid("aaaaaaaa-0000-0000-0000-000000000001")),
            false,
            true,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            true,
            0,
            0,
            0,
            ImmutableArray<string>.Empty);

        Assert.Equal(0, template.RemoteBitrateCeiling);
        Assert.Equal(0, template.SimultaneousStreamCeiling);
        Assert.Equal(0, template.ParentalRatingCeiling);
    }

    /// <summary>
    /// A half-written revocation names the half that was supplied.
    /// </summary>
    /// <remarks>
    /// The refusal itself was asserted; which field it names was not, so the
    /// condition choosing between the two names could be inverted, or fixed to
    /// either side, with nothing noticing. It names the argument that arrived
    /// without its partner, which is what an argument refusal is for, and it is
    /// the only part of the refusal that says which half of the revocation the
    /// caller has.
    /// </remarks>
    [Theory]
    [InlineData(true, "revokedAt")]
    [InlineData(false, "revokedBy")]
    public void AHalfWrittenRevocationNamesTheHalfThatWasSupplied(bool instantIsPresent, string named)
    {
        var refusal = Assert.Throws<ArgumentException>(() => Record(
            revokedAt: instantIsPresent ? _minted : null,
            revokedBy: instantIsPresent ? null : _operator));

        Assert.Equal(named, refusal.ParamName);
    }

    /// <summary>
    /// A record built with no accounts list at all carries an empty one rather
    /// than an uninitialised one.
    /// </summary>
    /// <remarks>
    /// The normalisation in the constructor is what keeps every reader of
    /// <c>AccountsProduced</c> from having to know that a default
    /// <c>ImmutableArray</c> throws on the members an empty one answers. With
    /// the normalisation gone the field is the default value, and this test
    /// fails on the first member it reads rather than on an assertion.
    /// </remarks>
    [Fact]
    public void ARecordBuiltWithNoAccountsListCarriesAnEmptyOne()
    {
        var record = Record(accountsProduced: default(ImmutableArray<Guid>));

        Assert.False(record.AccountsProduced.IsDefault);
        Assert.Empty(record.AccountsProduced);
    }

    /// <summary>
    /// Both value types answer the two questions every equality is asked first:
    /// nothing equals null, and everything equals itself.
    /// </summary>
    /// <remarks>
    /// Both early returns could be inverted without a test noticing. The null
    /// arm decides what happens when a record is compared against one that was
    /// never read back from a store, and the reference arm is the answer to
    /// <c>x.Equals(x)</c>, which the field comparison below it would also get
    /// right; a mutant that returns false there is a type that does not equal
    /// itself, and the suite said nothing about it.
    /// </remarks>
    [Fact]
    public void NothingEqualsNullAndEverythingEqualsItself()
    {
        var record = Record();
        var template = Template();

        Assert.False(record.Equals(null));
        Assert.False(template.Equals(null));

        Assert.True(record.Equals(record));
        Assert.True(template.Equals(template));
    }

    /// <summary>
    /// Two values that differ hash differently, so the hash is derived from the
    /// fields rather than being a constant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The suite asserted the direction that a constant satisfies: equal values
    /// hash equally, which is also true of a hash that returns the same number
    /// for everything. Removing either body left the suite green and turned
    /// every dictionary and every set over these types into a linear scan.
    /// </para>
    /// <para>
    /// Two unequal values are permitted to share a hash code, so this asserts a
    /// pair rather than a rule. The pair is chosen to differ in a field each
    /// type's hash is documented to carry, and a collision on it would be a
    /// fact about <c>HashCode.Combine</c> that a run would report rather than
    /// hide.
    /// </para>
    /// </remarks>
    [Fact]
    public void TwoValuesThatDifferHashDifferently()
    {
        Assert.NotEqual(
            Record().GetHashCode(),
            Record(id: new Guid("cccccccc-0000-0000-0000-000000000002")).GetHashCode());

        Assert.NotEqual(
            Template().GetHashCode(),
            Template(mayDownload: true).GetHashCode());
    }

    /// <summary>
    /// A record and a template each compare every field they say they compare.
    /// </summary>
    /// <remarks>
    /// This is the neighbour of the test above rather than a repeat of it: the
    /// hash may collide and equality may not, so the fields the hash is built
    /// from are asserted here as differences equality actually reports.
    /// </remarks>
    [Fact]
    public void ADifferenceInOneFieldIsADifference()
    {
        Assert.False(Record().Equals(Record(usesRemaining: 2)));
        Assert.False(Record().Equals(Record(id: _second)));
        Assert.False(Template().Equals(Template(mayDownload: true)));
        Assert.False(Template().Equals(Template(simultaneousStreamCeiling: 5)));
    }
}
