using System;
using System.Globalization;

namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// What a rotation of the keyed hash secret would cost, worked out before it
/// happens.
/// </summary>
/// <remarks>
/// <para>
/// Rotation is a revoke-everything operation. The stored hashes were computed
/// under the old secret, so a new one makes every record in the store
/// unverifiable at once. #30 says that is acceptable and must be stated, and
/// this type is where it is stated: <see cref="HashSecret.Rotate"/> takes one of
/// these and there is no other way in, so an operator surface cannot rotate
/// without first having the number in its hand.
/// </para>
/// <para>
/// <b>The count is every record the store holds.</b> It is not narrowed to the
/// ones that could still have been redeemed, and the reason is a rule rather
/// than laziness: whether an invitation would be honoured is the redemption
/// decision's judgement, in one routine, and a second copy of it here would be
/// a place that could answer differently. So the number reads high by however
/// many records were already expired, spent or revoked, and the sentence in
/// <see cref="Detail"/> says so rather than letting an operator read it as a
/// count of live links.
/// </para>
/// </remarks>
public sealed class HashSecretRotation
{
    internal HashSecretRotation(string directory, string path, int invalidates)
    {
        Directory = directory;
        Path = path;
        Invalidates = invalidates;
        Detail = string.Format(
            CultureInfo.InvariantCulture,
            "Rotating the secret in {0} makes all {1} record(s) in the store unverifiable, so no invitation minted before it can ever be redeemed again. That count is every record held, including any that were already expired, spent or revoked. No account is touched.",
            path,
            invalidates);
    }

    /// <summary>
    /// Gets the directory the secret sits in.
    /// </summary>
    internal string Directory { get; }

    /// <summary>
    /// Gets the file this rotation would replace.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets how many stored records the rotation would make unverifiable.
    /// </summary>
    public int Invalidates { get; }

    /// <summary>
    /// Gets the sentence to put in front of an operator before they confirm.
    /// </summary>
    /// <remarks>
    /// It carries the file, the count and what the count includes, and nothing
    /// out of the store or the secret. A confirmation prompt is read by whoever
    /// is standing behind the operator.
    /// </remarks>
    public string Detail { get; }

    /// <summary>
    /// The refusal for a confirmation made against a store that has moved.
    /// </summary>
    /// <param name="planned">The count the plan carried.</param>
    /// <param name="found">The count the store holds now.</param>
    /// <returns>The refusal, with both counts in its message.</returns>
    /// <remarks>
    /// An operator confirms the sentence they were shown, and that sentence
    /// names a number. A store that gained or lost records between the plan and
    /// the confirmation makes the number they agreed to the wrong one, and
    /// carrying on would rotate against a cost nobody stated. Planning again is
    /// one call and shows the new number.
    /// </remarks>
    public static InvalidOperationException CountMoved(int planned, int found)
    {
        return new InvalidOperationException(string.Format(
            CultureInfo.InvariantCulture,
            "The rotation was planned against {0} record(s) and the store now holds {1}. Nothing was rotated. Plan again, so what is confirmed is the cost that will actually be paid.",
            planned,
            found));
    }
}
