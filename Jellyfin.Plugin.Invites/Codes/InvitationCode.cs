using System;
using System.Security.Cryptography;

namespace Jellyfin.Plugin.Invites.Codes;

/// <summary>
/// Mints an invitation code and reduces a presented one to its canonical form.
/// </summary>
/// <remarks>
/// <para>
/// The code is the credential. Whoever holds one gets an account, so the whole
/// of what a code is lives here rather than being spread over a generator, a
/// validator and whatever the redemption route decided to do to the string on
/// its way in.
/// </para>
/// <para>
/// <b>The alphabet.</b> Thirty-two characters, one case:
/// <c>0123456789ABCDEFGHJKMNPQRSTVWXYZ</c>. It is the digits and the upper-case
/// letters with <c>I</c>, <c>L</c>, <c>O</c> and <c>U</c> taken out. The first
/// three are the pairs somebody transcribing a code off a screen or reading one
/// down a telephone gets wrong, and <c>U</c> is out so that no minted code can
/// come out as a word an operator has to apologise for. A single-case alphabet
/// is what makes case handling free: there is no lower-case code for an
/// upper-case one to collide with, so accepting either spelling divides nothing.
/// A mixed-case alphabet with a case-insensitive lookup is the trap, because it
/// halves the keyspace per letter while the length still claims the undivided
/// figure.
/// </para>
/// <para>
/// <b>The length and the entropy.</b> Twenty-six characters, each an
/// independent uniform draw over the thirty-two:
/// </para>
/// <code>
/// 26 characters * log2(32) bits a character = 26 * 5 = 130 bits
/// </code>
/// <para>
/// docs/code-entropy.md requires 128 bits and derives it from the live
/// invitation count, the achievable guess rate and a stated margin. 130 clears
/// that with two bits to spare, and it is that page rather than this comment
/// that owns the requirement: a change to the alphabet size or the length is
/// checked against the arithmetic there, not against this number.
/// </para>
/// <para>
/// The draw is per character rather than a base-32 encoding of sixteen random
/// bytes, because thirty-two divides two hundred and fifty-six exactly. Masking
/// a uniform byte to its low five bits is therefore uniform over the alphabet
/// with no rejection step and no bias to argue about, and there are no leftover
/// padding bits at the end of the string to explain.
/// </para>
/// <para>
/// <b>What is not in a code.</b> All 130 bits are random. No mint time, no
/// operator identifier, no prefix, no checksum. Every such field is subtracted
/// from the figure above, and one that reads as metadata to whoever added it
/// reads as a shorter code to whoever is guessing.
/// </para>
/// </remarks>
public static class InvitationCode
{
    /// <summary>
    /// The number of characters in a code, in its canonical form.
    /// </summary>
    public const int Length = 26;

    /// <summary>
    /// The characters a code is drawn from and the only ones a canonical code
    /// contains.
    /// </summary>
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>
    /// Mints a code from the platform's cryptographic random source.
    /// </summary>
    /// <returns>A code in canonical form.</returns>
    public static string Mint()
    {
        Span<byte> draws = stackalloc byte[Length];
        RandomNumberGenerator.Fill(draws);

        Span<char> code = stackalloc char[Length];
        for (var position = 0; position < Length; position++)
        {
            // Thirty-two divides two hundred and fifty-six, so the low five
            // bits of a uniform byte are uniform over the alphabet.
            code[position] = Alphabet[draws[position] & 0x1F];
        }

        return new string(code);
    }

    /// <summary>
    /// Reduces a code as somebody presented it to the one form everything else
    /// uses.
    /// </summary>
    /// <param name="presented">The code as it arrived, or <c>null</c>.</param>
    /// <returns>
    /// The canonical form, or <c>null</c> when what was presented is not a code
    /// at all.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is the only place a code is trimmed, upper-cased or otherwise
    /// altered. Everything downstream, the keyed hash in #29 above all, is
    /// computed over what this returns, so a second place that normalises a
    /// code is a second definition of which codes are equal and the two drift
    /// silently. <c>code-canonicalised-outside-one-function</c> in
    /// .github/lint/invariants.sh refuses the spellings of that mistake.
    /// </para>
    /// <para>
    /// What it does, in order: drops spacing and hyphens, so a code broken into
    /// groups for reading survives being typed back; upper-cases, since the
    /// alphabet has one case; maps the three characters left out of the
    /// alphabet for being confusable onto the ones they are confused with,
    /// <c>I</c> and <c>L</c> to <c>1</c> and <c>O</c> to zero. Anything left
    /// that is not in the alphabet, and any length other than
    /// <see cref="Length"/>, is not a code.
    /// </para>
    /// <para>
    /// It refuses rather than repairs, and it says nothing about whether the
    /// code exists. A caller learns only that what it holds is shaped like a
    /// code; whether one was ever minted is the lookup's answer, and the four
    /// ways that can fail are indistinguishable to whoever asked, in
    /// docs/refusal-response.md.
    /// </para>
    /// </remarks>
    public static string? Canonicalise(string? presented)
    {
        if (presented is null)
        {
            return null;
        }

        Span<char> canonical = stackalloc char[Length];
        var written = 0;

        foreach (var presentedCharacter in presented)
        {
            if (presentedCharacter is ' ' or '\t' or '\r' or '\n' or '-')
            {
                continue;
            }

            var character = char.ToUpperInvariant(presentedCharacter);
            character = character switch
            {
                'I' or 'L' => '1',
                'O' => '0',
                _ => character,
            };

            if (Alphabet.AsSpan().IndexOf(character) < 0 || written == Length)
            {
                return null;
            }

            canonical[written] = character;
            written++;
        }

        return written == Length ? new string(canonical) : null;
    }
}
