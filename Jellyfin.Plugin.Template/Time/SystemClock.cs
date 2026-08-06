using System;

namespace Jellyfin.Plugin.Template.Time;

/// <summary>
/// The clock the plugin runs on outside a test.
/// </summary>
/// <remarks>
/// This is the only place in the plugin that reads the machine's clock. It has
/// no logic of its own on purpose: everything a test would want to steer lives
/// in whatever took an <see cref="IClock"/>, so nothing is lost by this type
/// being the one thing a test never exercises.
/// </remarks>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
