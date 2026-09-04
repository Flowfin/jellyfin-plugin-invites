using System;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.Invites.Setup;

/// <summary>
/// Which names the server will accept, applied here before a redemption spends
/// anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a copy of a rule the server owns, and copying it is the point.</b>
/// The server refuses a name it will not take inside the call that creates the
/// account, which is after this plugin has taken a use off the invitation. So a
/// redemption carrying a name with a disallowed character consumed a use and
/// created nothing, which is the worst outcome #67 names. Asking the creation
/// call instead cannot fix that, because the only place it answers is past the
/// point of no return.
/// </para>
/// <para>
/// <b>The expression is the server's own, character for character.</b> It is
/// identical at the floor this plugin declares and at the newest release of the
/// line, and it was read off the server's source at both rather than off a
/// package this tree restores:
/// </para>
/// <code>
/// $ gh api repos/jellyfin/jellyfin/git/ref/tags/v10.11.0 --jq .object.sha
/// 877251bcaec3780d44b7657c54684dc28646b1c3
/// $ gh api repos/jellyfin/jellyfin/git/ref/tags/v10.11.11 --jq .object.sha
/// 1fbd8739292cce610231be93daf43368733edf63
/// $ ... Jellyfin.Server.Implementations/Users/UserManager.cs, line 119 at both:
/// [GeneratedRegex(@"^(?!\s)[\w\ \-'._@+]+(?&lt;!\s)$")]
/// </code>
/// <para>
/// <b>Mirroring the message instead would have been wrong, and it is the mistake
/// standing right beside the expression.</b> The refusal the server throws names
/// unicode symbols, numbers, dashes, underscores, apostrophes and periods, and
/// the expression also accepts at-signs, plus signs and interior spaces. A rule
/// written from that sentence would refuse names the server takes, which is the
/// failure this type exists to avoid in the other direction.
/// </para>
/// <para>
/// <b>Being no stricter than the server is the property that matters.</b> A copy
/// that refuses less than the server only loses the early refusal and leaves the
/// server's own refusal to catch it. A copy that refuses MORE turns a name
/// somebody may legitimately choose into a link they cannot use, and no error
/// message tells them that this plugin, rather than the server, said no.
/// </para>
/// <para>
/// <b>What is read and deliberately not mirrored.</b> The user entity declares
/// <c>MaxLength(255)</c> and <c>StringLength(255)</c> on the name at both tags.
/// Whether either is enforced at run time depends on the database provider the
/// server is running, and nothing here measured that, so a ceiling copied from
/// the declaration could refuse a name a real server accepts. It is left to the
/// server, and this is the disclosure rather than an oversight.
/// </para>
/// </remarks>
public static class UsernameRules
{
    /// <summary>
    /// How long a name may take to judge before this gives up on it.
    /// </summary>
    /// <remarks>
    /// The expression has no nested quantifier, so it cannot backtrack
    /// catastrophically and this should never fire. It is here because "should
    /// never" is not a property anything checks, and a request that hangs a
    /// redemption is worse than one that is refused.
    /// </remarks>
    private static readonly TimeSpan _givingUp = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The server's own expression, as it stands at the floor and at the newest
    /// release of the line.
    /// </summary>
    /// <remarks>
    /// Public so a test can assert what is applied rather than assert against a
    /// second copy of it, and so a reader comparing this against the server's
    /// source has one string to compare rather than a construction.
    /// </remarks>
    public const string ServerExpression = @"^(?!\s)[\w\ \-'._@+]+(?<!\s)$";

    /// <summary>
    /// The compiled form, with no options, which is how the server declares it.
    /// </summary>
    /// <remarks>
    /// No <c>RegexOptions</c>, deliberately. <c>\w</c> without
    /// <c>ECMAScript</c> matches unicode letters, digits and the underscore,
    /// which is what the comment beside the server's declaration means by
    /// "whatever else unicode is cool with", and adding an option here would be
    /// a difference from the rule this is a copy of.
    /// </remarks>
    private static readonly Regex _allowed = new(ServerExpression, RegexOptions.None, _givingUp);

    /// <summary>
    /// Gets the reason a name is refused, which is one constant and is never
    /// built out of what was typed.
    /// </summary>
    /// <remarks>
    /// One sentence for every refusal, so nothing this returns can carry a name
    /// into a response, a log or a support thread. It says what the expression
    /// accepts rather than what the server's own message says, for the reason
    /// this type's remarks give.
    /// </remarks>
    public static string Refused { get; } =
        "A name may hold letters, digits, spaces, and any of - _ ' . @ +, and may not begin or end with a space.";

    /// <summary>
    /// Says whether the server would refuse this name for its shape, before
    /// anything has been spent on it.
    /// </summary>
    /// <param name="username">What the person typed, or <c>null</c>.</param>
    /// <returns>
    /// <see cref="Refused"/> where the server's expression does not accept the
    /// name, and <c>null</c> where it does.
    /// </returns>
    /// <remarks>
    /// A name that takes longer than <see cref="_givingUp"/> to judge is
    /// refused rather than passed, which is the one place this is deliberately
    /// stricter than the server: it cannot answer, and answering "acceptable"
    /// because it could not decide is the direction that costs a use.
    /// </remarks>
    public static string? WhyRefused(string? username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return Refused;
        }

        try
        {
            return _allowed.IsMatch(username) ? null : Refused;
        }
        catch (RegexMatchTimeoutException)
        {
            return Refused;
        }
    }
}
