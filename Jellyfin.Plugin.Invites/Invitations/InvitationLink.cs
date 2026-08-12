using System;
using System.Globalization;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// The address an operator hands to somebody they are inviting.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing about a request reaches this.</b> There is no parameter for one,
/// and that is the whole of #50. The easy way to build a link is from the
/// incoming request's host header, which is attacker controlled: a minting
/// request carrying a forged host produces a link pointing at the attacker's
/// server, and the invited person types their new password into it. A link is
/// also minted for somebody who is not on the other end of any request, so
/// there is no correct version of deriving one from a request even where the
/// request is honest.
/// </para>
/// <para>
/// The greppable half of that rule is <c>link-built-from-a-request-header</c>
/// and <c>link-built-from-the-request-url-helper</c> in
/// <c>.github/lint/invariants.sh</c>. Neither exempts this file, because there
/// is nothing here for them to catch: the shape they refuse cannot be written
/// against a signature that takes no request.
/// </para>
/// <para>
/// <b>The base address is configuration and has no fallback that guesses.</b>
/// Where <see cref="Configuration.PluginConfiguration.PublicBaseUrl"/> is not
/// set, this refuses rather than reaching for the server's own published
/// address. Two reasons, and the second is measured rather than argued: a
/// server behind a reverse proxy is exactly the case where the server's own
/// idea of its address is wrong and the operator's is right, which is why the
/// setting exists at all; and the members a server answers that question with
/// move between the lines this plugin loads on, which
/// <see cref="Accounts.ServerAccounts"/> is the standing evidence for. A
/// refusal that names the setting is a support question with an answer. A link
/// built from the wrong address is a support question without one.
/// </para>
/// <para>
/// <b>The code goes in the path.</b> That is decided in docs/redemption-flow.md
/// and the route is written down in docs/api.md as <c>GET /redeem/{code}</c>.
/// A query string reaches more logs, more analytics and more referrer headers
/// than a path does.
/// </para>
/// <para>
/// <b>It does not canonicalise.</b> What is handed in is the code as it was
/// minted, and turning one spelling into another is
/// <see cref="Codes.InvitationCode.Canonicalise"/>'s job in one place. This
/// percent-encodes for the URL and nothing else, which is a different operation
/// on a different alphabet and is undone by the browser rather than by this
/// plugin.
/// </para>
/// </remarks>
public static class InvitationLink
{
    /// <summary>
    /// The path segment the redemption route sits under.
    /// </summary>
    /// <remarks>
    /// It is the route in docs/api.md rather than a choice made here, and it is
    /// a constant so the controller that serves it and the link that points at
    /// it cannot drift into two spellings.
    /// </remarks>
    public const string Segment = "redeem";

    /// <summary>
    /// Builds the link for one code against a configured base address.
    /// </summary>
    /// <param name="publicBaseUrl">
    /// The address from
    /// <see cref="Configuration.PluginConfiguration.PublicBaseUrl"/>, absolute
    /// and carrying a scheme.
    /// </param>
    /// <param name="code">The code as it was minted.</param>
    /// <returns>The address to hand to the invited person.</returns>
    /// <exception cref="ArgumentException">
    /// The base address is not set, is not absolute, is not http or https, or
    /// carries a query or a fragment; or the code is blank. Every one of those
    /// produces a link that does not work, and a link that does not work is
    /// better found here than by the person holding it.
    /// </exception>
    public static string For(string publicBaseUrl, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A link needs the code it is a link to.", nameof(code));
        }

        var address = BaseAddress(publicBaseUrl);

        // GetLeftPart(UriPartial.Path) rather than the original string: it
        // drops nothing an operator set and normalises the default port away,
        // so a base written with a trailing slash and one written without it
        // produce the same link. A path prefix survives, which is the reverse
        // proxy serving this server under a subdirectory.
        var reachable = address.GetLeftPart(UriPartial.Path).TrimEnd('/');

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}/{1}/{2}",
            reachable,
            Segment,
            Uri.EscapeDataString(code));
    }

    /// <summary>
    /// Reads the configured base address, refusing every shape that cannot
    /// carry a link.
    /// </summary>
    /// <param name="publicBaseUrl">The configured address.</param>
    /// <returns>The address, absolute and http or https.</returns>
    private static Uri BaseAddress(string publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            throw new ArgumentException(
                "No public address is configured, so there is no address to build an invitation link from. Set PublicBaseUrl to the address a stranger reaches this server on. It is not taken from the request on purpose: a request can say anything about which host it reached.",
                nameof(publicBaseUrl));
        }

        if (!Uri.TryCreate(publicBaseUrl.Trim(), UriKind.Absolute, out var address))
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The configured public address is not an absolute address, so a link built from it would not resolve. It reads {0} and it wants a scheme and a host, as in https://media.example.org.",
                    publicBaseUrl),
                nameof(publicBaseUrl));
        }

        if (!string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            && !string.Equals(address.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The configured public address carries the scheme {0}, and an invitation link is followed in a browser. It wants http or https.",
                    address.Scheme),
                nameof(publicBaseUrl));
        }

        if (address.Query.Length > 0 || address.Fragment.Length > 0)
        {
            throw new ArgumentException(
                "The configured public address carries a query or a fragment, and appending a path to one produces an address that does not reach the redemption route. It wants a scheme, a host and at most a path prefix.",
                nameof(publicBaseUrl));
        }

        return address;
    }
}
