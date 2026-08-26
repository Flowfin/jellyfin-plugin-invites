using System;
using System.Collections.Generic;
using Jellyfin.Plugin.Invites.Time;

namespace Jellyfin.Plugin.Invites.Redemption;

/// <summary>
/// How many presented codes may be judged, per source address and across all of
/// them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every number and every lifetime here is decided in docs/rate-limit.md and
/// none of them is chosen in this file.</b> That page settles the lifetime before
/// the thresholds on purpose, because a number chosen first invites a lifetime
/// chosen to make the number look load-bearing. What is written here is the
/// counter that page describes and nothing else.
/// </para>
/// <para>
/// <b>An attempt is a presented code being judged.</b> Fetching the setup page is
/// not one: that route reads no invitation and decides nothing, so counting it
/// would count somebody opening their link twice. It follows that a request this
/// limiter refuses is not an attempt either, and this is the half worth reading
/// carefully. A refused request is not counted, so the guarantee is exact rather
/// than approximate: at most <see cref="PerAddressCeiling"/> codes are judged for
/// one address in a window and at most <see cref="GlobalCeiling"/> across all of
/// them, which is precisely what docs/code-entropy.md's throttled row assumes.
/// Counting refusals as well would let somebody who is already being refused go
/// on adding to a total that has stopped meaning anything.
/// </para>
/// <para>
/// <b>Fixed windows, and the cost is measured rather than waved at.</b> A fixed
/// window lets somebody run at twice the stated rate across a boundary. That
/// doubling adds exactly one bit to what a search needs, against forty-one bits
/// of headroom, and it is why the window is fixed rather than sliding. The
/// argument is docs/rate-limit.md's and is not remade here.
/// </para>
/// <para>
/// <b>What bounds the memory it holds is the other limit rather than a cap
/// written here.</b> The per-address counter has to remember an address to limit
/// it, and the map is replaced whole when the window turns rather than being
/// swept entry by entry. Its size inside one window is bounded by the global
/// limit, because an entry is only ever created for an attempt that was allowed:
/// at ten a second, an hour admits at most thirty-six thousand of them. So an
/// attacker with a million addresses does not buy a million entries; they buy the
/// same thirty-six thousand attempts everybody else is sharing.
/// </para>
/// <para>
/// <b>It answers and never speaks.</b> There is no logging here at all. The key
/// is a source address, which docs/personal-data.md keeps out of anything
/// durable, and a log line is durable.
/// </para>
/// <para>
/// <b>What this type does not decide.</b> What a refused attempt looks like is
/// docs/refusal-response.md, and a limiter that answered differently from the
/// ordinary refusal would be the oracle that whole set exists to close. Whether a
/// throttled attempt appends a trail entry is docs/attempt-outcomes.md. Neither
/// is answered here, and neither can be until there is a route that judges a
/// presented code. Nothing calls this type today.
/// </para>
/// </remarks>
public sealed class AttemptLimiter
{
    /// <summary>
    /// Attempts one source address may have judged in a window.
    /// </summary>
    public const int PerAddressCeiling = 20;

    /// <summary>
    /// Attempts all sources together may have judged in a window.
    /// </summary>
    public const int GlobalCeiling = 10;

    private readonly IClock _clock;
    private readonly object _gate = new();

    private Dictionary<string, int> _perAddress = new(StringComparer.Ordinal);
    private long _perAddressWindow = long.MinValue;

    private int _global;
    private long _globalWindow = long.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="AttemptLimiter"/> class.
    /// </summary>
    /// <param name="clock">The one time source, so a test can move it.</param>
    public AttemptLimiter(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Gets how long the per-address window lasts.
    /// </summary>
    public static TimeSpan PerAddressWindow { get; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets how long the global window lasts.
    /// </summary>
    public static TimeSpan GlobalWindow { get; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets how many source addresses the counter is holding right now.
    /// </summary>
    /// <remarks>
    /// Here so the claim that the address is held for its window and no longer is
    /// something a test can read rather than something this file asserts about
    /// itself. It is a count and never the addresses.
    /// </remarks>
    public int AddressesHeld
    {
        get
        {
            lock (_gate)
            {
                return _perAddressWindow == Window(_clock.UtcNow, PerAddressWindow) ? _perAddress.Count : 0;
            }
        }
    }

    /// <summary>
    /// Whether a presented code from this address may be judged, counting it if
    /// so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both windows are read from one clock reading, so a call cannot be inside
    /// one window and outside the other because time passed between two reads.
    /// </para>
    /// <para>
    /// The global limit is asked first and the per-address count is only raised
    /// when both allow it. Raising one of the two for a request the other refused
    /// would spend an allowance on an attempt that was never judged, which is the
    /// same mistake as counting a refusal.
    /// </para>
    /// </remarks>
    /// <param name="sourceAddress">
    /// The address the request came from, as the caller reads it. This type never
    /// derives it and never keeps it beyond its window.
    /// </param>
    /// <returns>
    /// <c>true</c> where the attempt is within both limits and has been counted.
    /// </returns>
    /// <exception cref="ArgumentException">The address is null or blank.</exception>
    public bool MayJudge(string sourceAddress)
    {
        if (string.IsNullOrWhiteSpace(sourceAddress))
        {
            throw new ArgumentException(
                "A limiter keyed by source address cannot count an attempt that names no address. A caller that cannot read one has to decide what that means rather than being counted as everybody.",
                nameof(sourceAddress));
        }

        var now = _clock.UtcNow;

        lock (_gate)
        {
            var globalWindow = Window(now, GlobalWindow);
            if (globalWindow != _globalWindow)
            {
                _globalWindow = globalWindow;
                _global = 0;
            }

            var addressWindow = Window(now, PerAddressWindow);
            if (addressWindow != _perAddressWindow)
            {
                _perAddressWindow = addressWindow;

                // Replaced whole rather than cleared, so nothing from the last
                // window is reachable through this instance for any address,
                // including one that never comes back.
                _perAddress = new Dictionary<string, int>(StringComparer.Ordinal);
            }

            if (_global >= GlobalCeiling)
            {
                return false;
            }

            _perAddress.TryGetValue(sourceAddress, out var forThisAddress);
            if (forThisAddress >= PerAddressCeiling)
            {
                return false;
            }

            _global++;
            _perAddress[sourceAddress] = forThisAddress + 1;

            return true;
        }
    }

    /// <summary>
    /// Which fixed window an instant falls in.
    /// </summary>
    /// <param name="now">The clock reading.</param>
    /// <param name="length">How long a window lasts.</param>
    /// <returns>The window's number, counted from the epoch.</returns>
    private static long Window(DateTimeOffset now, TimeSpan length)
    {
        return now.UtcTicks / length.Ticks;
    }
}
