using System;
using System.Collections.Immutable;
using System.Globalization;

namespace Jellyfin.Plugin.Invites.Setup;

/// <summary>
/// What this plugin requires of the password an invited person chooses, as one
/// value.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are the plugin's rules and not the server's.</b> #76 asks that the
/// rules be read from the server's own configuration where it has any, and the
/// server line this plugin builds against has none. Neither end of the range in
/// build.yaml carries a member that would express one:
/// </para>
/// <para>
/// <c>grep -acE 'MinimumPasswordLength|PasswordPolicy|PasswordRequirement|PasswordComplexity|MinPasswordLength|PasswordRules'</c>
/// over <c>MediaBrowser.Model.dll</c> and <c>MediaBrowser.Controller.dll</c> at
/// 10.11.0 and 10.11.11 finds nothing. What those assemblies do carry is
/// <c>ChangePassword</c>, which is the path the password is set through and
/// which accepts whatever it is handed.
/// </para>
/// <para>
/// So every rule here is stricter than the server, and the cost of that is
/// stated rather than hidden: a password this plugin refuses is one the server
/// would have taken. docs/password-rules.md is where that trade is argued and
/// where the numbers are read from.
/// </para>
/// <para>
/// <b>One place, and the page is checked against it.</b> The page is an
/// embedded resource served byte for byte, which is #74's decision and the
/// reason nothing a request carries can reach the markup, so the sentences a
/// person reads are text in that file rather than values substituted into it.
/// What keeps the two from drifting is <c>PasswordRulesTests</c>, which requires
/// every statement below to appear in the page ahead of the password field. That
/// is a checked agreement rather than a derivation, and the difference is worth
/// knowing: a rule reworded in the page alone reds the suite, and a rule nobody
/// wrote into either place is invisible to both.
/// </para>
/// <para>
/// <b>What this type does not do.</b> It refuses a password and never sees an
/// account. Validating the post, refusing it without consuming a use and
/// answering the person are #75 and #77, and the message they show comes from
/// here rather than from a second wording beside the route. Nothing here reads a
/// breach list or judges whether a password is a common phrase, which
/// docs/password-rules.md records as the residual rather than leaving to be
/// assumed.
/// </para>
/// </remarks>
public static class PasswordRules
{
    /// <summary>
    /// The fewest characters a password may have.
    /// </summary>
    /// <remarks>
    /// The number is twelve because no composition rule is imposed, so length
    /// carries the whole of it. A rule demanding a digit and a capital buys a
    /// predictable substitution rather than entropy, and the guidance that once
    /// asked for one has withdrawn it. docs/password-rules.md holds the
    /// argument and the residual.
    /// </remarks>
    public const int MinimumLength = 12;

    /// <summary>
    /// The most characters a password may have.
    /// </summary>
    /// <remarks>
    /// A bound on what is handed to the server's hashing path, which does work
    /// proportional to what it is given and is reachable by a stranger. It is
    /// set far above any password a person types, so it refuses a submission
    /// rather than a passphrase.
    /// </remarks>
    public const int MaximumLength = 256;

    /// <summary>
    /// Gets what a person is told when their password is too short.
    /// </summary>
    public static string TooShort { get; } = string.Format(
        CultureInfo.InvariantCulture,
        "A password needs at least {0} characters.",
        MinimumLength);

    /// <summary>
    /// Gets what a person is told when their password is too long.
    /// </summary>
    public static string TooLong { get; } = string.Format(
        CultureInfo.InvariantCulture,
        "A password may be at most {0} characters.",
        MaximumLength);

    /// <summary>
    /// Gets the sentences the page states before the password field, in the
    /// order it states them.
    /// </summary>
    /// <remarks>
    /// The first two are the refusals <see cref="WhyRefused"/> hands back, so a
    /// person reads the same sentence before typing as after being refused. The
    /// third is a promise rather than a refusal, and it is here because it
    /// changes what somebody types: a plugin that trimmed a password would store
    /// something other than what was typed, and the person would find out at the
    /// next sign-in rather than now.
    /// </remarks>
    public static ImmutableArray<string> Statements { get; } =
    [
        TooShort,
        TooLong,
        "Every character counts, spaces included, and nothing is trimmed.",
    ];

    /// <summary>
    /// Says which rule a password misses, or that it misses none.
    /// </summary>
    /// <param name="password">What the person typed, or <c>null</c>.</param>
    /// <returns>
    /// One of <see cref="TooShort"/> or <see cref="TooLong"/>, or <c>null</c>
    /// where the password meets every rule.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The answer is one of two constants and is never built out of what was
    /// typed, so a message this returns cannot carry a password into a response,
    /// a log or a support thread. That is the half of #76's message clause this
    /// type can hold on its own; whether the response shows this sentence and
    /// nothing else is #75's.
    /// </para>
    /// <para>
    /// Length is counted in text elements rather than in UTF-16 units, because
    /// what a person means by a character is what they can delete with one
    /// keystroke. Counting units instead would let a password of six emoji pass
    /// a rule asking for twelve characters.
    /// </para>
    /// </remarks>
    public static string? WhyRefused(string? password)
    {
        var characters = Characters(password);

        if (characters < MinimumLength)
        {
            return TooShort;
        }

        if (characters > MaximumLength)
        {
            return TooLong;
        }

        return null;
    }

    /// <summary>
    /// How many characters a person would say a password has.
    /// </summary>
    /// <param name="password">What the person typed, or <c>null</c>.</param>
    /// <returns>The number of text elements, and zero for nothing at all.</returns>
    public static int Characters(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return 0;
        }

        var counted = 0;
        var elements = StringInfo.GetTextElementEnumerator(password);
        while (elements.MoveNext())
        {
            counted++;
        }

        return counted;
    }
}
