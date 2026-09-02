using System;

namespace Jellyfin.Plugin.Invites.Attempts;

/// <summary>
/// One line of the attempt trail: what happened, when, and against which
/// invitation where one matched.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fields are the inventory's fields and there is no room for a fourth.</b>
/// docs/personal-data.md lists three for this trail - the invitation identifier
/// where one matched, the outcome from a fixed set, and the time - and refuses
/// the source address by name, because a value held for as long as the trail is
/// a different thing from a value held for as long as a rate-limiting window.
/// No code, no hash and no password is here, and none can be: the type carries
/// no string at all, so there is nothing for one to be put in.
/// </para>
/// <para>
/// <b><see cref="AttemptsCovered"/> is the fourth member and it is not a fourth
/// field of the inventory.</b> It is a count of attempts rather than anything
/// about a person, and it exists because two entries stand for more than one
/// attempt each: a rate-limiting episode, which is one entry for as many refused
/// requests as the episode ran to, and the trail's own drop notice, which is one
/// entry for as many attempts as went out of the bound with it. Every other entry
/// carries one. The property that makes it worth having is that the sum over the
/// whole trail is the number of attempts that were ever appended, so a trail that
/// has dropped half its entries still says how many attempts it saw.
/// </para>
/// <para>
/// <b>Nothing appends one of these yet.</b> The route that judges a presented
/// code is #399, and until it exists this type records nothing on a running
/// server. What is here is the entry, its set and its bound; what is not is a
/// caller.
/// </para>
/// <para>
/// THAT SENTENCE NAMED #74 BESIDE IT. #74 landed the setup page and judges no
/// code; the act was split on 2026-08-31 under #71 and the post became #399. A
/// reader following the pair reached one issue that was done and read the caller
/// as half built.
/// </para>
/// </remarks>
public sealed class AttemptEntry
{
    private AttemptEntry(Guid? invitation, AttemptOutcome outcome, DateTimeOffset at, int attemptsCovered)
    {
        Invitation = invitation;
        Outcome = outcome;
        At = at;
        AttemptsCovered = attemptsCovered;
    }

    /// <summary>
    /// Gets the invitation the attempt was made against, or <c>null</c> where
    /// the presented code matched no record.
    /// </summary>
    /// <remarks>
    /// The null is the inventory's own word: the field is empty where the
    /// presented code matched nothing, because there is nothing to name. A
    /// <see cref="AttemptOutcome.NoSuchInvitation"/> entry that named one would
    /// be claiming a lookup succeeded, and it is refused rather than ignored.
    /// </remarks>
    public Guid? Invitation { get; }

    /// <summary>
    /// Gets what happened, as one value from the closed set.
    /// </summary>
    public AttemptOutcome Outcome { get; }

    /// <summary>
    /// Gets when it happened, as the caller's single clock reading.
    /// </summary>
    /// <remarks>
    /// Read through <see cref="Time.IClock"/> by whoever appends, once per
    /// redemption, which is the rule #51 states for every comparison in one
    /// redemption and which this trail follows rather than restates.
    /// </remarks>
    public DateTimeOffset At { get; }

    /// <summary>
    /// Gets how many attempts this entry accounts for, which is one for every
    /// entry except a rate-limiting episode and the trail's drop notice.
    /// </summary>
    public int AttemptsCovered { get; }

    /// <summary>
    /// Gets a value indicating whether this entry is the trail's admission that
    /// it dropped failures rather than a record of an attempt.
    /// </summary>
    public bool IsDropNotice => Outcome == AttemptOutcome.FailuresDropped;

    /// <summary>
    /// Gets a value indicating whether this entry is a failure, which is what
    /// the trail's bound counts.
    /// </summary>
    /// <remarks>
    /// A success is every entry saying an account was created, and those are
    /// kept: nothing a stranger does produces one without also producing an
    /// account, and how many accounts the plugin may create is bounded by the
    /// ceilings in #33 instead. The drop notice is neither, or a trail at its
    /// bound would drop the entry that says it dropped something.
    /// </remarks>
    public bool IsFailure => Outcome != AttemptOutcome.Accepted && !IsDropNotice;

    /// <summary>
    /// One attempt, judged.
    /// </summary>
    /// <param name="invitation">
    /// The invitation the attempt was against, or <c>null</c> where the
    /// presented code matched no record.
    /// </param>
    /// <param name="outcome">What was concluded.</param>
    /// <param name="at">The caller's clock reading for this redemption.</param>
    /// <param name="attemptsCovered">
    /// How many attempts this entry accounts for. One for everything except a
    /// rate-limiting episode.
    /// </param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The outcome is <see cref="AttemptOutcome.FailuresDropped"/>, which is the
    /// trail's own and not an attempt's; or the count is below one; or the
    /// outcome is <see cref="AttemptOutcome.NoSuchInvitation"/> and an
    /// invitation was named anyway.
    /// </exception>
    public static AttemptEntry Of(Guid? invitation, AttemptOutcome outcome, DateTimeOffset at, int attemptsCovered)
    {
        if (outcome == AttemptOutcome.FailuresDropped)
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "FailuresDropped is the trail's admission that it dropped entries, not the outcome of an attempt. Only AttemptTrail writes one.");
        }

        if (attemptsCovered < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptsCovered),
                attemptsCovered,
                "An entry accounts for at least one attempt. An entry covering none would be a line in the trail that nothing happened for.");
        }

        if (outcome == AttemptOutcome.NoSuchInvitation && invitation is not null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(invitation),
                "NoSuchInvitation says the presented code matched no record, so there is no invitation to name. Naming one would record a lookup that did not happen.");
        }

        return new AttemptEntry(invitation, outcome, at, attemptsCovered);
    }

    /// <summary>
    /// The trail's admission that it dropped failure entries.
    /// </summary>
    /// <param name="attemptsDropped">
    /// How many attempts went out of the bound, counted through the
    /// <see cref="AttemptsCovered"/> of every entry that was dropped, so a
    /// dropped episode takes its whole count with it.
    /// </param>
    /// <param name="at">When the drop happened.</param>
    /// <returns>The entry.</returns>
    /// <remarks>
    /// Public so that a reader of a persisted trail can build one back, and
    /// refused by <see cref="AttemptTrail.Append"/> so that handing one to a
    /// trail is not a way to write an admission the trail did not earn.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Nothing was dropped, which is not a thing to write a notice about.
    /// </exception>
    public static AttemptEntry Dropped(int attemptsDropped, DateTimeOffset at)
    {
        if (attemptsDropped < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptsDropped),
                attemptsDropped,
                "A drop notice says how many attempts were dropped, so there is nothing to say where none were.");
        }

        return new AttemptEntry(null, AttemptOutcome.FailuresDropped, at, attemptsDropped);
    }
}
