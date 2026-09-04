using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Configuration;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Maintenance;
using Jellyfin.Plugin.Invites.Redemption;
using Jellyfin.Plugin.Invites.Server;
using Jellyfin.Plugin.Invites.Storage;
using Jellyfin.Plugin.Invites.Time;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Invites.Startup;

/// <summary>
/// What this plugin adds to the server's own service collection.
/// </summary>
/// <remarks>
/// <para>
/// Everything here exists so that something happens when the server starts.
/// <see cref="LoadOnStart"/> is the moment #46 and #96 were both waiting for.
/// The rest are the seams: <see cref="IClock"/> is where every instant in this
/// plugin is read, <see cref="IStoreDirectory"/> is where the store sits, and
/// <see cref="IPublicAddress"/> is the address a minted link is written
/// against, each wired to the one implementation that answers from the machine
/// and from the server. The method below is the list rather than this
/// paragraph, which would go stale the next time one is added.
/// </para>
/// <para>
/// The seams are registered rather than constructed where they are needed. A
/// caller that built its own clock would be a second place the machine clock is
/// reached, which is what that seam exists to prevent, and the greppable rule
/// that refuses a direct read exempts one file by name. A caller that read
/// <see cref="Plugin.Instance"/> for the public address would be the same
/// failure one static along, and #50 is why that one matters: an address read
/// from anywhere a request can reach is a link an attacker chooses.
/// </para>
/// </remarks>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IClock, SystemClock>();
        serviceCollection.AddSingleton<IStoreDirectory, PluginStoreDirectory>();
        serviceCollection.AddSingleton<IPublicAddress, PluginPublicAddress>();
        serviceCollection.AddSingleton<IConfiguredTemplates, PluginConfiguredTemplates>();
        serviceCollection.AddSingleton<IServerAccounts, ServerAccounts>();
        serviceCollection.AddSingleton<IServerAccountWrites, ServerAccountWrites>();
        serviceCollection.AddSingleton<IRunningServer, RunningServer>();
        serviceCollection.AddSingleton<ServerLineGate>();
        serviceCollection.AddSingleton<RefuseOnAServerLineMismatch>();
        serviceCollection.Configure<MvcOptions>(options => options.Conventions.Add(new ThisPluginsControllers()));
        serviceCollection.AddSingleton<InvitationOperations>();
        serviceCollection.AddSingleton<AttemptLimiter>();
        serviceCollection.AddScoped<IOperatorIdentity, RequestOperatorIdentity>();
        serviceCollection.AddSingleton<IScheduledTask, RetentionSweep>();
        serviceCollection.AddHostedService<LoadOnStart>();
    }
}
