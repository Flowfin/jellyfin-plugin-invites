using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.Invites.Storage;

namespace Jellyfin.Plugin.Invites.Codes;

/// <summary>
/// The keyed hash the store holds in place of a code.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the stored value is keyed at all.</b> A code is twenty-six characters
/// over a thirty-two character alphabet, so an unkeyed hash of one is a table
/// somebody builds once and then reads every store they are ever given. The key
/// is what makes the stored value worthless away from the server that wrote it,
/// and it is the whole difference between a store that holds account-creation
/// credentials and one that holds bytes.
/// </para>
/// <para>
/// <b>HMAC rather than a hash of the key and the code concatenated.</b> The
/// hand-rolled version has to answer which side the key goes on, what happens
/// when the code length varies, and length extension, and it answers them by
/// whoever writes it remembering. HMAC has one answer to all three and a key
/// size that <see cref="HashSecret.Bytes"/> is already set from.
/// </para>
/// <para>
/// <b>Not a password derivation function, deliberately.</b> Stretching exists
/// because a human-chosen secret has far less entropy than its length suggests.
/// A code is 130 bits of uniform draw, sized in docs/code-entropy.md against a
/// guessing rate rather than against a cracking rate, so an iteration count buys
/// nothing an attacker would notice and costs the server real time on every
/// presented code. The endpoint that presents them is unauthenticated by
/// construction, which makes per-attempt work an exposed cost rather than a
/// hidden one. If the arithmetic on that page ever stops clearing its
/// requirement the repair is the code, not an iteration count.
/// </para>
/// <para>
/// <b>What this type does not do.</b> It does not compare. Two keyed hashes are
/// compared where a record is looked up, in constant time, which is #56's
/// routine and the rule two entries of <c>.github/lint/invariants.sh</c> refuse
/// the spellings of. It does not read the key from anywhere: the key's life
/// cycle, where it is drawn, what its permissions are and what a missing one
/// means, is <see cref="HashSecret"/>. And it does not log, because a code and
/// the key are both on the never list in docs/logging.md.
/// </para>
/// <para>
/// <b>The mint path constructs one; no redemption path does.</b> This remark
/// said nothing in the plugin constructs one yet. That was true when it was
/// written and was overtaken without the sentence moving, which is the shape #257
/// is about, so it is corrected here rather than deleted.
/// </para>
/// <para>
/// The construction is in <see cref="Invitations.InvitationOperations"/>, twice:
/// a mint hashes the code it has just drawn so the record carries the keyed form
/// and never the code, and a redemption hashes what somebody presented so it can
/// be compared with a stored value.
/// </para>
/// <para>
/// THIS REMARK SAID NOTHING HERE HAS YET COMPARED A HASH OF SOMETHING A STRANGER
/// TYPED, ON THE GROUND THAT #56'S ROUTINE HAD NO CALLER. It has one, and the
/// second construction above is that caller's. docs/threat-model.md carries the
/// commands; they are not pasted here, because a command written into source is a
/// line the same command then finds. The suite also constructs this directly.
/// </para>
/// </remarks>
public sealed class InvitationCodeHash : IInvitationCodeHash
{
    /// <summary>
    /// The number of bytes one keyed hash is.
    /// </summary>
    /// <remarks>
    /// The output size of the construction below, stated so a caller sizing a
    /// column or a fixture reads it from here rather than from a hash it
    /// happened to print.
    /// </remarks>
    public const int Bytes = 32;

    private readonly byte[] _key;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvitationCodeHash"/> class.
    /// </summary>
    /// <param name="key">
    /// The key, as <see cref="HashSecret"/> produces it. Exactly
    /// <see cref="HashSecret.Bytes"/> bytes.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The key is absent or is not the length a secret is.
    /// </exception>
    /// <remarks>
    /// The length is refused rather than accepted and worked with. A short key
    /// is a weaker keyed hash that produces values of the same size and reads
    /// exactly like a good one from every direction except an attack, and the
    /// case it arises from is a truncated file rather than a caller choosing a
    /// number.
    /// </remarks>
    public InvitationCodeHash(ImmutableArray<byte> key)
    {
        if (key.IsDefault)
        {
            throw new ArgumentException(
                "No key was supplied. The key is drawn and held by HashSecret and is never defaulted here.",
                nameof(key));
        }

        if (key.Length != HashSecret.Bytes)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"A key is {HashSecret.Bytes} bytes and this one is {key.Length}. A key of another length is a truncated or foreign file rather than a shorter key."),
                nameof(key));
        }

        _key = [.. key];
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="canonicalCode"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="canonicalCode"/> is not in the form
    /// <see cref="InvitationCode.Canonicalise"/> produces.
    /// </exception>
    /// <remarks>
    /// <para>
    /// A code that is not already canonical is refused rather than canonicalised
    /// on the way in. Reducing it here would make this a second place that
    /// decides which codes are equal, and two such places disagree the first
    /// time either is changed. The caller canonicalises once, on what arrived,
    /// and hands the result to this.
    /// </para>
    /// <para>
    /// The check compares the argument with its own canonical form, and an
    /// ordinary comparison is right for it. Both operands are the caller's own
    /// input, nothing stored is on either side, and what it decides is whether
    /// the caller made a programming mistake rather than whether somebody
    /// presented the right credential.
    /// </para>
    /// </remarks>
    public ImmutableArray<byte> Of(string canonicalCode)
    {
        ArgumentNullException.ThrowIfNull(canonicalCode);

        var canonical = InvitationCode.Canonicalise(canonicalCode);
        if (!string.Equals(canonical, canonicalCode, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The argument is not in canonical form. Call InvitationCode.Canonicalise on what arrived and pass what it returns, so that one function decides which codes are equal.",
                nameof(canonicalCode));
        }

        // The canonical alphabet is inside ASCII, and the argument has just been
        // held to it, so this encoding is exact rather than lossy. It is named
        // rather than left to a default because what is hashed has to be the
        // same bytes on every platform this plugin loads on.
        Span<byte> written = stackalloc byte[Bytes];
        HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(canonicalCode), written);
        return [.. written];
    }
}
