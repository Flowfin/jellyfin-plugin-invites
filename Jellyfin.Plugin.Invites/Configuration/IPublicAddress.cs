namespace Jellyfin.Plugin.Invites.Configuration;

/// <summary>
/// The address this server is reached on from outside, as one thing to ask
/// rather than a static to reach for.
/// </summary>
/// <remarks>
/// <para>
/// The same shape as <see cref="Storage.IStoreDirectory"/> and for the same
/// reason: the value lives on <see cref="Plugin.Instance"/>, which is a static
/// the server sets, so a routine that reads it can only be tested by a test
/// that arranges a global, and two tests arranging the same global cannot run
/// beside each other.
/// </para>
/// <para>
/// <b>It answers from configuration and never from a request.</b> That is the
/// whole of #50. A request can say anything about which host it reached, and a
/// link built from what it said points wherever the caller chose. There is no
/// member here that takes a request, so the shape a greppable rule cannot see -
/// a request accepted and politely ignored - cannot be written against this
/// interface either.
/// </para>
/// </remarks>
public interface IPublicAddress
{
    /// <summary>
    /// Gets the configured public base address, or <c>null</c> where the server
    /// has not constructed the plugin yet.
    /// </summary>
    /// <remarks>
    /// Empty is the ordinary answer on a fresh install rather than a fault, and
    /// what it means is decided by
    /// <see cref="Invitations.InvitationLink"/>: no link is built and the
    /// refusal names this setting.
    /// </remarks>
    string? PublicBaseUrl { get; }
}
