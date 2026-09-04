using System;
using Jellyfin.Plugin.Invites.Time;

namespace Jellyfin.Plugin.Invites.Accounts;

/// <summary>
/// How many accounts this plugin may create in a window, across every
/// invitation.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the third of #33's ceilings and the one that still holds when the
/// other two are set badly.</b> The use count bounds one invitation and the live
/// ceiling bounds how many invitations may stand at once, and both are chosen by
/// an operator or by a constant an operator can be talked past. Five hundred
/// live invitations at ten uses each is five thousand accounts the standing set
/// can authorise with no further operator action, which is what this bounds.
/// </para>
/// <para>
/// <b>Fifty accounts in twenty-four hours, and the number is reasoned rather
/// than counted.</b> Nobody has watched a real server. What is known is what the
/// tree already decides: an invitation is good for at most ten accounts, a
/// household or a group of friends is well under fifty in a day, and an operator
/// moving a whole server's users has the server's own user editor, which this
/// plugin never touches. Against the five thousand above, this turns one event
/// into a hundred days of growth somebody would notice.
/// </para>
/// <para>
/// <b>The window is fixed and aligned to the clock rather than to the first
/// request.</b> It runs from one midnight to the next, the same as it does for
/// everybody, rather than starting when somebody first asks. Twice the number is
/// therefore reachable across a boundary, which is the same trade
/// <see cref="Redemption.AttemptLimiter"/> makes and for the same reason: a
/// sliding window has to remember every instant it counted, and an endpoint a
/// stranger can hammer is the wrong place to keep a growing list. A hundred
/// accounts around one crossing is still a bound, and the number above is chosen
/// with that doubling in mind.
/// </para>
/// <para>
/// <b>An allowed request is counted even where nothing is created, and that
/// direction is deliberate.</b> A request the server then refuses has spent a
/// place, so the count is an upper bound on accounts created rather than the
/// exact number of them. For a ceiling that is the safe direction: the mistake
/// it must never make is letting one more account exist than the number allows.
/// <see cref="Redemption.AttemptLimiter"/> argues the opposite for itself
/// because it bounds guesses, and a guess that was refused was never a guess.
/// </para>
/// <para>
/// <b>It counts in memory, for the life of the process.</b> A restart resets it,
/// which is stated rather than hidden: nothing on a record says when an account
/// was created, so there is nothing durable to count, and the field that would
/// close it is #52's. What a restart costs is one fresh window, and somebody who
/// can restart the server has powers this ceiling is not the defence against.
/// </para>
/// <para>
/// <b>It decides nothing about what a refused person sees.</b> That is
/// docs/refusal-response.md, which lists a ceiling refusal as one of the six
/// cases answered with the one page, so a caller cannot tell this refusal from
/// any other. It also holds no address, no code and no identifier: it is one
/// integer and a window number.
/// </para>
/// </remarks>
public sealed class CreationCeiling
{
    /// <summary>
    /// Accounts this plugin may create in one window, across every invitation.
    /// </summary>
    public const int AccountsInAWindow = 50;

    private readonly IClock _clock;
    private readonly object _gate = new();

    private int _created;
    private long _window = long.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreationCeiling"/> class.
    /// </summary>
    /// <param name="clock">The one time source, so a test can move it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
    public CreationCeiling(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        _clock = clock;
    }

    /// <summary>
    /// Gets how long a window lasts.
    /// </summary>
    public static TimeSpan Window { get; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets how many creations have been allowed in the window the clock is in
    /// now.
    /// </summary>
    /// <remarks>
    /// Here so the claim that the count is per window is something a test can
    /// read rather than something this file asserts about itself. It is a count
    /// and never the accounts.
    /// </remarks>
    public int AllowedInThisWindow
    {
        get
        {
            lock (_gate)
            {
                return WindowOf(_clock.UtcNow) == _window ? _created : 0;
            }
        }
    }

    /// <summary>
    /// Whether an account may be created now, counting it if so.
    /// </summary>
    /// <remarks>
    /// Asked before the use is taken, so a redemption this refuses leaves the
    /// invitation exactly as it found it. An invitation spent against a ceiling
    /// would cost the operator a fresh mint for a refusal that was nothing to do
    /// with the person holding the link.
    /// </remarks>
    /// <returns>
    /// <c>true</c> where the window has room and this creation has been counted.
    /// </returns>
    public bool MayCreate()
    {
        var window = WindowOf(_clock.UtcNow);

        lock (_gate)
        {
            if (window != _window)
            {
                _window = window;
                _created = 0;
            }

            if (_created >= AccountsInAWindow)
            {
                return false;
            }

            _created++;

            return true;
        }
    }

    /// <summary>
    /// Which fixed window an instant falls in.
    /// </summary>
    /// <param name="now">The clock reading.</param>
    /// <returns>The window's number, counted from the epoch.</returns>
    private static long WindowOf(DateTimeOffset now) =>
        now.ToUnixTimeMilliseconds() / (long)Window.TotalMilliseconds;
}
