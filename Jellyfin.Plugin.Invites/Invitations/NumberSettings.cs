using System;
using System.Globalization;
using Jellyfin.Plugin.Invites.Configuration;
using Jellyfin.Plugin.Invites.Redemption;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// The three numbers an operator may set, the range each may take, and the one
/// place a configured number becomes a value the plugin acts on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the rules are here and not in the Configuration directory.</b> The
/// same reason <see cref="Accounts.TemplateSettings"/> gives for sitting where
/// it does: docs/coverage-floors.md leaves the configuration area out of a floor
/// on the argument that it carries values and no decisions, and this file is
/// nothing but decisions. It sits in a measured and mutated area instead, so a
/// rule here that stopped biting is found by the suite rather than by an
/// operator. The seam it reads through stays in the configuration directory,
/// because that one is a value and no decision.
/// </para>
/// <para>
/// <b>Which number carries the guarantee is the whole of this file.</b> Each of
/// the three has a compiled maximum and a configured value, and the promise
/// belongs to the maximum. A configured value is an operator's own restraint and
/// can be relaxed by whoever holds that account; the maximum cannot be moved
/// without a release. So a setting here may only ever tighten what the source
/// already promises, and a setting that could be raised without limit would look
/// like a bound while defending against a mistake rather than against an attack.
/// </para>
/// <para>
/// <b>Two of the maxima are not chosen here and one is.</b> The two rate limits
/// take their maxima from the constants the arithmetic in docs/code-entropy.md
/// and docs/rate-limit.md was computed at, by naming those constants rather than
/// restating their values, so an operator can lower a limit and cannot raise one
/// past the number those pages reason about. The retention maximum is decided
/// here: docs/personal-data.md argues against an indefinite register of who was
/// invited, and ten years is where indefinite starts for a record of that kind.
/// It is a decision rather than a measurement, like the ninety days it bounds.
/// </para>
/// <para>
/// <b>Nothing here falls back.</b> A number outside its range is refused, and
/// the refusal names the setting and the rule. #86 asks for exactly that and
/// gives the reason: a silent fallback on a bound is the bound gone, and an
/// operator who typed a number and got the plugin's own is one who was corrected
/// without being told.
/// </para>
/// <para>
/// <b>What a caller does with the refusal is the caller's.</b> This decides
/// whether a value can be used and never what happens next.
/// <see cref="Startup.LoadOnStart"/> writes it out when the server starts, and
/// each of the two routines that would act on a number refuses to act rather
/// than acting on a number nobody may have.
/// </para>
/// </remarks>
public static class NumberSettings
{
    /// <summary>
    /// The fewest days a record may be kept for.
    /// </summary>
    /// <remarks>
    /// Zero is not a stricter retention period, it is deletion at the moment a
    /// record stops being usable, which destroys the only link between an
    /// account and the invitation that produced it before anybody could read it.
    /// <see cref="Retention"/> says why every rounding in the rule is towards
    /// keeping, and this is the same direction at the bottom of the range.
    /// </remarks>
    public const int FewestRetentionDays = 1;

    /// <summary>
    /// The most days a record may be kept for.
    /// </summary>
    /// <remarks>
    /// Ten years. docs/personal-data.md argues that what is left behind must not
    /// be an indefinite register of who was invited, and a bound is what makes
    /// that sentence true of a configured value as well as of the default. The
    /// number is reasoned rather than measured.
    /// </remarks>
    public const int MostRetentionDays = 3650;

    /// <summary>
    /// The fewest attempts either rate limit may admit.
    /// </summary>
    /// <remarks>
    /// Zero attempts is not a tighter limit, it is the redemption route closed
    /// by a number an operator meant as a restraint, with no message saying so.
    /// An operator who wants nobody redeeming revokes the invitations.
    /// </remarks>
    public const int FewestAttempts = 1;

    /// <summary>
    /// Gets the name of the retention setting, as an operator meets it on the
    /// configuration page and in a refusal.
    /// </summary>
    public static string RetentionSettingName => nameof(PluginConfiguration.RecordRetentionDays);

