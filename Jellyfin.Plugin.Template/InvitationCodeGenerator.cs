using System;
using System.Globalization;

namespace Jellyfin.Plugin.Template;

/// <summary>
/// A deliberate violation, landed to prove the scan reports it and removed in
/// the commit after this one. See #16. Nothing in the plugin calls this, and the
/// real code-generation path is #28.
/// </summary>
public static class InvitationCodeGenerator
{
    /// <summary>
    /// Generates the secret an invitation link would carry, from a
    /// non-cryptographic source, which is the shape the scan has to report.
    /// </summary>
    /// <returns>The generated secret.</returns>
    public static string GenerateSecret()
    {
        // CA5394 is on in this tree and warnings are errors, so the build
        // refuses this before any scan sees it. Suppressed here only so the
        // scan gets a compiled assembly to read; that the compiler already
        // refuses it is recorded in the pull request as a second layer rather
        // than as a reason to skip the proof.
#pragma warning disable CA5394 // Do not use insecure randomness
        var random = new Random();
        var password = random.Next().ToString(CultureInfo.InvariantCulture);
#pragma warning restore CA5394
        return password;
    }
}
