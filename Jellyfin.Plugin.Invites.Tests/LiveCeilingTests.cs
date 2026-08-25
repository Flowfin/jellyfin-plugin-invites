using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Jellyfin.Plugin.Invites.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The ceiling on how many invitations may be live at once, from #33.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is asserted is the pair and not the refusal.</b> A test that only
/// drove the ceiling over its limit passes just as well against a routine that
/// refuses everything, and a mint refused one below the ceiling is the failure
/// an operator meets rather than an attacker. Both directions are asserted at
/// the boundary itself, which is where a comparison written with the wrong
/// relation lands.
/// </para>
/// <para>
/// <b>The store is seeded rather than minted into.</b> Five hundred mints are
/// five hundred whole-file writes of a file that grows on each one, and what
/// they would prove is what one written store proves. The records are built
/// through <see cref="InvitationMint"/> so that what is counted is the shape the
/// plugin actually writes.
/// </para>
/// <para>
/// <b>Nothing here compares an expiry or a use count.</b> Whether a record is
/// live is asked of <see cref="RedemptionDecision"/>, which is the one authority
/// for that question and the reason a count at minting can exist at all.
/// </para>
/// </remarks>
public class LiveCeilingTests
{
    private const string Configured = "https://media.example.org";

    private static readonly DateTimeOffset _now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _operator = Guid.Parse("aaaabbbb-cccc-dddd-eeee-ffff00001111");

    /// <summary>
    /// A store holding one invitation fewer than the ceiling still mints, and
    /// the same store at the ceiling refuses.
    /// </summary>
    /// <remarks>
    /// The two halves are one test because the boundary is what is being
    /// asserted. Split into two, a routine comparing with the wrong relation
    /// fails one of them and reads as a single unrelated failure.
    /// </remarks>
    [Fact]
    public void TheCeilingIsMetAndNotCrossed()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var operations = new InvitationOperations(
            new StubStoreDirectory(directory.Path), clock, new StubPublicAddress(Configured));

        Seed(directory.Path, InvitationOperations.LiveCeiling - 1);

        var minted = operations.Mint(_operator, "Household", TimeSpan.FromDays(7), uses: 1);
        Assert.Equal(InvitationOperations.LiveCeiling, operations.All().Length);
        Assert.NotEqual(Guid.Empty, minted.Invitation.Id);

        var refused = Assert.Throws<LiveCeilingReachedException>(
            () => operations.Mint(_operator, "Household", TimeSpan.FromDays(7), uses: 1));

