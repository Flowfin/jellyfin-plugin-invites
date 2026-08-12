namespace Jellyfin.Plugin.Invites.Redemption;

/// <summary>
/// What the decision routine concluded about one presented code.
/// </summary>
/// <remarks>
/// <para>
/// These are the operator's words rather than the visitor's. #28 requires every
/// failed redemption to look the same to whoever presented the code, so nothing
/// here reaches a page: the page's single indistinguishable message is #77's and
/// the mapping from one of these to that message is written there. What these
/// are for is the trail an operator reads when they want to know why a link did
/// not work.
/// </para>
/// <para>
/// The refusals are ordered as the routine tests them, and the order is a
/// decision rather than an accident. It is written out on
/// <see cref="RedemptionDecision"/>.
/// </para>
/// </remarks>
public enum RedemptionOutcome
{
    /// <summary>
    /// No stored record matches the presented code, or what was presented is
    /// not a code at all.
    /// </summary>
    /// <remarks>
    /// The two are one outcome on purpose. A caller that could tell an
    /// unreadable code from a readable one nobody minted would be an oracle for
    /// which codes exist, which is the enumeration half of #28.
    /// </remarks>
    NoSuchInvitation = 0,

    /// <summary>
    /// The record was revoked, whatever else is true of it.
    /// </summary>
    Revoked = 1,

    /// <summary>
    /// The record has passed its expiry at the instant the decision was made.
    /// </summary>
    Expired = 2,

    /// <summary>
    /// The record has no uses left.
    /// </summary>
    Spent = 3,

    /// <summary>
    /// The invitation may produce an account.
    /// </summary>
    Honoured = 4,
}
