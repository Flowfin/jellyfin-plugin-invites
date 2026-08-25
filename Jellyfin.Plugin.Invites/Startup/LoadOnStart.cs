using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Configuration;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Server;
using Jellyfin.Plugin.Invites.Storage;
using Jellyfin.Plugin.Invites.Time;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Invites.Startup;

/// <summary>
/// Claims the store directory when the server starts, reads the store once, and
/// writes what it found to the log.
/// </summary>
/// <remarks>
/// <para>
/// This is the caller two issues were waiting for. #96 asks that two servers
/// over one store be detected at startup and refused; #46 asks that a load
/// compare what the store claims to have created against the accounts the
/// server has. Both mechanisms already existed and neither ran, because the
/// plugin had no moment between being constructed and being asked to serve a
/// page.
/// </para>
/// <para>
/// <b>The accounts handed to the comparison are every account on the server.</b>
/// That is the widest reading available and it is stated here because it decides
/// what the second direction of the report means: this plugin puts no mark on an
/// account, so an account it created and forgot is indistinguishable from one an
/// operator made by hand, and every hand-made account is therefore reported as
/// claimed by no invitation. <see cref="ConsistencyReport"/> says the same thing
/// from its own side. The first direction, an account a record claims that the
/// server does not have, carries no such ambiguity and is the half a restored
/// backup shows up in.
/// </para>
/// <para>
/// <b>It also reads the configured public address once.</b> #86 asks that a
/// setting be judged when the plugin loads rather than only where it is used,
/// and this is the moment the plugin has for that. An address an operator
/// mistyped is otherwise met by whoever mints next, holding half an invitation
/// they cannot hand to anybody. Nothing is corrected and nothing is guessed at:
/// the setting is read, the same question <see cref="InvitationLink"/> asks is
/// asked of it, and a setting that cannot be used is named.
/// </para>
/// <para>
/// <b>It is also where the plugin finds out it is on the wrong server.</b> #97
/// asks that a mismatch leave no partial operation behind, and this is the only
/// thing in the plugin that acts without a request having arrived: it claims a
/// directory on disk and reads the server's accounts. So the verdict is read
/// first and nothing below it runs on a mismatch. The routes are refused
/// separately, by <see cref="RefuseOnAServerLineMismatch"/>, and the two halves
/// are not the same half: one is what a request meets and this one is what
/// happens whether or not a request ever arrives.
/// </para>
/// <para>
/// <b>What is written to the log obeys docs/logging.md.</b> The lines carry
/// invitation identifiers and account identifiers, both rows of the inventory in
/// docs/personal-data.md, and nothing else about a record. No code, no link and
/// nothing derived from either passes through here, and there is nothing in a
/// load that could carry one.
/// </para>
/// </remarks>
public sealed class LoadOnStart : IHostedService, IDisposable
{
    /// <summary>
    /// How many disagreements of each direction are written out one by one
    /// before the rest are counted instead.
    /// </summary>
    /// <remarks>
    /// A server whose store was restored from far enough back disagrees about
    /// every account on it, and a plugin that answers that by writing one line
    /// per account fills the log an operator is trying to read the first line
    /// of. The count is always exact; what is bounded is the enumeration.
    /// </remarks>
    public const int MostNamedOneByOne = 20;

    private readonly ServerLineGate _line;
    private readonly IStoreDirectory _directory;
    private readonly IPublicAddress _address;
    private readonly IServerAccounts _accounts;
    private readonly IClock _clock;
    private readonly ILogger<LoadOnStart> _logger;
    private StoreLoad? _load;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoadOnStart"/> class.
    /// </summary>
    /// <param name="line">The comparison against the server line this plugin was built for.</param>
    /// <param name="directory">Where the store sits.</param>
    /// <param name="address">The configured public address, read once.</param>
    /// <param name="accounts">The server's own account list.</param>
    /// <param name="clock">The time source the claim is stamped from.</param>
    /// <param name="logger">Where the answer goes.</param>
    public LoadOnStart(ServerLineGate line, IStoreDirectory directory, IPublicAddress address, IServerAccounts accounts, IClock clock, ILogger<LoadOnStart> logger)
    {
        _line = line;
        _directory = directory;
        _address = address;
        _accounts = accounts;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_line.MayRun)
        {
            // Before anything else, and before anything is read or claimed.
            // Nothing below this line is a read: the store directory is claimed
            // for the lifetime of the process, so a plugin that got this far on
            // the wrong server would be holding a claim no request of its own
            // could ever use, against a second server that could.
            _logger.LogError("{Finding}", _line.Verdict.Message);
            return Task.CompletedTask;
        }

