using System;
using System.Collections.Immutable;
using System.Linq;
using Jellyfin.Plugin.Invites.Invitations;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The two writes a honoured redemption makes to the record it was honoured
/// against.
/// </summary>
/// <remarks>
/// They are asserted apart from the route because they are the pair that has to
/// be right for a use count to mean anything, and a test that could only reach
/// them through a request would say nothing about the fields the request did not
/// happen to touch.
/// </remarks>
public class SpendingTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid _account = Guid.Parse("44444444-4444-4444-8444-444444444444");

    /// <summary>
    /// One use goes and every other field is carried across untouched.
    /// </summary>
    /// <remarks>
    /// The record built here carries a value in every field, including a
    /// revocation and an account it already produced, so a routine that dropped
    /// one has something to drop. A fixture of defaults would round-trip through
    /// a writer that lost half of them.
    /// </remarks>
    [Fact]
    public void OneUseGoesAndNothingElseMoves()
    {
        var before = ARecord(usesGranted: 3, usesRemaining: 2);

        var after = Spending.Of(before);

        Assert.Equal(1, after.UsesRemaining);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.CodeHash, after.CodeHash);
        Assert.Equal(before.MintedBy, after.MintedBy);
        Assert.Equal(before.MintedAt, after.MintedAt);
        Assert.Equal(before.ExpiresAt, after.ExpiresAt);
        Assert.Equal(before.UsesGranted, after.UsesGranted);
        Assert.Equal(before.RevokedAt, after.RevokedAt);
        Assert.Equal(before.RevokedBy, after.RevokedBy);
        Assert.Equal(before.TemplateLabel, after.TemplateLabel);
        Assert.Equal(before.Template, after.Template);
        Assert.Equal(before.AccountsProduced, after.AccountsProduced);
    }

    /// <summary>
    /// Taking a use from a record that has none is refused by the record's own
    /// invariant rather than passed over.
    /// </summary>
    /// <remarks>
    /// A caller that reached here without asking the decision routine has
    /// skipped the decision, and answering that with a record that quietly did
    /// not change would leave the account created and the count untouched, which
    /// is the failure the count exists against. This routine writes no
    /// comparison of its own, because a second opinion about a use count is what
    /// the invariant lint refuses; what refuses this is the count being a count
    /// of the granted uses.
    /// </remarks>
    [Fact]
    public void TakingAUseFromASpentRecordIsRefused()
    {
        var spent = ARecord(usesGranted: 1, usesRemaining: 0);

        Assert.Throws<ArgumentException>(() => Spending.Of(spent));
    }

    /// <summary>
    /// The account is recorded and the count is not touched by recording it.
    /// </summary>
    /// <remarks>
    /// The two writes are separate acts at separate moments, and a routine that
    /// did both at once would take a second use off every record whose account
    /// arrived.
    /// </remarks>
    [Fact]
    public void RecordingAnAccountLeavesTheCountAlone()
    {
        var before = ARecord(usesGranted: 3, usesRemaining: 1);

        var after = Spending.With(before, _account);

        Assert.Equal(1, after.UsesRemaining);
        Assert.Equal(before.AccountsProduced.Add(_account).ToArray(), after.AccountsProduced.ToArray());
    }

    /// <summary>
    /// Recording the same account twice writes nothing the second time, and the
    /// caller can see that by reference.
    /// </summary>
    /// <remarks>
    /// A record claiming one account twice would make the operator's view say an
    /// invitation produced two accounts that are one account, and the store
    /// would move on disk for a write that changed nothing.
    /// </remarks>
    [Fact]
    public void RecordingAnAccountTwiceWritesNothing()
    {
        var once = Spending.With(ARecord(usesGranted: 3, usesRemaining: 1), _account);

        Assert.Same(once, Spending.With(once, _account));
    }

    /// <summary>
    /// An account recorded against nobody is refused.
    /// </summary>
    /// <remarks>
    /// The empty identifier reads like an answer to the question this field
    /// exists to answer, and the question is the one an operator asks when an
    /// account they do not recognise appears.
    /// </remarks>
    [Fact]
    public void AnAccountRecordedAgainstNobodyIsRefused()
    {
        Assert.Throws<ArgumentException>(
            () => Spending.With(ARecord(usesGranted: 3, usesRemaining: 1), Guid.Empty));
    }

    /// <summary>
    /// Neither routine takes a null record.
    /// </summary>
    [Fact]
    public void NeitherRoutineTakesNothing()
    {
        Assert.Throws<ArgumentNullException>(() => Spending.Of(null!));
        Assert.Throws<ArgumentNullException>(() => Spending.With(null!, _account));
    }

    /// <summary>
    /// A record with a value in every field, so a routine that dropped one is
    /// caught rather than passing over a default that matched.
    /// </summary>
    /// <param name="usesGranted">How many uses it was minted with.</param>
    /// <param name="usesRemaining">How many are left.</param>
    /// <returns>The record.</returns>
    private static Invitation ARecord(int usesGranted, int usesRemaining) =>
        new(
            id: Guid.Parse("0f1e2d3c-4b5a-4968-8776-65544332211a"),
            codeHash: ImmutableArray.Create<byte>(0x01, 0x7f, 0x80, 0xff),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: _minted,
            expiresAt: _minted.AddDays(7),
            usesGranted: usesGranted,
            usesRemaining: usesRemaining,
            revokedAt: _minted.AddDays(1),
            revokedBy: Guid.Parse("44445555-6666-7777-8888-99990000aaaa"),
            templateLabel: "Household",
            template: TestTemplates.Household,
            accountsProduced: ImmutableArray.Create(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")));
}
