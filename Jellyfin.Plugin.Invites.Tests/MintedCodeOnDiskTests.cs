using System;
using System.IO;
using System.Text;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Invitations;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The code exists in one response and nowhere on disk, over the routine the
/// server actually calls.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is not the store test again.</b>
/// <c>InvitationModelTests.AMintedCodeIsNotRecoverableFromTheStore</c> writes a
/// record the test built and reads back the file the store wrote. This calls
/// <c>InvitationOperations.Mint</c>, which is what the minting route calls, and
/// looks at every file the operation left behind rather than at the one the
/// suite knows the name of. The minting operation writes two: the store and the
/// file the hash secret lives in, and only one of those has ever been read for
/// this.
/// </para>
/// <para>
/// <b>What it looks for.</b> Anything shaped like a code, and the code that was
/// minted, in that order of importance. A field added later that carries a
/// different code is caught by the first and not by the second, which is why the
/// first is the one whose failure message says how close the file came.
/// </para>
/// <para>
/// <b>What it does not claim.</b> That nothing anywhere logs a code. Nothing
/// here reads a log, and the greppable half of that is
/// <c>code-or-link-in-a-log-call</c> in the invariant lint. This is the disk.
/// </para>
/// </remarks>
public class MintedCodeOnDiskTests
{
    private static readonly DateTimeOffset _now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid _operator = Guid.Parse("11112222-3333-4444-5555-666677778888");

    /// <summary>
    /// A mint through the operation the route calls leaves no file carrying
    /// anything shaped like a code.
    /// </summary>
    [Fact]
    public void NothingTheMintLeavesOnDiskIsShapedLikeACode()
    {
        using var directory = new OwnedDirectory();
        var operations = new InvitationOperations(
            new StubStoreDirectory(directory.Path),
            new TestClock(_now));

        var minting = operations.Mint(_operator, "Household", validity: null, uses: null);

        Assert.Equal(InvitationCode.Length, InvitationCode.Canonicalise(minting.Code)!.Length);

        var files = Directory.GetFiles(directory.Path, "*", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            // Read as bytes and decode permissively, so a file the store did not
            // write in the encoding this test assumed is still looked at rather
            // than skipped or thrown over.
            var text = Encoding.Latin1.GetString(File.ReadAllBytes(file));
            var longest = CodeShape.LongestRunIn(text);

            Assert.True(
                longest < InvitationCode.Length,
                Path.GetFileName(file) + " carries a run of " + longest
                + " characters of the code alphabet, and a code is " + InvitationCode.Length
                + ". Something the mint wrote is shaped like a code.");

            Assert.DoesNotContain(minting.Code, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// And the mint wrote more than one file, so the assertion above is a
    /// statement about what the operation leaves behind rather than about the
    /// one file the suite already knew to look at.
    /// </summary>
    [Fact]
    public void TheMintLeavesMoreThanTheStoreFileBehind()
    {
        using var directory = new OwnedDirectory();
        var operations = new InvitationOperations(
            new StubStoreDirectory(directory.Path),
            new TestClock(_now));

        operations.Mint(_operator, "Household", validity: null, uses: null);

        Assert.True(
            Directory.GetFiles(directory.Path, "*", SearchOption.AllDirectories).Length > 1,
            "The mint left one file behind. The assertion beside this one is then a second reading of the store file and nothing more.");
    }
}
