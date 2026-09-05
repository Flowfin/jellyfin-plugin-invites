using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// Minting, and the count it decides.
/// </summary>
/// <remarks>
/// The last two facts here are about the count staying the record's own answer
/// rather than about minting, and they sit beside it because that is the rule
/// minting exists to keep: the number is chosen once and nothing recomputes it
/// afterwards.
/// </remarks>
public class InvitationMintTests
{
    private const string PresentableCode = "23456789234567892345678923";

    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _expires = new(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _wellInside = new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly IInvitationCodeHash _codeHash = new TestCodeHash();

    /// <summary>
    /// A freshly minted invitation is worth every use it was granted, has
    /// created nothing and has not been revoked.
    /// </summary>
    [Fact]
    public void AMintedInvitationCarriesEveryUseItWasGranted()
    {
        var invitation = AMintFor(uses: 3);

        Assert.Equal(3, invitation.UsesGranted);
        Assert.Equal(3, invitation.UsesRemaining);
        Assert.Empty(invitation.AccountsProduced);
        Assert.Null(invitation.RevokedAt);
        Assert.False(invitation.IsRevoked);
        Assert.Equal(_expires, invitation.ExpiresAt);
    }

    /// <summary>
    /// A count of zero, and a count below it, are refused. Neither is a
    /// stricter invitation; both are a link that refuses everybody while
    /// reading to the operator exactly like one that works.
    /// </summary>
    /// <param name="uses">The count asked for.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void AnInvitationForNoAccountsIsRefused(int uses)
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => AMintFor(uses));

        Assert.Equal("uses", refusal.ParamName);
    }

    /// <summary>
    /// The ceiling, at the three points that decide it. One below is minted,
    /// the ceiling itself is minted, and one above is refused.
    /// </summary>
    /// <remarks>
    /// The boundary is written as an inclusive ceiling in #33, so the fact that
    /// the ceiling itself mints is the assertion carrying the whole direction of
    /// the comparison. A run that only tested a large number would pass with the
    /// comparison one out.
    /// </remarks>
    [Fact]
    public void TheCeilingHoldsAtItsOwnValueAndRefusesOneAbove()
    {
        Assert.Equal(
            InvitationMint.UsesCeiling - 1,
            AMintFor(InvitationMint.UsesCeiling - 1).UsesGranted);

        Assert.Equal(
            InvitationMint.UsesCeiling,
            AMintFor(InvitationMint.UsesCeiling).UsesGranted);

        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => AMintFor(InvitationMint.UsesCeiling + 1));

        Assert.Equal("uses", refusal.ParamName);
    }

    /// <summary>
    /// The refusal says both numbers, because an operator who has just been
    /// told no is owed what the limit was as well as what they asked for.
    /// </summary>
    [Fact]
    public void TheCeilingRefusalNamesTheLimitAndWhatWasAsked()
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => AMintFor(InvitationMint.UsesCeiling + 7));

        Assert.Contains(InvitationMint.UsesCeiling.ToString(System.Globalization.CultureInfo.InvariantCulture), refusal.Message, StringComparison.Ordinal);
        Assert.Contains((InvitationMint.UsesCeiling + 7).ToString(System.Globalization.CultureInfo.InvariantCulture), refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// An account created from an invitation and then deleted does not give the
    /// use back.
    /// </summary>
    /// <remarks>
    /// Deleting an account happens outside this plugin, so what the suite can
    /// hold is the record's side of it: the accounts a record names are taken
    /// away, and the count does not move. The second half is what makes the
    /// first half matter. A decision that counted the accounts produced instead
    /// of reading the field would honour this record, because the accounts are
    /// gone and the arithmetic would say the invitation was never used.
    /// </remarks>
    [Fact]
    public void DeletingAnAccountAnInvitationCreatedDoesNotRestoreAUse()
    {
        var spent = ASpentRecordThatProduced(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Guid.Parse("99999999-8888-7777-6666-555555555555"));

        var afterTheAccountsWereDeleted = Without(spent, spent.AccountsProduced.Accounts());

        Assert.Empty(afterTheAccountsWereDeleted.AccountsProduced);
        Assert.Equal(spent.UsesRemaining, afterTheAccountsWereDeleted.UsesRemaining);
        Assert.Equal(0, afterTheAccountsWereDeleted.UsesRemaining);

        var verdict = RedemptionDecision.Decide(
            PresentableCode,
            _codeHash,
            [afterTheAccountsWereDeleted],
            _wellInside);

        Assert.False(verdict.MayCreateAnAccount);
        Assert.Equal(RedemptionOutcome.Spent, verdict.Outcome);
    }

    /// <summary>
    /// The granted count is a second reading of the same event and is not
    /// touched either, so a record whose accounts are gone still says what it
    /// was worth when it was minted.
    /// </summary>
    [Fact]
    public void WhatAnInvitationWasWorthSurvivesTheAccountsItCreated()
    {
        var spent = ASpentRecordThatProduced(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

        var afterTheAccountWasDeleted = Without(spent, spent.AccountsProduced.Accounts());

        Assert.Equal(spent.UsesGranted, afterTheAccountWasDeleted.UsesGranted);
    }

    private static Invitation AMintFor(int uses)
    {
        var canonical = InvitationCode.Canonicalise(PresentableCode);
        Assert.NotNull(canonical);

        return InvitationMint.Mint(
            id: Guid.Parse("6f0a2d1e-6b3c-4f8a-9f1d-2a7c5e8b0d34"),
            codeHash: _codeHash.Of(canonical),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: _minted,
            expiresAt: _expires,
            uses: uses,
            templateLabel: "Household",
            template: TestTemplates.Household);
    }

    /// <summary>
    /// A record minted for two accounts, both of them created, so nothing is
    /// left on it.
    /// </summary>
    /// <param name="accounts">The accounts it produced.</param>
    /// <returns>The record.</returns>
    private static Invitation ASpentRecordThatProduced(params Guid[] accounts)
    {
        var minted = AMintFor(accounts.Length);

        return new Invitation(
            id: minted.Id,
            codeHash: minted.CodeHash,
            mintedBy: minted.MintedBy,
            mintedAt: minted.MintedAt,
            expiresAt: minted.ExpiresAt,
            usesGranted: minted.UsesGranted,
            usesRemaining: 0,
            revokedAt: null,
            revokedBy: null,
            templateLabel: minted.TemplateLabel,
            template: minted.Template,
            accountsProduced: ProducedAccounts.ThatDoNotExpire(accounts));
    }

    /// <summary>
    /// The same record with some of the accounts it names taken off it, which
    /// is what an operator deleting those accounts leaves behind once the
    /// record catches up with them.
    /// </summary>
    /// <param name="record">The record.</param>
    /// <param name="deleted">The accounts that are gone.</param>
    /// <returns>The record without them.</returns>
    private static Invitation Without(Invitation record, IEnumerable<Guid> deleted)
    {
        var gone = deleted.ToHashSet();

        return new Invitation(
            id: record.Id,
            codeHash: record.CodeHash,
            mintedBy: record.MintedBy,
            mintedAt: record.MintedAt,
            expiresAt: record.ExpiresAt,
            usesGranted: record.UsesGranted,
            usesRemaining: record.UsesRemaining,
            revokedAt: record.RevokedAt,
            revokedBy: record.RevokedBy,
            templateLabel: record.TemplateLabel,
            template: record.Template,
            accountsProduced: [.. record.AccountsProduced.Where(claim => !gone.Contains(claim.Account))]);
    }
}
