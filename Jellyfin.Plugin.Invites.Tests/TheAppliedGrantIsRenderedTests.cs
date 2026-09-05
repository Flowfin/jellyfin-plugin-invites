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
/// <para>
/// The first clause of #94: what this plugin applied to an account is recorded
/// and rendered per account. The recording landed with #61, which put a copy of
/// the grant on the record; what is held here is that an operator asking about
/// an account is handed that copy rather than a name.
/// </para>
/// <para>
/// <b>Why a name is not enough, and this is the whole of the issue's argument.</b>
/// An operator opens this route months later. A name resolved against the
/// configuration at that moment answers what the name means today, which is a
/// different question from what was applied, and the two differ exactly when
/// somebody has edited the template - which is the case the operator is asking
/// about. So the row carries the value.
/// </para>
/// <para>
/// Nothing here creates an account and nothing needs to. The claim sits on the
/// record, and the grant beside it, so what a route answers is read out of the
/// store rather than off a server.
/// </para>
/// </summary>
public class TheAppliedGrantIsRenderedTests
{
    private const string Configured = "https://media.example.org";

    private static readonly DateTimeOffset _now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _operator = Guid.Parse("12341234-5678-5678-9012-901290129012");
    private static readonly Guid _account = Guid.Parse("dddddddd-0000-0000-0000-000000000004");

    /// <summary>
    /// An account traced to its invitation carries the grant that was applied,
    /// field by field, rather than only the template name.
    /// </summary>
    [Fact]
    public void TheAccountRouteRendersTheGrantThatWasApplied()
    {
        using var directory = new OwnedDirectory();
        Seed(directory.Path, TestTemplates.Household);

        var rendered = TheOnlyRowFor(directory, _account);

        Assert.Equal("Household", rendered.Template);
        Assert.NotNull(rendered.Grant);
        AssertTheSameGrant(TestTemplates.Household, rendered.Grant!);
    }

    /// <summary>
    /// And it is the grant on the record rather than the configured template of
    /// the same name. The record here is written with a grant the configured
    /// list does not carry under that name, which is what an operator editing a
    /// template after minting produces, and the row answers with what was
    /// applied.
    /// </summary>
    [Fact]
    public void TheGrantIsTheCopyOnTheRecordAndNotTheConfiguredTemplate()
    {
        using var directory = new OwnedDirectory();
        Seed(directory.Path, TestTemplates.Guest, label: "Household");

        var rendered = TheOnlyRowFor(directory, _account);

        Assert.Equal("Household", rendered.Template);
        AssertTheSameGrant(TestTemplates.Guest, rendered.Grant!);
        Assert.NotEqual(TestTemplates.Household.Libraries.ToArray(), rendered.Grant!.Libraries.ToArray());
    }

    /// <summary>
    /// A record minted before the copy existed renders no grant rather than an
    /// empty one. The store brings such a record forward from its first version
    /// with the grant absent rather than guessing one, and an empty grant would
    /// read as a template somebody wrote that grants nothing, which is a
    /// different fact.
    /// </summary>
    [Fact]
    public void ARecordWithNoCopyRendersNoGrant()
    {
        using var directory = new OwnedDirectory();
        Seed(directory.Path, grant: null);

        var rendered = TheOnlyRowFor(directory, _account);

        Assert.Null(rendered.Grant);
    }

    /// <summary>
    /// Every member of the grant reaches the response. Without this a member
    /// added to the template later is dropped from the answer by a serialiser
    /// nobody asked, and an operator reads a complete-looking row that is short
    /// of the field they came for.
    /// </summary>
    [Fact]
    public void EveryMemberOfTheGrantIsOnTheRenderedRow()
    {
        using var directory = new OwnedDirectory();
        Seed(directory.Path, TestTemplates.Household);

        var rendered = TheOnlyRowFor(directory, _account).Grant;
        var members = typeof(AccountTemplate)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(property => property.Name)
            .ToList();

        Assert.NotEmpty(members);
        foreach (var member in members)
        {
            var property = typeof(AccountTemplate).GetProperty(member);
            Assert.NotNull(property);

            var expected = property!.GetValue(TestTemplates.Household);
            var actual = property.GetValue(rendered);

            // An immutable array compares by the array it wraps rather than by
            // its contents, so a value that survived a round trip through the
            // store is a different instance holding the same members. The two
            // sequences are what this assertion is about.
            if (expected is System.Collections.IEnumerable left && actual is System.Collections.IEnumerable right)
            {
                Assert.Equal(left.Cast<object>().ToArray(), right.Cast<object>().ToArray());
                continue;
            }

            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    /// The row still carries no code and no hash. The grant is a value an
    /// operator may see; the two fields this view exists to be unable to express
    /// are unchanged by it.
    /// </summary>
    [Fact]
    public void TheRowStillCannotCarryACodeOrAHash()
    {
        var named = typeof(InvitationView)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(property => property.Name)
            .ToList();

        Assert.DoesNotContain(named, name => name.Contains("Code", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(named, name => name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertTheSameGrant(AccountTemplate expected, AccountTemplate rendered)
    {
        Assert.Equal(expected.Libraries.ToArray(), rendered.Libraries.ToArray());
        Assert.Equal(expected.MayDownload, rendered.MayDownload);
        Assert.Equal(expected.MayPlayFromOutsideTheNetwork, rendered.MayPlayFromOutsideTheNetwork);
        Assert.Equal(expected.MayManage, rendered.MayManage);
        Assert.Equal(expected.RemoteBitrateCeiling, rendered.RemoteBitrateCeiling);
        Assert.Equal(expected.SimultaneousStreamCeiling, rendered.SimultaneousStreamCeiling);
        Assert.Equal(expected.ParentalRatingCeiling, rendered.ParentalRatingCeiling);
        Assert.Equal(expected.ServerDefaultsLeftAlone.ToArray(), rendered.ServerDefaultsLeftAlone.ToArray());
    }

    private static InvitationView TheOnlyRowFor(OwnedDirectory directory, Guid account)
    {
        var controller = ControllerOver(directory, new StubServerAccounts([account]));
        var answer = Assert.IsType<OkObjectResult>(controller.WhichInvitationsCreated(account).Result);

        return Assert.Single((IReadOnlyList<InvitationView>)answer.Value!);
    }

    private static void Seed(string directory, AccountTemplate? grant, string label = "Household")
    {
        HashSecret.OpenOrCreate(directory, ImmutableArray<Invitation>.Empty);

        new InvitationStore(directory).Write(
            ImmutableArray.Create(
                new Invitation(
                    id: Guid.NewGuid(),
                    codeHash: ImmutableArray.Create(new byte[32]),
                    mintedBy: _operator,
                    mintedAt: _now,
                    expiresAt: _now.AddDays(7),
                    usesGranted: 3,
                    usesRemaining: 1,
                    revokedAt: null,
                    revokedBy: null,
                    templateLabel: label,
                    template: grant,
                    accountsProduced: ImmutableArray.Create(_account))));
    }

    private static InvitesController ControllerOver(OwnedDirectory directory, IServerAccounts accounts)
        => new(
            new InvitationOperations(
                new StubStoreDirectory(directory.Path),
                new TestClock(_now),
                new StubPublicAddress(Configured),
                TestTemplates.AsConfigured),
            new StubOperatorIdentity(_operator),
            accounts)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
}
