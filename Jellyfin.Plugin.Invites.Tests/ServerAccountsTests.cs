using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Invites.Accounts;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A user manager shaped the way the version this plugin compiles against
/// shapes it: the account identifiers behind a method.
/// </summary>
internal sealed class AServerAnsweringWithAMethod
{
    private readonly Guid[] _accounts;

    public AServerAnsweringWithAMethod(params Guid[] accounts)
    {
        _accounts = accounts;
    }

    public IEnumerable<Guid> GetUsersIds() => _accounts;
}

/// <summary>
/// A user manager shaped the way the floor this plugin claims shapes it: the
/// same identifiers behind a property.
/// </summary>
internal sealed class AServerAnsweringWithAProperty
{
    private readonly Guid[] _accounts;

    public AServerAnsweringWithAProperty(params Guid[] accounts)
    {
        _accounts = accounts;
    }

    public IEnumerable<Guid> UsersIds => _accounts;
}

/// <summary>
/// A user manager that answers for neither, which is the case this plugin was
/// not built for.
/// </summary>
internal sealed class AServerAnsweringForNeither
{
    public IEnumerable<Guid> Accounts => [];
}

/// <summary>
/// A user manager whose member is there and answers something else.
/// </summary>
internal sealed class AServerAnsweringSomethingElse
{
    public string UsersIds => "not a list of identifiers";
}

/// <summary>
/// Reading the account identifiers off a user manager whose member changed
/// shape inside the supported server line.
/// </summary>
/// <remarks>
/// The stand-ins here are not stand-ins for a server. They are the two member
/// shapes, which is the whole of what this reads, so each one asks the question
/// exactly. What is not exercised is that a real Jellyfin user manager carries
/// one of them: that is read off the packages and is quoted on
/// <see cref="ServerAccounts"/> rather than asserted here.
/// </remarks>
public class ServerAccountsTests
{
    private static readonly Guid _first = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid _second = Guid.Parse("22222222-2222-4222-8222-222222222222");

    /// <summary>
    /// The shape on the version this plugin compiles against.
    /// </summary>
    [Fact]
    public void TheAccountsAreReadWhenTheServerAnswersWithAMethod()
    {
        var read = ServerAccounts.Of(new AServerAnsweringWithAMethod(_first, _second));

        Assert.Equal([_first, _second], read);
    }

    /// <summary>
    /// The shape on the floor the manifest declares. This is the leg the ABI
    /// floor build refused a direct call for.
    /// </summary>
    [Fact]
    public void TheAccountsAreReadWhenTheServerAnswersWithAProperty()
    {
        var read = ServerAccounts.Of(new AServerAnsweringWithAProperty(_first));

        Assert.Equal([_first], read);
    }

    /// <summary>
    /// A server carrying neither member answers nothing rather than an empty
    /// list. The caller reports that; it does not compare a store against it.
    /// </summary>
    [Fact]
    public void AServerCarryingNeitherMemberIsNotReadAsHavingNoAccounts()
    {
        Assert.Null(ServerAccounts.Of(new AServerAnsweringForNeither()));
    }

    /// <summary>
    /// A member of the right name answering the wrong thing is the same answer
    /// as no member at all, rather than an exception out of a load.
    /// </summary>
    [Fact]
    public void AMemberAnsweringSomethingElseIsNotRead()
    {
        Assert.Null(ServerAccounts.Of(new AServerAnsweringSomethingElse()));
    }

    /// <summary>
    /// Nothing is read off nothing.
    /// </summary>
    [Fact]
    public void ThereIsNothingToReadWithoutAUserManager()
    {
        Assert.Throws<ArgumentNullException>(() => ServerAccounts.Of(null!));
    }

    /// <summary>
    /// What a refusal says it was looking for names both members, so an
    /// operator on a server this plugin was not built for can see which.
    /// </summary>
    [Fact]
    public void TheRefusalNamesBothMembers()
    {
        var named = ServerAccounts.WhatWasLookedFor();

        Assert.Contains("GetUsersIds", named, StringComparison.Ordinal);
        Assert.Contains("UsersIds", named, StringComparison.Ordinal);
    }
}
