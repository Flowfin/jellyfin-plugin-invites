using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Invites.Setup;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.Invites.Controllers;

/// <summary>
/// Whether the answers a post carries may be acted on, decided on the server
/// and never on the page.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything here is read out of the request alone.</b> Nothing in this type
/// looks at a code, at a record or at the store, which is what lets the post
/// answer a malformed request before any invitation is judged: an answer that
/// depended on the code would tell a caller whether their code was worth
/// anything, one guess at a time. <c>docs/refusal-response.md</c> is where that
/// rule is argued and it is the reason this is a separate judgement rather than
/// a branch inside the redemption.
/// </para>
/// <para>
/// <b>The page enforces none of this and is not trusted to.</b> The form is a
/// public endpoint, so every constraint the page appears to apply is decoration
/// until it is applied here; the page's version is a convenience for somebody
/// who loaded it and nothing more. What the page states above the password field
/// comes from <see cref="PasswordRules.Statements"/>, and what refuses a
/// password here is <see cref="PasswordRules.WhyRefused"/>, so the two cannot
/// drift into stating one rule and enforcing another.
/// </para>
/// <para>
/// <b>The reason is decided and then discarded, and that is a gap rather than a
/// design.</b> A refused answer is answered with the bare bad request this
/// route already gives a post missing a field. Showing the person which rule
/// they missed needs the form served again with the reason on it, which
/// <c>docs/refusal-response.md</c> records as not served by this route.
/// </para>
/// </remarks>
public static class SetupAnswers
{
    /// <summary>
    /// Gets the field names a post may carry, as the model binder matches them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Derived from <see cref="SetupSubmission"/> rather than written out,
    /// because a list typed here is a second authority for what the form asks
    /// and the two would agree only until somebody added a control. What ties
    /// that type to the served page in both directions is
    /// <c>SetupFormInventoryTests.ThePostBindsTheFormsFieldsAndNothingWider</c>,
    /// so a field added to the form and bound by the post is accepted here on
    /// the same change rather than needing a third edit.
    /// </para>
    /// <para>
    /// Lower-cased, and compared ignoring case below, because that is how the
    /// binder matches a form key to a member and a check that matched more
    /// strictly than the binder would refuse posts the binder had already
    /// accepted.
    /// </para>
    /// </remarks>
    public static ImmutableArray<string> Fields { get; } =
    [
        .. typeof(SetupSubmission)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name.ToLowerInvariant())
            .OrderBy(name => name, StringComparer.Ordinal),
    ];

    /// <summary>
    /// Judges the answers a post carries and hands back what the server
    /// accepted, or nothing where it accepted none of it.
    /// </summary>
    /// <param name="submission">What the post bound, or <c>null</c>.</param>
    /// <param name="request">
    /// The request it arrived on, read for the keys it carries and for nothing
    /// else.
    /// </param>
    /// <returns>
    /// The accepted answers where the post may be acted on, and <c>null</c>
    /// where it may not. One refusal for every rule, because the caller answers
    /// all of them with the same bad request and a caller able to tell them
    /// apart learns which rule it missed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The username is judged for the shape the server would accept, by
    /// <see cref="UsernameRules"/>, which is a copy of the server's own
    /// expression rather than a rule invented here. What is NOT judged is
    /// whether the name collides with one the server already holds: answering
    /// that needs a reading of the server's usernames that this plugin has no
    /// seam for, so a colliding name still costs the use before the server
    /// refuses it. That half is #67's and is unmet.
    /// </para>
    /// <para>
    /// The two copies of the password are compared ordinally. A comparison that
    /// folded case or normalised would call two different passwords equal and
    /// let somebody set one they cannot type back.
    /// </para>
    /// </remarks>
    public static AcceptedAnswers? Accept(SetupSubmission? submission, HttpRequest? request)
    {
        if (submission is null
            || string.IsNullOrEmpty(submission.Username)
            || string.IsNullOrEmpty(submission.Password)
            || submission.Confirmation is null)
        {
            return null;
        }

        if (CarriesAFieldTheFormDoesNotDefine(request))
        {
            return null;
        }

        if (PasswordRules.WhyRefused(submission.Password) is not null)
        {
            return null;
        }

        if (!string.Equals(submission.Password, submission.Confirmation, StringComparison.Ordinal))
        {
            return null;
        }

        if (UsernameRules.WhyRefused(submission.Username) is not null)
        {
            return null;
        }

        return new AcceptedAnswers(submission.Username, submission.Password);
    }

    /// <summary>
    /// Says whether a posted body carries a field the form does not define.
    /// </summary>
    /// <param name="request">The request the post arrived on.</param>
    /// <returns>
    /// <c>true</c> where the body carries a key <see cref="Fields"/> does not
    /// name, and <c>false</c> where every key is one the form defines.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Refused rather than ignored, which is the whole point of reading them: a
    /// binder ignores what it cannot place, so a body carrying a field this
    /// plugin never meant to offer looks identical to the form's own. Either it
    /// is a client that has drifted from the page or it is somebody probing what
    /// the endpoint will take, and both are worth answering rather than
    /// absorbing.
    /// </para>
    /// <para>
    /// <b>A request carrying no form is not judged here and does not need to
    /// be.</b> The action binds its answers with <c>FromForm</c>, so a request
    /// the server does not read as a form binds nothing and is already refused
    /// by <see cref="Accept"/> for carrying no username. Reading
    /// <c>Request.Form</c> unconditionally would throw on exactly those
    /// requests, and turning a probe into an exception is a worse answer than
    /// the bad request it would replace.
    /// </para>
    /// </remarks>
    public static bool CarriesAFieldTheFormDoesNotDefine(HttpRequest? request)
    {
        if (request is null || !request.HasFormContentType)
        {
            return false;
        }

        return request.Form.Keys.Any(
            key => !Fields.Contains(key, StringComparer.OrdinalIgnoreCase));
    }
}
