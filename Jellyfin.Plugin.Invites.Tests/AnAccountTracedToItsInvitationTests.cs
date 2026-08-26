using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The second direction of #89's view: an account, and the invitations that
/// claim to have created it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing is stored to make this answerable and nothing needs to be.</b> The
/// claim already sits on the record, so the reverse index is the listing walked
/// the other way. That is why this is a route and a shape rather than a change
/// to the store, and it is what lets the question be answered for every record
/// already on disk rather than only for those written after it.
/// </para>
/// <para>
/// <b>No account is created in this file.</b> Nothing in this plugin creates
/// one, and none is needed: what a route answers is a record's claim held
/// against what the server reports, so the records are written with their
/// claims on them and the server's answer is the read seam already in the tree.
/// That is the same arrangement <c>AGoneAccountTests</c> uses and the reason is
/// written there.
/// </para>
/// </remarks>
public class AnAccountTracedToItsInvitationTests
{
    private const string Configured = "https://media.example.org";

    private static readonly DateTimeOffset _now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _operator = Guid.Parse("12341234-5678-5678-9012-901290129012");
    private static readonly Guid _hers = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid _theirs = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid _byHand = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    /// <summary>
    /// An account the plugin created is traced to its invitation, and that
    /// invitation names the account again.
    /// </summary>
    /// <remarks>
    /// Both halves, because the clause in #89 is "traced to its invitation and
    /// back". A route that answered with some record would pass the first half
    /// on its own; asserting that the record it answered with claims the account
    /// that was asked about is what makes the round trip close. The store holds
    /// two records here rather than one, so answering with the whole listing
    /// would fail as loudly as answering with nothing.
    /// </remarks>
    [Fact]
    public void AnAccountIsTracedToItsInvitationAndTheInvitationNamesItBack()
    {
        using var directory = new OwnedDirectory();
        var (hers, theirs) = SeedTwo(directory.Path);
        var controller = ControllerOver(directory, new StubServerAccounts(new[] { _hers, _theirs, _byHand }));

        var found = Assert.Single(Traced(controller, _hers));

        Assert.Equal(hers, found.Id);
        Assert.NotEqual(theirs, found.Id);
        Assert.Contains(_hers, found.AccountsProduced.Select(account => account.Id));
    }

    /// <summary>
    /// An account no record claims answers with an empty list rather than a
    /// not-found.
    /// </summary>
    /// <remarks>
    /// This is the decision on the route rather than an incidental shape. This
    /// plugin puts no mark on an account, so an account it never created reads
    /// exactly like one an operator made by hand, and on a real server most
    /// accounts are the second. A 404 would make the ordinary answer an error
    /// and would leave a caller unable to tell "this plugin did not create it"
    /// from "this route is not there". The status is asserted beside the
    /// emptiness so that a later change to one of the two cannot pass by moving
    /// the other.
    /// </remarks>
    [Fact]
    public void AnAccountNoRecordClaimsAnswersAnEmptyListRatherThanANotFound()
    {
        using var directory = new OwnedDirectory();
        SeedTwo(directory.Path);
        var controller = ControllerOver(directory, new StubServerAccounts(new[] { _hers, _theirs, _byHand }));

        var answer = controller.WhichInvitationsCreated(_byHand);

        var ok = Assert.IsType<OkObjectResult>(answer.Result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<InvitationView>>(ok.Value));
    }

    /// <summary>
    /// Two records claiming one account both come back.
    /// </summary>
    /// <remarks>
    /// A store where two records claim one account is a store disagreeing with
    /// itself, and it is the state an operator asking where an account came from
    /// would most want to see. A route shaped to return one record would have to
    /// choose between them with nothing to choose on, and the operator would be
    /// shown one answer with no sign that there was another.
    /// <c>ConsistencyReport</c> takes the same position on the same data and
    /// says so on itself.
    /// </remarks>
    [Fact]
    public void EveryRecordClaimingOneAccountComesBackRatherThanTheFirst()
    {
        using var directory = new OwnedDirectory();
        var (hers, theirs) = SeedTwo(directory.Path, bothClaim: _hers);
        var controller = ControllerOver(directory, new StubServerAccounts(new[] { _hers, _theirs }));

        Assert.Equal(
            new[] { hers, theirs },
            Traced(controller, _hers).Select(view => view.Id));
    }

    /// <summary>
    /// The record a trace answers with says what became of the account that was
    /// traced.
    /// </summary>
    /// <remarks>
    /// #45's join is on the view rather than on the listing action, so this
    /// route inherits it and there is no second place for it to be forgotten.
    /// It is asserted here anyway, because "inherits it" is a property of how
    /// the view is built today and an action handing back rows with the join
    /// missing is exactly the blank that decision refuses. The account traced
    /// for is one the server no longer has, which is the case where a route
    /// that never asked and a route that asked look different.
    /// </remarks>
    [Fact]
    public void TheTracedRecordSaysWhatBecameOfTheAccount()
    {
        using var directory = new OwnedDirectory();
        SeedTwo(directory.Path);
        var controller = ControllerOver(directory, new StubServerAccounts(new[] { _theirs }));

        var found = Assert.Single(Traced(controller, _hers));

        Assert.Equal(
            AccountPresence.Gone,
            Assert.Single(found.AccountsProduced, account => account.Id == _hers).Presence);
    }

    /// <summary>
    /// Reads the route and asserts nothing about the answer beyond its shape.
    /// </summary>
    /// <param name="controller">The controller under test.</param>
    /// <param name="account">The account to trace.</param>
    /// <returns>The records the route answered with.</returns>
    private static IReadOnlyList<InvitationView> Traced(InvitesController controller, Guid account) =>
        Assert.IsAssignableFrom<IReadOnlyList<InvitationView>>(
            Assert.IsType<OkObjectResult>(controller.WhichInvitationsCreated(account).Result).Value);

    /// <summary>
    /// Writes two records, each claiming one account.
    /// </summary>
    /// <param name="directory">The store directory the test owns.</param>
    /// <param name="bothClaim">
    /// An account to put on the second record as well, for the case where the
    /// store disagrees with itself. Null leaves the two records claiming one
    /// account each.
    /// </param>
    /// <returns>The two record identifiers, in the order they are written.</returns>
    private static (Guid Hers, Guid Theirs) SeedTwo(string directory, Guid? bothClaim = null)
    {
        HashSecret.OpenOrCreate(directory, ImmutableArray<Invitation>.Empty);

        var hers = Guid.NewGuid();
        var theirs = Guid.NewGuid();

        var second = bothClaim is null
            ? ImmutableArray.Create(_theirs)
            : ImmutableArray.Create(_theirs, bothClaim.Value);

        new InvitationStore(directory).Write(
            ImmutableArray.Create(
                Record(hers, "Household", ImmutableArray.Create(_hers)),
                Record(theirs, "Guest", second)));

        return (hers, theirs);
    }

    private static Invitation Record(Guid id, string template, ImmutableArray<Guid> accounts) =>
        new(
            id: id,
            codeHash: ImmutableArray.Create(new byte[32]),
            mintedBy: _operator,
            mintedAt: _now,
            expiresAt: _now.AddDays(7),
            usesGranted: 3,
            usesRemaining: 1,
            revokedAt: null,
            revokedBy: null,
            templateLabel: template,
            accountsProduced: accounts);

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
