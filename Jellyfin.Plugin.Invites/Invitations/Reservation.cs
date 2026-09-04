using Jellyfin.Plugin.Invites.Redemption;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// What one presented code was worth: the verdict, and the record whose use was
/// taken where one was.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two answers rather than one, because a honoured verdict is not by itself
/// an account.</b> <see cref="Redemption.RedemptionDecision"/> answers whether
/// the record's expiry, use count and revocation allow it, and that is the whole
/// of what it answers. Whether the record carries a grant to create an account
/// from is a different question about a different field, and a record minted
/// before the grant was copied onto it carries none.
/// </para>
/// <para>
/// <b>Honoured with nothing reserved is the case to read carefully.</b> It is a
/// version one record: <see cref="Invitation.Template"/> says what such a record
/// is and that it can create nothing, and it says the strict answer is to refuse
/// rather than to resolve the label at redemption, which would make an edit to a
/// template change what a live invitation already grants. Nothing is written in
/// that case, so the record keeps its count, and the person is answered with the
/// same refusal as every other case.
/// </para>
/// </remarks>
public sealed class Reservation
{
    private Reservation(RedemptionVerdict verdict, Invitation? reserved)
    {
        Verdict = verdict;
        Reserved = reserved;
    }

    /// <summary>
    /// Gets what the decision made of the presented code.
    /// </summary>
    public RedemptionVerdict Verdict { get; }

    /// <summary>
    /// Gets the record as it stands after one use was taken, or <c>null</c>
    /// where nothing was written.
    /// </summary>
    public Invitation? Reserved { get; }

    /// <summary>
    /// Gets a value indicating whether an account may now be created against
    /// this reservation.
    /// </summary>
    /// <remarks>
    /// It reads the reservation rather than the verdict on purpose. A caller
    /// that asked the verdict would create an account for a record whose use
    /// nothing took, and the invitation would be worth an account on every
    /// presentation until it expired.
    /// </remarks>
    public bool MayCreateAnAccount => Reserved is not null;

    /// <summary>
    /// A code the decision honoured, whose use has been taken and written.
    /// </summary>
    /// <param name="verdict">The verdict the decision returned.</param>
    /// <param name="reserved">The record as it now stands.</param>
    /// <returns>The reservation.</returns>
    public static Reservation Taken(RedemptionVerdict verdict, Invitation reserved) =>
        new(verdict, reserved);

    /// <summary>
    /// A code that produces no account, whether the decision refused it or the
    /// record it matched carries no grant. Nothing was written.
    /// </summary>
    /// <param name="verdict">The verdict the decision returned.</param>
    /// <returns>The reservation.</returns>
    public static Reservation Nothing(RedemptionVerdict verdict) => new(verdict, null);
}
