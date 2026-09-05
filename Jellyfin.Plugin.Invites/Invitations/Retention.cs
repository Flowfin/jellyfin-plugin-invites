using System;
using Jellyfin.Plugin.Invites.Redemption;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// How long a record that has stopped being usable is kept, and whether a
/// particular one has been kept that long.
/// </summary>
/// <remarks>
/// <para>
/// <b>The number is a decision rather than a measurement.</b> Decision 8 in #11
/// chose ninety days on 2026-08-09 and docs/personal-data.md carries the
/// argument: long enough that an operator meeting an account they do not
/// recognise can still find where it came from, short enough that what is left
/// behind is not an indefinite register of who was invited. Nothing in this tree
/// can be run to produce that number and nothing here should be read as having
/// measured it.
/// </para>
/// <para>
/// <b>This decides removal and never decides usability.</b> #59 warns in as many
/// words against a sweep that marks invitations expired, because that would be a
/// second authority for a fact <see cref="RedemptionDecision"/> already owns plus
/// a window in which an expired invitation is still honoured because the sweep
/// has not run. So the only question asked here is arithmetic on an instant the
/// decision routine hands back, and a record this type says may be removed is one
/// that was already refused before the sweep existed.
/// </para>
/// <para>
/// <b>What it costs to be wrong in each direction is not symmetric.</b> Removing
/// a record early destroys the only link between an account and the invitation
/// that produced it, which is the answer an operator opens this plugin to get and
/// which nothing restores. Removing one late leaves a record that goes on the
/// next sweep. So every rounding in this type and in
/// <see cref="RedemptionDecision.RetentionStartsAt"/> is towards keeping.
/// </para>
/// </remarks>
public static class Retention
{
    /// <summary>
    /// Gets how long a spent, expired or revoked record is kept after it stops
    /// being usable, on a server where nobody has changed the setting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A property rather than a constant because <see cref="TimeSpan"/> cannot be
    /// one, and static readonly rather than computed per call so there is a single
    /// value to point at. docs/personal-data.md names it <c>record-retention</c>
    /// and that is the name to search for.
    /// </para>
    /// <para>
    /// THIS IS THE DEFAULT RATHER THAN THE PERIOD EVERY SERVER USES, since #86.
    /// <c>RecordRetentionDays</c> on the configuration type carries what this
    /// server keeps, bounded at both ends, and the sweep asks
    /// <see cref="NumberSettings"/> for it. This value is what a
    /// server that never opened the configuration page runs on and what the
    /// setting defaults to, and it is still the one number the argument above is
    /// written about.
    /// </para>
    /// </remarks>
    public static TimeSpan RecordRetention { get; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Whether the retention period has run out for this record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// False for every live record, because a live record has no instant to count
    /// from: <see cref="RedemptionDecision.RetentionStartsAt"/> answers null and
    /// this answers false. That is the clause of #59 that says the sweep never
    /// deletes an invitation with uses remaining that has not expired, and it is
    /// held by there being no arithmetic to reach rather than by a second test of
    /// the record's fields.
    /// </para>
    /// <para>
    /// The comparison is inclusive, so a record whose period ends exactly at
    /// <paramref name="now"/> may be removed. The instant is a boundary a sweep
    /// running on a schedule will practically never land on, and choosing the
    /// direction anyway is cheaper than leaving it to whoever reads this next.
    /// </para>
    /// </remarks>
    /// <param name="invitation">The record to judge.</param>
    /// <param name="now">
    /// The clock reading, read once by the caller through
    /// <see cref="Time.IClock"/> and used for every record in one sweep.
    /// </param>
    /// <returns>
    /// <c>true</c> where the record stopped being usable at least
    /// <see cref="RecordRetention"/> ago.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="invitation"/> is null.</exception>
    public static bool MayBeRemoved(Invitation invitation, DateTimeOffset now) =>
        MayBeRemoved(invitation, now, RecordRetention);

    /// <summary>
    /// Whether a retention period of this length has run out for this record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same arithmetic as the two-argument form against a period a caller
    /// chose, which is how the setting #86 landed reaches the rule without the
    /// rule reading a configuration. Everything the remarks above say about
    /// direction, about the inclusive comparison and about a live record having
    /// no instant to count from holds here unchanged, because there is one
    /// comparison and this is it.
    /// </para>
    /// <para>
    /// The period is not judged here. Whether a number an operator typed may be
    /// used at all is
    /// <see cref="NumberSettings"/>, which refuses one outside its
    /// range rather than substituting the default, and a caller that reached this
    /// with a period has already been past that.
    /// </para>
    /// </remarks>
    /// <param name="invitation">The record to judge.</param>
    /// <param name="now">
    /// The clock reading, read once by the caller through
    /// <see cref="Time.IClock"/> and used for every record in one sweep.
    /// </param>
    /// <param name="period">How long a record is kept after it stops being usable.</param>
    /// <returns>
    /// <c>true</c> where the record stopped being usable at least
    /// <paramref name="period"/> ago.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="invitation"/> is null.</exception>
    public static bool MayBeRemoved(Invitation invitation, DateTimeOffset now, TimeSpan period)
    {
        // Stryker disable once Statement : the same refusal stands one call
        // down. RedemptionDecision.RetentionStartsAt raises ArgumentNullException
        // for the same argument under the same name, so removing this line
        // changes nothing a caller can observe and no test can kill the mutant.
        // The line stays because a public boundary that refuses its own null is
        // not a duplicate of a private one: whoever edits that routine next is
        // not editing this contract. Argued in docs/mutation-testing.md under
        // #376.
        ArgumentNullException.ThrowIfNull(invitation);

        return RedemptionDecision.RetentionStartsAt(invitation, now) is { } since
            && since + period <= now;
    }
}
