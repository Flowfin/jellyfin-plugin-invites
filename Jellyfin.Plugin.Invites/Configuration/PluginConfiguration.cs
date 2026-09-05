using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Invites.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
/// <remarks>
/// Every setting here owes a row in docs/configuration.md, which
/// <c>.github/lint/configuration-reference.sh</c> refuses the absence of, and a
/// decided fresh-install value in
/// <c>Jellyfin.Plugin.Invites.Tests.FreshInstallConfigurationTests</c>, which
/// reds without one. The two are different questions: what the setting means,
/// and what a server that never opened this page is worth.
/// </remarks>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the address invitation links are built from, as an operator
    /// wants a stranger to reach this server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is a setting rather than something worked out at run time because the
    /// alternative is the incoming request, and that is attacker controlled.
    /// A minting request carrying a forged host produces a link pointing at the
    /// attacker's server, and the invited person types their new password into
    /// it. #50 is that decision and
    /// <see cref="Invitations.InvitationLink"/> is where it is carried out.
    /// </para>
    /// <para>
    /// Empty on a fresh install, which refuses to build a link rather than
    /// guessing at one. A guess here is not a broken link an operator notices;
    /// it is a link that works from inside the network and not from outside it,
    /// which surfaces as a stranger who cannot redeem and an operator who cannot
    /// reproduce the problem.
    /// </para>
    /// </remarks>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the named account templates an operator can mint an
    /// invitation against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Decision 6 in #11 keeps several named templates, and this is where they
    /// are written down. Each is the stored shape of what an invitation minted
    /// against it grants, and <see cref="ConfiguredTemplate"/> says what a
    /// member left out is worth. The whole list is judged when the plugin
    /// loads, by <see cref="Accounts.TemplateSettings"/>, and a list that
    /// breaks a rule is refused whole with the position and the rule named,
    /// rather than corrected or thinned to the entries that pass.
    /// </para>
    /// <para>
    /// Empty on a fresh install. No template is the closed answer: a template
    /// the plugin invented would be a grant nobody decided, and since the mint
    /// copies a template out of this list, nothing can be minted until an
    /// operator has written one down.
    /// </para>
    /// </remarks>
    public ConfiguredTemplate[]? Templates { get; set; } = [];

    /// <summary>
    /// Gets or sets how many days a record that has stopped being usable is
    /// kept before the sweep removes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ninety days on a fresh install, which is the period decision 8 in #11
    /// chose and <see cref="Invitations.Retention"/> carries the argument for.
    /// It is a setting rather than a constant because a retention policy is a
    /// thing installations genuinely differ on: what counts as long enough to
    /// answer where an account came from, and short enough not to be a register
    /// of who was invited, is not the same question on a household server and on
    /// one with a hundred users.
    /// </para>
    /// <para>
    /// Bounded at both ends by <see cref="Invitations.NumberSettings"/>, and a value outside
    /// the range refuses where it would be used rather than being replaced by
    /// the ninety. The bottom of the range is the one to read carefully: zero is
    /// not a stricter setting, it is deletion at the moment a record stops being
    /// usable, which destroys the trace before anybody could read it.
    /// </para>
    /// </remarks>
    public int RecordRetentionDays { get; set; } = 90;

    /// <summary>
    /// Gets or sets how many presented codes one source address may have judged
    /// in an hour.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Twenty on a fresh install, which is the number docs/rate-limit.md reasons
    /// about and <see cref="Redemption.AttemptLimiter"/> compiles. It is a
    /// setting because how many people are behind one address differs between
    /// installations: a family behind one address and a server whose users all
    /// arrive through one proxy are not the same server.
    /// </para>
    /// <para>
    /// It may only be lowered. <see cref="Invitations.NumberSettings"/> bounds it by the
    /// compiled constant rather than letting it replace one, because the
    /// arithmetic in docs/code-entropy.md rests on that number, and a limit an
    /// operator can raise is one whoever holds that account can raise before
    /// searching.
    /// </para>
    /// </remarks>
    public int RedemptionAttemptsPerAddressInAnHour { get; set; } = 20;

    /// <summary>
    /// Gets or sets how many presented codes all sources together may have
    /// judged in a second.
    /// </summary>
    /// <remarks>
    /// Ten on a fresh install, and lowerable only, for the reason the setting
    /// above carries. This is the number the throttled rows of
    /// docs/code-entropy.md are computed at, so it is the one whose maximum the
    /// entropy argument rests on directly.
    /// </remarks>
    public int RedemptionAttemptsPerSecond { get; set; } = 10;
}
