// A configuration type whose settings all have a row in the reference beside
// it. Two settings rather than one, so a check that reported the first name it
// found and stopped would not pass this pair.
//
// This file is never compiled. It is scanned as text, which is why it carries
// the shapes a real configuration type carries rather than the smallest thing
// that parses: a property with an initialiser, a nullable property, a
// getter-only property that is not a setting, and a static one that is not
// either.
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Fixture.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string? PublicBaseAddress { get; set; }

    public int MaximumUseCount { get; set; } = 1;

    // A setting whose setter runs once. It is written by the deserialiser like
    // any other and owes a row like any other, and a pattern that only knows
    // the word set stops seeing it.
    public bool AllowRemoteAccess { get; init; }

    // Not a setting: nothing can write it, so it owes no row.
    public bool IsConfigured { get; }

    // Not a setting either: it is not part of what the server deserialises.
    public static string FileName { get; set; } = "invitations.xml";
}
