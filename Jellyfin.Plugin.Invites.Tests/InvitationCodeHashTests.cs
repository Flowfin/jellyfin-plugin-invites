using System;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Storage;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// What the store holds in place of a code. Every test here names the way the
/// stored value stops being worth what #29 claims for it.
/// </summary>
public class InvitationCodeHashTests
{
    private static readonly ImmutableArray<byte> _key = KeyOf(1);
    private static readonly ImmutableArray<byte> _otherKey = KeyOf(2);

    /// <summary>
    /// The same code under the same key reduces to the same value. A lookup is
    /// an equality on this value, so a hash that varied per call would refuse
    /// every genuine invitation and would do it intermittently.
    /// </summary>
    [Fact]
    public void TheSameCodeUnderTheSameKeyGivesTheSameValue()
    {
        var code = InvitationCode.Mint();
        var hash = new InvitationCodeHash(_key);

        Assert.Equal(hash.Of(code).ToArray(), hash.Of(code).ToArray());
        Assert.Equal(new InvitationCodeHash(_key).Of(code).ToArray(), hash.Of(code).ToArray());
    }

    /// <summary>
    /// The value is thirty-two bytes. The constant is what a caller sizing a
    /// column or a fixture reads, and one that disagreed with what the
    /// construction writes would be a number nobody could trust.
    /// </summary>
    [Fact]
    public void TheValueIsTheLengthTheConstantDeclares()
    {
        var produced = new InvitationCodeHash(_key).Of(InvitationCode.Mint());

        Assert.Equal(32, InvitationCodeHash.Bytes);
        Assert.Equal(InvitationCodeHash.Bytes, produced.Length);
    }

    /// <summary>
    /// It is keyed. This is the property the whole issue is about: an unkeyed
    /// hash of a twenty-six character code is a table lookup for whoever takes
    /// the store, so two servers holding one code have to hold different bytes.
    /// </summary>
    [Fact]
    public void TwoKeysReduceOneCodeToDifferentValues()
    {
        var code = InvitationCode.Mint();

        var under = new InvitationCodeHash(_key).Of(code);
        var underAnother = new InvitationCodeHash(_otherKey).Of(code);

        Assert.NotEqual(under.ToArray(), underAnother.ToArray());
    }

    /// <summary>
    /// It is keyed in the way that matters, which is that the key is not
    /// ignored. A construction that hashed the code alone would pass the test
    /// above only by accident and would pass every other test here, so the
    /// unkeyed value is named and compared against.
    /// </summary>
    [Fact]
    public void TheValueIsNotTheUnkeyedHashOfTheCode()
    {
        var code = InvitationCode.Mint();

        var keyed = new InvitationCodeHash(_key).Of(code);
        var unkeyed = SHA256.HashData(Encoding.ASCII.GetBytes(code));

        Assert.NotEqual(keyed.ToArray(), unkeyed);
    }

    /// <summary>
    /// Two codes one character apart reduce to unrelated values. A lookup keyed
    /// on a prefix, or on anything that carried the code's shape through, would
    /// let somebody who has one code narrow another.
    /// </summary>
    [Fact]
    public void TwoCodesOneCharacterApartGiveDifferentValues()
    {
        var code = InvitationCode.Mint();
        var neighbour = code[..^1] + (code[^1] == 'Z' ? '0' : 'Z');
        var hash = new InvitationCodeHash(_key);

        Assert.NotEqual(code, neighbour);
        Assert.NotEqual(hash.Of(code).ToArray(), hash.Of(neighbour).ToArray());
    }

    /// <summary>
    /// A code that is not already canonical is refused rather than reduced.
    /// Canonicalising here would make this a second answer to which codes are
    /// equal, and the two answers diverge the first time either is edited.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("not a code")]
    public void ACodeThatIsNotCanonicalIsRefused(string presented)
    {
        var hash = new InvitationCodeHash(_key);

        var refusal = Assert.Throws<ArgumentException>(() => hash.Of(presented));
        Assert.Equal("canonicalCode", refusal.ParamName);
    }

    /// <summary>
    /// The lower-case spelling of a real code is refused for the same reason,
    /// and it is the case worth its own line: it is a code, it canonicalises
    /// back to itself in upper case, and reducing it here would silently be the
    /// second normalisation the refusal exists against.
    /// </summary>
    [Fact]
    public void ARealCodeInTheWrongSpellingIsRefused()
    {
        // The local is not called a code on these lines on purpose. The
        // canonicalisation rule in .github/lint/invariants.sh refuses a code
        // cased anywhere outside InvitationCode, and this test exists to hand
        // that exact shape to something else, so it names what it holds
        // differently rather than exempting the rule.
        var minted = InvitationCode.Mint();
        var wrongSpelling = minted.ToLowerInvariant();
        var hash = new InvitationCodeHash(_key);

        Assert.Equal(minted, InvitationCode.Canonicalise(wrongSpelling));
        Assert.Throws<ArgumentException>(() => hash.Of(wrongSpelling));
    }

    /// <summary>
    /// Nothing at all is refused as an argument rather than reduced to the
    /// value of the empty string.
    /// </summary>
    [Fact]
    public void NothingAtAllIsRefused()
    {
        var hash = new InvitationCodeHash(_key);

        Assert.Throws<ArgumentNullException>(() => hash.Of(null!));
    }

    /// <summary>
    /// A key of the wrong length is refused at construction. The case it comes
    /// from is a truncated file, and a short key produces values of the right
    /// size that read exactly like good ones from every direction except an
    /// attack.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(HashSecret.Bytes - 1)]
    [InlineData(HashSecret.Bytes + 1)]
    public void AKeyOfTheWrongLengthIsRefused(int length)
    {
        var wrong = KeyOf(3, length);

        var refusal = Assert.Throws<ArgumentException>(() => new InvitationCodeHash(wrong));
        Assert.Equal("key", refusal.ParamName);
    }

    /// <summary>
    /// An absent key is refused too. A default array is not an empty one, and a
    /// construction that reached into it would throw somewhere further away
    /// than the call that supplied nothing.
    /// </summary>
    [Fact]
    public void AnAbsentKeyIsRefused()
    {
        var refusal = Assert.Throws<ArgumentException>(() => new InvitationCodeHash(default));
        Assert.Equal("key", refusal.ParamName);
    }

    /// <summary>
    /// The key length this refuses to depart from is the one a secret is drawn
    /// at, read from that type rather than copied, so the two cannot drift.
    /// </summary>
    [Fact]
    public void TheKeyLengthIsTheOneASecretIsDrawnAt()
    {
        Assert.Equal(32, HashSecret.Bytes);

        var ofThatLength = KeyOf(4, HashSecret.Bytes);

        Assert.Equal(InvitationCodeHash.Bytes, new InvitationCodeHash(ofThatLength).Of(InvitationCode.Mint()).Length);
    }

    private static ImmutableArray<byte> KeyOf(byte seed, int length = HashSecret.Bytes)
        => [.. Enumerable.Range(0, length).Select(position => (byte)(seed + position))];
}
