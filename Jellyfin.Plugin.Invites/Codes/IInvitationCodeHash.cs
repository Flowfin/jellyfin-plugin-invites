using System.Collections.Immutable;

namespace Jellyfin.Plugin.Invites.Codes;

/// <summary>
/// The keyed hash a presented code is reduced to before it is compared with
/// anything stored.
/// </summary>
/// <remarks>
/// <para>
/// The store holds the keyed hash of a code and never the code, which is what
/// makes the arithmetic in docs/code-entropy.md worth doing: whoever reads the
/// file still has to guess. So a redemption has to reduce what somebody typed
/// to the same value the same way, and this is the one surface that does it.
/// </para>
/// <para>
/// It is a seam for the same reason <see cref="Time.IClock"/> is one. The
/// secret behind the hash has a life cycle, which is where it is generated,
/// what its permissions are, what happens when it is missing and what rotation
/// costs, and that is #30 rather than this interface. Until #30 lands there is
/// no implementation of this in the plugin, and the suite supplies its own.
/// The decision routine takes this rather than a secret so that neither the
/// routine nor its table has to know any of that.
/// </para>
/// </remarks>
public interface IInvitationCodeHash
{
    /// <summary>
    /// Reduces one canonical code to the value a stored record is compared
    /// against.
    /// </summary>
    /// <param name="canonicalCode">
    /// The code in the form <see cref="InvitationCode.Canonicalise"/> produces.
    /// Passing anything else is a second definition of which codes are equal,
    /// which is the defect that function exists against.
    /// </param>
    /// <returns>The keyed hash of that code. Never empty.</returns>
    ImmutableArray<byte> Of(string canonicalCode);
}
