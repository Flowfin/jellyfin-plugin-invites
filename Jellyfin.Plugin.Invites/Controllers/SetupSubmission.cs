namespace Jellyfin.Plugin.Invites.Controllers;

/// <summary>
/// What the setup form sends back.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three members, because the form defines three controls and nothing
/// wider.</b> <c>SetupFormInventoryTests</c> reads the field names off the
/// served page, and a member here that the form has no control for would be a
/// value a stranger can set that nobody meant to offer. The names are the
/// control names rather than a second vocabulary for them.
/// </para>
/// <para>
/// <b>Nothing here is validated and that is deliberate.</b> Whether an answer is
/// acceptable is decided on the server under #75, the password rules are #76's
/// and already exist as <see cref="Setup.PasswordRules"/>, and refusing a
/// username the server would reject or one that collides is #67's. This type
/// carries what arrived and makes no judgement about it.
/// </para>
/// <para>
/// <b>The anti-forgery token is not here.</b> docs/api.md names one on this
/// route and #78 owns it. A member for it now would be a field nothing
/// validates, which reads to the next person as though something did.
/// </para>
/// </remarks>
public sealed class SetupSubmission
{
    /// <summary>
    /// Gets or sets the name the person will sign in with.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password the person chose.
    /// </summary>
    /// <remarks>
    /// It is never stored by this plugin, never written to a log line and never
    /// carried in a link. It is handed to the server's own credential routine
    /// and nothing here keeps it.
    /// </remarks>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the second copy of the password the form asks for.
    /// </summary>
    /// <remarks>
    /// Bound and not read. That the two copies agree is an answer being
    /// validated on the server, which is #75's, and the response to a
    /// disagreement is the form again rather than the single refusal, which
    /// docs/refusal-response.md fixes and this route does not serve. Binding it
    /// without reading it is what makes the absence visible here rather than
    /// leaving a control on the page whose value goes nowhere.
    /// </remarks>
    public string? Confirmation { get; set; }
}
