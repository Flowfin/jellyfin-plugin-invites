using System;

namespace Jellyfin.Plugin.Template.Time;

/// <summary>
/// The one time source the plugin reads.
/// </summary>
/// <remarks>
/// Expiry, rate-limit windows, retention and lockout are all clock reads. A
/// call site that reads the system clock directly can only be tested by a test
/// that sleeps or by no test at all, and a suite that sleeps gets slower until
/// people stop running it. Everything that needs the time takes this instead,
/// so a test supplies a clock it controls and moves it.
/// </remarks>
public interface IClock
{
    /// <summary>
    /// Gets the current instant, as an absolute point in time at offset zero.
    /// </summary>
    /// <remarks>
    /// The type is <see cref="DateTimeOffset"/> rather than
    /// <see cref="DateTime"/> because a <see cref="DateTime"/> carries a kind
    /// that is easy to lose and a local time that means nothing once it is
    /// written to a file somebody else reads. An operator who changes the
    /// server's timezone must not change when their invitations expire, and an
    /// absolute instant is what makes that true rather than a convention
    /// everybody has to remember.
    /// </remarks>
    DateTimeOffset UtcNow { get; }
}
