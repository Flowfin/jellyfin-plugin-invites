using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Events;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Startup;
using Jellyfin.Plugin.Invites.Storage;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Users;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A store directory the test hands in, standing in for the data directory the
/// server gives the plugin.
/// </summary>
internal sealed class StubStoreDirectory : IStoreDirectory
{
    public StubStoreDirectory(string? path)
    {
        Path = path;
    }

    public string? Path { get; }
}

/// <summary>
/// An account list that answers one question and refuses every other call.
/// </summary>
/// <remarks>
/// The refusals are the point rather than scaffolding. A load reads the accounts
/// the server has and does nothing else to them, and that is a property of what
/// it never calls rather than of anything observable afterwards, so the fake
/// fails the test the moment anything reaches for a second member.
/// </remarks>
internal sealed class RefusingUserManager : IUserManager
{
    private readonly Guid[] _accounts;

    public RefusingUserManager(params Guid[] accounts)
    {
        _accounts = accounts;
    }

    public event EventHandler<GenericEventArgs<User>>? OnUserUpdated
    {
        add => throw new NotSupportedException("A load does not subscribe to account changes.");
        remove => throw new NotSupportedException("A load does not subscribe to account changes.");
    }

    public IEnumerable<Guid> GetUsersIds() => _accounts;

    public IEnumerable<User> GetUsers()
        => throw new NotSupportedException("A load reads identifiers, not accounts.");

    public Task InitializeAsync()
        => throw new NotSupportedException("A load does not initialize the account list.");

    public User GetUserById(Guid id)
        => throw new NotSupportedException("A load reads identifiers, not accounts.");

    public User GetFirstUser()
        => throw new NotSupportedException("A load reads identifiers, not accounts.");

    public User GetUserByName(string name)
        => throw new NotSupportedException("A load reads identifiers, not accounts.");

    public Task RenameUser(Guid id, string oldName, string newName)
        => throw new NotSupportedException("A load changes no account.");

    public Task UpdateUserAsync(User user)
        => throw new NotSupportedException("A load changes no account.");

    public Task<User> CreateUserAsync(string name)
        => throw new NotSupportedException("A load creates no account.");

    public Task DeleteUserAsync(Guid id)
        => throw new NotSupportedException("A load deletes no account.");

    public Task ResetPassword(Guid id)
        => throw new NotSupportedException("A load changes no credential.");

    public Task ChangePassword(Guid id, string newPassword)
        => throw new NotSupportedException("A load changes no credential.");

    public UserDto GetUserDto(User user, string? remoteEndPoint = null)
        => throw new NotSupportedException("A load renders nothing.");

    public Task<User?> AuthenticateUser(string username, string password, string remoteEndPoint, bool isUserSession)
        => throw new NotSupportedException("A load authenticates nobody.");

    public Task<ForgotPasswordResult> StartForgotPasswordProcess(string enteredUsername, bool isInNetwork)
        => throw new NotSupportedException("A load starts nothing.");

    public Task<PinRedeemResult> RedeemPasswordResetPin(string pin)
        => throw new NotSupportedException("A load redeems nothing here.");

    public NameIdPair[] GetAuthenticationProviders()
        => throw new NotSupportedException("A load reads no provider list.");

    public NameIdPair[] GetPasswordResetProviders()
        => throw new NotSupportedException("A load reads no provider list.");

    public Task UpdateConfigurationAsync(Guid id, UserConfiguration config)
        => throw new NotSupportedException("A load changes no account.");

    public Task UpdatePolicyAsync(Guid id, UserPolicy policy)
        => throw new NotSupportedException("A load grants nothing.");

    public Task ClearProfileImageAsync(User user)
        => throw new NotSupportedException("A load changes no account.");
}

/// <summary>
/// A logger that keeps what it was given, so a test reads the lines an operator
/// would read.
/// </summary>
/// <typeparam name="T">The category the logger is for.</typeparam>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Lines.Add((logLevel, formatter(state, exception)));
    }
}