    /// <summary>
    /// Gets the name of the per-address rate limit, as an operator meets it.
    /// </summary>
    public static string PerAddressSettingName => nameof(PluginConfiguration.RedemptionAttemptsPerAddressInAnHour);

    /// <summary>
    /// Gets the name of the global rate limit, as an operator meets it.
    /// </summary>
    public static string PerSecondSettingName => nameof(PluginConfiguration.RedemptionAttemptsPerSecond);

    /// <summary>
    /// Gets the most attempts one source address may be allowed in an hour,
    /// whatever is configured.
    /// </summary>
    /// <remarks>
    /// The constant the row in docs/rate-limit.md headed "one address, 20 an
    /// hour for a year" is computed at, named rather than restated so the two
    /// cannot drift.
    /// </remarks>
    public static int MostAttemptsPerAddressInAnHour => AttemptLimiter.PerAddressCeiling;

    /// <summary>
    /// Gets the most attempts all sources together may be allowed in a second,
    /// whatever is configured.
    /// </summary>
    /// <remarks>
    /// The constant the throttled rows of docs/code-entropy.md are computed at,
    /// named rather than restated. An operator who could raise this would be
    /// moving the number those rows rest on without the pages that carry the
    /// argument moving with it.
    /// </remarks>
    public static int MostAttemptsPerSecond => AttemptLimiter.GlobalCeiling;

    /// <summary>
    /// Says why the configured numbers cannot be used, or nothing where each is
    /// inside its range.
    /// </summary>
    /// <param name="numbers">The numbers as configured. A null member reads as none configured.</param>
    /// <returns>One sentence naming the setting and the rule, or <c>null</c>.</returns>
    /// <remarks>
    /// The first refusal is the one named, in the order the settings are
    /// declared. An operator repairs it, loads again and meets the next, which
    /// costs a reload per fault and never hides one behind another. That is the
    /// rule <see cref="Accounts.TemplateSettings"/> already follows for the
    /// template list, and it is the same rule here.
    /// </remarks>
    public static string? WhyRefused(IConfiguredNumbers? numbers)
    {
        if (numbers is null)
        {
            return null;
        }

        return WhyRetentionRefused(numbers.RecordRetentionDays)
            ?? WhyPerAddressRefused(numbers.RedemptionAttemptsPerAddressInAnHour)
            ?? WhyPerSecondRefused(numbers.RedemptionAttemptsPerSecond);
    }

    /// <summary>
    /// Says why the configured retention period cannot be used.
    /// </summary>
    /// <param name="days">The period as configured, in days. Null reads as none configured.</param>
    /// <returns>One sentence naming the setting and the rule, or <c>null</c>.</returns>
    public static string? WhyRetentionRefused(int? days) =>
        WhyOutsideItsRange(days, RetentionSettingName, FewestRetentionDays, MostRetentionDays, "days");

    /// <summary>
    /// Says why the configured per-address rate limit cannot be used.
    /// </summary>
    /// <param name="attempts">The limit as configured. Null reads as none configured.</param>
    /// <returns>One sentence naming the setting and the rule, or <c>null</c>.</returns>
    public static string? WhyPerAddressRefused(int? attempts) =>
        WhyOutsideItsRange(attempts, PerAddressSettingName, FewestAttempts, MostAttemptsPerAddressInAnHour, "attempts");

    /// <summary>
    /// Says why the configured global rate limit cannot be used.
    /// </summary>
    /// <param name="attempts">The limit as configured. Null reads as none configured.</param>
    /// <returns>One sentence naming the setting and the rule, or <c>null</c>.</returns>
    public static string? WhyPerSecondRefused(int? attempts) =>
        WhyOutsideItsRange(attempts, PerSecondSettingName, FewestAttempts, MostAttemptsPerSecond, "attempts");