        Assert.Equal(InvitationOperations.LiveCeiling, refused.Live);
        Assert.Equal(InvitationOperations.LiveCeiling, refused.Ceiling);
    }

    /// <summary>
    /// A refused mint writes nothing. The store on the disk afterwards is byte
    /// for byte the store that was there before.
    /// </summary>
    /// <remarks>
    /// This is the half a refusal loses quietly. A routine that wrote the record
    /// and then threw would leave the ceiling exceeded by exactly the invitation
    /// it refused, and the caller would see a refusal either way.
    /// </remarks>
    [Fact]
    public void ARefusedMintWritesNothing()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var operations = new InvitationOperations(
            new StubStoreDirectory(directory.Path), clock, new StubPublicAddress(Configured));

        Seed(directory.Path, InvitationOperations.LiveCeiling);
        var before = File.ReadAllBytes(new InvitationStore(directory.Path).Path);

        Assert.Throws<LiveCeilingReachedException>(
            () => operations.Mint(_operator, "Household", TimeSpan.FromDays(7), uses: 1));

        Assert.Equal(before, File.ReadAllBytes(new InvitationStore(directory.Path).Path));
        Assert.Equal(InvitationOperations.LiveCeiling, operations.All().Length);
    }

    /// <summary>
    /// Revoking one invitation makes room for one more, which is the act the
    /// refusal tells the operator to take.
    /// </summary>
    /// <remarks>
    /// A refusal naming a repair that does not work is worse than one naming
    /// none, so the sentence in the exception is held to the behaviour rather
    /// than only read.
    /// </remarks>
    [Fact]
    public void RevokingOneMakesRoomForOne()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var operations = new InvitationOperations(
            new StubStoreDirectory(directory.Path), clock, new StubPublicAddress(Configured));

        Seed(directory.Path, InvitationOperations.LiveCeiling);
        Assert.Throws<LiveCeilingReachedException>(
            () => operations.Mint(_operator, "Household", TimeSpan.FromDays(7), uses: 1));

        operations.Revoke(operations.All()[0].Id, _operator);

        var minted = operations.Mint(_operator, "Household", TimeSpan.FromDays(7), uses: 1);
        Assert.NotEqual(Guid.Empty, minted.Invitation.Id);
        Assert.Equal(InvitationOperations.LiveCeiling + 1, operations.All().Length);
    }

    /// <summary>
    /// An invitation the clock has passed does not count against the ceiling,
    /// and the record it left stays in the store.
    /// </summary>
    /// <remarks>
    /// This is what separates this ceiling from a bound on the store file. The
    /// count goes down when the clock moves and the file does not get smaller,
    /// which is the entry docs/limits.md holds about expiry not being deletion,
    /// read from the other side.
    /// </remarks>
    [Fact]
    public void AnExpiredInvitationDoesNotCountAgainstTheCeiling()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var operations = new InvitationOperations(
            new StubStoreDirectory(directory.Path), clock, new StubPublicAddress(Configured));

        Seed(directory.Path, InvitationOperations.LiveCeiling);
        Assert.Throws<LiveCeilingReachedException>(
            () => operations.Mint(_operator, "Household", TimeSpan.FromDays(7), uses: 1));

        clock.MoveTo(_now.AddDays(30));

        var minted = operations.Mint(_operator, "Household", TimeSpan.FromDays(7), uses: 1);
        Assert.NotEqual(Guid.Empty, minted.Invitation.Id);
        Assert.Equal(InvitationOperations.LiveCeiling + 1, operations.All().Length);
    }

    /// <summary>
    /// What minting counts and what a presented code is judged by are the same
    /// routine, over every state a record can be in.
    /// </summary>
    /// <remarks>
    /// The rule this holds is that there is one authority for whether an
    /// invitation may still produce an account. A second comparison written
    /// beside the store would pass every other test in this file and would drift
    /// the first time either side moved, so the agreement is asserted directly
    /// rather than inferred from both being right today.
    /// </remarks>
    [Fact]
    public void LivenessIsTheSameQuestionARedemptionAsks()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_now);
        var operations = new InvitationOperations(
            new StubStoreDirectory(directory.Path), clock, new StubPublicAddress(Configured));

        var honoured = operations.Mint(_operator, "Household", TimeSpan.FromDays(7), uses: 1);
        var revoked = operations.Mint(_operator, "Household", TimeSpan.FromDays(7), uses: 1);
        var expired = operations.Mint(_operator, "Household", TimeSpan.FromDays(1), uses: 1);
        operations.Revoke(revoked.Invitation.Id, _operator);

        clock.MoveTo(_now.AddDays(2));

        var records = operations.All();
        var hash = new InvitationCodeHash(HashSecret.OpenOrCreate(directory.Path, records).Value);

        var codes = new[] { honoured.Code, revoked.Code, expired.Code };
        var honouredSeen = 0;

        foreach (var code in codes)
        {
            var verdict = RedemptionDecision.Decide(code, hash, records, clock.UtcNow);
            Assert.NotNull(verdict.Invitation);

            var isHonoured = verdict.Outcome == RedemptionOutcome.Honoured;
            Assert.Equal(isHonoured, RedemptionDecision.IsLive(verdict.Invitation!, clock.UtcNow));

            if (isHonoured)
            {
                honouredSeen++;
            }
        }

        // One of the three is still live, so the agreement above is asserted over
        // both answers rather than over three refusals that would agree with a
        // routine returning false for everything.
        Assert.Equal(1, honouredSeen);
    }

    /// <summary>
    /// The operator meets the ceiling as a conflict carrying the sentence the
    /// operation wrote, rather than as a bad request.
    /// </summary>
    /// <remarks>
    /// The code matters as much as the message. Everything the caller sent was
    /// acceptable, so a 400 would send an operator to look for a fault in their
    /// own request, and what they have to do instead is revoke an invitation.
    /// The message is asserted through the exception's own value rather than by
    /// repeating the sentence here, which would be a second copy of it.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheRouteAnswersTheCeilingWithAConflict()
    {
        using var directory = new OwnedDirectory();
        var controller = new InvitesController(
            new InvitationOperations(
                new StubStoreDirectory(directory.Path),
                new TestClock(_now),
                new StubPublicAddress(Configured)),
            new StubOperatorIdentity(_operator))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        Seed(directory.Path, InvitationOperations.LiveCeiling);

        var answered = Assert.IsType<ConflictObjectResult>(
            (await controller.Mint(new MintRequest { Template = "Household", Uses = 1 })).Result);

        Assert.Equal(StatusCodes.Status409Conflict, answered.StatusCode);
        Assert.Equal(
            new LiveCeilingReachedException(InvitationOperations.LiveCeiling, InvitationOperations.LiveCeiling).Message,
            answered.Value);
        Assert.Equal(InvitationOperations.LiveCeiling, new InvitationStore(directory.Path).Read().Invitations.Length);
    }

    /// <summary>
    /// Writes <paramref name="count"/> live records straight into the store.
    /// </summary>
    /// <param name="directory">The store directory the test owns.</param>
    /// <param name="count">How many records to write.</param>
    private static void Seed(string directory, int count)
    {
        // Made before the records are written, because the store refuses to
        // create a secret for a directory that already holds records: every
        // stored hash was computed under one, and inventing a second would make
        // all of them unmatchable while reporting a healthy start. A store
        // seeded the other way round trips that refusal rather than this
        // ceiling.
        HashSecret.OpenOrCreate(directory, ImmutableArray<Invitation>.Empty);

        var records = ImmutableArray.CreateBuilder<Invitation>(count);

        for (var i = 0; i < count; i++)
        {
            var bytes = new byte[32];
            bytes[0] = (byte)(i & 0xFF);
            bytes[1] = (byte)((i >> 8) & 0xFF);

            records.Add(InvitationMint.Mint(
                id: Guid.NewGuid(),
                codeHash: ImmutableArray.Create(bytes),
                mintedBy: _operator,
                mintedAt: _now,
                expiresAt: _now.AddDays(7),
                uses: 1,
                templateLabel: "Household"));
        }

        new InvitationStore(directory).Write(records.ToImmutable());
    }
}
