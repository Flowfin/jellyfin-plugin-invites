using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Configuration;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Jellyfin.Plugin.Invites.Server;
using Jellyfin.Plugin.Invites.Startup;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The three configured numbers, read as an operator wrote them.
/// </summary>
/// <remarks>
/// A record rather than three arguments, so a test that is about one of them
/// says which one and leaves the other two at the value a fresh install carries.
/// </remarks>
internal sealed class StubConfiguredNumbers : IConfiguredNumbers
{
    /// <inheritdoc />
    public int? RecordRetentionDays { get; set; } = 90;

    /// <inheritdoc />
    public int? RedemptionAttemptsPerAddressInAnHour { get; set; } = AttemptLimiter.PerAddressCeiling;

    /// <inheritdoc />
    public int? RedemptionAttemptsPerSecond { get; set; } = AttemptLimiter.GlobalCeiling;
}

/// <summary>
/// <para>
/// The rules the three numbers an operator may set are judged by, one test per
/// rule, which is the last clause of #86.
/// </para>
/// <para>
/// Every rule is driven from both sides of its boundary rather than from one.
/// A range check written with the wrong comparison passes every test that only
/// asks whether a plainly bad value is refused, and the value an operator
/// actually types is the one at the edge.
/// </para>
/// <para>
/// What this file does not decide is what the numbers are. The maxima are named
/// rather than restated, so a test here cannot pass because it copied a number
/// out of the source it is judging, and the two drift guards below are the only
/// place a literal appears at all.
/// </para>
/// </remarks>
public class NumberSettingsTests
{
    private static readonly DateTimeOffset _now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A retention period below the floor is refused, and the sentence names the
    /// setting. Zero is the value somebody types meaning "keep nothing", which
    /// is deletion the moment a record stops being usable rather than a stricter
    /// policy.
    /// </summary>
    /// <param name="days">The configured period.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void ARetentionPeriodBelowTheFloorIsRefused(int days)
    {
        var why = NumberSettings.WhyRetentionRefused(days);

        Assert.NotNull(why);
        Assert.Contains(NumberSettings.RetentionSettingName, why, StringComparison.Ordinal);
        Assert.Contains("below", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// And one above the ceiling is refused too, which is the direction that
    /// turns the trace into the indefinite register docs/personal-data.md argues
    /// against.
    /// </summary>
    /// <param name="days">The configured period.</param>
    [Theory]
    [InlineData(NumberSettings.MostRetentionDays + 1)]
    [InlineData(int.MaxValue)]
    public void ARetentionPeriodAboveTheCeilingIsRefused(int days)
    {
        var why = NumberSettings.WhyRetentionRefused(days);

        Assert.NotNull(why);
        Assert.Contains(NumberSettings.RetentionSettingName, why, StringComparison.Ordinal);
        Assert.Contains("above", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// Both ends of the range are inside it. This is the half a range check
    /// written with the wrong comparison fails, and the half every other test
    /// here would pass without.
    /// </summary>
    /// <param name="days">The configured period.</param>
    [Theory]
    [InlineData(NumberSettings.FewestRetentionDays)]
    [InlineData(NumberSettings.MostRetentionDays)]
    [InlineData(90)]
    public void ARetentionPeriodInsideItsRangeIsAccepted(int days)
    {
        Assert.Null(NumberSettings.WhyRetentionRefused(days));
    }

    /// <summary>
    /// The per-address rate limit is refused below one and above the compiled
    /// ceiling, and accepted at each end. Zero is the case worth naming: it
    /// closes the redemption route with a number an operator meant as a
    /// restraint.
    /// </summary>
    /// <param name="attempts">The configured limit.</param>
    /// <param name="refused">Whether the rule refuses it.</param>
    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(AttemptLimiter.PerAddressCeiling + 1, true)]
    [InlineData(NumberSettings.FewestAttempts, false)]
    [InlineData(AttemptLimiter.PerAddressCeiling, false)]
    public void ThePerAddressLimitIsHeldToItsRange(int attempts, bool refused)
    {
        var why = NumberSettings.WhyPerAddressRefused(attempts);

        Assert.Equal(refused, why is not null);
        if (refused)
        {
            Assert.Contains(NumberSettings.PerAddressSettingName, why!, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The global rate limit, the same way. Its ceiling is the number the
    /// throttled rows of docs/code-entropy.md are computed at, so a value above
    /// it would move the ground that argument stands on.
    /// </summary>
    /// <param name="attempts">The configured limit.</param>
    /// <param name="refused">Whether the rule refuses it.</param>
    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(AttemptLimiter.GlobalCeiling + 1, true)]
    [InlineData(NumberSettings.FewestAttempts, false)]
    [InlineData(AttemptLimiter.GlobalCeiling, false)]
    public void ThePerSecondLimitIsHeldToItsRange(int attempts, bool refused)
    {
        var why = NumberSettings.WhyPerSecondRefused(attempts);

        Assert.Equal(refused, why is not null);
        if (refused)
        {
            Assert.Contains(NumberSettings.PerSecondSettingName, why!, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Nothing configured is not a fault. It is the state before the server has
    /// constructed the plugin, and an operator who has not opened the
    /// configuration page is not owed an error for not having opened it.
    /// </summary>
    [Fact]
    public void NothingConfiguredIsNoFault()
    {
        Assert.Null(NumberSettings.WhyRefused(null));
        Assert.Null(NumberSettings.WhyRefused(new StubConfiguredNumbers
        {
            RecordRetentionDays = null,
            RedemptionAttemptsPerAddressInAnHour = null,
            RedemptionAttemptsPerSecond = null,
        }));
    }

    /// <summary>
    /// And nothing configured resolves to what the source compiles rather than
    /// to nothing. A server before its configuration is loaded runs on the
    /// default period and on the two compiled ceilings.
    /// </summary>
    [Fact]
    public void NothingConfiguredResolvesToWhatTheSourceCompiles()
    {
        Assert.Equal(Retention.RecordRetention, NumberSettings.RetentionPeriod(null));
        Assert.Equal(AttemptLimiter.PerAddressCeiling, NumberSettings.AttemptsPerAddressInAnHour(null));
        Assert.Equal(AttemptLimiter.GlobalCeiling, NumberSettings.AttemptsPerSecond(null));
    }

    /// <summary>
    /// The first fault in declaration order is the one named, and the second is
    /// met on the next load. Two faults reported at once would let an operator
    /// repair the one they read and meet the other as a surprise.
    /// </summary>
    [Fact]
    public void TheFirstFaultIsTheOneNamed()
    {
        var configured = new StubConfiguredNumbers
        {
            RecordRetentionDays = 0,
            RedemptionAttemptsPerAddressInAnHour = 0,
            RedemptionAttemptsPerSecond = 0,
        };

        var why = NumberSettings.WhyRefused(configured);

        Assert.NotNull(why);
        Assert.Contains(NumberSettings.RetentionSettingName, why, StringComparison.Ordinal);
        Assert.DoesNotContain(NumberSettings.PerAddressSettingName, why, StringComparison.Ordinal);

        configured.RecordRetentionDays = 90;
        Assert.Contains(
            NumberSettings.PerAddressSettingName,
            NumberSettings.WhyRefused(configured)!,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A refused number is refused where it would be used, and nothing is put in
    /// its place. This is the clause of #86 that the whole arrangement exists
    /// for: a silent fallback on a bound is the bound gone.
    /// </summary>
    [Fact]
    public void ARefusedNumberIsNeverSubstitutedFor()
    {
        var configured = new StubConfiguredNumbers { RecordRetentionDays = 0 };
        var refused = Assert.Throws<ConfiguredNumbersRefusedException>(
            () => NumberSettings.RetentionPeriod(configured));

        Assert.Contains(NumberSettings.RetentionSettingName, refused.Message, StringComparison.Ordinal);

        Assert.Throws<ConfiguredNumbersRefusedException>(
            () => NumberSettings.AttemptsPerAddressInAnHour(
                new StubConfiguredNumbers { RedemptionAttemptsPerAddressInAnHour = AttemptLimiter.PerAddressCeiling + 1 }));
        Assert.Throws<ConfiguredNumbersRefusedException>(
            () => NumberSettings.AttemptsPerSecond(
                new StubConfiguredNumbers { RedemptionAttemptsPerSecond = 0 }));
    }

    /// <summary>
    /// A value inside its range is the value that is used, rather than the
    /// compiled one. Without this every test above passes over a routine that
    /// ignores the setting entirely.
    /// </summary>
    [Fact]
    public void AnAcceptedNumberIsTheOneThatIsUsed()
    {
        var configured = new StubConfiguredNumbers
        {
            RecordRetentionDays = 7,
            RedemptionAttemptsPerAddressInAnHour = 2,
            RedemptionAttemptsPerSecond = 1,
        };

        Assert.Equal(TimeSpan.FromDays(7), NumberSettings.RetentionPeriod(configured));
        Assert.Equal(2, NumberSettings.AttemptsPerAddressInAnHour(configured));
        Assert.Equal(1, NumberSettings.AttemptsPerSecond(configured));
    }

    /// <summary>
    /// The refusal names the setting, the range and the direction, and never the
    /// value that was typed. It is written to a log when the plugin loads, and
    /// docs/logging.md admits a value there only where it is a row in
    /// docs/personal-data.md, which a server setting is not.
    /// </summary>
    [Fact]
    public void TheRefusalDoesNotCarryTheValueThatWasTyped()
    {
        var why = NumberSettings.WhyRetentionRefused(4242);

        Assert.NotNull(why);
        Assert.DoesNotContain("4242", why, StringComparison.Ordinal);
        Assert.Contains(
            NumberSettings.MostRetentionDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
            why,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The two rate limits may only be lowered, and the maximum each may take is
    /// the constant the limiter compiles rather than a second copy of it. A
    /// number restated here would let the two drift and leave the pages that
    /// reason about them describing a server nobody runs.
    /// </summary>
    [Fact]
    public void TheMaximaAreTheConstantsTheLimiterCompiles()
    {
        Assert.Equal(AttemptLimiter.PerAddressCeiling, NumberSettings.MostAttemptsPerAddressInAnHour);
        Assert.Equal(AttemptLimiter.GlobalCeiling, NumberSettings.MostAttemptsPerSecond);
    }

    /// <summary>
    /// And what a fresh install carries is what those constants and the default
    /// retention period say. The configuration type has to write literals,
    /// because the check that holds docs/configuration.md to it reads the
    /// initialiser as text, so this is where the two are compared.
    /// </summary>
    [Fact]
    public void TheInitialisersAgreeWithWhatTheSourceCompiles()
    {
        var fresh = new PluginConfiguration();

        Assert.Equal(Retention.RecordRetention.TotalDays, fresh.RecordRetentionDays);
        Assert.Equal(AttemptLimiter.PerAddressCeiling, fresh.RedemptionAttemptsPerAddressInAnHour);
        Assert.Equal(AttemptLimiter.GlobalCeiling, fresh.RedemptionAttemptsPerSecond);
    }

    /// <summary>
    /// A configured limit lower than the compiled one is what the limiter acts
    /// on. One attempt a second, and the second attempt inside that second is
    /// refused.
    /// </summary>
    [Fact]
    public void TheLimiterActsOnALoweredSetting()
    {
        var clock = new TestClock(_now);
        var limiter = new AttemptLimiter(clock, new StubConfiguredNumbers { RedemptionAttemptsPerSecond = 1 });

        Assert.True(limiter.MayJudge("198.51.100.7"));
        Assert.False(limiter.MayJudge("198.51.100.7"));

        clock.Advance(AttemptLimiter.GlobalWindow);

        Assert.True(limiter.MayJudge("198.51.100.7"));
    }

    /// <summary>
    /// An out-of-range limit refuses every attempt rather than falling back to
    /// the compiled ceiling. Nothing is counted while it stands, so the refusal
    /// is not itself a way of spending anybody's allowance.
    /// </summary>
    [Fact]
    public void TheLimiterRefusesRatherThanFallingBack()
    {
        var clock = new TestClock(_now);
        var configured = new StubConfiguredNumbers { RedemptionAttemptsPerSecond = AttemptLimiter.GlobalCeiling + 1 };
        var limiter = new AttemptLimiter(clock, configured);

        Assert.False(limiter.MayJudge("198.51.100.7"));
        Assert.Equal(0, limiter.AddressesHeld);

        configured.RedemptionAttemptsPerSecond = AttemptLimiter.GlobalCeiling;

        Assert.True(limiter.MayJudge("198.51.100.7"));
    }

    /// <summary>
    /// The sweep removes on the configured period rather than on the compiled
    /// one. A record past seven days goes on a server that asked for seven and
    /// stays on one that asked for the default.
    /// </summary>
    [Fact]
    public void TheSweepRemovesOnTheConfiguredPeriod()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var configured = new StubConfiguredNumbers { RecordRetentionDays = 7 };
        var operations = Operations(directory, clock, configured);

        var validity = TimeSpan.FromDays(1);
        var minted = operations.Mint(Guid.NewGuid(), "Household", validity, uses: 1);

        clock.MoveTo(_now + validity + TimeSpan.FromDays(7));

        Assert.Equal(new[] { minted.Invitation.Id }, operations.Sweep());
    }

    /// <summary>
    /// And an out-of-range period removes nothing at all. The period is read
    /// before the store is opened, so a sweep that meets one has read nothing
    /// and written nothing, which is the direction to fail in: a run that
    /// deleted on the compiled default instead would be removing records on a
    /// period the operator did not ask for.
    /// </summary>
    [Fact]
    public void TheSweepRefusesRatherThanRemovingOnTheDefault()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var configured = new StubConfiguredNumbers { RecordRetentionDays = 0 };
        var operations = Operations(directory, clock, configured);

        var validity = TimeSpan.FromDays(1);
        var minted = operations.Mint(Guid.NewGuid(), "Household", validity, uses: 1);

        clock.MoveTo(_now + validity + Retention.RecordRetention + TimeSpan.FromDays(1));

        var refused = Assert.Throws<ConfiguredNumbersRefusedException>(() => operations.Sweep());
        Assert.Contains(NumberSettings.RetentionSettingName, refused.Message, StringComparison.Ordinal);

        Assert.Contains(operations.All(), record => record.Id == minted.Invitation.Id);
    }

    /// <summary>
    /// The plugin reads the three when the server starts and names the setting
    /// that cannot be used, which is #86's clause that validation runs on load
    /// and not only where the value is used.
    /// </summary>
    /// <returns>The started load.</returns>
    [Fact]
    public async Task AnOutOfRangeNumberIsNamedWhenThePluginLoads()
    {
        using var directory = new OwnedDirectory();
        var logger = new RecordingLogger<LoadOnStart>();

        using var load = new LoadOnStart(
            OnTheDeclaredLine(),
            new StubStoreDirectory(directory.Path),
            new StubPublicAddress(null),
            new StubConfiguredTemplates([]),
            new StubServerAccounts([]),
            new TestClock(_now),
            logger,
            new StubConfiguredNumbers { RedemptionAttemptsPerSecond = 0 });

        await load.StartAsync(CancellationToken.None);

        Assert.Contains(
            logger.Lines,
            line => line.Level == LogLevel.Error
                && line.Message.Contains(NumberSettings.PerSecondSettingName, StringComparison.Ordinal));
    }

    /// <summary>
    /// And a load whose three numbers are all inside their ranges says nothing
    /// about them. A line on every start is a line an operator stops reading.
    /// </summary>
    /// <returns>The started load.</returns>
    [Fact]
    public async Task NothingIsWrittenWhereEveryNumberIsInsideItsRange()
    {
        using var directory = new OwnedDirectory();
        var logger = new RecordingLogger<LoadOnStart>();

        using var load = new LoadOnStart(
            OnTheDeclaredLine(),
            new StubStoreDirectory(directory.Path),
            new StubPublicAddress(null),
            new StubConfiguredTemplates([]),
            new StubServerAccounts([]),
            new TestClock(_now),
            logger,
            new StubConfiguredNumbers());

        await load.StartAsync(CancellationToken.None);

        Assert.DoesNotContain(
            logger.Lines,
            line => line.Message.Contains(NumberSettings.RetentionSettingName, StringComparison.Ordinal)
                || line.Message.Contains(NumberSettings.PerAddressSettingName, StringComparison.Ordinal)
                || line.Message.Contains(NumberSettings.PerSecondSettingName, StringComparison.Ordinal));
    }

    /// <summary>
    /// A gate that agrees, so the load reaches the settings rather than stopping
    /// at the server line.
    /// </summary>
    /// <returns>The gate, agreeing.</returns>
    private static ServerLineGate OnTheDeclaredLine()
    {
        return new ServerLineGate("42.7", new StubRunningServer(new Version(42, 7, 3)));
    }

    private static InvitationOperations Operations(OwnedDirectory directory, TestClock clock, IConfiguredNumbers numbers)
    {
        return new InvitationOperations(
            new StubStoreDirectory(directory.Path),
            clock,
            new StubPublicAddress("https://media.example.org"),
            TestTemplates.AsConfigured,
            numbers);
    }
}
