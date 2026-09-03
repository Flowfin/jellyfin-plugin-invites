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
    /// the plugin invented would be a grant nobody decided, and once the mint
    /// copies a template out of this list, nothing can be minted until an
    /// operator has written one down.
    /// </para>
    /// </remarks>
    public ConfiguredTemplate[]? Templates { get; set; } = [];
}
