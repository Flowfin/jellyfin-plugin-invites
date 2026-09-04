using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Accounts;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A write seam that writes nothing and remembers what it was asked for.
/// </summary>
/// <remarks>
/// It records the calls in the order they arrived, with their arguments, so an
/// assertion can be about what the routine asked the server for rather than
/// about the state at the end. The three orders this routine could have been
/// written in produce the same end state and different call trails, and the
/// trail is the thing #398 is about.
/// </remarks>
internal sealed class ARecordingWriteSeam : IServerAccountWrites
{
    private readonly List<string> _asked = new();

    /// <summary>
    /// Gets what the seam was asked for, in order.
    /// </summary>
    public IReadOnlyList<string> Asked => _asked;

    /// <summary>
    /// Gets or sets the identifier the creation hands back.
    /// </summary>
    public Guid Answers { get; set; } = Guid.Parse("33333333-3333-4333-8333-333333333333");

    /// <summary>
    /// Gets or sets what the credential arm raises instead of recording.
    /// </summary>
    public Exception? CredentialRefusal { get; set; }

    /// <summary>
    /// Gets the grant the template arm was handed, or null where it was never
    /// reached.
    /// </summary>
    /// <remarks>
    /// The call trail records how many libraries a grant carried, which is
    /// enough to tell one call from another and not enough to tell one grant
    /// from another. A caller asking which grant reached the server needs the
    /// value, and #61's rule is exactly a question about which of two equal-sized
    /// grants arrived.
    /// </remarks>
    public AccountTemplate? AppliedTemplate { get; private set; }

    /// <inheritdoc />
    public Task<Guid> CreateAccountAsync(string username)
    {
        _asked.Add("create " + username);
        return Task.FromResult(Answers);
    }

    /// <inheritdoc />
    public Task SetCredentialAsync(Guid account, string password)
    {
        if (CredentialRefusal is not null)
        {
            return Task.FromException(CredentialRefusal);
        }

        _asked.Add(string.Format(
            CultureInfo.InvariantCulture,
            "credential {0} {1}",
            account,
            password.Length));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ApplyTemplateAsync(Guid account, AccountTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        AppliedTemplate = template;
        _asked.Add(string.Format(
            CultureInfo.InvariantCulture,
            "template {0} {1}",
            account,
            template.Libraries.Length));
        return Task.CompletedTask;
    }
}

/// <summary>
/// The routine that turns an honoured redemption into an account.
/// </summary>
/// <remarks>
/// Every assertion here is about the call trail. What each call does to the
/// server is the seam's, and what a template does to a policy is
/// <c>AccountTemplateApplicationTests</c>'.
/// </remarks>
public class AccountCreationTests
{
    private static readonly Guid _account = Guid.Parse("44444444-4444-4444-8444-444444444444");

    private static readonly Guid _library = Guid.Parse("55555555-5555-4555-8555-555555555555");

    /// <summary>
    /// The order is the security property. An account that exists with no
    /// credential and the server's default policy is a window somebody can sign
    /// in through, so the credential goes on before the grant and both go on
    /// after the account exists.
    /// </summary>
    [Fact]
    public async Task TheAccountIsCreatedThenGivenItsCredentialThenGivenItsGrant()
    {
        var server = new ARecordingWriteSeam { Answers = _account };

        await AccountCreation.CreateAsync(server, "invited", "a-chosen-credential", ATemplate());

        Assert.Equal(
            new[]
            {
                "create invited",
                "credential " + _account + " 19",
                "template " + _account + " 1",
            },
            server.Asked);
    }

    /// <summary>
    /// The identifier comes back from the creation rather than from a second
    /// lookup, which could answer about somebody else's account of the same
    /// name.
    /// </summary>
    [Fact]
    public async Task TheIdentifierHandedBackIsTheOneTheCreationAnswered()
    {
        var server = new ARecordingWriteSeam { Answers = _account };

        var created = await AccountCreation.CreateAsync(server, "invited", "a-chosen-credential", ATemplate());

        Assert.Equal(_account, created);
    }

    /// <summary>
    /// The grant is addressed to the account this creation made, and not to
    /// whatever the caller happened to hold.
    /// </summary>
    [Fact]
    public async Task TheGrantIsAppliedToTheAccountThisCreationMade()
    {
        var elsewhere = Guid.Parse("66666666-6666-4666-8666-666666666666");
        var server = new ARecordingWriteSeam { Answers = elsewhere };

        await AccountCreation.CreateAsync(server, "invited", "a-chosen-credential", ATemplate());

        Assert.Contains("template " + elsewhere + " 1", server.Asked);
    }

