using System;
using System.IO;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Jellyfin.Plugin.Invites.Storage;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The invitation model, end to end, against the real store.
/// </summary>
/// <remarks>
/// <para>
/// #101 lists seven assertions the model owes beyond the happy path. Two of
/// them arrived with the code that made them possible and are not written again
/// here: two mints differ, and canonicalisation is idempotent, both in
/// <c>InvitationCodeTests</c>. Three of the remaining five need a routine that
/// consumes a use, which nothing in this tree has.
/// </para>
/// <para>
/// The two below are the ones the model can answer today. Neither uses a fake:
/// the store is the real one, over a directory the test creates and removes,
/// which is what #101 asks for and what makes the first assertion worth
/// anything at all.
/// </para>
/// </remarks>
public class InvitationModelTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _expires = new(2026, 5, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _now = new(2026, 5, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly IInvitationCodeHash _codeHash = new TestCodeHash();

    /// <summary>
    /// Mints a record for a code, through the routine that mints records.
    /// </summary>
    /// <param name="canonicalCode">The code the record is for.</param>
    /// <param name="templateLabel">The template the operator picked.</param>
    /// <returns>The record, with every use still on it.</returns>
    private static Invitation ARecordFor(string canonicalCode, string templateLabel = "Household")
    {
        return InvitationMint.Mint(
            id: Guid.Parse("5a4b3c2d-1e0f-4a9b-8c7d-6e5f4a3b2c1d"),
            codeHash: _codeHash.Of(canonicalCode),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: _minted,
            expiresAt: _expires,
            uses: 3,
            templateLabel: templateLabel);
    }

    /// <summary>
    /// A code that was minted, stored and read back cannot be recovered from
    /// the file the store wrote.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The record type is asked the same question by
    /// <c>NoMemberOfTheRecordHandsBackSomethingShapedLikeACode</c>, and this is
    /// not that assertion again. That one asks a type for its members; this one
    /// mints a real code, writes a real store and reads the bytes off the disk,
    /// so it also covers everything the writer adds on the way out.
    /// </para>
    /// <para>
    /// The second assertion looks for anything code-shaped rather than for the
    /// code, which is what survives a member being added later that carries a
    /// different one. A code is twenty-six characters of a fixed alphabet, and
    /// the file's own longest run is asserted rather than a substring search,
    /// so the failure says how close the file came.
    /// </para>
    /// </remarks>
    [Fact]
    public void AMintedCodeIsNotRecoverableFromTheStore()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        var code = InvitationCode.Mint();
        var canonical = InvitationCode.Canonicalise(code);
        Assert.NotNull(canonical);

        store.Write(new[] { ARecordFor(canonical!) });

        var whole = File.ReadAllText(store.Path);

        Assert.DoesNotContain(code, whole, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            CodeShape.LongestRunIn(whole) < InvitationCode.Length,
            "The store file carries a run of " + CodeShape.LongestRunIn(whole)
            + " characters of the code alphabet, and a code is " + InvitationCode.Length
            + ". Something in it is shaped like a code.");
    }

    /// <summary>
    /// A code that differs from a minted one by a single character is refused,
    /// and it is refused by the lookup rather than by the parser.
    /// </summary>
    /// <remarks>
    /// This is the assertion in #101's list that reads as covered by
    /// <c>CanonicaliseRefusesWhatIsNotACode</c> and is not. A code that differs
    /// from a minted one by one character is still twenty-six characters of the
    /// alphabet, so it is a perfectly good code and canonicalisation hands it
    /// back. The first assertion below is what keeps this test honest: without
    /// it, a change that made the parser stricter would leave this green while
    /// proving something else entirely.
    /// </remarks>
    [Fact]
    public void ACodeThatDiffersByOneCharacterIsRefused()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        var code = InvitationCode.Mint();
        var canonical = InvitationCode.Canonicalise(code);
        Assert.NotNull(canonical);
        store.Write(new[] { ARecordFor(canonical!) });

        var characters = canonical!.ToCharArray();
        characters[0] = characters[0] == CodeShape.Alphabet[0] ? CodeShape.Alphabet[1] : CodeShape.Alphabet[0];
        var neighbour = new string(characters);

        Assert.Equal(neighbour, InvitationCode.Canonicalise(neighbour));

        var verdict = RedemptionDecision.Decide(
            neighbour,
            _codeHash,
            store.Read().Invitations,
            _now);

        Assert.Equal(RedemptionOutcome.NoSuchInvitation, verdict.Outcome);
        Assert.Null(verdict.Invitation);
    }
}
