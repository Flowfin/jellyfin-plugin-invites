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
/// costs, and that is #30 rather than this interface. The decision routine takes
/// this rather than a secret so that neither the routine nor its table has to
/// know any of that.
/// </para>
/// <para>
/// <b>What implements it.</b> <see cref="InvitationCodeHash"/>, which landed
/// under #29 and takes its key from <see cref="Storage.HashSecret"/>. This
/// remark said there was no implementation in the plugin until #30 landed, which
/// was true when it was written and stopped being true without the sentence
/// moving. It is corrected here rather than deleted, because a reader who opens
/// this file to find out whether the stored form is keyed was being told it is
/// not, in the file that defines what keying means here.
/// </para>
/// <para>
/// <b>What still does not call it.</b> One routine in the plugin takes this
/// interface, the one in <see cref="Redemption.RedemptionDecision"/> that decides
/// a presented code, and nothing calls that routine. So the seam is exercised
/// only by the suite. docs/threat-model.md carries the command that shows the
/// absence, written there rather than here because a command pasted into source
/// is a line the same command then finds.
/// </para>
/// <para>
/// The mint path does construct the implementation and hash a code with it, in
/// <see cref="Invitations.InvitationOperations"/>, but it holds the concrete type
/// rather than this interface. So the keyed form is in use for writing a record
/// and has never been used for deciding one.
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
