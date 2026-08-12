using System;
using System.Globalization;

namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// Raised where the keyed hash secret cannot be used and generating a new one
/// would be a rotation nobody asked for.
/// </summary>
/// <remarks>
/// <para>
/// Two situations reach this and they are one refusal. The secret file is not
/// there while the store holds records, or it is there and is not a secret this
/// build can use. In both the stored hashes were computed under a key that is
/// now unavailable, so every live invitation is already unverifiable, and the
/// only thing left to decide is whether the plugin says so or hides it.
/// </para>
/// <para>
/// It hides nothing. The alternative is generating a fresh secret on the spot,
/// which starts cleanly, reports a healthy server and turns every live
/// invitation into one that can never be redeemed. That is
/// <see cref="HashSecret.Rotate"/> arriving through the side door, without the
/// count an operator is owed before it happens, and #30 refuses it by name.
/// </para>
/// <para>
/// The repair is an operator's rather than the plugin's: put the file back from
/// a backup, or rotate deliberately and accept the count that action prints.
/// Nothing here writes, moves or deletes anything on the way out, so a file that
/// was merely unreadable this minute is still there to be read the next.
/// </para>
/// </remarks>
public class HashSecretRefusedException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HashSecretRefusedException"/> class.
    /// </summary>
    public HashSecretRefusedException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HashSecretRefusedException"/> class.
    /// </summary>
    /// <param name="message">What went wrong.</param>
    public HashSecretRefusedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HashSecretRefusedException"/> class.
    /// </summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">What it happened during.</param>
    public HashSecretRefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// The refusal for a secret file that is not there while the store holds
    /// records.
    /// </summary>
    /// <param name="path">The file that was looked for.</param>
    /// <param name="records">How many records the store held.</param>
    /// <returns>The refusal, with the file and the count in its message.</returns>
    public static HashSecretRefusedException Missing(string path, int records)
    {
        return new HashSecretRefusedException(string.Format(
            CultureInfo.InvariantCulture,
            "{0} is not there and the store holds {1} record(s). Every stored hash was computed under that secret, so a new one would make all {1} unredeemable while reporting a healthy start. Put the file back, or rotate deliberately.",
            path,
            records));
    }

    /// <summary>
    /// The refusal for a secret file that is there and is not the right length.
    /// </summary>
    /// <param name="path">The file that was read.</param>
    /// <param name="found">How many bytes it holds.</param>
    /// <param name="wanted">How many bytes a secret is.</param>
    /// <returns>The refusal, with the file and both lengths in its message.</returns>
    /// <remarks>
    /// The length is the only thing about the bytes this says out loud, and it
    /// is a fact about the file rather than about the key. A message quoting any
    /// part of the value would put the secret in whatever reads the message.
    /// </remarks>
    public static HashSecretRefusedException WrongLength(string path, int found, int wanted)
    {
        return new HashSecretRefusedException(string.Format(
            CultureInfo.InvariantCulture,
            "{0} holds {1} byte(s) and a secret is {2}. It is left exactly as it was: a file that is not the secret may still be one somebody can restore from, and overwriting it here would remove that chance.",
            path,
            found,
            wanted));
    }
}
