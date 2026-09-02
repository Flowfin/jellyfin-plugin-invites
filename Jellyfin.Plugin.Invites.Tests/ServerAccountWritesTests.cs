using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// An account entity, standing for the one the server hands back on the floor.
/// </summary>
internal sealed class AnAccountTheServerHolds
{
    public AnAccountTheServerHolds(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; }
}

/// <summary>
/// A user manager shaped the way the floor this plugin claims shapes it: the
/// credential member takes the account entity, so the caller has to look the
/// account up first.
/// </summary>
internal sealed class AServerTakingTheAccountItself
{
    private readonly Dictionary<Guid, AnAccountTheServerHolds> _accounts = new();

    public AnAccountTheServerHolds? Told { get; private set; }

    public string? Given { get; private set; }

    public AnAccountTheServerHolds Hold(Guid id)
    {
        var account = new AnAccountTheServerHolds(id);
        _accounts[id] = account;
        return account;
    }

    public AnAccountTheServerHolds? GetUserById(Guid id) => _accounts.TryGetValue(id, out var account) ? account : null;

    public Task ChangePassword(AnAccountTheServerHolds user, string newPassword)
    {
        Told = user;
        Given = newPassword;
        return Task.CompletedTask;
    }
}

/// <summary>
/// A user manager shaped the way the version this plugin compiles against
/// shapes it: the credential member takes the identifier.
/// </summary>
internal sealed class AServerTakingTheIdentifier
{
    public Guid Told { get; private set; }

    public string? Given { get; private set; }

    public Task ChangePassword(Guid userId, string newPassword)
    {
        Told = userId;
        Given = newPassword;
        return Task.CompletedTask;
    }
}

/// <summary>
/// A user manager carrying the credential member in neither shape, which is a
/// server this plugin was not built for.
/// </summary>
internal sealed class AServerTakingNeither
{
    public Task SetPassword(Guid userId, string newPassword) => Task.CompletedTask;
}

/// <summary>
/// A user manager whose credential member wants the account entity and whose
/// lookup does not know it, which is the account having gone between two calls.
/// </summary>
internal sealed class AServerThatHasLostTheAccount
{
    public AnAccountTheServerHolds? GetUserById(Guid id) => null;

    public Task ChangePassword(AnAccountTheServerHolds user, string newPassword) => Task.CompletedTask;
}

/// <summary>
/// A user manager whose credential member wants the account entity and which
/// carries no way to look one up, which is a server shaped like neither end of
/// the declared line.
/// </summary>
internal sealed class AServerWithNoWayToLookAnAccountUp
{
    public Task ChangePassword(AnAccountTheServerHolds user, string newPassword) => Task.CompletedTask;
}

/// <summary>
/// Setting a credential on a user manager whose member changed shape inside the
/// supported server line.
/// </summary>
/// <remarks>
/// <para>
/// The stand-ins here are not stand-ins for a server. They are the two member
/// shapes, which is the whole of what this arm reads, so each one asks the
/// question exactly. What is not exercised is that a real Jellyfin user manager
/// carries one of them: that is read off the packages and is quoted on
/// <see cref="ServerAccountWrites"/> rather than asserted here.
/// </para>
/// <para>
/// A fake of the server's own interface would carry one shape and would not
/// compile at the other end of the declared line, which is why this arm takes an
/// object and why the seam's other three arms have no test of their own.
/// </para>
/// </remarks>
public class ServerAccountWritesTests
{
    private static readonly Guid _account = Guid.Parse("77777777-7777-4777-8777-777777777777");

    /// <summary>
    /// The shape on the version this plugin compiles against.
    /// </summary>
    /// <returns>A task that completes when the credential has been set.</returns>
    [Fact]
    public async Task TheCredentialIsSetWhenTheServerTakesTheIdentifier()
    {
        var server = new AServerTakingTheIdentifier();

        await ServerAccountWrites.SetCredentialOn(server, _account, "a-chosen-credential");

        Assert.Equal(_account, server.Told);
        Assert.Equal("a-chosen-credential", server.Given);
    }

