using System;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// What a code looks like, for the assertions that have to ask whether
/// something is shaped like one without knowing which code was minted.
/// </summary>
/// <remarks>
/// The alphabet is written here once. It is the suite's own copy of the one
/// <c>InvitationCode</c> draws from, which is private to that type, and a second
/// copy of it inside a test file is a vocabulary that drifts silently: a
/// narrower copy finds fewer runs and the assertion goes quiet rather than red.
/// </remarks>
internal static class CodeShape
{
    /// <summary>
    /// The characters a code is drawn from.
    /// </summary>
    public const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>
    /// The longest run of characters in a text that are all drawn from the code
    /// alphabet.
    /// </summary>
    /// <param name="text">The text to look through.</param>
    /// <returns>The length of the longest run.</returns>
    public static int LongestRunIn(string text)
    {
        var longest = 0;
        var run = 0;

        foreach (var character in text)
        {
            run = Alphabet.IndexOf(character, StringComparison.Ordinal) < 0 ? 0 : run + 1;
            longest = Math.Max(longest, run);
        }

        return longest;
    }
}
