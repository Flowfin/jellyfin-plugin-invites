using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Time;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The code is the credential, so these are tests of the credential and not of
/// a string helper. Each one names the mistake it exists to catch.
/// </summary>
public class InvitationCodeTests
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>
    /// The shape a code has. A minted code that was shorter than the length the
    /// entropy calculation asks for, or that carried a character the alphabet
    /// leaves out, would be a weaker credential than docs/code-entropy.md
    /// claims, and nothing outside this test would say so.
    /// </summary>
    [Fact]
    public void AMintedCodeIsTwentySixCharactersFromTheAlphabet()
    {
        var minted = InvitationCode.Mint();

        Assert.Equal(26, InvitationCode.Length);
        Assert.Equal(InvitationCode.Length, minted.Length);
        Assert.All(minted, character => Assert.Contains(character, Alphabet));
    }

    /// <summary>
    /// Every character of the alphabet is reachable. The mask that turns a
    /// random byte into a position is one character away from <c>0x0F</c>,
    /// which would halve the alphabet and cost a bit per character without
    /// changing the length, the shape or any other test here.
    /// </summary>
    [Fact]
    public void EveryCharacterOfTheAlphabetIsMinted()
    {
        var seen = new HashSet<char>();

        for (var mint = 0; mint < 500; mint++)
        {
            foreach (var character in InvitationCode.Mint())
            {
                seen.Add(character);
            }
        }

        Assert.Equal(Alphabet.Length, seen.Count);
        Assert.All(Alphabet, character => Assert.Contains(character, seen));
    }

    /// <summary>
    /// Two codes minted in the same millisecond differ. This is the clause that
    /// refuses a generator derived from the clock: a code built from a
    /// timestamp, or seeded from one, repeats itself for every mint inside the
    /// same tick, and an operator minting a batch would hand two people the
    /// same credential.
    /// </summary>
    /// <remarks>
    /// The millisecond is read through the clock seam rather than off
    /// <c>DateTimeOffset</c>, because the seam is this plugin's only route to
    /// the machine clock and the invariant lint refuses the other one. The test
    /// does not sleep and does not wait for a tick: it mints a batch, groups by
    /// the millisecond each mint was made in, and asserts against a group that
    /// actually holds more than one.
    /// </remarks>
    [Fact]
    public void TwoCodesMintedInTheSameMillisecondDiffer()
    {
        var clock = new SystemClock();
        var minted = new List<(long Millisecond, string Code)>();

        for (var mint = 0; mint < 1000; mint++)
        {
            minted.Add((clock.UtcNow.ToUnixTimeMilliseconds(), InvitationCode.Mint()));
        }

        var withinOneMillisecond = minted
            .GroupBy(entry => entry.Millisecond)
            .Where(group => group.Count() > 1)
            .Select(group => group.Select(entry => entry.Code).ToList())
            .FirstOrDefault();

        Assert.NotNull(withinOneMillisecond);
        Assert.True(
            withinOneMillisecond!.Count > 1,
            "no two of a thousand mints landed in one millisecond, so this test asserted nothing");
        Assert.Equal(withinOneMillisecond.Count, withinOneMillisecond.Distinct(StringComparer.Ordinal).Count());

        var all = minted.Select(entry => entry.Code).ToList();
        Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// A minted code is already in canonical form. If it were not, the code an
    /// operator copies out of the dashboard and the code the store is keyed on
    /// would be different strings, and the invitation would refuse its own
    /// link.
    /// </summary>
    [Fact]
    public void AMintedCodeIsAlreadyCanonical()
    {
        for (var mint = 0; mint < 100; mint++)
        {
            var minted = InvitationCode.Mint();

            Assert.Equal(minted, InvitationCode.Canonicalise(minted));
        }
    }

    /// <summary>
    /// A code read down a telephone and typed back comes out as the code that
    /// was minted. Lower case, hyphens for grouping, and the three characters
    /// the alphabet leaves out because they are confusable are all what the
    /// canonical form absorbs.
    /// </summary>
    [Fact]
    public void ATranscribedCodeCanonicalisesBackToTheMintedOne()
    {
        var minted = InvitationCode.Mint();

        var transcribed = new StringBuilder();
        for (var position = 0; position < minted.Length; position++)
        {
            if (position > 0 && position % 5 == 0)
            {
                transcribed.Append('-');
            }

            transcribed.Append(minted[position] switch
            {
                '1' => 'l',
                '0' => 'O',
                var character => char.ToLowerInvariant(character),
            });
        }

        Assert.Equal(minted, InvitationCode.Canonicalise(transcribed.ToString()));
        Assert.Equal(minted, InvitationCode.Canonicalise("  " + transcribed + "\n"));
    }

    /// <summary>
    /// What is not a code is refused rather than repaired into one. A
    /// canonicalisation that padded, truncated or dropped an unknown character
    /// would turn a typing mistake into a lookup against a different code,
    /// which is a redemption attempt nobody made.
    /// </summary>
    /// <param name="presented">What arrived instead of a code.</param>
    [Theory]
    [InlineData("")]
    [InlineData("----")]
    [InlineData("0123456789ABCDEFGHJKMNPQR")] // one short
    [InlineData("0123456789ABCDEFGHJKMNPQRST")] // one long
    [InlineData("0123456789ABCDEFGHJKMNPQRU")] // U is not in the alphabet
    [InlineData("0123456789ABCDEFGHJKMNPQR?")]
    public void CanonicaliseRefusesWhatIsNotACode(string presented)
    {
        Assert.Null(InvitationCode.Canonicalise(presented));
    }

    /// <summary>
    /// Nothing is presented at all. The redemption route will hand this
    /// whatever a stranger put in a form field, and a missing field is a
    /// refusal rather than an exception in a log.
    /// </summary>
    [Fact]
    public void CanonicaliseRefusesNothingAtAll()
    {
        Assert.Null(InvitationCode.Canonicalise(null));
    }

    /// <summary>
    /// Canonicalising twice gives what canonicalising once gave. The keyed hash
    /// in #29 is computed over this, so a form that moved on a second pass
    /// would key one code under two hashes depending on how many times it went
    /// through.
    /// </summary>
    [Fact]
    public void CanonicaliseIsIdempotent()
    {
        var once = InvitationCode.Canonicalise("Ol234-56789-abcdefghjk-mnpqrs");

        Assert.Equal("0123456789ABCDEFGHJKMNPQRS", once);
        Assert.Equal(once, InvitationCode.Canonicalise(once));
    }
}
