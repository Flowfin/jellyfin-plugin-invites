using System;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Invites.Setup;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The password rules, and the page that has to state them.
/// </summary>
/// <remarks>
/// <para>
/// <b>The page is compared against the rules after its whitespace is
/// collapsed.</b> The formatter owns the line breaks inside an element and a
/// sentence it wrapped is the same sentence to a browser, so a comparison
/// against the raw bytes would red on a reflow and say nothing about the rule.
/// What is asserted is what a person reads.
/// </para>
/// <para>
/// <b>What is not asserted here.</b> Nothing renders the page and nothing posts
/// to it. Whether a refused password leaves no account behind is #75 and #77,
/// and it has no subject in this tree: there is no route that takes a password.
/// </para>
/// </remarks>
public class PasswordRulesTests
{
    /// <summary>
    /// The two boundaries, one either side. The near-miss this is written
    /// against is the comparison that admits one character too few, which is
    /// the mistake somebody makes and which no other assertion in the suite
    /// would notice.
    /// </summary>
    /// <param name="characters">How long the password is.</param>
    /// <param name="refused">Whether that length is refused.</param>
    [Theory]
    [InlineData(0, true)]
    [InlineData(PasswordRules.MinimumLength - 1, true)]
    [InlineData(PasswordRules.MinimumLength, false)]
    [InlineData(PasswordRules.MinimumLength + 1, false)]
    [InlineData(PasswordRules.MaximumLength - 1, false)]
    [InlineData(PasswordRules.MaximumLength, false)]
    [InlineData(PasswordRules.MaximumLength + 1, true)]
    public void TheBoundariesAreWhereTheRulesSayTheyAre(int characters, bool refused)
    {
        var password = new string('p', characters);

        Assert.Equal(refused, PasswordRules.WhyRefused(password) is not null);
    }

    /// <summary>
    /// Nothing at all is refused for being too short rather than accepted as an
    /// empty password or thrown at.
    /// </summary>
    [Fact]
    public void NothingTypedIsTooShort()
    {
        Assert.Equal(PasswordRules.TooShort, PasswordRules.WhyRefused(null));
        Assert.Equal(PasswordRules.TooShort, PasswordRules.WhyRefused(string.Empty));
    }

    /// <summary>
    /// A character is what a person would delete with one keystroke, not a
    /// UTF-16 unit. Six letters each carrying a combining accent are twelve
    /// units and six characters, and a rule asking for twelve refuses them.
    /// </summary>
    /// <remarks>
    /// This is the assertion that holds the counting decision. Counting units
    /// instead would let this password through while the person sees six
    /// characters on the screen, and no other test in the suite can tell the
    /// two readings apart.
    /// </remarks>
    [Fact]
    public void ACharacterIsWhatAPersonCounts()
    {
        var six = string.Concat(Enumerable.Repeat("e\u0301", 6));

        Assert.Equal(12, six.Length);
        Assert.Equal(6, PasswordRules.Characters(six));
        Assert.Equal(PasswordRules.TooShort, PasswordRules.WhyRefused(six));
    }

    /// <summary>
    /// A refusal is one of the two declared sentences and is never built out of
    /// what was typed, so a message cannot carry a password into a response, a
    /// log or a support thread.
    /// </summary>
    /// <remarks>
    /// The assertion is that the answer is the declared object rather than that
    /// it does not contain the password. The weaker reading fails on its own
    /// terms: a one-letter password is a substring of any sentence in English,
    /// so a message built out of the input would pass a containment test while
    /// carrying the credential.
    /// </remarks>
    /// <param name="password">A password to be refused.</param>
    [Theory]
    [InlineData("short")]
    [InlineData("a")]
    [InlineData("   ")]
    public void ARefusalSaysNothingThatWasTyped(string password)
    {
        var answer = PasswordRules.WhyRefused(password);

        Assert.NotNull(answer);
        Assert.True(
            ReferenceEquals(answer, PasswordRules.TooShort)
                || ReferenceEquals(answer, PasswordRules.TooLong),
            "The refusal was assembled rather than chosen: " + answer);
    }

    /// <summary>
    /// Spaces at the ends are characters like any other and nothing trims them,
    /// which is the third sentence the page states above the field. Ten letters
    /// between two spaces are twelve characters and are accepted.
    /// </summary>
    /// <remarks>
    /// The near-miss is a <c>Trim</c> added out of tidiness, which would store
    /// something other than what the person typed and surface at the next
    /// sign-in rather than here.
    /// </remarks>
    [Fact]
    public void NothingIsTrimmedOffTheEnds()
    {
        Assert.Equal(12, PasswordRules.Characters(" abcdefghij "));
        Assert.Null(PasswordRules.WhyRefused(" abcdefghij "));
    }

    /// <summary>
    /// A passphrase somebody would actually choose is accepted, and it is
    /// accepted without a composition rule to satisfy. Four ordinary words and
    /// three spaces carry no digit and no capital, and there is nothing here
    /// for them to fail.
    /// </summary>
    [Fact]
    public void APassphraseIsAccepted()
    {
        Assert.Null(PasswordRules.WhyRefused("correct horse battery staple"));
    }

    /// <summary>
    /// Every sentence the rules declare is on the page, and all of them are
    /// ahead of the box the person types into. A rule stated under the field is
    /// a rule read after the mistake.
    /// </summary>
    [Fact]
    public void ThePageStatesEveryRuleBeforeThePasswordField()
    {
        var page = Collapsed(SetupPage.Html);
        var field = page.IndexOf("name=\"password\"", StringComparison.Ordinal);

        Assert.True(field >= 0, "The page has no password field to state the rules ahead of.");

        foreach (var statement in PasswordRules.Statements)
        {
            var at = page.IndexOf(Collapsed(statement), StringComparison.Ordinal);

            Assert.True(at >= 0, "The page does not state: " + statement);
            Assert.True(at < field, "The page states this after the field: " + statement);
        }
    }

    /// <summary>
    /// The numbers on the page are the numbers in the rules. A page saying
    /// eight where the code refuses anything under twelve is worse than a page
    /// saying nothing, because the person meets the refusal after doing what
    /// they were told.
    /// </summary>
    [Fact]
    public void ThePageQuotesNoOtherNumbers()
    {
        var page = Collapsed(SetupPage.Html);

        Assert.Contains(
            PasswordRules.MinimumLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Collapsed(PasswordRules.TooShort),
            StringComparison.Ordinal);

        Assert.Contains(Collapsed(PasswordRules.TooShort), page, StringComparison.Ordinal);
        Assert.Contains(Collapsed(PasswordRules.TooLong), page, StringComparison.Ordinal);
    }

    /// <summary>
    /// Runs of whitespace become one space, which is what a browser does with
    /// the text of an element.
    /// </summary>
    /// <param name="text">The text to collapse.</param>
    /// <returns>The same text with every run of whitespace as one space.</returns>
    private static string Collapsed(string text)
    {
        return Regex.Replace(text, @"\s+", " ");
    }
}
