using System.Net.Sockets;
using Xunit;

namespace Jellyfin.Plugin.Template.Tests;

/// <summary>
/// Breaks the headless rule on purpose, so the job that enforces it can be seen
/// to go red for that reason. Removed in the commit after the one that proves it.
/// </summary>
public class HeadlessProbeTests
{
    /// <summary>
    /// Opens a connection, which the rule forbids.
    /// </summary>
    [Fact]
    public void OpensANetworkConnection()
    {
        using var client = new TcpClient();
        client.Connect("1.1.1.1", 443);
        Assert.True(client.Connected);
    }
}
