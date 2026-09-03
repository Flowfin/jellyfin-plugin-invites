using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Maintenance;
using Jellyfin.Plugin.Invites.Storage;
using MediaBrowser.Model.Tasks;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The sweep that applies the retention rule, and the scheduled task that asks
/// for it.
/// </summary>
/// <remarks>
/// <para>
/// #59 asks for four things this file can hold and one it cannot. It holds that
/// the sweep deletes only what the rule allows, that it marks nothing, that it
/// writes through the one routine that takes the store monitor, and that the task
/// carries a schedule. What it does not hold is the last clause: a test running
/// the sweep concurrently with a redemption. Nothing in this tree consumes a use,
/// so a redemption that writes does not exist to run against, and a test naming
/// two operations that both only read would look like that clause and prove none
/// of it.
/// </para>
/// <para>
/// The clock is moved rather than the records being written with fabricated
/// instants wherever that is possible, because the thing under test is a
/// comparison against a clock reading and a fixture that hard-codes both sides of
/// it can agree with a routine that is wrong.
/// </para>
/// </remarks>
public class RetentionSweepTests
{
    private const string Configured = "https://media.example.org";

    private static readonly DateTimeOffset _minted = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan _validity = TimeSpan.FromDays(7);
    private static readonly Guid _operator = Guid.Parse("aaaabbbb-cccc-dddd-eeee-ffff00001111");

    /// <summary>
    /// A record whose retention period has run out goes, and the store on the
    /// disk no longer holds it. Minted, left to expire, and swept once the period
    /// has passed.
    /// </summary>
    [Fact]
    public void ARecordPastItsRetentionPeriodIsRemoved()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var operations = Operations(directory, clock);

        var minted = operations.Mint(_operator, "Household", _validity, uses: 1);

        clock.MoveTo(_minted + _validity + Retention.RecordRetention);

        var removed = operations.Sweep();

