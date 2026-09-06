using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Jellyfin.Plugin.Invites.Setup;

/// <summary>
/// The page a finished redemption is sent to.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the end of the flow and it is an address of its own.</b> docs/api.md
/// fixes the completion route as one that takes no parameters and reads no
/// invitation, and that is the whole reason it is a route rather than a state of
/// the post: a completion that re-read the invitation would meet the record its
/// own redemption spent, refuse, and turn a finished, correct redemption into an
/// error. A route with nothing to look up cannot do that, so a refresh and a
/// back button are both harmless.
/// </para>
/// <para>
/// <b>It is bytes, for the reason <see cref="RefusalPage"/> and
/// <see cref="SetupPage"/> are.</b> An embedded resource served exactly as it
/// was compiled in, so nothing a request carried reaches the markup. Unlike the
/// setup page there is not even a token to write in: the page takes no
/// substitution at all.
/// </para>
/// <para>
/// <b>It says nothing about the account that was just created.</b> That follows
/// from the route reading nothing, and it is what makes the page safe to leave
/// in a browser history on a shared machine. No code, no invitation identifier,
/// no username and no password. <c>CompletionPageTests</c> asserts each of
/// those.
/// </para>
/// <para>
/// <b>The sign-in is the server's own and this plugin mints no session.</b>
/// docs/redemption-flow.md decides that under what the flow does not cover: the
/// first authentication of the new account goes through exactly the path every
/// later one will, and issuing a session from a bearer credential that has just
/// been spent is a second decision rather than a convenience. So the page
/// carries one relative link and no session.
/// </para>
/// <para>
/// <b>What the link does not cover.</b> It is the address the server's web
/// client is served from, and a server started without one answers nothing
/// there. Such a server has no sign-in page at all, so no link this page could
/// carry would reach one; the disclosure is that the person then has to be told
/// where to sign in by whoever invited them.
/// </para>
/// <para>
/// <b>The policy is derived from the page.</b> Computed by the same routine the
/// other two served pages use, over these bytes, so the hash names exactly this
/// style element.
/// </para>
/// </remarks>
public static class CompletionPage
{
    /// <summary>
    /// The name of the embedded resource holding the page.
    /// </summary>
    public const string ResourceName = "Jellyfin.Plugin.Invites.Setup.completionPage.html";

    /// <summary>
    /// What the page is served as. A browser was redirected here, so the
    /// completion answers with markup for the same reason the other two pages
    /// do.
    /// </summary>
    public const string ContentType = SetupPage.ContentType;

    /// <summary>
    /// The address the page sends the person to in order to sign in. It is the
    /// server's own web client, relative so that it stays on the server the
    /// person already reached rather than on an address this page would have to
    /// be told.
    /// </summary>
    public const string SignIn = "/web/";

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
        var assembly = typeof(CompletionPage).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                "The completion page is not in this assembly under "
                + ResourceName
                + ". It is an embedded resource declared in the project file, and a build that dropped it would leave every finished redemption redirected to an address that answers nothing.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
