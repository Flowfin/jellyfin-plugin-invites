using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Jellyfin.Plugin.Invites.Setup;
using Jellyfin.Plugin.Invites.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The post that receives the setup form, driven at the route.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is where the single refusal is compared, and it is compared once.</b>
/// docs/refusal-response.md says the byte-for-byte comparison lives at the route
/// level because that is the only place that sees the response the server
/// actually sends, and that writing it four times produces four tests that drift
/// apart. So one assertion below walks every case this route serves and compares
/// them against each other on the whole list that page names: the status code,
/// the body, the content type, and every header the route sets. #107 adds cases
/// to that assertion rather than writing a second one.
/// </para>
/// <para>
/// <b>The store is the real one, in a directory the test owns.</b> A fake here
/// would prove that the fake round-trips, and what these are about is what is on
/// disk after a request: whether a use was taken, and whether the account was
/// written onto the record. The write seam over the server's user table is a
/// stand-in, because there is no server.
/// </para>
/// <para>
/// <b>No web host.</b> The controller is an ordinary object and the response is
/// a context the test owns, which is the headless rule rather than a shortcut.
/// The bound is the same one <see cref="SetupPageTests"/> records: nothing here
/// says what a server's own pipeline does with a plugin route, no request has
/// crossed a socket, and no browser has rendered any of these bytes.
/// </para>
/// </remarks>
public class RedeemPostTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A honoured code creates the account, takes the use, records the account
    /// on the invitation, and sends the person to the completion address.
    /// </summary>
    /// <remarks>
    /// The four are one assertion rather than four, because each of them alone
    /// passes for an implementation that is wrong in a way the other three
    /// catch: an account created with no use taken is an invitation that works
    /// again, a use taken with no account is a link that was spent for nothing,
    /// and a record that does not claim the account is an operator who cannot
    /// answer where it came from.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AHonouredCodeCreatesTheAccountAndSpendsTheUse()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 2);
        var seam = new ARecordingWriteSeam();
        var context = RedeemRoute.Request();
        var controller = RedeemRoute.Over(directory.Path, clock, seam, context);

        var answer = await controller.Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        var redirect = Assert.IsType<StatusCodeResult>(answer);
        Assert.Equal(StatusCodes.Status303SeeOther, redirect.StatusCode);
        Assert.Equal("/redeem/done", context.Response.Headers.Location.ToString());

        Assert.Equal(
            [
                "create newcomer",
                string.Format(CultureInfo.InvariantCulture, "credential {0} {1}", seam.Answers, "a password long enough".Length),
            ],
            seam.Asked.Take(2));

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(1, stored.UsesRemaining);
        Assert.Equal(2, stored.UsesGranted);
        Assert.Equal(seam.Answers, Assert.Single(stored.AccountsProduced));
        Assert.Equal(minted.Invitation.Id, stored.Id);
    }

    /// <summary>
    /// The grant handed to the creation routine is the copy on the record, and
    /// not a template looked up by name at redemption.
    /// </summary>
    /// <remarks>
    /// #61's whole rule is that editing a configured template changes the next
    /// invitation and not a live one, and the place that rule is broken is this
    /// route: whoever writes it has the label in hand and needs a grant. So the
    /// configured list is moved between the mint and the post, and what the
    /// account is created from has to be what was minted.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheGrantAppliedIsTheCopyOnTheRecord()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();
        var context = RedeemRoute.Request();
        var controller = RedeemRoute.Over(directory.Path, clock, seam, context);

        await controller.Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(minted.Invitation.Template, seam.AppliedTemplate);
        Assert.Equal(TestTemplates.Household, seam.AppliedTemplate);
    }

    /// <summary>
    /// A code no record matches is refused, and the refusal is the page
    /// docs/refusal-response.md fixes under the status this route picked.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ACodeNoRecordMatchesIsRefusedWithTheOnePage()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();
        var context = RedeemRoute.Request();
        var controller = RedeemRoute.Over(directory.Path, clock, seam, context);

        var answer = await controller.Submit("not-a-real-code", RedeemRoute.Filled("newcomer", "a password long enough"));

        var refusal = Assert.IsType<ContentResult>(answer);
        Assert.Equal(StatusCodes.Status403Forbidden, refusal.StatusCode);
        Assert.Equal(RefusalPage.Html, refusal.Content);
        Assert.Equal(RefusalPage.ContentType, refusal.ContentType);
        Assert.Empty(seam.Asked);
    }

    /// <summary>
    /// Every case this route refuses answers with the same response, compared on
    /// everything docs/refusal-response.md says identical covers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The six cases are driven through the action rather than constructed, so
    /// what is compared is the response the route produces and not a value a
    /// test assembled. A stranger able to tell any two of these apart can ask
    /// this route which codes exist, one guess at a time.
    /// </para>
    /// <para>
    /// IT WAS FIVE AND IT IS SIX. The ceiling on how many accounts the plugin may
    /// create in a window is refused by something now, so the last row of
    /// docs/refusal-response.md's table has a response to compare. That case is
    /// the one that would most obviously deserve its own message and most
    /// obviously must not have one: a page saying the server has created too many
    /// accounts today tells a stranger something true about the server they had
    /// no other way to learn, while refusing them.
    /// </para>
    /// <para>
    /// What this cannot assert is timing, and docs/refusal-response.md says so
    /// in its own words: a test that measured durations on a shared runner would
    /// measure the runner. The plugin does not defend against an attacker who
    /// can measure a store hit against a store miss.
    /// </para>
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task EveryRefusalThisRouteServesIsTheSameResponse()
    {
        var answers = new List<(string Case, string Compared)>();
        foreach (var refusal in await RefusalsAsync().ConfigureAwait(true))
        {
            answers.Add(refusal);
        }

        Assert.True(answers.Count >= 6, "Fewer cases were driven than this route serves: " + answers.Count);

        var first = answers[0];
        var differing = answers
            .Where(answer => !string.Equals(answer.Compared, first.Compared, StringComparison.Ordinal))
            .Select(answer => answer.Case)
            .ToList();

        Assert.True(
            differing.Count == 0,
            "These refusals do not answer with the same response as "
            + first.Case
            + ": "
            + string.Join(", ", differing)
            + ". docs/refusal-response.md fixes the status code, the body, the content type and every header this route sets as one response for every case, because a caller able to tell two of them apart can ask this route which codes exist.");
    }

    /// <summary>
    /// A post that does not carry the fields the form defines is answered out of
    /// the request alone: nothing is looked up and no attempt is counted.
    /// </summary>
    /// <param name="username">What the post carried as the username.</param>
    /// <param name="password">What it carried as the password.</param>
    /// <param name="confirmation">What it carried as the confirmation.</param>
    /// <returns>Nothing a caller reads.</returns>
    [Theory]
    [InlineData(null, "a password long enough", "a password long enough")]
    [InlineData("newcomer", null, "a password long enough")]
    [InlineData("newcomer", "a password long enough", null)]
    [InlineData("", "a password long enough", "a password long enough")]
    public async Task APostMissingAFieldIsAnsweredWithoutJudgingTheCode(
        string? username,
        string? password,
        string? confirmation)
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();
        var limiter = new AttemptLimiter(clock);
        var controller = RedeemRoute.Over(directory.Path, clock, limiter, seam, RedeemRoute.Request());

        var answer = await controller.Submit(
            minted.Code,
            new SetupSubmission { Username = username, Password = password, Confirmation = confirmation });

        Assert.IsType<BadRequestResult>(answer);
        Assert.Equal(0, limiter.AddressesHeld);
        Assert.Empty(seam.Asked);
        Assert.Equal(1, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);
    }

    /// <summary>
    /// A post whose answers the server refuses is answered out of the request
    /// alone: nothing is looked up, no attempt is counted and the use stands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The page states the password rules above the field and compares the two
    /// copies as they are typed, and a caller that never loaded the page does
    /// neither. So both cases below are ones no browser produces and every
    /// client that skips the page can: what refuses them has to be the server.
    /// </para>
    /// <para>
    /// The invitation is live and its code is right, so the only thing that can
    /// leave the use standing is the answers having been judged before the code
    /// was. Which rule refused which case is asserted in
    /// <c>SetupAnswersTests</c> rather than here.
    /// </para>
    /// </remarks>
    /// <param name="password">What the post carried as the password.</param>
    /// <param name="confirmation">What it carried as the confirmation.</param>
    /// <returns>Nothing a caller reads.</returns>
    [Theory]
    [InlineData("a password long enough", "a different password")]
    [InlineData("a password long enough", "A password long enough")]
    [InlineData("short", "short")]
    public async Task APostWhoseAnswersAreRefusedIsAnsweredWithoutJudgingTheCode(
        string password,
        string confirmation)
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();
        var limiter = new AttemptLimiter(clock);
        var controller = RedeemRoute.Over(directory.Path, clock, limiter, seam, RedeemRoute.Request());

        var answer = await controller.Submit(
            minted.Code,
            new SetupSubmission { Username = "newcomer", Password = password, Confirmation = confirmation });

        Assert.IsType<BadRequestResult>(answer);
        Assert.Equal(0, limiter.AddressesHeld);
        Assert.Empty(seam.Asked);
        Assert.Equal(1, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);
    }

    /// <summary>
    /// A body crafted with fields the form does not define creates no account,
    /// takes no use and leaves the record exactly as it was.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the crafted post #75 asks for, and the three extra fields are
    /// chosen to be the ones that would matter if any of them were read: two
    /// name members of the grant an account is created with and one names the
    /// template itself. Nothing in the plugin binds them, so a route that
    /// ignored an unexpected field rather than refusing it would create an
    /// ordinary account here and every other assertion in this file would still
    /// pass.
    /// </para>
    /// <para>
    /// What the assertions read is the store and the write seam rather than the
    /// response, because the property is that the request changed nothing: the
    /// use is unspent, the record claims no account, and the seam was never
    /// asked to write one. An account whose grant was steered by a posted field
    /// would show up as a call on the seam, and there are none.
    /// </para>
    /// <para>
    /// The bound is that the body is a dictionary the test builds rather than
    /// bytes a client sent. No request has crossed a socket in this file and
    /// nothing here says what a server's own pipeline does with a body it reads
    /// off the wire.
    /// </para>
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ACraftedBodyCarryingExtraFieldsLeavesTheInvitationUntouched()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();
        var limiter = new AttemptLimiter(clock);

        var body = RedeemRoute.Body("newcomer", "a password long enough");
        body["maymanage"] = "true";
        body["libraries"] = "every one of them";
        body["template"] = "Household";

        var controller = RedeemRoute.Over(
            directory.Path,
            clock,
            limiter,
            seam,
            RedeemRoute.Posting(body));

        var answer = await controller.Submit(
            minted.Code,
            RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.IsType<BadRequestResult>(answer);
        Assert.Equal(0, limiter.AddressesHeld);
        Assert.Empty(seam.Asked);
        Assert.Null(seam.AppliedTemplate);

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(1, stored.UsesRemaining);
        Assert.Empty(stored.AccountsProduced);
    }

    /// <summary>
    /// A name the server would refuse for its shape leaves the invitation
    /// exactly as it was: no use taken, no account, nothing asked of the server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is #67's first clause and it is the reason the rule is copied into
    /// this plugin at all. The server refuses such a name too, inside the call
    /// that creates the account, which is after the use has been taken: the
    /// reservation spends first and the creation throws afterwards, so the
    /// person is left with a link that is gone and no account, and a fresh mint
    /// is the only way back. Applying the rule before the reservation is what
    /// turns that into a refusal that costs nothing.
    /// </para>
    /// <para>
    /// The three names are the shapes a person produces by accident rather than
    /// on purpose: a name pasted with a space on the end, one carrying a
    /// separator the server does not take, and one that is only whitespace.
    /// </para>
    /// </remarks>
    /// <param name="username">A name the server's expression refuses.</param>
    /// <returns>Nothing a caller reads.</returns>
    [Theory]
    [InlineData("ada ")]
    [InlineData("ada/lovelace")]
    [InlineData("   ")]
    public async Task ANameTheServerWouldRefuseCostsNoUse(string username)
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();
        var limiter = new AttemptLimiter(clock);
        var controller = RedeemRoute.Over(directory.Path, clock, limiter, seam, RedeemRoute.Request());

        var answer = await controller.Submit(
            minted.Code,
            RedeemRoute.Filled(username, "a password long enough"));

        Assert.IsType<BadRequestResult>(answer);
        Assert.Equal(0, limiter.AddressesHeld);
        Assert.Empty(seam.Asked);

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(1, stored.UsesRemaining);
        Assert.Empty(stored.AccountsProduced);
    }

    /// <summary>
    /// A name the server accepts is not refused here, including the three the
    /// server's own message forgets to mention.
    /// </summary>
    /// <remarks>
    /// The half of the copy that is easy to leave untested. A rule that refused
    /// everything would satisfy the theory above and every other assertion about
    /// a refusal in this file, and the person who could not use their link would
    /// be the one who found out. The redemption is driven to its end rather than
    /// only to its answer, so what is asserted is that the account was created
    /// with the name as it was typed and not with some altered form of it, which
    /// is this issue's clause that no name is ever silently changed.
    /// </remarks>
    /// <param name="username">A name the server's expression accepts.</param>
    /// <returns>Nothing a caller reads.</returns>
    [Theory]
    [InlineData("Ada Lovelace")]
    [InlineData("ada@example.org")]
    [InlineData("ada+guest")]
    [InlineData("O'Brien")]
    public async Task ANameTheServerAcceptsIsCreatedExactlyAsItWasTyped(string username)
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();
        var context = RedeemRoute.Request();
        var controller = RedeemRoute.Over(directory.Path, clock, seam, context);

        var answer = await controller.Submit(
            minted.Code,
            RedeemRoute.Filled(username, "a password long enough"));

        Assert.Equal(StatusCodes.Status303SeeOther, Assert.IsType<StatusCodeResult>(answer).StatusCode);
        Assert.Equal("create " + username, seam.Asked[0]);
        Assert.Equal(0, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);
    }

    /// <summary>
    /// The limiter is asked before the code is judged, so an attempt over the
    /// threshold takes no use off a live invitation.
    /// </summary>
    /// <remarks>
    /// The threshold is crossed by driving the limiter rather than by waiting,
    /// which is the clock seam doing what it exists for. The invitation is live
    /// and its code is right, so the only thing that can refuse this post is the
    /// limiter, and the only thing that can leave the use standing is the
    /// limiter having been asked first.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AnAttemptOverTheLimitIsRefusedBeforeTheCodeIsJudged()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();
        var limiter = new AttemptLimiter(clock);
        var controller = RedeemRoute.Over(directory.Path, clock, limiter, seam, RedeemRoute.Request());

        for (var spent = 0; spent < AttemptLimiter.GlobalCeiling; spent++)
        {
            Assert.True(limiter.MayJudge("somebody else"));
        }

        var answer = await controller.Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(answer).StatusCode);
        Assert.Empty(seam.Asked);
        Assert.Equal(1, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);
    }

    /// <summary>
    /// A redemption refused by the ceiling on accounts leaves the invitation
    /// exactly as it was.
    /// </summary>
    /// <remarks>
    /// The ceiling is asked before the use is taken, so a person who meets it
    /// can follow the same link again once the window turns. An invitation spent
    /// against a ceiling would cost the operator a fresh mint for a refusal that
    /// had nothing to do with the person holding the link, and it would be spent
    /// silently, because the response says nothing about which case refused it.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ARedemptionOverTheCeilingTakesNoUseAndCreatesNothing()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var ceiling = new CreationCeiling(clock);
        var seam = new ARecordingWriteSeam();

        for (var created = 0; created < CreationCeiling.AccountsInAWindow; created++)
        {
            Assert.True(ceiling.MayCreate());
        }

        var refused = await RedeemRoute
            .Over(directory.Path, clock, ceiling, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(refused).StatusCode);
        Assert.Empty(seam.Asked);
        Assert.Equal(1, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);

        // The window turns and the same link works, which is what "leaves the
        // invitation exactly as it was" has to mean to the person holding it.
        clock.Advance(CreationCeiling.Window);
        var honoured = await RedeemRoute
            .Over(directory.Path, clock, ceiling, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status303SeeOther, Assert.IsType<StatusCodeResult>(honoured).StatusCode);
        Assert.Equal(0, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);
    }

    /// <summary>
    /// A request the server cannot place is refused rather than judged
    /// uncounted.
    /// </summary>
    /// <remarks>
    /// The limiter refuses to count an attempt naming no address, because
    /// counting one as everybody would let a caller with no address spend the
    /// allowance of every caller that has one. The alternative to refusing here
    /// is judging a presented code outside the limit altogether, which is the
    /// one route on this server where free guesses matter.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ARequestWithNoSourceAddressIsRefused()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();
        var controller = RedeemRoute.Over(directory.Path, clock, seam, RedeemRoute.WithoutAnAddress());

        var answer = await controller.Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(answer).StatusCode);
        Assert.Empty(seam.Asked);
        Assert.Equal(1, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);
    }

    /// <summary>
    /// A single-use invitation produces one account and refuses the second post,
    /// which is the whole of what a use count is for.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ASingleUseInvitationIsRefusedTheSecondTime()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();

        var first = await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("first", "a password long enough"));
        var second = await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("second", "a password long enough"));

        Assert.Equal(StatusCodes.Status303SeeOther, Assert.IsType<StatusCodeResult>(first).StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(second).StatusCode);
        Assert.Equal(0, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);
        Assert.Equal(1, seam.Asked.Count(asked => asked.StartsWith("create ", StringComparison.Ordinal)));
    }

    /// <summary>
    /// A server that refuses the write leaves the use taken and the person with
    /// the same refusal as everybody else.
    /// </summary>
    /// <remarks>
    /// This is the fail-closed direction stated as an assertion rather than as a
    /// sentence in a comment. The other direction, giving the use back, is a
    /// route that hands an attacker a way to make a write fail and keep trying.
    /// Telling a taken username from a server that refused for another reason is
    /// #67's and is not done here, which is why both arrive as this response.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AServerThatRefusesTheWriteLeavesTheUseTaken()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam
        {
            CredentialRefusal = new ServerAccountWriteRefusedException("the server refused"),
        };
        var controller = RedeemRoute.Over(directory.Path, clock, seam, RedeemRoute.Request());

        var answer = await controller.Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(answer).StatusCode);

        var stored = Assert.Single(new InvitationStore(directory.Path).Read().Invitations);
        Assert.Equal(0, stored.UsesRemaining);
        Assert.Empty(stored.AccountsProduced);
    }

    /// <summary>
    /// A record carrying no grant creates nothing and keeps its count.
    /// </summary>
    /// <remarks>
    /// That is <see cref="Invitation.Template"/>'s own sentence about a version
    /// one record read forward, driven at the route rather than restated: it
    /// keeps its name, keeps its count and can create nothing. The tempting
    /// alternative is to look the label up now, which would make an edit to a
    /// configured template change what a live invitation already grants, and it
    /// is exactly what somebody holding a label and needing a grant would write.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ARecordWithNoGrantCreatesNothingAndKeepsItsCount()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);

        // The document as it would have been written before the grant was
        // copied onto a record: the same record, the same keyed hash, and no
        // grant, under the version that shape was written under.
        var path = Path.Combine(directory.Path, InvitationStore.FileName);
        var written = File.ReadAllText(path);
        var grantAt = written.IndexOf("\"template\":", StringComparison.Ordinal);
        var afterGrant = written.IndexOf("\"accountsProduced\":", StringComparison.Ordinal);
        Assert.True(grantAt > 0 && afterGrant > grantAt, "The store no longer writes a grant this test can take back out.");
        File.WriteAllText(
            path,
            (written[..grantAt] + written[afterGrant..]).Replace("\"version\": 2", "\"version\": 1", StringComparison.Ordinal));

        var seam = new ARecordingWriteSeam();
        var controller = RedeemRoute.Over(directory.Path, clock, seam, RedeemRoute.Request());

        var answer = await controller.Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(answer).StatusCode);
        Assert.Empty(seam.Asked);
        Assert.Equal(1, Assert.Single(new InvitationStore(directory.Path).Read().Invitations).UsesRemaining);
    }

    /// <summary>
    /// A server that has given this plugin no data directory refuses rather than
    /// raising, which is branch 10 of docs/redemption-flow.md.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task AStoreThatIsNotThereIsARefusalRatherThanAFailure()
    {
        var seam = new ARecordingWriteSeam();
        var controller = RedeemRoute.Over(store: null, new TestClock(_minted), seam, RedeemRoute.Request());

        var answer = await controller.Submit("any-code", RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ContentResult>(answer).StatusCode);
        Assert.Empty(seam.Asked);
    }

    /// <summary>
    /// The redirect that ends a redemption carries the referrer policy, so the
    /// browser does not hand the completion address the invitation code.
    /// </summary>
    /// <remarks>
    /// The code is in the path of the address being left, which is what makes
    /// this header load-bearing on this route rather than a habit.
    /// <see cref="RedemptionRouteHeadersTests"/> asks that each action set all
    /// five at all; this asks it of the one response that leg does not drive.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheRedirectDoesNotHandTheCodeOnInAReferrer()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var context = RedeemRoute.Request();
        var controller = RedeemRoute.Over(directory.Path, clock, new ARecordingWriteSeam(), context);

        await controller.Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("DENY", context.Response.Headers.XFrameOptions.ToString());
        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions.ToString());
        Assert.False(string.IsNullOrEmpty(context.Response.Headers.ContentSecurityPolicy.ToString()));
    }

    /// <summary>
    /// Drives every case this route refuses and hands back what each answered
    /// with, flattened into one comparable string per case.
    /// </summary>
    /// <returns>The case name and what it is compared on.</returns>
    private static async Task<IReadOnlyList<(string Case, string Compared)>> RefusalsAsync()
    {
        var answers = new List<(string, string)>();

        using (var directory = new OwnedDirectory())
        {
            var clock = new TestClock(_minted);
            RedeemRoute.Mint(directory.Path, clock, uses: 1);
            answers.Add(await DriveAsync("no such invitation", directory.Path, clock, "not-a-real-code").ConfigureAwait(true));
        }

        using (var directory = new OwnedDirectory())
        {
            var clock = new TestClock(_minted);
            var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
            clock.MoveTo(minted.Invitation.ExpiresAt);
            answers.Add(await DriveAsync("expired", directory.Path, clock, minted.Code).ConfigureAwait(true));
        }

        using (var directory = new OwnedDirectory())
        {
            var clock = new TestClock(_minted);
            var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
            await RedeemRoute
                .Over(directory.Path, clock, new ARecordingWriteSeam(), RedeemRoute.Request())
                .Submit(minted.Code, RedeemRoute.Filled("first", "a password long enough"))
                .ConfigureAwait(true);
            answers.Add(await DriveAsync("spent", directory.Path, clock, minted.Code).ConfigureAwait(true));
        }

        using (var directory = new OwnedDirectory())
        {
            var clock = new TestClock(_minted);
            var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
            RedeemRoute.Operations(directory.Path, clock)
                .Revoke(minted.Invitation.Id, Guid.Parse("44445555-6666-7777-8888-99990000aaaa"));
            answers.Add(await DriveAsync("revoked", directory.Path, clock, minted.Code).ConfigureAwait(true));
        }

        using (var directory = new OwnedDirectory())
        {
            var clock = new TestClock(_minted);
            var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
            var limiter = new AttemptLimiter(clock);
            for (var spent = 0; spent < AttemptLimiter.GlobalCeiling; spent++)
            {
                Assert.True(limiter.MayJudge("somebody else"));
            }

            var context = RedeemRoute.Request();
            var answer = await RedeemRoute
                .Over(directory.Path, clock, limiter, new ARecordingWriteSeam(), context)
                .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"))
                .ConfigureAwait(true);
            answers.Add(("refused by the rate limit", ComparedOn(answer, context)));
        }

        using (var directory = new OwnedDirectory())
        {
            var clock = new TestClock(_minted);
            var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
            var ceiling = new CreationCeiling(clock);
            for (var created = 0; created < CreationCeiling.AccountsInAWindow; created++)
            {
                Assert.True(ceiling.MayCreate());
            }

            var context = RedeemRoute.Request();
            var answer = await RedeemRoute
                .Over(directory.Path, clock, ceiling, new ARecordingWriteSeam(), context)
                .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"))
                .ConfigureAwait(true);
            answers.Add(("refused by the ceiling on accounts", ComparedOn(answer, context)));
        }

        return answers;
    }

    /// <summary>
    /// Posts one code at a route over a store and flattens the answer.
    /// </summary>
    /// <param name="name">What the case is called in a failure message.</param>
    /// <param name="store">The store directory.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="code">The code to present.</param>
    /// <returns>The case name and what it is compared on.</returns>
    private static async Task<(string Case, string Compared)> DriveAsync(
        string name,
        string store,
        TestClock clock,
        string code)
    {
        var context = RedeemRoute.Request();
        var answer = await RedeemRoute
            .Over(store, clock, new ARecordingWriteSeam(), context)
            .Submit(code, RedeemRoute.Filled("newcomer", "a password long enough"))
            .ConfigureAwait(true);

        return (name, ComparedOn(answer, context));
    }

    /// <summary>
    /// Everything docs/refusal-response.md says a refusal is compared on, in one
    /// string.
    /// </summary>
    /// <remarks>
    /// The headers are read off the response rather than named here, so a header
    /// this route starts setting joins the comparison without anybody adding it,
    /// and one that differs between two cases is a difference this sees. The
    /// body is compared whole, which covers the content length that page names:
    /// two bodies of different lengths differ before their lengths are mentioned.
    /// </remarks>
    /// <param name="answer">What the action returned.</param>
    /// <param name="context">The response it wrote its headers to.</param>
    /// <returns>The comparable form.</returns>
    private static string ComparedOn(IActionResult answer, HttpContext context)
    {
        var content = answer as ContentResult;
        var headers = context.Response.Headers
            .OrderBy(header => header.Key, StringComparer.Ordinal)
            .Select(header => header.Key + ": " + header.Value.ToString());

        return string.Join(
            "\n",
            [
                "status " + (content?.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "none"),
                "type " + (content?.ContentType ?? "none"),
                "body " + (content?.Content ?? "none"),
                .. headers,
            ]);
    }
}
