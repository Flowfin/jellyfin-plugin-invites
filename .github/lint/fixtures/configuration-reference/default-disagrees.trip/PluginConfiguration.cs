// The same type as the clean fixture. What differs is one cell of the reference
// beside it: the row for MaximumUseCount states the top of its own Bounds cell
// instead of the value the initialiser sets. That is the mistake this pair
// stands for, and it is the one somebody makes while widening a bound and
// reading one cell to the right.
//
// This file is never compiled. It is scanned as text.
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Fixture.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string? PublicBaseAddress { get; set; }

    public int MaximumUseCount { get; set; } = 1;

    public bool AllowRemoteAccess { get; init; }
}