    /// <summary>
    /// How long a record that has stopped being usable is kept on this server.
    /// </summary>
    /// <param name="numbers">The numbers as configured, or <c>null</c> where none are.</param>
    /// <returns>The configured period, or the default where nothing is configured.</returns>
    /// <exception cref="ConfiguredNumbersRefusedException">
    /// The configured period is outside its range. Nothing is substituted for
    /// it, because a sweep acting on the default instead would be this plugin
    /// deleting records on a schedule the operator did not ask for.
    /// </exception>
    public static TimeSpan RetentionPeriod(IConfiguredNumbers? numbers)
    {
        var days = numbers?.RecordRetentionDays;
        if (days is null)
        {
            return Retention.RecordRetention;
        }

        var why = WhyRetentionRefused(days);
        return why is null
            ? TimeSpan.FromDays(days.Value)
            : throw new ConfiguredNumbersRefusedException(why);
    }

    /// <summary>
    /// How many presented codes one source address may have judged in an hour on
    /// this server.
    /// </summary>
    /// <param name="numbers">The numbers as configured, or <c>null</c> where none are.</param>
    /// <returns>The configured limit, or the compiled maximum where nothing is configured.</returns>
    /// <exception cref="ConfiguredNumbersRefusedException">The configured limit is outside its range.</exception>
    public static int AttemptsPerAddressInAnHour(IConfiguredNumbers? numbers)
    {
        var attempts = numbers?.RedemptionAttemptsPerAddressInAnHour;
        if (attempts is null)
        {
            return MostAttemptsPerAddressInAnHour;
        }

        var why = WhyPerAddressRefused(attempts);
        return why is null ? attempts.Value : throw new ConfiguredNumbersRefusedException(why);
    }

    /// <summary>
    /// How many presented codes all sources together may have judged in a second
    /// on this server.
    /// </summary>
    /// <param name="numbers">The numbers as configured, or <c>null</c> where none are.</param>
    /// <returns>The configured limit, or the compiled maximum where nothing is configured.</returns>
    /// <exception cref="ConfiguredNumbersRefusedException">The configured limit is outside its range.</exception>
    public static int AttemptsPerSecond(IConfiguredNumbers? numbers)
    {
        var attempts = numbers?.RedemptionAttemptsPerSecond;
        if (attempts is null)
        {
            return MostAttemptsPerSecond;
        }

        var why = WhyPerSecondRefused(attempts);
        return why is null ? attempts.Value : throw new ConfiguredNumbersRefusedException(why);
    }

    /// <summary>
    /// The one sentence every refusal here is written as.
    /// </summary>
    /// <remarks>
    /// It names the setting, the range and the direction the value went out of
    /// it, and never the value that was typed. The line is written to a log when
    /// the plugin loads, and docs/logging.md admits a value there only where it
    /// is a row in docs/personal-data.md, which a server setting is not. That is
    /// the rule <see cref="Accounts.TemplateSettings"/> already follows and it is
    /// the same rule here.
    /// </remarks>
    /// <param name="value">The configured value, or <c>null</c> where none is.</param>
    /// <param name="setting">The setting as an operator meets it.</param>
    /// <param name="fewest">The lowest value the setting may take.</param>
    /// <param name="most">The highest value the setting may take.</param>
    /// <param name="unit">What the number counts, for the sentence.</param>
    /// <returns>The refusal, or <c>null</c>.</returns>
    private static string? WhyOutsideItsRange(int? value, string setting, int fewest, int most, string unit)
    {
        if (value is null || (value.Value >= fewest && value.Value <= most))
        {
            return null;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} is set {1} the range it may take, which is {2} to {3} {4}. Nothing is substituted for it: a number outside its range is refused where it would be used, so the setting is repaired rather than quietly replaced by the one this plugin would have chosen.",
            setting,
            value.Value < fewest ? "below" : "above",
            fewest.ToString(CultureInfo.InvariantCulture),
            most.ToString(CultureInfo.InvariantCulture),
            unit);
    }
}
