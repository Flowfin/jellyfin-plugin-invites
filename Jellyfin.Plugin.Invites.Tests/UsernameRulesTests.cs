using System.Text.RegularExpressions;
using Jellyfin.Plugin.Invites.Setup;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The copy of the server's username rule, held to the server's own expression
/// in both directions.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both directions, because only one of them is the interesting failure.</b>
/// A copy that refuses less than the server loses the early refusal and leaves
/// the server to catch the name, which costs a use. A copy that refuses MORE
/// turns a name somebody may legitimately choose into a link they cannot use,
/// with no message anywhere saying that this plugin rather than the server said
/// no. So the accepted cases below are as load-bearing as the refused ones.
/// </para>
/// <para>
/// <b>What this does not do is run the expression against a server.</b> The
/// expression is read off the server's source at the floor and at the newest
/// release of the line, and the assertions below are what that expression means
/// rather than what a running server answered. No server was started for any of
/// them.
/// </para>
/// </remarks>
public class UsernameRulesTests
{
    /// <summary>
    /// The expression this applies is the server's own, character for
    /// character, and not a rendering of the message beside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The literal below is the one at line 119 of
    /// <c>Jellyfin.Server.Implementations/Users/UserManager.cs</c> at both
    /// <c>v10.11.0</c> (<c>877251bcaec3780d44b7657c54684dc28646b1c3</c>) and
    /// <c>v10.11.11</c> (<c>1fbd8739292cce610231be93daf43368733edf63</c>).
    /// </para>
    /// <para>
    /// A test comparing a constant against a literal proves nothing about the
    /// server on its own, and this one is not pretending otherwise: what it
    /// catches is somebody editing the expression to fix a name they wanted
    /// accepted, which is exactly the change that silently stops it being a copy
    /// of anything. The cases below are what say the expression means what this
    /// file claims.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheExpressionIsTheServersOwn()
    {
        Assert.Equal(@"^(?!\s)[\w\ \-'._@+]+(?<!\s)$", UsernameRules.ServerExpression);
    }

    /// <summary>
    /// Every name the server's expression accepts is accepted here, including
    /// the three the server's own refusal message forgets to mention.
    /// </summary>
    /// <remarks>
    /// The at-sign, the plus and the interior space are the three the message
    /// omits and the expression takes. A rule written from that message rather
    /// than from the expression would refuse all three, which is the failure
    /// that is invisible until somebody with such a name cannot use their link.
    /// </remarks>
    /// <param name="username">A name the server would accept.</param>
    [Theory]
    [InlineData("newcomer")]
    [InlineData("a")]
    [InlineData("Ada Lovelace")]
    [InlineData("ada@example.org")]
    [InlineData("ada+guest")]
    [InlineData("ada.lovelace")]
    [InlineData("ada-lovelace")]
    [InlineData("ada_lovelace")]
    [InlineData("O'Brien")]
    [InlineData("0123456789")]
    [InlineData("Ada  Lovelace")]
    public void ANameTheServersExpressionAcceptsIsNotRefused(string username)
    {
        Assert.Null(UsernameRules.WhyRefused(username));
        Assert.Matches(UsernameRules.ServerExpression, username);
    }

    /// <summary>
    /// A name the server would refuse is refused here, before anything is spent
    /// on it.
    /// </summary>
    /// <remarks>
    /// The leading and trailing space are the two the expression names
    /// explicitly with its own lookaround, and they are the ones a person
    /// produces by accident rather than on purpose: a name pasted with a space
    /// on the end is refused by the server after the use is gone, which is what
    /// applying the rule here prevents.
    /// </remarks>
    /// <param name="username">A name the server would refuse.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" ada")]
    [InlineData("ada ")]
    [InlineData("ada\tlovelace")]
    [InlineData("ada\nlovelace")]
    [InlineData("ada/lovelace")]
    [InlineData("ada:lovelace")]
    [InlineData("ada,lovelace")]
    [InlineData("ada;lovelace")]
    [InlineData("ada#lovelace")]
    [InlineData("<script>")]
    [InlineData("ada\u00A0lovelace")]
    public void ANameTheServersExpressionRefusesIsRefused(string username)
    {
        Assert.Equal(UsernameRules.Refused, UsernameRules.WhyRefused(username));
        Assert.DoesNotMatch(UsernameRules.ServerExpression, username);
    }

    /// <summary>
    /// A name that is not there at all is refused rather than passed.
    /// </summary>
    /// <remarks>
    /// The expression cannot be asked about null, so this is the one case the
    /// routine decides on its own, and it decides it the same way the server's
    /// own guard does: that guard refuses a name that is null or whitespace
    /// before it reaches the expression.
    /// </remarks>
    [Fact]
    public void ANameThatIsNotThereIsRefused()
    {
        Assert.Equal(UsernameRules.Refused, UsernameRules.WhyRefused(null));
    }

    /// <summary>
    /// The reason a name is refused is one constant and never carries the name
    /// that was refused.
    /// </summary>
    /// <remarks>
    /// A message built out of what was typed is a route by which a name reaches
    /// a response, a log line or a support thread. <c>PasswordRules</c> holds
    /// the same property for the same reason and this is its counterpart, so the
    /// one that matters most is asserted rather than assumed: every refusal
    /// above hands back the same object.
    /// </remarks>
    [Fact]
    public void TheReasonIsOneConstantAndNeverCarriesTheName()
    {
        var refusals = new[]
        {
            UsernameRules.WhyRefused("ada/lovelace"),
            UsernameRules.WhyRefused(" ada"),
            UsernameRules.WhyRefused(null),
            UsernameRules.WhyRefused(string.Empty),
        };

        foreach (var refusal in refusals)
        {
            Assert.Same(UsernameRules.Refused, refusal);
        }

        Assert.DoesNotContain("ada", UsernameRules.Refused, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// The expression is applied with no options, which is how the server
    /// declares it, so a unicode letter is a word character here as it is
    /// there.
    /// </summary>
    /// <remarks>
    /// The failure this refuses is somebody adding <c>RegexOptions.ECMAScript</c>
    /// to the construction, under which <c>\w</c> narrows to ASCII and every
    /// name outside it stops being acceptable. The comment beside the server's
    /// declaration says the rule takes "whatever else unicode is cool with", and
    /// these are three of those.
    /// </remarks>
    /// <param name="username">A name whose letters are outside ASCII.</param>
    [Theory]
    [InlineData("Zoë")]
    [InlineData("Ада")]
    [InlineData("日本")]
    public void AUnicodeLetterIsAWordCharacterHereAsItIsOnTheServer(string username)
    {
        Assert.Null(UsernameRules.WhyRefused(username));
        Assert.True(Regex.IsMatch(username, UsernameRules.ServerExpression, RegexOptions.None));
    }
}
