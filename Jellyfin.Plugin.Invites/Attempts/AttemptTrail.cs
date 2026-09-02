using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Jellyfin.Plugin.Invites.Attempts;

/// <summary>
/// The trail of redemption attempts, oldest first, with the bound that keeps an
/// endpoint a stranger can hammer from filling a disk.
/// </summary>
/// <remarks>
/// <para>
/// <b>The bound is on failures and not on the whole trail.</b> One oldest-first
/// ring over everything satisfies the word bounded and is refused by name on
/// docs/attempt-outcomes.md as a history-erasing attack: a few thousand failures
/// would push out every success the operator opened the trail to find. Successes
/// are kept, and they are already bounded by other means, because nothing a
/// stranger does produces one without also producing an account and how many
/// accounts the plugin may create is a ceiling in #33.
/// </para>
/// <para>
/// <b>The number is one thousand and it is read off the limits rather than
/// chosen here.</b> docs/attempt-outcomes.md derives it from the two rates in
/// docs/rate-limit.md: at twenty judged attempts an hour per source address, one
/// source running all the way to its ceiling fills a fiftieth of the trail, so it
/// is visible whole and cannot push the rest out. That page is where the
/// arithmetic lives and where an argument with the number belongs.
/// </para>
/// <para>
/// <b>The drop notice is folded rather than accumulated, and that is the part to
/// read carefully.</b> A notice appended on every drop and never removed would be
/// the unbounded thing the bound exists against, arriving through the door
/// marked honesty: an attacker hammering for a week would leave a trail of
/// thousands of admissions. So a drop takes any earlier notice with it and adds
/// that notice's count into the new one. The trail therefore holds at most one
/// notice, its count is cumulative, and nothing about the trail grows without
/// bound.
/// </para>
/// <para>
/// <b>Every value is carried in, and that is not a convenience.</b> The instant
/// comes from the caller's single clock reading rather than from the machine
/// clock, which is the seam #41 built and which
/// <c>clock-read-outside-the-seam</c> refuses a second authority for. Nothing
/// here judges an expiry, a use count or a revocation either: that is
/// <see cref="Redemption.RedemptionDecision"/>'s, and
/// <c>expiry-or-use-count-judged-outside-the-decision</c> refuses a second one.
/// </para>
/// <para>
/// <b>What is not here.</b> Nothing appends to a trail on a running server,
/// because the route that judges a presented code is #399 and does not exist.
/// THAT SENTENCE NAMED #74 BESIDE IT, which is the setup page rather than the
/// post, for the reason <see cref="AttemptEntry"/> carries. Nothing writes a trail to disk either: where it is persisted, and under
/// which store version, is not decided anywhere in this tree and is not decided
/// here. The value semantics below are what a persisting caller would write and
/// read back, rather than a claim that one does.
/// </para>
/// </remarks>
public sealed class AttemptTrail
{
    /// <summary>
    /// How many failure entries are kept before the oldest are dropped.
    /// </summary>
    /// <remarks>
    /// docs/personal-data.md names this <c>trail-bound</c> and that is the name
    /// to search for. It counts entries rather than attempts, so an episode
    /// standing for many refused requests is one of the thousand.
    /// </remarks>
    public const int FailureBound = 1000;

    private readonly ImmutableArray<AttemptEntry> _entries;

    private AttemptTrail(ImmutableArray<AttemptEntry> entries, long attemptsSeen)
    {
        _entries = entries;
        AttemptsSeen = attemptsSeen;
    }

    /// <summary>
    /// Gets a trail with nothing in it, which is what a fresh install has.
    /// </summary>
    public static AttemptTrail Empty { get; } = new AttemptTrail(ImmutableArray<AttemptEntry>.Empty, 0);

    /// <summary>
    /// Gets the entries, oldest first.
    /// </summary>
    public ImmutableArray<AttemptEntry> Entries => _entries;

    /// <summary>
    /// Gets how many attempts have ever been appended, including the ones whose
    /// entries have since been dropped.
    /// </summary>
    /// <remarks>
    /// Counted as the appends happen rather than by reading the entries back, so
    /// that it is a second and independent statement of the same quantity: the
    /// sum of <see cref="AttemptEntry.AttemptsCovered"/> over
    /// <see cref="Entries"/> equals this, and a drop that lost a count would make
    /// the two disagree.
    /// </remarks>
    public long AttemptsSeen { get; }

    /// <summary>
    /// Gets how many failure entries the trail is holding.
    /// </summary>
    public int Failures => _entries.Count(entry => entry.IsFailure);

    /// <summary>
    /// Appends one attempt and applies the bound.
    /// </summary>
    /// <param name="entry">The attempt, built through <see cref="AttemptEntry.Of"/>.</param>
    /// <returns>
    /// A trail carrying the entry, with the oldest failures dropped and one
    /// notice appended where the bound was passed.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The entry is a drop notice. Those are the trail's own and a caller
    /// handing one in would be writing the admission rather than earning it.
    /// </exception>
    public AttemptTrail Append(AttemptEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.IsDropNotice)
        {
            throw new ArgumentOutOfRangeException(
                nameof(entry),
                "A drop notice is written by the trail when it drops something, so a caller handing one in would be recording a drop that did not happen.");
        }

        var seen = AttemptsSeen + entry.AttemptsCovered;
        var kept = _entries.Add(entry);

        var failures = kept.Count(one => one.IsFailure);
        if (failures <= FailureBound)
        {
            return new AttemptTrail(kept, seen);
        }

        // One append adds one entry, so a trail that was inside the bound is at
        // most one over it, and exactly one failure goes. Written as one removal
        // rather than as a loop for the reason this repository states about
        // guards: a loop that can only ever run once is a branch no test can be
        // put to, and a branch nothing can reach proves nothing.
        var dropped = 0;
        var survivors = new List<AttemptEntry>(kept.Length);
        var taken = false;

        foreach (var one in kept)
        {
            // The earlier notice goes with it and its count is carried into the
            // new one. Keeping it would leave one notice per drop, which is the
            // growth the bound exists against arriving through the door marked
            // honesty.
            if (one.IsDropNotice)
            {
                dropped += one.AttemptsCovered;
                continue;
            }

            // Oldest first, and only failures. A success is never dropped, so
            // the walk passes over it rather than stopping at it.
            if (!taken && one.IsFailure)
            {
                dropped += one.AttemptsCovered;
                taken = true;
                continue;
            }

            survivors.Add(one);
        }

        survivors.Add(AttemptEntry.Dropped(dropped, entry.At));

        return new AttemptTrail(survivors.ToImmutableArray(), seen);
    }
}