/// <summary>
/// The load the server starts, driven the way the server drives it.
/// </summary>
/// <remarks>
/// Nothing here has been run against a server. What is exercised is the routine
/// the server calls and the lines it writes; that the server calls it at all is
/// a registration in
/// <see cref="Jellyfin.Plugin.Invites.Startup.PluginServiceRegistrator"/> and is
/// not asserted by anything in this suite.
/// </remarks>
public class LoadOnStartTests
{
    private static readonly Guid _accountTheServerKept = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid _accountTheServerLost = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid _accountNoRecordClaims = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid _invitation = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly DateTimeOffset _started = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A start over a directory nobody holds claims it and says what it found,
    /// and the claim goes when the server stops.
    /// </summary>
    [Fact]
    public async Task AStartClaimsTheDirectoryAndAStopReleasesIt()
    {
        using var directory = new OwnedDirectory();
        var logger = new RecordingLogger<LoadOnStart>();
        using var load = ALoad(directory.Path, logger);

        await load.StartAsync(CancellationToken.None);

        var claim = Path.Combine(directory.Path, StoreLock.FileName);
        Assert.True(File.Exists(claim));
        Assert.Equal(LogLevel.Information, Assert.Single(logger.Lines).Level);

        await load.StopAsync(CancellationToken.None);

        Assert.False(File.Exists(claim));
    }

    /// <summary>
    /// The restored-backup case as an operator meets it: a warning that says
    /// how many, then the invitation and the account each disagreement is
    /// about.
    /// </summary>
    [Fact]
    public async Task AStartOverAStoreThatDisagreesNamesBothDirections()
    {
        using var directory = new OwnedDirectory();
        new InvitationStore(directory.Path)
            .Write([ARecordClaiming(_invitation, _accountTheServerKept, _accountTheServerLost)]);

        var logger = new RecordingLogger<LoadOnStart>();
        using var load = ALoad(directory.Path, logger, _accountTheServerKept, _accountNoRecordClaims);

        await load.StartAsync(CancellationToken.None);

        Assert.Equal(LogLevel.Warning, logger.Lines[0].Level);

        var claimedButAbsent = Assert.Single(
            logger.Lines,
            line => line.Message.Contains(_accountTheServerLost.ToString(), StringComparison.Ordinal));
        Assert.Equal(LogLevel.Warning, claimedButAbsent.Level);
        Assert.Contains(_invitation.ToString(), claimedButAbsent.Message, StringComparison.Ordinal);

        Assert.Single(
            logger.Lines,
            line => line.Message.Contains(_accountNoRecordClaims.ToString(), StringComparison.Ordinal));

        Assert.DoesNotContain(
            logger.Lines,
            line => line.Message.Contains(_accountTheServerKept.ToString(), StringComparison.Ordinal));
    }

