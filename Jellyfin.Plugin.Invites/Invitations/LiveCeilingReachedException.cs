using System;
using System.Globalization;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// Raised when minting would take the store past the ceiling on how many
/// invitations may be live at once.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is not an argument fault, and that is why it is not an
/// <see cref="ArgumentException"/>.</b> Every value the caller sent is
/// acceptable; what refuses the mint is the state of the store. The same call
/// made after an operator revokes one invitation, or after one expires,
/// succeeds unchanged. A refusal that told an operator to fix their request
/// would be telling them to change the one thing that is not wrong.
/// </para>
/// <para>
/// It carries the count and the ceiling because the two together are what an
/// operator acts on: a refusal saying only that a limit was reached leaves them
/// guessing whether they are one over or a hundred over, and the second case is
/// a different problem from the first.
/// </para>
/// </remarks>
public sealed class LiveCeilingReachedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LiveCeilingReachedException"/> class.
    /// </summary>
    /// <param name="live">How many invitations were live when the mint was refused.</param>
    /// <param name="ceiling">The ceiling that refused it.</param>
    public LiveCeilingReachedException(int live, int ceiling)
        : base(Sentence(live, ceiling))
    {
        Live = live;
        Ceiling = ceiling;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveCeilingReachedException"/> class.
    /// </summary>
    /// <remarks>
    /// The three constructors below carry no counts, so
    /// <see cref="Live"/> and <see cref="Ceiling"/> take the default of their
    /// type and nothing here writes them. THEY EACH HELD <c>Live = 0;</c> AND
    /// <c>Ceiling = 0;</c> UNTIL #376, and those six statements were the same
    /// value written twice: a mutation run removed each body and no test
    /// noticed, because there is nothing to notice. The sentence is what the
    /// assignments were saying, and it says it without leaving three mutants
    /// nobody can kill.
    /// </remarks>
    public LiveCeilingReachedException()
        : base("This server already holds as many live invitations as this plugin allows.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveCeilingReachedException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public LiveCeilingReachedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveCeilingReachedException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">What went wrong underneath.</param>
    public LiveCeilingReachedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets how many invitations were live when the mint was refused.
    /// </summary>
    public int Live { get; }

    /// <summary>
    /// Gets the ceiling that refused the mint.
    /// </summary>
    public int Ceiling { get; }

    private static string Sentence(int live, int ceiling)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "This server holds {0} live invitations and this plugin allows at most {1} at once, so nothing was minted. Revoking one that is no longer wanted, or waiting for one to expire, makes room for another. The ceiling bounds what the outstanding set can authorise without a further operator action; it is not a bound on how large the store file may grow, because an expired or spent record stays where it is.",
            live,
            ceiling);
    }
}
