namespace Jellyfin.Plugin.Invites.Attempts;

/// <summary>
/// What one entry of the attempt trail says happened.
/// </summary>
/// <remarks>
/// <para>
/// The set is closed and the field is never free text, which is what lets
/// docs/personal-data.md say what the trail holds without knowing what happened.
/// docs/attempt-outcomes.md is where each member is argued and where a member
/// added here has to gain a row; <c>AttemptOutcomeSetTests</c> holds the two
/// together in both directions, so a member with no row and a row with no member
/// are both refused.
/// </para>
/// <para>
/// <b>These are the operator's words rather than the visitor's.</b> Nothing here
/// reaches a page. #28 requires every failed redemption to look the same to
/// whoever presented the code, so the trail is where the difference between an
/// absent, an expired, a spent and a revoked code is kept and the response is
/// where it is not.
/// </para>
/// <para>
/// <b>Why this is not <see cref="Redemption.RedemptionOutcome"/>.</b> That type
/// is what one routine concluded about one presented code. This one is what the
/// trail records, and the two sets differ at both ends: a decision can reach
/// <c>Honoured</c>, which says an invitation may produce an account, while the
/// trail records <see cref="Accepted"/>, which says one was created, and a
/// redemption honoured that then failed to create an account is the state
/// between them. At the other end the trail records refusals no decision makes,
/// because they happen before or after it.
/// </para>
/// </remarks>
public enum AttemptOutcome
{
    /// <summary>
    /// The code was honoured and an account was created.
    /// </summary>
    Accepted = 0,

    /// <summary>
    /// The presented code matched no record. The entry carries no invitation
    /// identifier, because there is none to carry.
    /// </summary>
    NoSuchInvitation = 1,

    /// <summary>
    /// The record was found and its expiry had passed at the single clock
    /// reading this redemption took.
    /// </summary>
    Expired = 2,

    /// <summary>
    /// The record was found and had no uses left.
    /// </summary>
    Spent = 3,

    /// <summary>
    /// The record was found and the operator had revoked it.
    /// </summary>
    Revoked = 4,

    /// <summary>
    /// The attempt was refused before or at the lookup because a limit was
    /// reached.
    /// </summary>
    /// <remarks>
    /// One entry per episode rather than one per refused request, which is the
    /// answer #43 and #31 share. The entry's
    /// <see cref="AttemptEntry.AttemptsCovered"/> is how many refused requests
    /// the episode accounts for. Where an episode starts and ends is the
    /// limiter's, not this type's.
    /// </remarks>
    RefusedByRateLimit = 5,

    /// <summary>
    /// The redemption was refused because a ceiling on what the plugin may
    /// create was reached.
    /// </summary>
    RefusedByCeiling = 6,

    /// <summary>
    /// The submission failed the cross-site check.
    /// </summary>
    RefusedByAntiForgery = 7,

    /// <summary>
    /// The answers on the form did not validate on the server.
    /// </summary>
    RefusedByValidation = 8,

    /// <summary>
    /// Failure entries were dropped to keep the trail inside its bound, and this
    /// entry says how many attempts went with them.
    /// </summary>
    /// <remarks>
    /// The trail's own admission rather than an attempt's outcome, and it is a
    /// member of this set so that every entry carries one value from one closed
    /// set and the inventory's sentence stays exactly true. A trail that silently
    /// forgot would be worse than one that says it did, and the admission costs
    /// one entry. <see cref="AttemptTrail"/> is the only thing that may write it.
    /// </remarks>
    FailuresDropped = 9,
}
