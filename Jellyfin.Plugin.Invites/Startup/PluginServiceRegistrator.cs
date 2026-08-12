using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Storage;
using Jellyfin.Plugin.Invites.Time;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Invites.Startup;

/// <summary>
/// What this plugin adds to the server's own service collection.
/// </summary>
/// <remarks>
/// <para>
/// Three registrations, and all of them exist so that something happens when the
/// server starts. <see cref="LoadOnStart"/> is the moment #46 and #96 were both
/// waiting for. <see cref="IClock"/> is the seam every instant in this plugin is
/// read through, and <see cref="IStoreDirectory"/> is where the store sits, both
/// wired here to the one implementation that answers from the machine and from
/// the server.
/// </para>
/// <para>
/// The two seams are registered rather than constructed where they are needed. A
/// caller that built its own clock would be a second place the machine clock is
/// reached, which is what that seam exists to prevent, and the greppable rule
/// that refuses a direct read exempts one file by name.
/// </para>
/// </remarks>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IClock, SystemClock>();
        serviceCollection.AddSingleton<IStoreDirectory, PluginStoreDirectory>();
        serviceCollection.AddSingleton<IServerAccounts, ServerAccounts>();
        serviceCollection.AddHostedService<LoadOnStart>();
    }
}
