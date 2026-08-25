using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// An account an invitation created and the server no longer has, from #45.
/// </summary>
/// <remarks>
/// <para>
/// <b>The decision this holds is that the pointer is kept rather than cleared.</b>
/// Keeping it costs nothing and answers the question an operator is actually
/// asking, which is what happened to the person they invited. It is only worth
/// keeping if the absence is then said out loud, so what is asserted here is
/// not that the record survives - it is that a row says which of its accounts
/// are gone.
/// </para>
/// <para>
/// <b>No account is created anywhere in this file, and none needs to be.</b> The
/// comment this file answers said these tests wait on a seam that can create an
/// account. They do not: what a route renders is a record's claim held against
/// what the server reports, so the record is written with the claim on it and
/// the server's answer is the read seam that is already in the tree. A write
/// seam would let a test arrange the same two values a longer way round.
/// </para>
/// </remarks>
public class AGoneAccountTests
{
    private const string Configured = "https://media.example.org";

    private static readonly DateTimeOffset _now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _operator = Guid.Parse("12341234-5678-5678-9012-901290129012");
    private static readonly Guid _stillThere = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid _deleted = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    /// <summary>
    /// Every route that hands a record back says which of its accounts the
    /// server still has.
    /// </summary>
    /// <remarks>
    /// All three routes rather than the listing alone. A join written into one
    /// action is a join the other two do not have, and an operator who followed
    /// a row to the invitation it names would be shown the blank this decision
    /// refuses.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AClaimedAccountTheServerNoLongerHasRendersAsGone()
    {
        using var directory = new OwnedDirectory();
        var record = Seed(directory.Path);
        var controller = ControllerOver(directory, new StubServerAccounts(new[] { _stillThere }));

        var listed = Assert.IsAssignableFrom<IReadOnlyList<InvitationView>>(
            Assert.IsType<OkObjectResult>(controller.List().Result).Value);
        AssertPresence(Assert.Single(listed));

        AssertPresence(Assert.IsType<InvitationView>(
            Assert.IsType<OkObjectResult>(controller.One(record).Result).Value));

        AssertPresence(Assert.IsType<InvitationView>(
            Assert.IsType<OkObjectResult>((await controller.Revoke(record)).Result).Value));
    }

    /// <summary>
    /// A server that does not report its accounts produces neither answer.
    /// </summary>
    /// <remarks>
    /// This is the case that decides whether the join is safe to have at all.
    /// The seam answers null where the server does not report its accounts in a
    /// shape this plugin knows, and a join that read that as an empty set would
    /// tell an operator that every account the plugin ever created had been
    /// deleted. Silence and deletion are opposite answers and the type has a
    /// value for each.
    /// </remarks>
    [Fact]
    public void AServerThatDoesNotAnswerRendersNeitherPresentNorGone()
    {
        using var directory = new OwnedDirectory();
        Seed(directory.Path);
        var controller = ControllerOver(directory, new StubServerAccounts(null));

        var listed = Assert.IsAssignableFrom<IReadOnlyList<InvitationView>>(
            Assert.IsType<OkObjectResult>(controller.List().Result).Value);

        Assert.All(
            Assert.Single(listed).AccountsProduced,
            account => Assert.Equal(AccountPresence.Unknown, account.Presence));
    }

    /// <summary>
    /// A row is rendered rather than an error, and the record keeps every
    /// pointer it had.
    /// </summary>
    /// <remarks>
    /// The clause is that a missing account renders as a stated outcome rather
    /// than an error, so the absence of an exception is half of it and the list
    /// still being two entries long is the other half. A join that dropped the
    /// entries it could not resolve would satisfy the first half and lose the
    /// audit trail the decision exists to keep.
    /// </remarks>
    [Fact]
    public void TheRecordKeepsThePointerRatherThanLosingIt()
    {
        using var directory = new OwnedDirectory();
        Seed(directory.Path);
        var controller = ControllerOver(directory, new StubServerAccounts(new[] { _stillThere }));

        var view = Assert.Single(
            Assert.IsAssignableFrom<IReadOnlyList<InvitationView>>(
                Assert.IsType<OkObjectResult>(controller.List().Result).Value));

        Assert.Equal(
            new[] { _stillThere, _deleted },
            view.AccountsProduced.Select(account => account.Id));

        var onDisk = new InvitationStore(directory.Path).Read().Invitations;
        Assert.Equal(new[] { _stillThere, _deleted }, Assert.Single(onDisk).AccountsProduced);
    }

