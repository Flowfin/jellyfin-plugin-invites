// The same type as the clean fixture. What differs is the reference beside it,
// which has lost the row for MaximumUseCount. The mistake this stands for is a
// setting added to the type in a change whose author did not open the
// reference.
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Fixture.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string? PublicBaseAddress { get; set; }

    public int MaximumUseCount { get; set; } = 1;
}