        ReportTheConfiguredAddress();

        var directory = _directory.Path;
        if (string.IsNullOrWhiteSpace(directory))
        {
            // The server constructs the plugin before it starts hosted
            // services, so this is not a state anything is expected to reach.
            // It is reported rather than thrown, because a plugin that cannot
            // find its own data directory must not be the reason a server fails
            // to start.
            _logger.LogError("The invitation store was not read at startup, because this plugin has no data directory to read it from. No store directory has been claimed.");
            return Task.CompletedTask;
        }

        var accounts = _accounts.Identifiers;
        var load = StoreLoad.Of(
            directory,
            Environment.MachineName,
            Environment.ProcessId,
            _clock,
            accounts);

        _load = load;

        if (!load.HoldsTheStore)
        {
            var refusal = load.Refusal!;
            _logger.LogError(
                "The invitation store directory is already claimed, so this server has not taken it and this plugin will not use it. It is held by {Holder}. The claim is the file {ClaimPath}, and it is removed by hand once that process is no longer running.",
                refusal.HeldBy,
                refusal.Path);
            return Task.CompletedTask;
        }

        if (accounts is null)
        {
            _logger.LogError(
                "The invitation store was claimed and was not compared against this server's accounts, because this server does not answer for them in a shape this plugin knows. It was asked for {Member}.",
                ServerAccounts.WhatWasLookedFor());
            return Task.CompletedTask;
        }

        Report(load.Report!);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Release();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Release();
    }

    /// <summary>
    /// Reads the configured public address and names the setting where it
    /// cannot be used.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A setting nobody has written is the decided fresh-install value rather
    /// than a fault, which docs/configuration.md argues under "A fresh install",
    /// so it is passed over in silence. An operator who has not opened the
    /// configuration page is not owed an error for not having opened it.
    /// </para>
    /// <para>
    /// What the line does NOT carry is the value that was configured. Every
    /// value in a log line here is a row in docs/personal-data.md, which a
    /// server setting is not, and the two refusals that quote what was typed
    /// are written for the operator who asked for something rather than for a
    /// log a support thread will paste. The setting is named, the rule it
    /// missed is not, and the refusal that says which rule arrives where an
    /// operator is already looking.
    /// </para>
    /// </remarks>
    private void ReportTheConfiguredAddress()
    {
        var configured = _address.PublicBaseUrl;
        if (string.IsNullOrWhiteSpace(configured))
        {
            return;
        }

        if (InvitationLink.WhyItCannotCarryALink(configured) is null)
        {
            return;
        }

        _logger.LogError(
            "The public address this plugin is configured with cannot be used, so nothing minted against it would reach the person it was meant for. The setting is PublicBaseUrl on this plugin's own configuration page, and it wants an absolute http or https address such as https://media.example.org, with an optional path prefix and no query or fragment. Minting is refused while it stands, and the refusal names which of those it missed.");
    }

    private void Report(ConsistencyReport report)
    {
        if (report.Agrees)
        {
            _logger.LogInformation("{Finding}", report.Summary);
            return;
        }

        _logger.LogWarning("{Finding}", report.Summary);

        foreach (var claimed in report.AccountsClaimedButAbsent.Take(MostNamedOneByOne))
        {
            _logger.LogWarning(
                "Invitation {InvitationId} says it created account {AccountId}, and this server does not have that account.",
                claimed.InvitationId,
                claimed.AccountId);
        }

        foreach (var unclaimed in report.AccountsPresentButUnclaimed.Take(MostNamedOneByOne))
        {
            _logger.LogInformation(
                "Account {AccountId} is claimed by no invitation. An account this plugin never created reads the same way, because nothing marks the ones it did.",
                unclaimed);
        }

        var notNamed = Math.Max(0, report.AccountsClaimedButAbsent.Length - MostNamedOneByOne)
            + Math.Max(0, report.AccountsPresentButUnclaimed.Length - MostNamedOneByOne);
        if (notNamed > 0)
        {
            _logger.LogWarning(
                "{Count} further disagreement(s) were counted and not written out one by one.",
                notNamed.ToString(CultureInfo.InvariantCulture));
        }
    }

    private void Release()
    {
        var load = _load;
        _load = null;
        load?.Dispose();
    }
}
