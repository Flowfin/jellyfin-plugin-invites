using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Invitations;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Invites.Maintenance;

/// <summary>
/// The server's own scheduler asking this plugin to apply its retention rule.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type is the trigger and nothing else.</b> Every decision about which
/// records go lives in <see cref="Retention"/> and in
/// <see cref="Redemption.RedemptionDecision.RetentionStartsAt"/>, and the write
/// lives in <see cref="InvitationOperations.Sweep"/> under the same monitor a
/// mint and a revocation take. What is left here is a name, a schedule and a log
/// line. That split is deliberate: a scheduled task is the one thing in this
/// plugin the suite cannot invoke the way the server does, so as little as
/// possible is decided inside it.
/// </para>
/// <para>
/// <b>It removes records and it marks nothing.</b> #59 warns that a task which
/// wrote an expired flag would create a second authority for expiry plus a window
/// in which an expired invitation is still honoured because the task has not run.
/// Expiry stays a comparison made at redemption, this runs late or not at all
/// without changing what any code is worth, and a server that never ran it holds
/// more records than the rule allows rather than honouring an invitation it
/// should refuse.
/// </para>
/// <para>
/// <b>Daily, in the small hours.</b> The rule is measured in days, so anything
/// finer buys nothing, and a task that walks the whole store is work an operator
/// should not meet while somebody is watching a film. A run missed because the
/// server was off is not a state to recover from: the next run deletes exactly
/// the same records plus whatever aged in the meantime.
/// </para>
/// <para>
/// <b>What is not claimed.</b> Whether the server's scheduler picks this up is
/// not decidable from anything this plugin references. The task list is built by
/// the server, `MediaBrowser.Model.Tasks` carries the interface and nothing else,
/// and no reading of the two packages this project references says how an
/// implementation is discovered. It is registered in the service collection like
/// every other service this plugin adds, and the e2e identity job asks a running
/// server whether the task is there.
/// </para>
/// </remarks>
public sealed class RetentionSweep : IScheduledTask, IConfigurableScheduledTask
{
    /// <summary>
    /// The hour the sweep runs at, as a whole hour of the server's day.
    /// </summary>
    private const int RunsAtHour = 3;

    private readonly InvitationOperations _operations;
    private readonly ILogger<RetentionSweep> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetentionSweep"/> class.
    /// </summary>
    /// <param name="operations">The one place the store is written from.</param>
    /// <param name="logger">Where a run says what it did.</param>
    public RetentionSweep(InvitationOperations operations, ILogger<RetentionSweep> logger)
    {
        _operations = operations;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Delete invitation records past their retention period";

    /// <summary>
    /// Gets the identifier the server stores this task's schedule and history
    /// under.
    /// </summary>
    /// <remarks>
    /// Fixed rather than derived from the type name, because a rename of the
    /// class would otherwise look to the server like a new task and would lose an
    /// operator's own schedule for it.
    /// </remarks>
    public string Key => "InvitesRetentionSweep";

    /// <inheritdoc />
    public string Description => string.Format(
        CultureInfo.InvariantCulture,
        "Removes invitation records that stopped being usable more than {0} days ago. A record that can still be redeemed is never removed, and no account is ever touched.",
        Retention.RecordRetention.TotalDays);

    /// <inheritdoc />
    public string Category => "Invites";

    /// <inheritdoc />
    public bool IsHidden => false;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsLogged => true;

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(RunsAtHour).Ticks,
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The whole sweep is one call under one monitor, so progress is reported at
    /// its two ends rather than per record. A progress bar that moved while the
    /// gate was held would be reporting on work no other operation could be doing
    /// anyway.
    /// </para>
    /// <para>
    /// A server with no data directory is not a fault here. The plugin says so at
    /// startup, and a scheduled task that threw every night for it would fill the
    /// log with the same sentence.
    /// </para>
    /// </remarks>
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        progress.Report(0);

        if (!_operations.StoreIsAvailable)
        {
            _logger.LogInformation("The retention sweep did not run, because this plugin has no data directory to sweep.");
            progress.Report(100);
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var removed = _operations.Sweep();

        if (removed.Length == 0)
        {
            _logger.LogInformation("The retention sweep removed nothing. No record has been unusable for longer than the retention period.");
        }
        else
        {
            _logger.LogInformation(
                "The retention sweep removed {Removed} invitation record(s) past the retention period: {Identifiers}",
                removed.Length,
                string.Join(", ", removed));
        }

        progress.Report(100);

        return Task.CompletedTask;
    }
}
