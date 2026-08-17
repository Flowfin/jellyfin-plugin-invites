using System;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// What one minting produced: the record that was stored, and the code that was
/// not.
/// </summary>
/// <remarks>
/// The two travel together for exactly one moment and then separate for good.
/// The record goes to the store; the code goes into the response and is
/// forgotten here. Returning them as one value is what makes the separation
/// visible at the call site rather than leaving a caller to notice that the
/// record it was handed has no code on it.
/// </remarks>
public sealed class Minting
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Minting"/> class.
    /// </summary>
    /// <param name="code">The code, in the form it goes into a link.</param>
    /// <param name="invitation">The record that was stored.</param>
    /// <exception cref="ArgumentException">The code is null or blank.</exception>
    /// <exception cref="ArgumentNullException">The record is null.</exception>
    public Minting(string code, Invitation invitation)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A minting that produced no code is a link nobody can follow.", nameof(code));
        }

        ArgumentNullException.ThrowIfNull(invitation);

        Code = code;
        Invitation = invitation;
    }

    /// <summary>
    /// Gets the code, which is the credential and is returned exactly once.
    /// </summary>
    /// <remarks>
    /// Nothing stores this. The store holds the keyed hash the code reduces to,
    /// so a caller that does not put this value in front of the operator now
    /// has lost it, and the repair is minting again rather than a lookup.
    /// </remarks>
    public string Code { get; }

    /// <summary>
    /// Gets the record that was written.
    /// </summary>
    public Invitation Invitation { get; }
}
