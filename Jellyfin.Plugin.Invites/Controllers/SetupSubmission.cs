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
/// <b>Nothing here is validated and that is deliberate.</b> This type carries
/// what arrived and makes no judgement about it; whether the answers are
/// acceptable is decided by <see cref="SetupAnswers"/>, which is what the post
/// asks before it looks at any code. A username the server's own expression
/// would reject is among those rules; one that COLLIDES is not, and is #67's.
/// </para>
/// <para>
/// <b>The anti-forgery token is here and something validates it.</b> The
/// paragraph that stood here said it was absent, and that a member for it with
/// nothing validating it would read to the next person as though something did.
/// <see cref="Jellyfin.Plugin.Invites.Setup.FormToken.Accompanies"/> is what reads it, and the post asks that
/// before it reads anything else. It sits on this type rather than beside it
/// because <see cref="SetupAnswers.Fields"/> derives the field names a post may
/// carry from these members: a token bound anywhere else would arrive as a field
/// the form does not define and be refused as a widening.
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
    /// Gets or sets the anti-forgery token the served page put on the form.
    /// </summary>
    /// <remarks>
    /// Not a question the person is asked. It is a hidden control this plugin
    /// filled in when it served the page, and it is here because this is the
    /// type the post binds a form into. What it is worth is decided by
    /// <see cref="Jellyfin.Plugin.Invites.Setup.FormToken.Accompanies"/> against the cookie written on the
    /// same response, and never by this type, which judges nothing.
    /// </remarks>
    public string? Token { get; set; }

    /// <summary>
    /// Gets or sets the second copy of the password the form asks for.
    /// </summary>
    /// <remarks>
    /// Read by <see cref="SetupAnswers.Accept"/> and compared ordinally against
    /// the password, and by nothing else: it never leaves that judgement, so no
    /// routine downstream is handed two copies of a password and a choice about
    /// which one to use. What is still not served is a response that tells the
    /// person the two disagreed. The form again, with the reason on it, is what
    /// docs/refusal-response.md fixes for that case and this route answers the
    /// bare bad request instead.
    /// </remarks>
    public string? Confirmation { get; set; }
}