    /// <summary>
    /// A refusal from the credential arm stops the routine there. Applying the
    /// grant afterwards would finish an account nobody can sign in to and
    /// nothing would say so.
    /// </summary>
    [Fact]
    public async Task AGrantIsNotAppliedToAnAccountThatNeverTookItsCredential()
    {
        var server = new ARecordingWriteSeam
        {
            CredentialRefusal = new ServerAccountWriteRefusedException("the server would not take it"),
        };

        await Assert.ThrowsAsync<ServerAccountWriteRefusedException>(
            () => AccountCreation.CreateAsync(server, "invited", "a-chosen-credential", ATemplate()));

        Assert.Equal(new[] { "create invited" }, server.Asked);
    }

    /// <summary>
    /// Nothing is asked of the server without all four of the things the routine
    /// needs. A null arriving here is a caller that has not read a form yet, and
    /// creating an account for it would be the worst of the failures available.
    /// </summary>
    /// <param name="which">Which argument is missing.</param>
    /// <returns>A task that completes when the refusal has been read.</returns>
    [Theory]
    [InlineData("server")]
    [InlineData("username")]
    [InlineData("password")]
    [InlineData("template")]
    public async Task NothingIsAskedOfTheServerWithoutAllFourArguments(string which)
    {
        var server = new ARecordingWriteSeam();

        await Assert.ThrowsAsync<ArgumentNullException>(() => AccountCreation.CreateAsync(
            which == "server" ? null! : server,
            which == "username" ? null! : "invited",
            which == "password" ? null! : "a-chosen-credential",
            which == "template" ? null! : ATemplate()));

        Assert.Empty(server.Asked);
    }

    /// <summary>
    /// The ceiling, refused inside the routine and before anything reaches the
    /// server. #62 asks for it here rather than as validation on the way in, so
    /// that a later caller which skips the validation still meets it.
    /// </summary>
    /// <returns>A task that completes when the refusal has been read.</returns>
    [Fact]
    public async Task ATemplateThatWouldManageTheServerIsRefusedBeforeAnythingIsCreated()
    {
        var server = new ARecordingWriteSeam();

        var refused = await Assert.ThrowsAsync<ArgumentException>(
            () => AccountCreation.CreateAsync(server, "invited", "a-chosen-credential", ATemplateThatManages()));

        Assert.Empty(server.Asked);
        Assert.Contains("no account an invitation creates is an administrator", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// No account that already exists is touched, and the reason is that there
    /// is no way to hand this routine one. Every parameter of every public
    /// member is the seam, a name, a credential or a template, so no account
    /// identifier can be passed in for a later change to start addressing.
    /// </summary>
    /// <remarks>
    /// This is the machine-checkable form of #62's second rule. Asserting after
    /// a call that no other account changed would pass for a routine that
    /// reached one and happened to leave it as it was, which is the version
    /// that changes something after the next edit. Add a <c>Guid</c> parameter
    /// and this goes red before anything is written with it.
    /// </remarks>
    [Fact]
    public void NothingHereCanBeHandedAnAccountThatAlreadyExists()
    {
        var allowed = new[] { typeof(IServerAccountWrites), typeof(string), typeof(AccountTemplate) };

        var parameters = typeof(AccountCreation)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters())
            .ToList();

        Assert.NotEmpty(parameters);
        Assert.All(parameters, parameter => Assert.Contains(parameter.ParameterType, allowed));
    }

    /// <summary>
    /// A template naming one library, which is enough for the trail to show
    /// which template arrived.
    /// </summary>
    /// <returns>The template.</returns>
    private static AccountTemplate ATemplate() => ATemplate(manages: false);

    /// <summary>
    /// The same template with the one grant this plugin will not honour.
    /// </summary>
    /// <returns>The template.</returns>
    private static AccountTemplate ATemplateThatManages() => ATemplate(manages: true);

    /// <summary>
    /// A template naming one library, which is enough for the trail to show
    /// which template arrived.
    /// </summary>
    /// <param name="manages">Whether the account it asks for manages the server.</param>
    /// <returns>The template.</returns>
    private static AccountTemplate ATemplate(bool manages)
    {
        return new AccountTemplate(
            libraries: ImmutableArray.Create(_library),
            mayDownload: false,
            mayPlayFromOutsideTheNetwork: false,
            mayManage: manages,
            mayControlOtherSessions: false,
            mayWatchLiveTelevision: false,
            mayManageLiveTelevision: false,
            mayDeleteContent: false,
            mayManageCollections: false,
            mayManageSubtitles: false,
            mayManageLyrics: false,
            mayChangeItsOwnPreferences: true,
            remoteBitrateCeiling: null,
            simultaneousStreamCeiling: null,
            parentalRatingCeiling: null,
            serverDefaultsLeftAlone: ImmutableArray<string>.Empty);
    }
}
