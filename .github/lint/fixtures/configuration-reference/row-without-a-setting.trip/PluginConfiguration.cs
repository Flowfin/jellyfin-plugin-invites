// One setting fewer than the reference beside it. PublicBaseAddress was removed
// or renamed and the row for it stayed, which is the direction that sends an
// operator looking on the configuration page for a field that is not there.
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Fixture.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public int MaximumUseCount { get; set; } = 1;
}
