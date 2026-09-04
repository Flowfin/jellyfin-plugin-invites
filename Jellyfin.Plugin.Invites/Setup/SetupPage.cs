using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Jellyfin.Plugin.Invites.Setup;

/// <summary>
/// The page a person following an invitation link is served, and the policy it
/// is served under.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing a request carried is put into the page.</b> It is an embedded
/// resource, so there is no place a presented code, a username or anything else
/// a caller sent could be written into the markup. That removes the injection
/// class rather than escaping it, and it is why
/// <see cref="Controllers.RedeemController"/> does not bind the code in its
/// route at all. The cost is that the page cannot yet say which server it
/// belongs to, which docs/setup-never-asks.md asks for under its presentation
/// rules and which is not met here.
/// </para>
/// <para>
/// <b>ONE value is written in, and it is this plugin's own.</b> The anti-forgery
/// token #78 puts on the form has to differ per page view or it defends
/// nothing, so the resource carries <see cref="Placeholder"/> where the value
/// goes and <see cref="For"/> is the one routine that fills it. The sentence
/// above is unchanged by that and is the reason the substitution is safe to
/// make: the value is minted here rather than received, and <see cref="For"/>
/// refuses anything <see cref="FormToken.IsWellFormed"/> does not accept, which
/// admits only hexadecimal digits. So what reaches the markup carries no
/// character HTML gives a meaning to, and there is nothing to escape rather than
/// an escape somebody has to have got right.
/// </para>
/// <para>
/// <b>No build step and no framework.</b> The whole page is one HTML file with
/// one style element and no script, which is #74's decision. Serving it from a
/// resource rather than from disk means it works on a server with no web client
/// installed and there is no path an operator can leave a stale copy at.
/// </para>
/// <para>
/// <b>The policy is derived from the page and never written beside it.</b> The
/// style element is inline, so the policy has to name it, and it names it by
/// hash rather than by <c>'unsafe-inline'</c>: a hash admits exactly these bytes
/// and an allowance admits any bytes at all, including ones an injection put
/// there. Computing it here from the same string that is served means the two
/// cannot drift, which is the failure a hash written into a header by hand has.
/// </para>
/// </remarks>
public static class SetupPage
{
    /// <summary>
    /// The name of the embedded resource holding the page.
    /// </summary>
    public const string ResourceName = "Jellyfin.Plugin.Invites.Setup.setupPage.html";

    /// <summary>
    /// What the page is served as. A browser follows the link, so this is the
    /// one surface of this plugin that answers with markup rather than JSON.
    /// </summary>
    public const string ContentType = "text/html; charset=utf-8";

    /// <summary>
    /// What stands in the resource where the anti-forgery token goes.
    /// </summary>
    /// <remarks>
    /// It is deliberately not shaped like a token. A placeholder that
    /// <see cref="FormToken.IsWellFormed"/> would accept is one that a
    /// substitution which silently did nothing would leave behind as a usable
    /// value, and every page view would then carry the same one.
    /// </remarks>
    public const string Placeholder = "__token__";

    private const string StyleOpen = "<style>";
    private const string StyleClose = "</style>";

    private static readonly string _html = Read();

    private static readonly string _policy = PolicyFor(_html);

    /// <summary>
    /// Gets the page, exactly as it was compiled in.
    /// </summary>
    public static string Html => _html;

    /// <summary>
    /// Gets the content security policy the page is served under.
    /// </summary>
    /// <remarks>
    /// <c>default-src 'none'</c> is the whole of docs/setup-never-asks.md's
    /// presentation rule expressed to the browser: no script, no font, no image
    /// and no frame from anywhere, this server included, so a resource added to
    /// the page later fails visibly instead of arriving quietly. What is opened
    /// back up is named one directive at a time.
    /// </remarks>
    public static string ContentSecurityPolicy => _policy;

    /// <summary>
    /// The page as it is served for one page view, carrying the token that view
    /// is bound to.
    /// </summary>
    /// <param name="token">The token minted for this response.</param>
    /// <returns>The page, with <see cref="Placeholder"/> replaced.</returns>
    /// <exception cref="ArgumentException">
    /// The token is not one this plugin mints. Refused rather than written in,
    /// because the argument that the substitution needs no escaping rests
    /// entirely on the alphabet of what is substituted, and a caller handing in
    /// something else is the one case that argument does not cover.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The compiled-in page does not carry the placeholder exactly once. Either
    /// way the response would go out with no token on the form or with two
    /// controls carrying one, and a form whose token nothing filled in is a form
    /// every post is refused for.
    /// </exception>
    public static string For(string token)
    {
        if (!FormToken.IsWellFormed(token))
        {
            throw new ArgumentException(
                "The setup page is only ever handed a token this plugin minted, which is "
                + Length()
                + " hexadecimal characters. What was handed in is not one, and writing it into the markup is the injection this page exists without.",
                nameof(token));
        }

        var occurrences = Occurrences(_html, Placeholder);
        if (occurrences != 1)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The compiled-in setup page carries the anti-forgery placeholder {0} times and the substitution fills exactly one. A page served with none is a page whose every post is refused, and a page served with two is two controls answering for one token.",
                    occurrences));
        }

        return _html.Replace(Placeholder, token, StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds the policy for a page, naming its one style element by hash.
    /// </summary>
    /// <param name="page">The page as it will be served.</param>
    /// <returns>The value of the <c>Content-Security-Policy</c> header.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The page carries no style element, or more than one. Either way a hash
    /// over the first one is a policy that describes a different page from the
    /// one being served, and a browser refusing a page's own style is a page
    /// nobody can read. Refusing here is the same failure at the moment it can
    /// still be fixed.
    /// </exception>
    public static string PolicyFor(string page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var opens = Occurrences(page, StyleOpen);
        var closes = Occurrences(page, StyleClose);
        if (opens != 1 || closes != 1)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "A served page carries {0} style openings and {1} closings, and the policy names exactly one. A page whose style the policy does not cover is a page a browser renders unstyled.",
                    opens,
                    closes));
        }

        var from = page.IndexOf(StyleOpen, StringComparison.Ordinal) + StyleOpen.Length;
        var to = page.IndexOf(StyleClose, StringComparison.Ordinal);
        if (to < from)
        {
            throw new InvalidOperationException(
                "A served page closes a style element before it opens one, so there is nothing between the two to hash.");
        }

        var style = page[from..to];
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(style));

        return string.Format(
            CultureInfo.InvariantCulture,
            "default-src 'none'; style-src 'sha256-{0}'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'",
            Convert.ToBase64String(digest));
    }

    /// <summary>
    /// How long a token is, as a sentence fragment, read from the type that
    /// decides it rather than typed here a second time.
    /// </summary>
    /// <returns>The length.</returns>
    private static string Length()
    {
        return FormToken.Length.ToString(CultureInfo.InvariantCulture);
    }

    private static int Occurrences(string page, string what)
    {
        var found = 0;
        var at = page.IndexOf(what, StringComparison.Ordinal);
        while (at >= 0)
        {
            found++;
            at = page.IndexOf(what, at + what.Length, StringComparison.Ordinal);
        }

        return found;
    }

    private static string Read()
    {
        var assembly = typeof(SetupPage).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                "The setup page is not in this assembly under "
                + ResourceName
                + ". It is an embedded resource declared in the project file, and a build that dropped it would otherwise serve an empty page.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