    /// <summary>
    /// The shared-store case. The line names the holder and the file to remove,
    /// because whoever reads it is looking at a plugin that will not use its
    /// store.
    /// </summary>
    [Fact]
    public async Task AStartRefusedByAnotherClaimNamesTheHolderAndTheFile()
    {
        using var directory = new OwnedDirectory();
        using var held = StoreLock.Take(directory.Path, "kitchen-server", 4242, _started);

        var logger = new RecordingLogger<LoadOnStart>();
        using var load = ALoad(directory.Path, logger);

        await load.StartAsync(CancellationToken.None);

        var refusal = Assert.Single(logger.Lines);
        Assert.Equal(LogLevel.Error, refusal.Level);
        Assert.Contains("kitchen-server", refusal.Message, StringComparison.Ordinal);
        Assert.Contains(held.Path, refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A start that is refused releases nothing on the way out. The claim it
    /// found belongs to somebody else, and a stop that removed it would hand
    /// the directory to a third process while the second is still writing.
    /// </summary>
    [Fact]
    public async Task AStopAfterARefusedStartLeavesTheOtherClaimWhereItIs()
    {
        using var directory = new OwnedDirectory();
        using var held = StoreLock.Take(directory.Path, "kitchen-server", 4242, _started);

        using var load = ALoad(directory.Path, new RecordingLogger<LoadOnStart>());
        await load.StartAsync(CancellationToken.None);
        await load.StopAsync(CancellationToken.None);

        Assert.True(File.Exists(held.Path));
    }

    /// <summary>
    /// A plugin with no data directory reports that and claims nothing, rather
    /// than picking a path or refusing to let the server start.
    /// </summary>
    [Fact]
    public async Task AStartWithNoDirectoryReportsItAndClaimsNothing()
    {
        var logger = new RecordingLogger<LoadOnStart>();
        using var load = new LoadOnStart(
            new StubStoreDirectory(null),
            new RefusingUserManager(),
            new TestClock(_started),
            logger);

        await load.StartAsync(CancellationToken.None);

        Assert.Equal(LogLevel.Error, Assert.Single(logger.Lines).Level);
    }

    /// <summary>
    /// A store disagreeing about more accounts than the load writes out one by
    /// one still says how many there were. A server restored from far enough
    /// back disagrees about every account on it, and a line per account fills
    /// the log an operator is trying to read the first line of.
    /// </summary>
    [Fact]
    public async Task DisagreementsBeyondTheBoundAreCountedRatherThanNamed()
    {
        using var directory = new OwnedDirectory();
        var records = Enumerable
            .Range(0, LoadOnStart.MostNamedOneByOne + 3)
            .Select(index => ARecordClaiming(AnIdentifier(index, 'a'), AnIdentifier(index, 'b')))
            .ToArray();
        new InvitationStore(directory.Path).Write(records);

        var logger = new RecordingLogger<LoadOnStart>();
        using var load = ALoad(directory.Path, logger);

        await load.StartAsync(CancellationToken.None);

        Assert.Equal(LoadOnStart.MostNamedOneByOne + 2, logger.Lines.Count);

        var counted = logger.Lines[^1];
        Assert.Equal(LogLevel.Warning, counted.Level);
        Assert.Contains(
            (records.Length - LoadOnStart.MostNamedOneByOne).ToString(CultureInfo.InvariantCulture),
            counted.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A load with the store directory, the account list and a clock the test
    /// holds.
    /// </summary>
    /// <param name="directory">The store directory.</param>
    /// <param name="logger">Where the lines are kept.</param>
    /// <param name="accounts">The accounts the server has.</param>
    /// <returns>The load, not yet started.</returns>
    private static LoadOnStart ALoad(string directory, RecordingLogger<LoadOnStart> logger, params Guid[] accounts)
    {
        return new LoadOnStart(
            new StubStoreDirectory(directory),
            new RefusingUserManager(accounts),
            new TestClock(_started),
            logger);
    }

    /// <summary>
    /// A distinct identifier per index, so a bound can be crossed without
    /// anything sharing a value.
    /// </summary>
    /// <param name="index">Which one.</param>
    /// <param name="direction">Which of the two per index.</param>
    /// <returns>The identifier.</returns>
    private static Guid AnIdentifier(int index, char direction)
    {
        return Guid.Parse(string.Format(
            CultureInfo.InvariantCulture,
            "{0:D8}-0000-4000-8000-00000000000{1}",
            index + 1,
            direction == 'a' ? "1" : "2"));
    }

    /// <summary>
    /// One invitation claiming the accounts it is handed.
    /// </summary>
    /// <param name="id">The invitation identifier.</param>
    /// <param name="accounts">The accounts the record says it created.</param>
    /// <returns>The record.</returns>
    private static Invitation ARecordClaiming(Guid id, params Guid[] accounts)
    {
        return new Invitation(
            id: id,
            codeHash: ImmutableArray.Create<byte>(0x01, 0x02, 0x03),
            mintedBy: Guid.Parse("55555555-5555-4555-8555-555555555555"),
            mintedAt: new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            expiresAt: new DateTimeOffset(2026, 2, 2, 3, 4, 5, TimeSpan.Zero),
            usesGranted: 4,
            usesRemaining: 1,
            revokedAt: null,
            templateLabel: "Household",
            accountsProduced: [.. accounts]);
    }
}
