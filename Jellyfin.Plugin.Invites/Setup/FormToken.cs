using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.Invites.Setup;

/// <summary>
/// The anti-forgery token the setup form carries, and the rule that decides
/// whether a post carried a good one.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the attack is.</b> The redemption form is unauthenticated, so none of
/// the session-based protections a signed-in route has apply to it. A page on
/// another site can post to this address, and the direction that costs something
/// is the one where an invited person's own browser is made to submit the form:
/// an account is created with a username and a password a stranger chose, from
/// that person's address, spending an invitation the operator meant for
/// somebody else. docs/threat-model.md carries the row.
/// </para>
/// <para>
/// <b>Why it is two halves and not one.</b> The value is written into the page
/// AND into a cookie on the same response, and the post has to carry both and
/// they have to agree. Half of it alone defends nothing, and it is worth being
/// exact about which half is doing the work. A value only in the page is one a
/// stranger reads by fetching the page, which they may do: the route is
/// anonymous. A value only in a cookie is one the browser attaches to a
/// cross-site post by itself, which is the whole mechanism of the attack. What
/// a page on another site cannot do is READ the cookie in order to put its
/// value into the form it forges, and that is the asymmetry the pair rests on.
/// </para>
/// <para>
/// <b>What it is not.</b> This does not stop somebody who holds a code from
/// redeeming it themselves, and it is not meant to: with the code in hand they
/// can post directly and gain nothing they did not already have. The subject
/// here is only the account created through somebody else's browser.
/// </para>
/// <para>
/// <b>Where the value comes from.</b> <see cref="RandomNumberGenerator"/>, for
/// the reason the invitation code uses it: a token a caller can predict is a
/// token they can put in a forged form. It is rendered as lower-case hexadecimal
/// so that <see cref="IsWellFormed"/> can be an alphabet test, which is what
/// lets the value be written into markup at all. See <see cref="SetupPage.For"/>
/// for the other end of that argument.
/// </para>
/// <para>
/// <b>The comparison is constant time</b> and reaches
/// <see cref="CryptographicOperations.FixedTimeEquals"/> rather than any of the
/// string comparisons, all of which return at the first differing character and
/// hand a caller measuring the response the prefix they have right.
/// <c>secret-compared-with-equality</c> and its two neighbours in
/// <c>.github/lint/invariants.sh</c> already name a variable spelled like this
/// one, so the three spellings of that mistake are refused here by rules that
/// landed before this file did.
/// </para>
/// </remarks>
public static class FormToken
{
    /// <summary>
    /// The name of the hidden control on the form and of the member the post
    /// binds it into. One constant, because a form control and a bound member
    /// that disagree are a token nothing ever receives.
    /// </summary>
    public const string Field = "token";

    /// <summary>
    /// The name of the cookie carrying the other half.
    /// </summary>
    /// <remarks>
    /// No <c>__Host-</c> prefix. That prefix obliges a cookie to be sent only
    /// over a secure connection and to be scoped to the whole site, and this one
    /// is deliberately scoped to the redemption route instead, so the prefix
    /// could not be honoured without giving up the narrower scope.
    /// </remarks>
    public const string CookieName = "invites_setup_token";

    /// <summary>
    /// How many characters a token is, which is 32 random bytes written out in
    /// hexadecimal.
    /// </summary>
    public const int Length = 64;

    private const int Bytes = Length / 2;

