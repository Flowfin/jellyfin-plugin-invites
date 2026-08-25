using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Configuration;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Server;
using Jellyfin.Plugin.Invites.Startup;
using Jellyfin.Plugin.Invites.Storage;
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
/// The configured public address, as the test sets it, standing in for the
/// setting an operator writes on the plugin's own configuration page.
/// </summary>
/// <remarks>
/// It takes a string and nothing else. There is no member here a request could
/// be handed to, which is the shape #50 is about rather than a spelling any
/// greppable rule reads.
/// </remarks>
internal sealed class StubPublicAddress : IPublicAddress
{
    public StubPublicAddress(string? publicBaseUrl)
    {
        PublicBaseUrl = publicBaseUrl;
    }

    public string? PublicBaseUrl { get; }
}

/// <summary>
/// An account list the test holds, standing in for the server's own.
/// </summary>
internal sealed class StubServerAccounts : IServerAccounts
{
    private readonly Guid[]? _accounts;

    public StubServerAccounts(Guid[]? accounts)
    {
        _accounts = accounts;
    }

    public IReadOnlyCollection<Guid>? Identifiers => _accounts;
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
            OnTheDeclaredLine(),
            new StubStoreDirectory(null),
            new StubPublicAddress(null),
            new StubServerAccounts([]),
            new TestClock(_started),
            logger);

        await load.StartAsync(CancellationToken.None);

        Assert.Equal(LogLevel.Error, Assert.Single(logger.Lines).Level);
    }

    /// <summary>
    /// A server that does not answer for its accounts is claimed and not
    /// compared. Comparing against nothing would report every account the store
    /// claims as one the server has lost, which is the loudest possible way to
    /// say that a member could not be found.
    /// </summary>
    [Fact]
    public async Task AServerThatDoesNotAnswerForItsAccountsIsClaimedAndNotCompared()
    {
        using var directory = new OwnedDirectory();
        new InvitationStore(directory.Path)
            .Write([ARecordClaiming(_invitation, _accountTheServerLost)]);

        var logger = new RecordingLogger<LoadOnStart>();
        using var load = new LoadOnStart(
            OnTheDeclaredLine(),
            new StubStoreDirectory(directory.Path),
            new StubPublicAddress(null),
            new StubServerAccounts(null),
            new TestClock(_started),
            logger);

        await load.StartAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(directory.Path, StoreLock.FileName)));

        var line = Assert.Single(logger.Lines);
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.DoesNotContain(_accountTheServerLost.ToString(), line.Message, StringComparison.Ordinal);
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
    /// An address that cannot be used is met when the server starts, and the
    /// line names the setting rather than the value somebody typed.
    /// </summary>
    /// <param name="configured">The address an operator wrote.</param>
    [Theory]
    [InlineData("media.example.org")]
    [InlineData("/redeem")]
    [InlineData("ftp://media.example.org")]
    [InlineData("https://media.example.org/?next=1")]
    [InlineData("https://media.example.org/#top")]
    public async Task AnAddressThatCannotBeUsedIsNamedWhenTheServerStarts(string configured)
    {
        using var directory = new OwnedDirectory();
        var logger = new RecordingLogger<LoadOnStart>();
        using var load = ALoadConfiguredWith(directory.Path, logger, configured);

        await load.StartAsync(CancellationToken.None);

        var named = Assert.Single(
            logger.Lines,
            line => line.Message.Contains("PublicBaseUrl", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Error, named.Level);
    }

    /// <summary>
    /// The line carries no part of what was configured. A server setting is not
    /// a row in the inventory docs/logging.md holds every logged value to, and
    /// the refusal that quotes what was typed is written for the operator who
    /// asked for something rather than for a log.
    /// </summary>
    [Fact]
    public async Task TheLineDoesNotCarryWhatWasConfigured()
    {
        using var directory = new OwnedDirectory();
        var logger = new RecordingLogger<LoadOnStart>();
        using var load = ALoadConfiguredWith(directory.Path, logger, "ftp://kitchen.example.net");

        await load.StartAsync(CancellationToken.None);

        Assert.DoesNotContain(
            logger.Lines,
            line => line.Message.Contains("kitchen.example.net", StringComparison.Ordinal));
    }

    /// <summary>
    /// A fresh install is silent. No address is the decided value for a server
    /// that never opened the configuration page, so an error there would be an
    /// error for every install that has not been configured yet.
    /// </summary>
    /// <param name="configured">The address, as a fresh install leaves it.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AFreshInstallIsNotReportedAsAFault(string? configured)
    {
        using var directory = new OwnedDirectory();
        var logger = new RecordingLogger<LoadOnStart>();
        using var load = ALoadConfiguredWith(directory.Path, logger, configured);

        await load.StartAsync(CancellationToken.None);

        Assert.DoesNotContain(
            logger.Lines,
            line => line.Message.Contains("PublicBaseUrl", StringComparison.Ordinal));
    }

    /// <summary>
    /// An address that can be used is not remarked on.
    /// </summary>
    /// <param name="configured">The address an operator wrote.</param>
    [Theory]
    [InlineData("https://media.example.org")]
    [InlineData("https://media.example.org/")]
    [InlineData("http://media.example.org:8096/jellyfin")]
    public async Task AnAddressThatCanBeUsedIsNotRemarkedOn(string configured)
    {
        using var directory = new OwnedDirectory();
        var logger = new RecordingLogger<LoadOnStart>();
        using var load = ALoadConfiguredWith(directory.Path, logger, configured);

        await load.StartAsync(CancellationToken.None);

        Assert.DoesNotContain(
            logger.Lines,
            line => line.Message.Contains("PublicBaseUrl", StringComparison.Ordinal));
    }

    /// <summary>
    /// A start on a server that is not on the line this plugin was built for
    /// claims nothing and reads nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is #97's third clause, that no partial operation follows a
    /// mismatch, and the load is the only thing in this plugin that acts
    /// without a request. The claim is the part that matters: it is taken for
    /// the lifetime of the process, so a plugin that got this far on the wrong
    /// server would be holding a directory against a second server that could
    /// use it.
    /// </para>
    /// <para>
    /// The store directory handed in is a real one the test owns, so an absent
    /// claim file is the load having declined rather than the load having had
    /// nowhere to write.
    /// </para>
    /// </remarks>
    /// <returns>A task.</returns>
    [Fact]
    public async Task AStartOnAnotherServerLineClaimsNothingAndReadsNothing()
    {
        using var directory = new OwnedDirectory();
        var logger = new RecordingLogger<LoadOnStart>();
        using var load = new LoadOnStart(
            new ServerLineGate("42.7", new StubRunningServer(new Version(9, 3, 1))),
            new StubStoreDirectory(directory.Path),
            new StubPublicAddress(null),
            new StubServerAccounts([]),
            new TestClock(_started),
            logger);

        await load.StartAsync(CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(directory.Path, StoreLock.FileName)));
        var line = Assert.Single(logger.Lines);
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.Contains("42.7", line.Message, StringComparison.Ordinal);
        Assert.Contains("9.3.1", line.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A server on the declared line, for every load in this class that is not
    /// about the line itself.
    /// </summary>
    /// <returns>The gate, agreeing.</returns>
    private static ServerLineGate OnTheDeclaredLine()
    {
        return new ServerLineGate("42.7", new StubRunningServer(new Version(42, 7, 3)));
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
            OnTheDeclaredLine(),
            new StubStoreDirectory(directory),
            new StubPublicAddress(null),
            new StubServerAccounts(accounts),
            new TestClock(_started),
            logger);
    }

    /// <summary>
    /// A load whose configured public address is the one the test names, over a
    /// store directory nobody holds.
    /// </summary>
    /// <param name="directory">The store directory.</param>
    /// <param name="logger">Where the lines are kept.</param>
    /// <param name="publicBaseUrl">The configured address.</param>
    /// <returns>The load, not yet started.</returns>
    private static LoadOnStart ALoadConfiguredWith(
        string directory,
        RecordingLogger<LoadOnStart> logger,
        string? publicBaseUrl)
    {
        return new LoadOnStart(
            OnTheDeclaredLine(),
            new StubStoreDirectory(directory),
            new StubPublicAddress(publicBaseUrl),
            new StubServerAccounts([]),
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
            revokedBy: null,
            templateLabel: "Household",
            accountsProduced: [.. accounts]);
    }
}
