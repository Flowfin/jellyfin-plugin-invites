using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Jellyfin.Plugin.Invites.Setup;

/// <summary>
/// The one page every refusal of a presented code is answered with.
/// </summary>
/// <remarks>
/// <para>
/// <b>One page, and the case is not in it.</b> docs/refusal-response.md fixes
/// the wording, the case list and what identical covers, and the property it
/// exists for is that a stranger cannot tell a code that was never real from one
/// that expired, was spent, was revoked or arrived after a limit. Nothing here
/// takes an outcome, so there is no argument a later change could interpolate:
/// a body assembled per case differs by its length even where the text reads the
/// same, and a length is the cheapest field of a response to measure.
/// </para>
/// <para>
/// <b>It is bytes, for the reason <see cref="SetupPage"/> is.</b> An embedded
/// resource served exactly as it was compiled in, so no presented code and no
/// answer from a form can reach the markup. The page carries no code, no
/// invitation identifier and nothing that varies between the cases, which is
/// also why it may sit in a browser history on a shared machine.
/// </para>
/// <para>
/// <b>The policy is derived from the page.</b> It is computed by the same
/// routine the setup page uses, over these bytes, so the hash names exactly this
/// style element. Writing one by hand into a header is the drift that routine
/// exists against.
/// </para>
/// </remarks>
public static class RefusalPage
{
    /// <summary>
    /// The name of the embedded resource holding the page.
    /// </summary>
    public const string ResourceName = "Jellyfin.Plugin.Invites.Setup.refusalPage.html";

    /// <summary>
    /// What the page is served as. A browser followed the link, so the refusal
    /// answers with markup for the same reason the setup page does.
    /// </summary>
    public const string ContentType = SetupPage.ContentType;

    private static readonly string _html = Read();

    private static readonly string _policy = SetupPage.PolicyFor(_html);

    /// <summary>
    /// Gets the page, exactly as it was compiled in.
    /// </summary>
    public static string Html => _html;

    /// <summary>
    /// Gets the content security policy the page is served under.
    /// </summary>
    public static string ContentSecurityPolicy => _policy;

    private static string Read()
    {
        var assembly = typeof(RefusalPage).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                "The refusal page is not in this assembly under "
                + ResourceName
                + ". It is an embedded resource declared in the project file, and a build that dropped it would otherwise answer a refused redemption with nothing.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