    /// <summary>
    /// Mints a token nobody can predict.
    /// </summary>
    /// <returns>
    /// <see cref="Length"/> lower-case hexadecimal characters, from
    /// <see cref="Bytes"/> bytes of a cryptographic source.
    /// </returns>
    public static string Fresh()
    {
        return Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(Bytes));
    }

    /// <summary>
    /// Says whether a value is shaped like a token this plugin minted.
    /// </summary>
    /// <param name="value">Whatever arrived, or <c>null</c>.</param>
    /// <returns>
    /// <c>true</c> where the value is exactly <see cref="Length"/> characters
    /// and every one of them is a lower-case hexadecimal digit.
    /// </returns>
    /// <remarks>
    /// It is an alphabet test rather than a length test, and both halves matter
    /// for different reasons. The length is what stops a short value being
    /// compared against a prefix of a real one. The alphabet is what makes the
    /// value safe to write into the page: a string of hexadecimal digits carries
    /// no character HTML gives a meaning to, so there is nothing to escape and
    /// nothing an escape could be got wrong about.
    /// </remarks>
    public static bool IsWellFormed(string? value)
    {
        if (value is null || value.Length != Length)
        {
            return false;
        }

        foreach (var character in value)
        {
            var hexadecimal = (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f');
            if (!hexadecimal)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The options the cookie is written under.
    /// </summary>
    /// <param name="secure">
    /// Whether the request that is being answered arrived over a secure
    /// connection.
    /// </param>
    /// <returns>The options.</returns>
    /// <remarks>
    /// <para>
    /// <b>The secure flag follows the connection rather than being set to
    /// true.</b> A cookie marked secure is not sent back over a plain
    /// connection at all, so a server reached over HTTP would mint a token,
    /// never receive the cookie again and refuse every post, which is the flow
    /// broken rather than defended. Following the scheme is stated plainly
    /// rather than hidden: on a server reached over HTTP the cookie travels in
    /// clear, and so does the password on the same form, so this flag is not
    /// what that deployment is short of.
    /// </para>
    /// <para>
    /// <b>Strict rather than lax.</b> A cross-site post is exactly the request
    /// this cookie must not accompany, and lax attaches a cookie to a top-level
    /// navigation, which a form submission from another site can be. So the
    /// cookie is not sent at all in the case being defended against, and the
    /// comparison below then fails for want of a cookie. That ordering is worth
    /// reading: on a browser that honours it the attack stops one step earlier
    /// than the token, and the token is what holds when it does not.
    /// </para>
    /// <para>
    /// <b>Scoped to the redemption route and to no other path</b>, so this
    /// plugin's cookie is not attached to every request a person makes to their
    /// media server. It is a session cookie with no expiry, because it is worth
    /// exactly one page view.
    /// </para>
    /// </remarks>
    public static CookieOptions OptionsFor(bool secure)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Path = "/" + Invitations.InvitationLink.Segment,
        };
    }

    /// <summary>
    /// Says whether a post carried a token and the cookie that goes with it.
    /// </summary>
    /// <param name="request">The request the post arrived on.</param>
    /// <param name="presentedToken">What the form field carried, or <c>null</c>.</param>
    /// <returns>
    /// <c>true</c> only where both halves arrived, both are well formed, and
    /// they are the same value.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Read out of the request alone. Nothing here looks at a code, at a record
    /// or at the store, which is what lets the post answer a forged submission
    /// before any invitation is judged and therefore without disclosing whether
    /// the code was worth anything.
    /// </para>
    /// <para>
    /// <b>Both names carry the word the invariant lint matches on, and that is
    /// deliberate rather than a style.</b> The three rules in
    /// <c>.github/lint/invariants.sh</c> that refuse an early-returning
    /// comparison of a secret match a SPELLING: a variable whose name carries
    /// <c>secret</c>, <c>token</c> or <c>hash</c> beside <c>==</c>,
    /// <c>.Equals</c>, <c>.SequenceEqual</c> or a comparer. Called anything else
    /// these two would walk straight through all three, which was measured
    /// rather than supposed. Naming them so the rule can see them is what makes
    /// the guard reach this file at all.
    /// </para>
    /// </remarks>
    public static bool Accompanies(HttpRequest? request, string? presentedToken)
    {
        if (request is null || !IsWellFormed(presentedToken))
        {
            return false;
        }

        var heldToken = request.Cookies[CookieName];
        if (!IsWellFormed(heldToken))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(heldToken!),
            Encoding.UTF8.GetBytes(presentedToken!));
    }
}
