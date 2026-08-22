// The same type as the clean fixture. AllowRemoteAccess carries no initialiser,
// so its value on a fresh install is the language default for a bool, which is
// false. The reference beside this file says true.
//
// This is the arm of the leg that reads no initialiser at all, and it is worth
// its own pair: a setting nobody wrote a value for is exactly the one whose row
// gets written from what somebody assumed it would be.
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