    /// <summary>
    /// The shape on the floor the manifest declares. This is the leg the ABI
    /// floor build refuses a direct call for.
    /// </summary>
    /// <returns>A task that completes when the credential has been set.</returns>
    [Fact]
    public async Task TheCredentialIsSetWhenTheServerTakesTheAccountItself()
    {
        var server = new AServerTakingTheAccountItself();
        var held = server.Hold(_account);

        await ServerAccountWrites.SetCredentialOn(server, _account, "a-chosen-credential");

        Assert.Same(held, server.Told);
        Assert.Equal("a-chosen-credential", server.Given);
    }

    /// <summary>
    /// A server carrying the member in neither shape is reported rather than
    /// guessed at. The account already exists at this point, so carrying on
    /// would leave one nobody can sign in to and nothing would say so.
    /// </summary>
    /// <returns>A task that completes when the refusal has been read.</returns>
    [Fact]
    public async Task AServerCarryingNeitherShapeIsRefusedRatherThanGuessedAt()
    {
        var refused = await Assert.ThrowsAsync<ServerAccountWriteRefusedException>(
            () => ServerAccountWrites.SetCredentialOn(new AServerTakingNeither(), _account, "a-chosen-credential"));

        Assert.Contains("ChangePassword", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The lookup the older shape needs answering nothing is a refusal too, and
    /// not a call made with nothing in the argument.
    /// </summary>
    /// <returns>A task that completes when the refusal has been read.</returns>
    [Fact]
    public async Task AnAccountTheServerNoLongerHoldsIsRefusedRatherThanPassedOnAsNothing()
    {
        await Assert.ThrowsAsync<ServerAccountWriteRefusedException>(
            () => ServerAccountWrites.SetCredentialOn(new AServerThatHasLostTheAccount(), _account, "a-chosen-credential"));
    }

    /// <summary>
    /// Nothing is set on nothing.
    /// </summary>
    /// <returns>A task that completes when the refusal has been read.</returns>
    [Fact]
    public async Task ThereIsNothingToSetACredentialOnWithoutAUserManager()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ServerAccountWrites.SetCredentialOn(null!, _account, "a-chosen-credential"));
    }

    /// <summary>
    /// The older shape needs a lookup, and a server carrying the credential
    /// member without one is refused by name rather than called with nothing.
    /// </summary>
    /// <returns>A task that completes when the refusal has been read.</returns>
    [Fact]
    public async Task AServerWithNoLookupForTheOlderShapeIsRefusedByName()
    {
        var refused = await Assert.ThrowsAsync<ServerAccountWriteRefusedException>(
            () => ServerAccountWrites.SetCredentialOn(new AServerWithNoWayToLookAnAccountUp(), _account, "a-chosen-credential"));

        Assert.Contains("GetUserById", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The refusal carries what it happened during where it was raised over
    /// something else, which is the shape a caller wrapping a server's own
    /// failure needs and which nothing in the plugin reaches for yet.
    /// </summary>
    [Fact]
    public void TheRefusalCanCarryWhatItHappenedDuring()
    {
        var underneath = new InvalidOperationException("what the server said");

        var refused = new ServerAccountWriteRefusedException("the credential was not taken", underneath);

        Assert.Same(underneath, refused.InnerException);
        Assert.Equal("the credential was not taken", refused.Message);
        Assert.NotEmpty(new ServerAccountWriteRefusedException().Message);
    }

    /// <summary>
    /// The refusal says what it was looking for, so an operator on an
    /// unsupported server reads the member rather than a stack.
    /// </summary>
    [Fact]
    public void TheRefusalNamesBothShapesItLookedFor()
    {
        var looked = ServerAccountWrites.WhatWasLookedFor();

        Assert.Contains("ChangePassword(account, password)", looked, StringComparison.Ordinal);
        Assert.Contains("ChangePassword(identifier, password)", looked, StringComparison.Ordinal);
    }
}
