using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
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
/// <para>
/// That rule is why one survivor is asserted somewhere else rather than here.
/// The block removal at the short-circuit in
/// <c>InvitationOperations.Revoke</c> changes nothing a caller can read: it
/// writes the store again with byte-identical contents, so only a test that
/// can see a write happen kills it, and seeing one means a real directory.
/// <see cref="ASecondRevocationWritesNothingTests"/> is where it lives, and
/// it is named here so that a reader working through the list on #376 finds
/// it rather than concluding that nothing was written for it.
/// </para>
/// </remarks>
public class MutationSurvivorTests
{
    private static readonly Guid _operator = new Guid("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid _second = new Guid("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _expires = new(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _firstLibrary = new Guid("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid _secondLibrary = new Guid("aaaaaaaa-0000-0000-0000-000000000002");

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
            TestTemplates.Household,
            accountsProduced ?? ImmutableArray<Guid>.Empty);

    /// <summary>
    /// A template built the same way every time, with one named parameter per
    /// grant, so a test changing one grant is changing one grant.
    /// </summary>
    /// <remarks>
    /// Every parameter's default is the baseline's value, so a caller naming
    /// none of them gets the baseline and a caller naming one gets the
    /// baseline with that grant moved and nothing else.
    /// </remarks>
    private static AccountTemplate Template(
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
            libraries ?? ImmutableArray.Create(_firstLibrary),
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
            serverDefaultsLeftAlone ?? ImmutableArray.Create("EnableSyncTranscoding"));

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
    /// <summary>
    /// Every field the hash is built from moves the hash on its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The test above asserts a pair, and one <c>hash.Add</c> call is enough
    /// to satisfy it. Removing any of the other fifteen left the suite green
    /// and left the type with a hash that ignores a grant, which the remarks
    /// on <c>AccountTemplate.GetHashCode</c> say a reader has no way of
    /// noticing: the shorter form is a lawful hash, and the only other
    /// assertion on this one is that equal templates agree.
    /// </para>
    /// <para>
    /// The two list fields enter the hash as their lengths, so each is moved
    /// here by a list of a different length rather than by different contents.
    /// A variant differing only in contents would leave the hash where it was
    /// for a reason the type documents, and this would red on correct code.
    /// </para>
    /// <para>
    /// The key set is held against the type's own properties rather than
    /// written out once, so a grant added to the template with no row here
    /// reds instead of arriving unhashed and unnoticed.
    /// </para>
    /// <para>
    /// Two unequal values are permitted to share a hash code, so a failure
    /// here is read as a field the hash dropped rather than proved to be one.
    /// The bound is the one the test above carries and the reason is the same:
    /// a collision would be a fact about <see cref="HashCode"/> that a run
    /// reports rather than hides.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryFieldTheHashIsBuiltFromMovesTheHashOnItsOwn()
    {
        var baseline = Template().GetHashCode();

        var varied = new Dictionary<string, AccountTemplate>(StringComparer.Ordinal)
        {
            ["Libraries"] = Template(libraries: ImmutableArray.Create(_firstLibrary, _secondLibrary)),
            ["MayDownload"] = Template(mayDownload: true),
            ["MayPlayFromOutsideTheNetwork"] = Template(mayPlayFromOutsideTheNetwork: false),
            ["MayManage"] = Template(mayManage: true),
            ["MayControlOtherSessions"] = Template(mayControlOtherSessions: true),
            ["MayWatchLiveTelevision"] = Template(mayWatchLiveTelevision: true),
            ["MayManageLiveTelevision"] = Template(mayManageLiveTelevision: true),
            ["MayDeleteContent"] = Template(mayDeleteContent: true),
            ["MayManageCollections"] = Template(mayManageCollections: true),
            ["MayManageSubtitles"] = Template(mayManageSubtitles: true),
            ["MayManageLyrics"] = Template(mayManageLyrics: true),
            ["MayChangeItsOwnPreferences"] = Template(mayChangeItsOwnPreferences: false),
            ["RemoteBitrateCeiling"] = Template(remoteBitrateCeiling: 4_000_001),
            ["SimultaneousStreamCeiling"] = Template(simultaneousStreamCeiling: 3),
            ["ParentalRatingCeiling"] = Template(parentalRatingCeiling: 14),
            ["ServerDefaultsLeftAlone"] = Template(
                serverDefaultsLeftAlone: ImmutableArray.Create("EnableSyncTranscoding", "MaxParentalSubRating")),
        };

        var grants = typeof(AccountTemplate)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(grants, varied.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray());

        var unmoved = varied
            .Where(pair => pair.Value.GetHashCode() == baseline)
            .Select(pair => pair.Key)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Array.Empty<string>(), unmoved);
    }

    /// <summary>
    /// Each argument the operations refuse is refused on its own.
    /// </summary>
    /// <remarks>
    /// Three statement removals survived here, one per <c>ThrowIfNull</c>. They
    /// are the class this file was opened for, a refusal that throws nothing,
    /// and the reason they survived is that every test in this suite builds the
    /// type with all its arguments present. Removing one leaves a type that
    /// accepts a null and fails later, somewhere the failure no longer names
    /// which of them the caller forgot. The fourth argument arrived with #61
    /// and is asked the same question from the start rather than after a run.
    /// </remarks>
    /// <param name="missing">The argument this case leaves out.</param>
    [Theory]
    [InlineData("directory")]
    [InlineData("clock")]
    [InlineData("address")]
    [InlineData("templates")]
    public void TheOperationsRefuseEachArgumentOnItsOwn(string missing)
    {
        var refusal = Assert.Throws<ArgumentNullException>(() => new InvitationOperations(
            missing == "directory" ? null! : new StubStoreDirectory("nowhere"),
            missing == "clock" ? null! : new TestClock(_minted),
            missing == "address" ? null! : new StubPublicAddress("https://films.example/"),
            missing == "templates" ? null! : TestTemplates.AsConfigured));

        Assert.Equal(missing, refusal.ParamName);
    }

    /// <summary>
    /// A minting that produced no code is refused, and so is one with no record.
    /// </summary>
    /// <remarks>
    /// Two statement removals survived in this constructor, and both are the
    /// same class as the three above. The code guard is the one worth being
    /// exact about: what it refuses is a blank code as well as a missing one,
    /// because a link built from whitespace is a link that resolves and cannot
    /// be redeemed, so the case is asked in all three spellings rather than
    /// only with null.
    /// </remarks>
    /// <param name="code">The code this case presents.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMintingWithNoCodeIsRefused(string? code)
    {
        var refusal = Assert.Throws<ArgumentException>(
            () => new Minting(code!, Record(), "https://films.example/"));

        Assert.Equal("code", refusal.ParamName);
    }

    /// <summary>
    /// A minting with no record is refused before anything is built from it.
    /// </summary>
    [Fact]
    public void AMintingWithNoRecordIsRefused()
    {
        var refusal = Assert.Throws<ArgumentNullException>(
            () => new Minting("ABCDEFGHJKLMNPQRSTUVWXYZ23", null!, "https://films.example/"));

        Assert.Equal("invitation", refusal.ParamName);
    }

    /// <summary>
    /// An operation on a server that told this plugin no data directory is
    /// refused, and the refusal says what to ask first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mutant that survived here is not a removal. Cutting the throw out
    /// does not compile, because the routine then returns a possible null; what
    /// the run produced is the branch handing its caller the unset value, so
    /// the site is reached at run time and the store is built on nothing.
    /// </para>
    /// <para>
    /// It survived because nothing asked any operation what it does on such a
    /// server. Both spellings of absent are asked here, an unset path and a
    /// blank one, because <see cref="InvitationOperations.StoreIsAvailable"/>
    /// treats them alike and a guard that agreed with it on only one of the two
    /// would be a second answer to the same question.
    /// </para>
    /// </remarks>
    /// <param name="path">The path the server handed the plugin.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnOperationWithNoDataDirectoryIsRefused(string? path)
    {
        var operations = new InvitationOperations(
            new StubStoreDirectory(path),
            new TestClock(_minted),
            new StubPublicAddress("https://films.example/"), TestTemplates.AsConfigured);

        Assert.False(operations.StoreIsAvailable);

        var refusal = Assert.Throws<InvalidOperationException>(() => operations.All());

        Assert.Contains("StoreIsAvailable", refusal.Message, StringComparison.Ordinal);

        Assert.Throws<InvalidOperationException>(
            () => operations.Mint(_operator, "guest", null, null));
    }

    /// <summary>
    /// The two readings a retention sweep is built out of refuse a record that
    /// is not there.
    /// </summary>
    /// <remarks>
    /// Two more statement removals of the same class, one per routine. Both are
    /// reached with a record every test in this suite supplies, so removing the
    /// guard leaves a routine that dereferences null and fails as a null
    /// reference rather than as a named argument. The assertion is the exception
    /// type and the parameter, because that is the difference the mutant makes:
    /// something is thrown either way.
    /// </remarks>
    /// <param name="routine">The routine this case drives.</param>
    [Theory]
    [InlineData("IsLive")]
    [InlineData("RetentionStartsAt")]
    public void TheDecisionReadingsRefuseARecordThatIsNotThere(string routine)
    {
        var refusal = Assert.Throws<ArgumentNullException>(() =>
        {
            if (routine == "IsLive")
            {
                RedemptionDecision.IsLive(null!, _minted);
                return;
            }

            RedemptionDecision.RetentionStartsAt(null!, _minted);
        });

        Assert.Equal("invitation", refusal.ParamName);
    }

    /// <summary>
    /// The refusal a full server hands back says how full it is.
    /// </summary>
    /// <remarks>
    /// The sentence this asserts is built in a private routine whose whole body
    /// survived the run, which means no test read the message back. What it is
    /// for is an operator who has just been refused a mint and needs to know
    /// what to do about it, so the numbers being in it is the point of the type
    /// carrying them at all.
    /// </remarks>
    [Fact]
    public void TheCeilingRefusalNamesTheCountAndTheCeiling()
    {
        var refusal = new LiveCeilingReachedException(500, 500);

        Assert.Equal(500, refusal.Live);
        Assert.Equal(500, refusal.Ceiling);
        Assert.Contains("500 live invitations", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("at most 500", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The three constructors this type carries for the analyser answer for
    /// themselves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three block removals survived here, one per constructor, and this did
    /// not kill them. Each body assigned zero to a property whose own default is
    /// zero, so removing it changed nothing a caller can read, which was probed
    /// rather than reasoned about: with all three bodies emptied by hand the
    /// whole suite stayed green, this test included. THEY WERE EQUIVALENT
    /// MUTANTS AND THE THRESHOLD COULD NOT REACH THEM, and under #376 the three
    /// bodies are empty in the tree rather than emptied by a probe. The six
    /// statements said the same value twice; deleting them removes the three
    /// mutants instead of leaving the run to report them every week.
    /// </para>
    /// <para>
    /// It is here anyway, because the property the survivors pointed at is
    /// worth holding whatever kills them. These three exist because an analyser
    /// rule asks an exception type for the standard set and nothing in the
    /// plugin calls them. What they must not do is look like the refusal that
    /// carries numbers: a caller reading <c>Live</c> off one of them gets a
    /// count that was never measured, and an assignment of the real count added
    /// here later would be caught by this rather than by nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheConstructorsCarriedForTheAnalyserCountNothing()
    {
        var bare = new LiveCeilingReachedException();

        Assert.Equal(0, bare.Live);
        Assert.Equal(0, bare.Ceiling);
        Assert.Contains("as many live invitations", bare.Message, StringComparison.Ordinal);

        var worded = new LiveCeilingReachedException("the store is full");

        Assert.Equal(0, worded.Live);
        Assert.Equal(0, worded.Ceiling);
        Assert.Equal("the store is full", worded.Message);

        var underneath = new InvalidOperationException("underneath");
        var wrapped = new LiveCeilingReachedException("the store is full", underneath);

        Assert.Equal(0, wrapped.Live);
        Assert.Equal(0, wrapped.Ceiling);
        Assert.Equal("the store is full", wrapped.Message);
        Assert.Same(underneath, wrapped.InnerException);
    }
}