    /// <summary>
    /// The other direction: the invitation is gone and the accounts are not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// There is no route that removes a record, so the removal here is made on
    /// the store directly, which is the strongest form of it available: the
    /// record is taken away by something with no opinion about accounts at all,
    /// and the accounts are still there afterwards.
    /// </para>
    /// <para>
    /// What this cannot show is that no code path would have removed one,
    /// because there is no such path to run. That is refused at the capability
    /// by <c>AccountsAreNeverWrittenTests</c> under #91 instead, and the two are
    /// different claims: this one is about the state after a removal, and that
    /// one is about the plugin being unable to reach an account to write.
    /// </para>
    /// </remarks>
    [Fact]
    public void RemovingAnInvitationLeavesTheServersAccountsWhereTheyWere()
    {
        using var directory = new OwnedDirectory();
        var record = Seed(directory.Path);
        var server = new StubServerAccounts(new[] { _stillThere, _deleted });
        var controller = ControllerOver(directory, server);

        new InvitationStore(directory.Path).Write(ImmutableArray<Invitation>.Empty);

        Assert.IsType<NotFoundResult>(controller.One(record).Result);
        Assert.Empty(
            Assert.IsAssignableFrom<IReadOnlyList<InvitationView>>(
                Assert.IsType<OkObjectResult>(controller.List().Result).Value));

        Assert.Equal(new[] { _stillThere, _deleted }, server.Identifiers);
    }

    /// <summary>
    /// The operator's own page says how many of a row's accounts are gone, and
    /// it reads both spellings the server could send.
    /// </summary>
    /// <remarks>
    /// The clause says "anywhere it is displayed", and the page is the other
    /// place. Which spelling of the state arrives is the host's serialiser
    /// rather than this plugin's: nothing this project references decides
    /// whether an enumeration is written as its name or as its number, so the
    /// page reads both and this assertion is what stops one of the two being
    /// dropped by somebody tidying.
    /// </remarks>
    [Fact]
    public void TheOperatorsPageNamesGoneAndReadsBothSpellingsOfIt()
    {
        using var stream = typeof(Plugin).Assembly
            .GetManifestResourceStream("Jellyfin.Plugin.Invites.Configuration.configPage.html");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var page = reader.ReadToEnd();

        // The call site rather than the routine's name. A page that still
        // declares the routine and builds its cell from the length again is the
        // repair somebody makes while tidying, and asserting the declaration
        // alone would pass over it.
        Assert.Contains(
            "invitesAccountsCell(invitation.AccountsProduced)",
            page,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "invitesCell(invitation.AccountsProduced.length)",
            page,
            StringComparison.Ordinal);
        Assert.Contains("\" gone)\"", page, StringComparison.Ordinal);
        Assert.Contains("Presence === \"Gone\"", page, StringComparison.Ordinal);
        Assert.Contains("Presence === 2", page, StringComparison.Ordinal);

        // The number the page reads is the enumeration's own value rather than
        // a constant somebody typed twice, so renaming or reordering the states
        // reddens this rather than leaving the page reading a stale number.
        Assert.Equal(2, (int)AccountPresence.Gone);
    }

    /// <summary>
    /// Writes one record claiming two accounts, one of which the server will be
    /// said to still have.
    /// </summary>
    /// <param name="directory">The store directory the test owns.</param>
    /// <returns>The record's identifier.</returns>
    private static Guid Seed(string directory)
    {
        HashSecret.OpenOrCreate(directory, ImmutableArray<Invitation>.Empty);

        var id = Guid.NewGuid();
        var record = new Invitation(
            id: id,
            codeHash: ImmutableArray.Create(new byte[32]),
            mintedBy: _operator,
            mintedAt: _now,
            expiresAt: _now.AddDays(7),
            usesGranted: 3,
            usesRemaining: 1,
            revokedAt: null,
            revokedBy: null,
            templateLabel: "Household",
            accountsProduced: ImmutableArray.Create(_stillThere, _deleted));

        new InvitationStore(directory).Write(ImmutableArray.Create(record));

        return id;
    }

    private static void AssertPresence(InvitationView view)
    {
        Assert.Collection(
            view.AccountsProduced,
            account =>
            {
                Assert.Equal(_stillThere, account.Id);
                Assert.Equal(AccountPresence.Present, account.Presence);
            },
            account =>
            {
                Assert.Equal(_deleted, account.Id);
                Assert.Equal(AccountPresence.Gone, account.Presence);
            });
    }

    private static InvitesController ControllerOver(OwnedDirectory directory, IServerAccounts accounts)
        => new(
            new InvitationOperations(
                new StubStoreDirectory(directory.Path),
                new TestClock(_now),
                new StubPublicAddress(Configured)),
            new StubOperatorIdentity(_operator),
            accounts)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
}
