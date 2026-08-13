using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Invites.Invitations;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// What revoking does to a record, and what it leaves alone.
/// </summary>
/// <remarks>
/// <para>
/// Three of the five clauses of #54 are asserted here: revoking twice is a
/// no-op that keeps the first time, the revoking operator and the time are
/// recorded, and no account is affected.
/// </para>
/// <para>
/// The two that are not are the immediacy clauses, and they are not here
/// because there is nothing yet for them to be asserted against. A revocation
/// taking effect for a redemption already on the page with the form filled in
/// is a property of where the lock sits in whoever redeems, and no routine in
/// this plugin redeems. Nothing below should be read as covering them.
/// </para>
/// </remarks>
public class RevocationTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _revokedAt = new(2026, 5, 3, 8, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _later = new(2026, 5, 4, 17, 45, 0, TimeSpan.Zero);

    private static readonly Guid _operator = Guid.Parse("44445555-6666-7777-8888-99990000aaaa");
    private static readonly Guid _anotherOperator = Guid.Parse("bbbbcccc-dddd-eeee-ffff-000011112222");

    /// <summary>
    /// The two things the record has to carry afterwards, which is the clause
    /// asking that the revoking operator and the time are recorded.
    /// </summary>
    [Fact]
    public void RevokingRecordsTheOperatorAndTheTime()
    {
        var revoked = Revocation.Of(ALiveInvitation(), _operator, _revokedAt);

        Assert.True(revoked.IsRevoked);
        Assert.Equal(_revokedAt, revoked.RevokedAt);
        Assert.Equal(_operator, revoked.RevokedBy);
    }

    /// <summary>
    /// Revoking again is not an error and moves nothing, including when the
    /// second one arrives later and from somebody else. The instant kept is the
    /// moment the invitation stopped working, and the operator kept is the one
    /// who stopped it.
    /// </summary>
    /// <remarks>
    /// Take the <c>IsRevoked</c> arm out of <see cref="Revocation.Of"/> and
    /// this goes red on the time, which is the mistake somebody makes: the
    /// second call reads like a call that has nothing to do.
    /// </remarks>
    [Fact]
    public void RevokingTwiceKeepsTheFirstTimeAndTheFirstOperator()
    {
        var once = Revocation.Of(ALiveInvitation(), _operator, _revokedAt);
        var twice = Revocation.Of(once, _anotherOperator, _later);

        Assert.Equal(_revokedAt, twice.RevokedAt);
        Assert.Equal(_operator, twice.RevokedBy);
        Assert.Equal(once, twice);
    }

    /// <summary>
    /// And the second revocation writes nothing at all, rather than writing a
    /// record that happens to be equal. A caller holding the store's lock can
    /// see that there is nothing to persist.
    /// </summary>
    [Fact]
    public void RevokingTwiceHandsBackTheRecordItWasGiven()
    {
        var once = Revocation.Of(ALiveInvitation(), _operator, _revokedAt);

        Assert.Same(once, Revocation.Of(once, _anotherOperator, _later));
    }

    /// <summary>
    /// Everything that is not the revocation survives it. The counts are the
    /// part worth naming: revoking does not spend a use, so a revoked
    /// invitation still says what it was worth and how much of that was left.
    /// </summary>
    [Fact]
    public void RevokingChangesNothingElseAboutTheRecord()
    {
        var live = ALiveInvitation();
        var revoked = Revocation.Of(live, _operator, _revokedAt);

        Assert.Equal(live.Id, revoked.Id);
        Assert.Equal(live.CodeHash, revoked.CodeHash);
        Assert.Equal(live.MintedBy, revoked.MintedBy);
        Assert.Equal(live.MintedAt, revoked.MintedAt);
        Assert.Equal(live.ExpiresAt, revoked.ExpiresAt);
        Assert.Equal(live.UsesGranted, revoked.UsesGranted);
        Assert.Equal(live.UsesRemaining, revoked.UsesRemaining);
        Assert.Equal(live.TemplateLabel, revoked.TemplateLabel, StringComparer.Ordinal);
        Assert.Equal(live.AccountsProduced, revoked.AccountsProduced);
    }

    /// <summary>
    /// The accounts the invitation already created are still named by the
    /// record it leaves behind. Revocation stops future accounts and does not
    /// disown past ones, and losing that list would be the quiet way of
    /// disowning them.
    /// </summary>
    [Fact]
    public void TheAccountsAlreadyCreatedAreStillNamed()
    {
        var live = ALiveInvitation();
        Assert.NotEmpty(live.AccountsProduced);

        Assert.Equal(live.AccountsProduced, Revocation.Of(live, _operator, _revokedAt).AccountsProduced);
    }

    /// <summary>
    /// No account is affected, and the reason is that there is no way to hand
    /// this routine one. Every parameter of every public member is a record, an
    /// identifier or an instant, so no user manager, account or server object
    /// can be passed in for a later change to start calling.
    /// </summary>
    /// <remarks>
    /// This is the machine-checkable form of the clause. Asserting after a call
    /// that nothing changed would pass for a routine that reached an account
    /// and happened to leave it as it was, which is the version that changes
    /// something after the next edit. Add an <c>IUserManager</c> parameter and
    /// this goes red before anything is written with it.
    /// </remarks>
    [Fact]
    public void NothingHereCanBeHandedAnAccount()
    {
        var allowed = new[] { typeof(Invitation), typeof(Guid), typeof(DateTimeOffset) };

        var parameters = typeof(Revocation)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters())
            .ToList();

        Assert.NotEmpty(parameters);
        Assert.All(parameters, parameter => Assert.Contains(parameter.ParameterType, allowed));
    }

    /// <summary>
    /// A revocation recorded against nobody answers the question the field
    /// exists to answer with a value that reads like an answer. Delete the
    /// guard and this goes red.
    /// </summary>
    [Fact]
    public void ARevocationByNobodyIsRefused()
    {
        Assert.Throws<ArgumentException>(() => Revocation.Of(ALiveInvitation(), Guid.Empty, _revokedAt));
    }

    /// <summary>
    /// And there has to be a record to revoke.
    /// </summary>
    [Fact]
    public void ARevocationOfNothingIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => Revocation.Of(null!, _operator, _revokedAt));
    }

    /// <summary>
    /// One invitation with a use spent and an account behind it, so a test that
    /// asserts nothing else moved has something that could have moved.
    /// </summary>
    /// <returns>The record.</returns>
    private static Invitation ALiveInvitation()
    {
        return new Invitation(
            id: Guid.Parse("6f0a2d1e-6b3c-4f8a-9f1d-2a7c5e8b0d34"),
            codeHash: ImmutableArray.Create<byte>(0x01, 0x7f, 0x80, 0xff),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: _minted,
            expiresAt: new DateTimeOffset(2026, 5, 8, 12, 0, 0, TimeSpan.Zero),
            usesGranted: 3,
            usesRemaining: 2,
            revokedAt: null,
            revokedBy: null,
            templateLabel: "Household",
            accountsProduced: ImmutableArray.Create(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")));
    }
}
