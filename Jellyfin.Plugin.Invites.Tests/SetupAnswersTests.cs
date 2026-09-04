using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Invites.Controllers;
using Jellyfin.Plugin.Invites.Setup;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// What the server decides about the answers a post carries, judged on its own
/// rather than through the route.
/// </summary>
/// <remarks>
/// <para>
/// <b>The route is where the ORDER of these refusals is asserted and this is
/// where each rule is.</b> <c>RedeemPostTests</c> drives the post and reads what
/// the store holds afterwards, which is what proves a refusal happened before
/// any code was judged; splitting the rules out to here is what stops that file
/// growing one whole redemption per rule.
/// </para>
/// <para>
/// <b>Nothing here reads a store, a code or a clock, and that is the property
/// under test as much as any assertion below.</b> These answers are judged out
/// of the request alone, which is what lets the post refuse a malformed body
/// without telling the caller whether the code it carried was worth anything.
/// </para>
/// </remarks>
public class SetupAnswersTests
{
    /// <summary>
    /// A password that satisfies every rule, so a test about some other field
    /// is not passing for the wrong reason.
    /// </summary>
    private const string Long = "a password long enough";

    /// <summary>
    /// The fields a post may carry are the members the form binds, and no
    /// wider set typed out beside them.
    /// </summary>
    /// <remarks>
    /// The widening this refuses is a field name added to the accepted list by
    /// hand for a control nobody added to the form, which would be a value a
    /// stranger may set that the page never offers. The other direction, the
    /// form and the bound type agreeing, is
    /// <c>SetupFormInventoryTests.ThePostBindsTheFormsFieldsAndNothingWider</c>,
    /// and this rests on it rather than reading the page a second time.
    /// </remarks>
    [Fact]
    public void TheAcceptedFieldsAreTheMembersTheFormBinds()
    {
        var bound = typeof(SetupSubmission)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name.ToLowerInvariant())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(bound);
        Assert.Equal(bound, SetupAnswers.Fields);
    }

    /// <summary>
    /// A post carrying every field, with a password that satisfies the rules and
    /// a confirmation that matches it, is accepted.
    /// </summary>
    /// <remarks>
    /// The clean case, first and on purpose. Every refusal below would also be
    /// produced by a judgement that refused everything, and this is what says it
    /// does not.
    /// </remarks>
    [Fact]
    public void AWellFormedPostIsAcceptedAndCarriesTheTwoAnswersForward()
    {
        var answers = SetupAnswers.Accept(
            Submission("newcomer", Long, Long),
            Posted(RedeemRoute.Body("newcomer", Long)));

        Assert.NotNull(answers);
        Assert.Equal("newcomer", answers.Username);
        Assert.Equal(Long, answers.Password);
    }

    /// <summary>
    /// A post whose two copies of the password disagree is refused.
    /// </summary>
    /// <remarks>
    /// The comparison is ordinal, so two strings a looser comparison would call
    /// equal are refused here: a judgement that folded case would let somebody
    /// set a password they cannot type back, and the person would find out at
    /// their next sign-in rather than now.
    /// </remarks>
    /// <param name="confirmation">The second copy the post carried.</param>
    [Theory]
    [InlineData("a password long enougH")]
    [InlineData("A password long enough")]
    [InlineData("a password long enough ")]
    [InlineData("")]
    public void APostWhoseTwoCopiesOfThePasswordDisagreeIsRefused(string confirmation)
    {
        Assert.Null(SetupAnswers.Accept(
            Submission("newcomer", Long, confirmation),
            Posted(RedeemRoute.Body("newcomer", Long))));
    }

    /// <summary>
    /// A password the rules refuse is refused here, and by the same routine the
    /// page states the rules from.
    /// </summary>
    /// <remarks>
    /// The boundary is taken from <see cref="PasswordRules"/> rather than
    /// written down, so this stays a test of the post applying the rule and does
    /// not become a second copy of what the rule is. Both ends are asserted:
    /// one character under the minimum and one character over the maximum are
    /// refused, and the two lengths themselves are accepted, so a judgement that
    /// refused the boundary would red here as loudly as one that let it past.
    /// </remarks>
    [Fact]
    public void APasswordTheRulesRefuseIsRefusedAndOneTheyAcceptIsNot()
    {
        Assert.Null(Judge(new string('x', PasswordRules.MinimumLength - 1)));
        Assert.NotNull(Judge(new string('x', PasswordRules.MinimumLength)));
        Assert.NotNull(Judge(new string('x', PasswordRules.MaximumLength)));
        Assert.Null(Judge(new string('x', PasswordRules.MaximumLength + 1)));
    }

    /// <summary>
    /// A name the server would refuse for its shape is refused here, and one it
    /// accepts is carried forward unaltered.
    /// </summary>
    /// <remarks>
    /// Which names the rule takes is <c>UsernameRulesTests</c>, and this is that
    /// the judgement asks it at all: without this the copy of the server's rule
    /// could be correct and reached by nothing. The accepted case also asserts
    /// the name comes back as it was typed, because this issue's clause is that
    /// no name is ever silently altered and a judgement that trimmed one would
    /// still refuse everything this file names.
    /// </remarks>
    [Fact]
    public void ANameTheServerWouldRefuseIsRefusedAndOneItAcceptsIsCarriedForward()
    {
        Assert.Null(SetupAnswers.Accept(
            Submission("ada/lovelace", Long, Long),
            Posted(RedeemRoute.Body("ada/lovelace", Long))));

        var answers = SetupAnswers.Accept(
            Submission("Ada Lovelace", Long, Long),
            Posted(RedeemRoute.Body("Ada Lovelace", Long)));

        Assert.NotNull(answers);
        Assert.Equal("Ada Lovelace", answers.Username);
    }

    /// <summary>
    /// A post carrying a field the form does not define is refused rather than
    /// having the field ignored.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every value below binds nothing, so a run that ignored the extra key
    /// would create an account and pass every other assertion in the suite. The
    /// four names are the shapes worth refusing: one naming a member of the
    /// grant, one naming a member of the invitation record, one that is a near
    /// miss for a field the form does have, and one that is simply unknown.
    /// </para>
    /// <para>
    /// The near miss is the one to keep. A body carrying <c>usernames</c> is
    /// what a client that has drifted by one character sends, and a judgement
    /// that matched loosely enough to let it through would also match a field
    /// somebody added on purpose.
    /// </para>
    /// </remarks>
    /// <param name="field">The name of the field the post carried as well.</param>
    [Theory]
    [InlineData("maymanage")]
    [InlineData("template")]
    [InlineData("usernames")]
    [InlineData("x")]
    public void APostCarryingAFieldTheFormDoesNotDefineIsRefused(string field)
    {
        var body = RedeemRoute.Body("newcomer", Long);
        body[field] = "anything at all";

        Assert.True(SetupAnswers.CarriesAFieldTheFormDoesNotDefine(Posted(body)));
        Assert.Null(SetupAnswers.Accept(Submission("newcomer", Long, Long), Posted(body)));
    }

    /// <summary>
    /// The fields the form does define are accepted whatever case they arrive
    /// in, because that is how the binder matches them.
    /// </summary>
    /// <remarks>
    /// A check stricter than the binder would refuse a body the binder had
    /// already read, which is a refusal nobody could act on: the post would see
    /// filled-in answers and a field it called unknown, for one body.
    /// </remarks>
    [Fact]
    public void TheFormsOwnFieldsAreAcceptedWhateverCaseTheyArriveIn()
    {
        var body = new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["Username"] = "newcomer",
            ["PASSWORD"] = Long,
            ["Confirmation"] = Long,
        };

        Assert.False(SetupAnswers.CarriesAFieldTheFormDoesNotDefine(Posted(body)));
    }

    /// <summary>
    /// A request the server does not read as a form is not judged for its
    /// fields, and a request that is not there at all is not either.
    /// </summary>
    /// <remarks>
    /// Reading the body of a request that carries none throws, and a probe
    /// turned into an exception is a worse answer than the bad request it
    /// replaces. Nothing is let through by this: such a request binds no answer,
    /// so the judgement refuses it for carrying no username, which the third
    /// assertion is.
    /// </remarks>
    [Fact]
    public void ARequestCarryingNoFormIsNotJudgedForItsFieldsAndIsStillRefused()
    {
        Assert.False(SetupAnswers.CarriesAFieldTheFormDoesNotDefine(new DefaultHttpContext().Request));
        Assert.False(SetupAnswers.CarriesAFieldTheFormDoesNotDefine(null));
        Assert.Null(SetupAnswers.Accept(new SetupSubmission(), new DefaultHttpContext().Request));
        Assert.Null(SetupAnswers.Accept(null, null));
    }

    /// <summary>
    /// A submission with the three members filled in as given.
    /// </summary>
    /// <param name="username">The username member.</param>
    /// <param name="password">The password member.</param>
    /// <param name="confirmation">The confirmation member.</param>
    /// <returns>The submission.</returns>
    private static SetupSubmission Submission(string? username, string? password, string? confirmation) =>
        new() { Username = username, Password = password, Confirmation = confirmation };

    /// <summary>
    /// The request of a context carrying the given body.
    /// </summary>
    /// <param name="body">The posted fields.</param>
    /// <returns>The request.</returns>
    private static HttpRequest Posted(IDictionary<string, StringValues> body) =>
        RedeemRoute.Posting(body).Request;

    /// <summary>
    /// Judges a post carrying one password in both copies.
    /// </summary>
    /// <param name="password">The password to put in both copies.</param>
    /// <returns>What the judgement decided.</returns>
    private static AcceptedAnswers? Judge(string password) =>
        SetupAnswers.Accept(
            Submission("newcomer", password, password),
            Posted(RedeemRoute.Body("newcomer", password)));
}