        Assert.Equal(new[] { minted.Invitation.Id }, removed);
        Assert.Empty(operations.All());
        Assert.Empty(new InvitationStore(directory.Path).Read().Invitations);
    }

    /// <summary>
    /// A live invitation is not removed however long the sweep runs for. This is
    /// the clause of #59 that costs the most to get wrong: a record removed here
    /// is an invitation somebody is holding a working link to, which stops working
    /// with no trace of why.
    /// </summary>
    [Fact]
    public void ALiveInvitationIsNeverRemoved()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var operations = Operations(directory, clock);

        operations.Mint(_operator, "Household", TimeSpan.FromDays(90), uses: 1);

        clock.MoveTo(_minted.AddDays(89));

        Assert.Empty(operations.Sweep());
        Assert.Single(operations.All());
    }

    /// <summary>
    /// An expired record inside its retention period stays, which is the entry
    /// docs/limits.md holds as expiry not being deletion read from the sweep's
    /// side. A record that stops working is not a record that disappears.
    /// </summary>
    [Fact]
    public void AnExpiredRecordInsideItsRetentionPeriodStays()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var operations = Operations(directory, clock);

        operations.Mint(_operator, "Household", _validity, uses: 1);

        clock.MoveTo(_minted + _validity + Retention.RecordRetention - TimeSpan.FromDays(1));

        Assert.Empty(operations.Sweep());
        Assert.Single(operations.All());
    }

    /// <summary>
    /// The sweep takes the aged record and leaves the others exactly as they
    /// were, field for field. This is #59's clause that the task never marks
    /// anything expired: a survivor whose bytes moved would be a record the sweep
    /// had an opinion about.
    /// </summary>
    [Fact]
    public void SurvivingRecordsComeThroughUnchanged()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var operations = Operations(directory, clock);

        var goes = operations.Mint(_operator, "Old", _validity, uses: 1);
        var stays = operations.Mint(_operator, "Current", TimeSpan.FromDays(90), uses: 3);

        var before = operations.One(stays.Invitation.Id);
        Assert.NotNull(before);

        clock.MoveTo(_minted + _validity + Retention.RecordRetention);

        Assert.Equal(new[] { goes.Invitation.Id }, operations.Sweep());

        var after = operations.One(stays.Invitation.Id);
        Assert.NotNull(after);
        Assert.Equal(before!.ExpiresAt, after!.ExpiresAt);
        Assert.Equal(before.UsesGranted, after.UsesGranted);
        Assert.Equal(before.UsesRemaining, after.UsesRemaining);
        Assert.Equal(before.RevokedAt, after.RevokedAt);
        Assert.Equal(before.TemplateLabel, after.TemplateLabel);
        Assert.Equal(before.MintedAt, after.MintedAt);
        Assert.Equal(before.CodeHash.ToArray(), after.CodeHash.ToArray());
        Assert.Equal(before.AccountsProduced.ToArray(), after.AccountsProduced.ToArray());
    }

    /// <summary>
    /// A sweep with nothing to remove does not write the file at all. A task that
    /// rewrote it every night would move the bytes an operator backs up daily for
    /// no reason, and would take away the ability to say that the file changes
    /// only when something happened.
    /// </summary>
    /// <remarks>
    /// The assertion is on the file's own write time rather than on its contents,
    /// and that is the whole point of writing it this way. The serialisation is
    /// deterministic, so a sweep that rewrote the same records would produce
    /// byte-identical output and a content comparison would pass over exactly the
    /// mistake this is about. The stamp is set to a fixed instant in the past
    /// first, so the check is against a value the test chose rather than against
    /// whatever the clock did during the run.
    /// </remarks>
    [Fact]
    public void ASweepWithNothingToRemoveDoesNotWriteTheFile()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var operations = Operations(directory, clock);

        operations.Mint(_operator, "Household", _validity, uses: 1);

        var path = new InvitationStore(directory.Path).Path;
        var before = File.ReadAllBytes(path);

        var stamped = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, stamped);

        clock.MoveTo(_minted.AddDays(10));

        Assert.Empty(operations.Sweep());
        Assert.Equal(stamped, File.GetLastWriteTimeUtc(path));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    /// <summary>
    /// The record the sweep removes takes its claim on an account with it and the
    /// account is not reached at all. The sweep is handed no seam over the
    /// server's accounts, which is the strongest form of this available here: it
    /// is not that the routine declines to delete an account, it is that it has
    /// nothing to delete one with.
    /// </summary>
    [Fact]
    public void TheSweepIsHandedNoWayToReachAnAccount()
    {
        var reached = typeof(InvitationOperations)
            .GetMethod(nameof(InvitationOperations.Sweep))!
            .GetParameters();

        Assert.Empty(reached);

        var handed = typeof(RetentionSweep)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "ILogger`1", "InvitationOperations" }, handed);
    }

    /// <summary>
    /// The task runs the sweep and says what it did. A run over a store with an
    /// aged record removes it and names the count.
    /// </summary>
    [Fact]
    public async Task TheTaskRunsTheSweep()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var operations = Operations(directory, clock);
        var logger = new RecordingLogger<RetentionSweep>();
        var task = new RetentionSweep(operations, logger);

        operations.Mint(_operator, "Household", _validity, uses: 1);
        clock.MoveTo(_minted + _validity + Retention.RecordRetention);

        var progress = new RecordingProgress();
        await task.ExecuteAsync(progress, CancellationToken.None);

        Assert.Empty(operations.All());
        Assert.Contains(logger.Lines, line => line.Message.Contains("removed 1 invitation record", StringComparison.Ordinal));
        Assert.Equal(new double[] { 0, 100 }, progress.Reported);
    }

    /// <summary>
    /// A server with no data directory is reported and not thrown at. The plugin
    /// already says so at startup, and a nightly task raising the same exception
    /// would say it again every night in a louder place.
    /// </summary>
    [Fact]
    public async Task ARunWithNoStoreDirectorySaysSoAndStops()
    {
        var operations = new InvitationOperations(
            new StubStoreDirectory(null),
            new TestClock(_minted),
            new StubPublicAddress(Configured), TestTemplates.AsConfigured);

        var logger = new RecordingLogger<RetentionSweep>();
        var progress = new RecordingProgress();

        await new RetentionSweep(operations, logger).ExecuteAsync(progress, CancellationToken.None);

        Assert.Contains(logger.Lines, line => line.Message.Contains("no data directory", StringComparison.Ordinal));
        Assert.Equal(new double[] { 0, 100 }, progress.Reported);
    }

    /// <summary>
    /// The task declares a daily trigger and an identifier the server stores its
    /// schedule under. The identifier is asserted as a literal on purpose: it is
    /// what an operator's own schedule is filed against, so a rename of the class
    /// must not silently move it.
    /// </summary>
    [Fact]
    public void TheTaskDeclaresADailyScheduleUnderAFixedIdentifier()
    {
        var task = new RetentionSweep(
            new InvitationOperations(new StubStoreDirectory(null), new TestClock(_minted), new StubPublicAddress(Configured), TestTemplates.AsConfigured),
            new RecordingLogger<RetentionSweep>());

        Assert.Equal("InvitesRetentionSweep", task.Key);
        Assert.NotEmpty(task.Name);
        Assert.NotEmpty(task.Category);
        Assert.Contains("90", task.Description, StringComparison.Ordinal);

        var triggers = task.GetDefaultTriggers().ToArray();

        Assert.Single(triggers);
        Assert.Equal(TaskTriggerInfoType.DailyTrigger, triggers[0].Type);
        Assert.NotNull(triggers[0].TimeOfDayTicks);
    }

    /// <summary>
    /// The task is what the server is offered, so a registration that dropped it
    /// would leave a sweep nothing ever calls. Read off the interface list rather
    /// than off the registrator, which is <see cref="RouteInventoryTests"/>'
    /// question about a different surface.
    /// </summary>
    [Fact]
    public void TheTaskIsAScheduledTaskTheServerCanBeHandedAndOneItCanConfigure()
    {
        Assert.Contains(typeof(IScheduledTask), typeof(RetentionSweep).GetInterfaces());
        Assert.Contains(typeof(IConfigurableScheduledTask), typeof(RetentionSweep).GetInterfaces());
    }

    /// <summary>
    /// One type in the plugin removes a record, and it is the routine that holds
    /// the store monitor. A second write path is how the sweep would come to race
    /// a mint, and it arrives without touching anything this file asserts about
    /// the sweep itself.
    /// </summary>
    /// <remarks>
    /// The store is reached by construction, so the walk is over constructor
    /// calls the compiler recorded rather than over source text: a type that
    /// opened the store for itself is a type that has an
    /// <see cref="InvitationStore"/> in its own body, whatever the file is called.
    /// </remarks>
    [Fact]
    public void OnlyTheRoutineHoldingTheMonitorConstructsTheStore()
    {
        var constructing = typeof(Jellyfin.Plugin.Invites.Plugin).Assembly
            .GetTypes()
            .Where(type => ConstructsTheStore(type))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                typeof(InvitationOperations).FullName,
                typeof(InvitationStore).FullName,
                typeof(StoreLoad).FullName,
            },
            constructing);
    }

    /// <summary>
    /// Whether a type's own code contains a construction of the store.
    /// </summary>
    /// <param name="type">The type to read.</param>
    /// <returns>True where its method bodies name the store's constructor.</returns>
    private static bool ConstructsTheStore(Type type)
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var tokens = typeof(InvitationStore)
            .GetConstructors(Any)
            .Select(constructor => constructor.MetadataToken)
            .ToArray();

        var module = typeof(InvitationStore).Module;

        foreach (var method in type.GetMethods(Any).Cast<MethodBase>().Concat(type.GetConstructors(Any)))
        {
            var body = method.GetMethodBody();
            if (body is null)
            {
                continue;
            }

            var il = body.GetILAsByteArray();
            if (il is null)
            {
                continue;
            }

            for (var i = 0; i + 4 < il.Length; i++)
            {
                // newobj, followed by the metadata token of the constructor.
                if (il[i] == 0x73 && tokens.Contains(BitConverter.ToInt32(il, i + 1)) && module == type.Module)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static InvitationOperations Operations(OwnedDirectory directory, TestClock clock)
    {
        return new InvitationOperations(
            new StubStoreDirectory(directory.Path),
            clock,
            new StubPublicAddress(Configured), TestTemplates.AsConfigured);
    }
}

/// <summary>
/// A progress sink that keeps what it was told, so a test reads what the server
/// would have been shown.
/// </summary>
internal sealed class RecordingProgress : IProgress<double>
{
    private readonly System.Collections.Generic.List<double> _reported = new();

    public System.Collections.Generic.IReadOnlyList<double> Reported => _reported;

    public void Report(double value) => _reported.Add(value);
}
