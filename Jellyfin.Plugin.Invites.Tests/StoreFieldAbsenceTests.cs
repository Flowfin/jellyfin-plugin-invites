using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Jellyfin.Plugin.Invites.Storage;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// What a store read does with a record that is missing a member.
/// </summary>
/// <remarks>
/// <para>
/// #93 names the failure exactly: a newer build reads an older store, a member
/// it expects is not there, so it takes the default, and the member that was
/// missing was the revocation. The default is not revoked, so an invitation an
/// operator took away is honoured again by an upgrade.
/// </para>
/// <para>
/// The rule that follows is per member rather than per document. Absence is an
/// error wherever the default would be more permissive than the value it
/// stands in for could have been, and it is not an error where the default
/// loses an invitation instead, which is the direction #53 asks for. Both
/// halves are asserted here, and the second is asserted as a property over
/// every member of the written document rather than as a list somebody keeps
/// up to date: a member added to the stored shape later is removed by the same
/// loop and has to answer the same question.
/// </para>
/// <para>
/// Usable is decided by <see cref="RedemptionDecision.Decide"/> rather than by
/// reading fields here. It is the one routine allowed to judge an expiry or a
/// count, which is #56 and which the invariant lint refuses elsewhere, so a
/// test that judged them itself would be the second authority that rule exists
/// against.
/// </para>
/// <para>
/// Nothing here sleeps and nothing reads the machine clock. Every instant is a
/// literal, and every file is inside a directory the test creates and removes.
/// </para>
/// </remarks>
public class StoreFieldAbsenceTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _now = new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _wellAfterNow = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _wellBeforeNow = new(2026, 5, 2, 12, 0, 0, TimeSpan.Zero);

    private static readonly IInvitationCodeHash _codeHash = new TestCodeHash();

    /// <summary>
    /// A code in the alphabet's own characters and at its own length, so it
    /// canonicalises and a decision reaches the comparison rather than stopping
    /// in front of it.
    /// </summary>
    private const string PresentableCode = "23456789234567892345678923";

    /// <summary>
    /// The three reasons an invitation is unusable, as the decision routine
    /// names them. Each record below is unusable for exactly one of them, so a
    /// removal that undoes that one reason is not covered up by another.
    /// </summary>
    /// <returns>One reason per row.</returns>
    public static IEnumerable<object[]> EveryReasonAnInvitationIsUnusable()
    {
        yield return new object[] { RedemptionOutcome.Revoked };
        yield return new object[] { RedemptionOutcome.Expired };
        yield return new object[] { RedemptionOutcome.Spent };
    }

    /// <summary>
    /// A record that is unusable for one reason and for no other.
    /// </summary>
    /// <param name="reason">Which of the three it is.</param>
    /// <returns>One invitation.</returns>
    private static Invitation ARecordThatIs(RedemptionOutcome reason)
    {
        var canonical = InvitationCode.Canonicalise(PresentableCode);
        Assert.NotNull(canonical);

        return new Invitation(
            id: Guid.Parse("3f5b8c1d-7e2a-4b60-9d34-5a6c7e8f9012"),
            codeHash: _codeHash.Of(canonical),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: _minted,
            expiresAt: reason == RedemptionOutcome.Expired ? _wellBeforeNow : _wellAfterNow,
            usesGranted: 3,
            usesRemaining: reason == RedemptionOutcome.Spent ? 0 : 2,
            revokedAt: reason == RedemptionOutcome.Revoked ? _minted : null,
            revokedBy: reason == RedemptionOutcome.Revoked
                ? Guid.Parse("44445555-6666-7777-8888-99990000aaaa")
                : null,
            templateLabel: "Household",
            template: TestTemplates.Household,
            accountsProduced: ImmutableArray<Guid>.Empty);
    }

    /// <summary>
    /// The members of the one record in a written document, in the order the
    /// writer put them.
    /// </summary>
    /// <param name="whole">The document as it was written.</param>
    /// <returns>The member names.</returns>
    private static string[] MembersOfTheRecordIn(string whole)
    {
        var document = JsonNode.Parse(whole);
        Assert.NotNull(document);

        var record = document!["invitations"]?.AsArray()[0]?.AsObject();
        Assert.NotNull(record);

        return record!.Select(member => member.Key).ToArray();
    }

    /// <summary>
    /// The same document with one member of its one record taken out.
    /// </summary>
    /// <param name="whole">The document as it was written.</param>
    /// <param name="member">The member to remove.</param>
    /// <returns>The document, as text.</returns>
    private static string TheDocumentWithout(string whole, string member)
    {
        var document = JsonNode.Parse(whole);
        var record = document!["invitations"]!.AsArray()[0]!.AsObject();

        Assert.True(record.Remove(member), member + " was not in the record, so removing it proves nothing.");

        return document.ToJsonString();
    }

    /// <summary>
    /// Removing any one member of a stored record either makes the store refuse
    /// the file or leaves the invitation as unusable as it was. It never turns
    /// an invitation nobody may redeem into one somebody may.
    /// </summary>
    /// <remarks>
    /// This is the property #93 asks for, written over the members of the
    /// document rather than over a list of field names. A member added to the
    /// stored shape after this was written is removed by the same loop, so the
    /// question is asked of it whether or not anybody remembers to ask.
    /// </remarks>
    /// <param name="reason">Which of the three reasons the record is unusable for.</param>
    [Theory]
    [MemberData(nameof(EveryReasonAnInvitationIsUnusable))]
    public void RemovingAMemberNeverMakesAnUnusableInvitationUsable(RedemptionOutcome reason)
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write(new[] { ARecordThatIs(reason) });
        var whole = File.ReadAllText(store.Path);

        var members = MembersOfTheRecordIn(whole);
        Assert.NotEmpty(members);

        foreach (var member in members)
        {
            File.WriteAllText(store.Path, TheDocumentWithout(whole, member));

            RedemptionOutcome outcome;
            try
            {
                var contents = store.Read();
                outcome = RedemptionDecision.Decide(PresentableCode, _codeHash, contents.Invitations, _now).Outcome;
            }
            catch (JsonException)
            {
                // The store refused the file, which is the other half of the
                // property. Which members are refused rather than tolerated is
                // asserted by name below.
                continue;
            }
            catch (ArgumentException)
            {
                // The record type refused what the document left behind, for
                // instance a record with no keyed hash to compare against.
                continue;
            }

            Assert.True(
                outcome != RedemptionOutcome.Honoured,
                "Removing \"" + member + "\" from an invitation that was " + reason + " left it "
                + outcome + ", so an absent member granted what the record refused.");
        }
    }

    /// <summary>
    /// A record with no revocation member at all is refused, rather than read as
    /// an invitation nobody revoked.
    /// </summary>
    /// <remarks>
    /// The named half of the property above. This is the exact failure #93 is
    /// written against, and it is asserted per member because the two are
    /// written and read as a pair.
    /// </remarks>
    /// <param name="member">The member the document leaves out.</param>
    [Theory]
    [InlineData("revokedAt")]
    [InlineData("revokedBy")]
    public void ARecordMissingARevocationMemberIsRefused(string member)
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write(new[] { ARecordThatIs(RedemptionOutcome.Revoked) });
        var whole = File.ReadAllText(store.Path);

        File.WriteAllText(store.Path, TheDocumentWithout(whole, member));

        var refused = Assert.Throws<JsonException>(() => store.Read());

        Assert.Contains(member, refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A record carrying neither revocation member is refused, rather than read
    /// as an invitation nobody revoked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the shape #93 is actually about and it is not the one above. An
    /// older build that had no revocation at all wrote neither member, so a
    /// store from it is missing the pair rather than one of the two, and the
    /// pair is where the defence has to be. Taking one member away is caught by
    /// the record type, which refuses a revocation instant with no operator
    /// beside it, and that guard says nothing about a document carrying
    /// neither.
    /// </para>
    /// <para>
    /// The property above removes one member at a time, so it cannot reach this
    /// state. That is the reason this is a case of its own rather than a row in
    /// it: found by taking the marker off one of the two members and watching
    /// the property stay green while the named test went red.
    /// </para>
    /// </remarks>
    [Fact]
    public void RemovingBothRevocationMembersNeverMakesARevokedInvitationUsable()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        store.Write(new[] { ARecordThatIs(RedemptionOutcome.Revoked) });
        var whole = File.ReadAllText(store.Path);

        var withoutTheRevocation = TheDocumentWithout(
            TheDocumentWithout(whole, "revokedAt"),
            "revokedBy");
        File.WriteAllText(store.Path, withoutTheRevocation);

        Assert.DoesNotContain("revoked", withoutTheRevocation, StringComparison.OrdinalIgnoreCase);

        StoreContents contents;
        try
        {
            contents = store.Read();
        }
        catch (JsonException refused)
        {
            // The store refused the file, which is what this build does. The
            // message names the members so an operator meeting it can see which
            // ones are missing rather than being told the file is bad.
            Assert.Contains("revoked", refused.Message, StringComparison.OrdinalIgnoreCase);
            return;
        }

        // Read rather than refused, so the assertion is what the reading is
        // worth: an invitation an operator took away must not come back usable
        // because a file did not mention it.
        var outcome = RedemptionDecision.Decide(PresentableCode, _codeHash, contents.Invitations, _now).Outcome;

        Assert.True(
            outcome != RedemptionOutcome.Honoured,
            "A document carrying neither revocation member was read, and the invitation it produced is "
            + outcome + ". An operator revoked that invitation and the file not mentioning it gave it back.");
    }

    /// <summary>
    /// Every document committed under <c>StoreShapes</c>, by file name.
    /// </summary>
    /// <remarks>
    /// Read off the directory rather than listed here, so a shape committed for
    /// a later store version is run through the property below on the day it
    /// arrives. #105 is where that directory's contents are decided.
    /// </remarks>
    /// <returns>One committed shape per row.</returns>
    public static IEnumerable<object[]> EveryCommittedShape()
    {
        foreach (var path in Directory.GetFiles(TheShapes, "version-*.json"))
        {
            yield return new object[] { Path.GetFileName(path) };
        }
    }

    /// <summary>
    /// The directory the committed documents are copied to beside the test
    /// host.
    /// </summary>
    private static string TheShapes => Path.Combine(AppContext.BaseDirectory, "StoreShapes");

    /// <summary>
    /// A store over a directory the test owns, holding a committed document.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="shape">The committed document, by path under the shapes directory.</param>
    /// <returns>The store.</returns>
    private static InvitationStore AStoreHolding(OwnedDirectory directory, string shape)
    {
        var path = Path.Combine(TheShapes, shape);
        Assert.True(File.Exists(path), path + " is not beside the test host, so this would assert nothing.");

        var store = new InvitationStore(directory.Path);
        Directory.CreateDirectory(directory.Path);
        File.WriteAllText(store.Path, File.ReadAllText(path));

        return store;
    }

    /// <summary>
    /// No record a committed document says is revoked comes back usable.
    /// </summary>
    /// <remarks>
    /// The property #93 asks for, run over the committed shapes rather than
    /// over a document the test wrote a moment earlier. What each record was
    /// before the read is taken from the file rather than from an expectation
    /// written here, so a fixture added later brings its own answer with it.
    /// </remarks>
    /// <param name="shape">Which committed document.</param>
    [Theory]
    [MemberData(nameof(EveryCommittedShape))]
    public void NoRecordACommittedShapeSaysIsRevokedComesBackUsable(string shape)
    {
        using var directory = new OwnedDirectory();
        var store = AStoreHolding(directory, shape);

        var records = JsonNode.Parse(File.ReadAllText(store.Path))!["invitations"]!.AsArray();
        var revokedInTheFile = records
            .Select((record, position) => new { record, position })
            .Where(pair => pair.record!["revokedAt"] is not null)
            .Select(pair => pair.position)
            .ToArray();

        Assert.NotEmpty(revokedInTheFile);

        var read = store.Read().Invitations;
        Assert.Equal(records.Count, read.Length);

        foreach (var position in revokedInTheFile)
        {
            Assert.True(
                read[position].IsRevoked,
                shape + " says the record at position " + position
                + " was revoked, and reading it back produced one that is not.");
        }
    }

    /// <summary>
    /// A committed document in the shape an older build would have written,
    /// with no revocation members at all, is refused rather than migrated
    /// forward into invitations nobody revoked.
    /// </summary>
    /// <remarks>
    /// The fixture #93's done condition asks for, as a file rather than as
    /// bytes a test assembles. It is the committed version one document with
    /// the two revocation members taken out of every record, which is what a
    /// store written before revocation existed would look like. It sits under
    /// <c>damaged</c> so the count of shapes #105 derives from the store's
    /// version is not moved by it.
    /// </remarks>
    [Fact]
    public void ACommittedShapeWithNoRevocationMembersIsRefused()
    {
        using var directory = new OwnedDirectory();
        var store = AStoreHolding(directory, Path.Combine("damaged", "version-1-with-no-revocation.json"));

        Assert.DoesNotContain("revoked", File.ReadAllText(store.Path), StringComparison.OrdinalIgnoreCase);

        var refused = Assert.Throws<JsonException>(() => store.Read());

        Assert.Contains("revoked", refused.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// An invitation nobody revoked still reads, and is still usable.
    /// </summary>
    /// <remarks>
    /// The pair to the test above, and the reason it is here: required means the
    /// member has to be present in the document, not that it has to carry a
    /// value. A rule that refused a null revocation would refuse every live
    /// invitation this plugin has ever written, and it would do it on the first
    /// read after an upgrade rather than in a test.
    /// </remarks>
    [Fact]
    public void ARecordCarryingAnEmptyRevocationStillReadsAndIsUsable()
    {
        using var directory = new OwnedDirectory();
        var store = new InvitationStore(directory.Path);
        var written = ARecordThatIs(RedemptionOutcome.Honoured);
        store.Write(new[] { written });

        var contents = store.Read();

        Assert.Equal(written, Assert.Single(contents.Invitations));
        Assert.Equal(
            RedemptionOutcome.Honoured,
            RedemptionDecision.Decide(PresentableCode, _codeHash, contents.Invitations, _now).Outcome);
    }
}
