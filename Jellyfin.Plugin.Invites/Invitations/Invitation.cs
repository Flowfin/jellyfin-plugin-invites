using System;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Jellyfin.Plugin.Invites.Accounts;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// One invitation, as the plugin holds it.
/// </summary>
/// <remarks>
/// <para>
/// The fields are the rows of the invitation record table in
/// docs/personal-data.md and nothing else. That page is the authority for what
/// this plugin holds about a person: a field absent from it does not appear
/// here, and a field here carries the reason it exists on its own member. The
/// two are held together by
/// <c>Jellyfin.Plugin.Invites.Tests.InvitationRecordTests</c>, which reads the
/// public members back off the type and refuses one the inventory does not
/// name.
/// </para>
/// <para>
/// <b>The code is not here, in any recoverable form.</b> What is stored is the
/// keyed hash from #29, over the canonical form
/// <see cref="Codes.InvitationCode.Canonicalise"/> produces. A field holding
/// the code, or anything a code could be recovered from, would make the
/// arithmetic in docs/code-entropy.md irrelevant, because whoever reads the
/// store would no longer have to guess.
/// </para>
/// <para>
/// <b>The value is immutable.</b> A redemption, a revocation and the retention
/// sweep each produce a record rather than editing one in place. That is what
/// lets #40 write a whole record atomically instead of a field at a time, and
/// it is why the count and the revocation time are constructor arguments and
/// not settable properties.
/// </para>
/// <para>
/// <b>What this type does not decide.</b> Whether a use may be spent, whether
/// the expiry has passed and whether a presented hash matches are the
/// redemption decision, which is #56 and lives in one routine. Whether a count
/// may be minted at all is #52 and #33. This type refuses only the states that
/// are not an invitation at all, in the constructor below, and it refuses
/// nothing anybody else already refuses: a rule enforced in two places is a
/// rule that drifts in one of them. The count of those states is not written
/// here, because it moved when #468 added one and a number in this paragraph
/// would go stale the next time it moves.
/// </para>
/// </remarks>
public sealed class Invitation : IEquatable<Invitation>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Invitation"/> class.
    /// </summary>
    /// <param name="id">The non-secret identifier. See <see cref="Id"/>.</param>
    /// <param name="codeHash">The keyed hash of the code. See <see cref="CodeHash"/>.</param>
    /// <param name="mintedBy">The operator who minted it. See <see cref="MintedBy"/>.</param>
    /// <param name="mintedAt">When it was minted. See <see cref="MintedAt"/>.</param>
    /// <param name="expiresAt">When it stops being usable. See <see cref="ExpiresAt"/>.</param>
    /// <param name="usesGranted">How many accounts it was good for. See <see cref="UsesGranted"/>.</param>
    /// <param name="usesRemaining">How many are left. See <see cref="UsesRemaining"/>.</param>
    /// <param name="revokedAt">When it was revoked, or <c>null</c>. See <see cref="RevokedAt"/>.</param>
    /// <param name="revokedBy">Who revoked it, or <c>null</c>. See <see cref="RevokedBy"/>.</param>
    /// <param name="templateLabel">The template name the operator picked. See <see cref="TemplateLabel"/>.</param>
    /// <param name="template">The copy of the grant taken at minting, or <c>null</c> where none was. See <see cref="Template"/>.</param>
    /// <param name="accountsProduced">The accounts it created, each with when it expires. See <see cref="AccountsProduced"/>.</param>
    /// <exception cref="ArgumentException">
    /// The keyed hash is absent, the remaining count is outside the granted
    /// one, the two revocation fields disagree about whether there was one, or
    /// a claim carries an account expiry from before this invitation was
    /// minted. All four are states no invitation can be in rather than rules
    /// about what may be minted or revoked.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// The template label is null, or one of the claims is.
    /// </exception>
    public Invitation(
        Guid id,
        ImmutableArray<byte> codeHash,
        Guid mintedBy,
        DateTimeOffset mintedAt,
        DateTimeOffset expiresAt,
        int usesGranted,
        int usesRemaining,
        DateTimeOffset? revokedAt,
        Guid? revokedBy,
        string templateLabel,
        AccountTemplate? template,
        ImmutableArray<ProducedAccount> accountsProduced)
    {
        // An invitation with no stored hash is one no presented code can ever
        // be checked against. It is not a usable record in some other state; it
        // is a record that would make every redemption against it a comparison
        // with nothing, which is the shape a half-written record has.
        if (codeHash.IsDefaultOrEmpty)
        {
            throw new ArgumentException(
                "An invitation carries the keyed hash of its code. A record without one can never be matched by any presented code.",
                nameof(codeHash));
        }

        // Remaining is a count of the granted uses, so a value outside them is
        // not a count of anything. Whether a particular number may be granted
        // in the first place is #52 and #33 and is not decided here.
        if (usesRemaining < 0 || usesRemaining > usesGranted)
        {
            throw new ArgumentException(
                "The remaining uses are a count of the granted ones, so they run from zero to the number granted.",
                nameof(usesRemaining));
        }

        // A revocation is one event, and the two fields that describe it are
        // the instant and the operator. A record carrying one without the
        // other says a revocation happened and cannot say who made it, or
        // names somebody and cannot say when, and neither is an answer an
        // operator reading the administrator view can act on. The same
        // reasoning already keeps the revoked flag derived from the instant
        // instead of stored beside it.
        if (revokedAt is null != revokedBy is null)
        {
            throw new ArgumentException(
                "A revocation is an instant and the operator who made it. A record carrying one of the two describes an event nobody can read back.",
                revokedAt is null ? nameof(revokedBy) : nameof(revokedAt));
        }

        var claims = accountsProduced.IsDefault ? ImmutableArray<ProducedAccount>.Empty : accountsProduced;

        // An account cannot have been created before the invitation that
        // produced it was minted, so an expiry earlier than the minting is
        // earlier than that account's creation whatever the creation instant
        // was. That is the strongest form of #468's rule this record can
        // carry, and it is weaker than the rule as that issue words it: the
        // record does not hold when each account was created, and a claim
        // carrying a creation instant nobody decided would be the invention a
        // migration is forbidden to make. So an expiry between the minting and
        // the account's creation is not refused here and nothing else refuses
        // it either.
        foreach (var claim in claims)
        {
            ArgumentNullException.ThrowIfNull(claim, nameof(accountsProduced));

            if (claim.ExpiresAt is { } expiry && expiry < mintedAt)
            {
                throw new ArgumentException(
                    "An account expires after the invitation that produced it was minted. A claim expiring before its own invitation existed describes an account nobody could have created.",
                    nameof(accountsProduced));
            }
        }

        Id = id;
        CodeHash = codeHash;
        MintedBy = mintedBy;
        MintedAt = mintedAt;
        ExpiresAt = expiresAt;
        UsesGranted = usesGranted;
        UsesRemaining = usesRemaining;
        RevokedAt = revokedAt;
        RevokedBy = revokedBy;
        TemplateLabel = templateLabel ?? throw new ArgumentNullException(nameof(templateLabel));
        Template = template;
        AccountsProduced = claims;
    }

    /// <summary>
    /// Gets the non-secret name of this invitation.
    /// </summary>
    /// <remarks>
    /// Required by #32. It is what a log line and the administrator view both
    /// point at, and having it is what lets the never list and a useful log
    /// line coexist: without a name that is not the code, one of the two has to
    /// give. It is also the <c>id</c> in the administrator routes in
    /// docs/api.md, so it is handed to whoever can call them and is not a
    /// secret in any sense.
    /// </remarks>
    public Guid Id { get; }

    /// <summary>
    /// Gets the keyed hash of the code, over its canonical form.
    /// </summary>
    /// <remarks>
    /// Required by #29, which owns the algorithm and the secret it is keyed
    /// with. It is here so that a presented code can be checked without the
    /// code being held, which is the whole of why this field is bytes and why
    /// there is no neighbouring field holding what they are a hash of.
    /// Comparing it is a fixed-time comparison rather than an equality test.
    /// The comparison a presented code is checked by belongs to #29 and is not
    /// here; the one comparison of this field that is here, in
    /// <see cref="Equals(Invitation?)"/>, is fixed-time for the same reason.
    /// </remarks>
    public ImmutableArray<byte> CodeHash { get; }

    /// <summary>
    /// Gets the operator account answerable for this invitation.
    /// </summary>
    /// <remarks>
    /// Personal data about the operator rather than about the invited person,
    /// and the field that makes the attempt trail in #43 answerable at all: an
    /// account that appeared from an invitation is traceable to whoever issued
    /// it or to nobody.
    /// </remarks>
    public Guid MintedBy { get; }

    /// <summary>
    /// Gets the instant this invitation was minted.
    /// </summary>
    /// <remarks>
    /// Fixes the invitation in time, which is what makes a trail readable. The
    /// type is <see cref="DateTimeOffset"/> for the reason
    /// <see cref="Time.IClock"/> gives: an operator changing the server's
    /// timezone must not change what any stored instant means.
    /// </remarks>
    public DateTimeOffset MintedAt { get; }

    /// <summary>
    /// Gets the instant this invitation stops being usable.
    /// </summary>
    /// <remarks>
    /// Read by the expiry comparison in #51, which owns the direction of that
    /// comparison and the awkward parts of it. Nothing here compares it to
    /// anything: this type holds the instant and #51 decides what passing it
    /// means.
    /// </remarks>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Gets how many accounts this invitation was granted.
    /// </summary>
    /// <remarks>
    /// Kept beside <see cref="UsesRemaining"/> rather than being derived from
    /// it, because an operator reading a spent invitation wants to know what it
    /// was worth as well as what is left. Decision 2 in #11 makes one the
    /// answer for a minted invitation; the field is a count either way, and
    /// #52 is where the ceiling and the refusal of zero live.
    /// </remarks>
    public int UsesGranted { get; }

    /// <summary>
    /// Gets how many accounts this invitation is still good for.
    /// </summary>
    /// <remarks>
    /// The count #52 makes authoritative. Nothing derives it from the length of
    /// <see cref="AccountsProduced"/>: an account deleted afterwards must not
    /// give a use back, which is one of #52's own clauses, and a derived count
    /// would hand one back silently.
    /// </remarks>
    public int UsesRemaining { get; }

    /// <summary>
    /// Gets the instant this invitation was revoked, or <c>null</c> where it
    /// has not been.
    /// </summary>
    /// <remarks>
    /// Revocation is #54. The time is the part an operator needs after a
    /// restore quietly undoes it, which is the case docs/disaster-cases.md
    /// describes and the plugin cannot detect.
    /// </remarks>
    public DateTimeOffset? RevokedAt { get; }

    /// <summary>
    /// Gets the operator account that revoked this invitation, or <c>null</c>
    /// where it has not been revoked.
    /// </summary>
    /// <remarks>
    /// Revocation is #54, which asks that the revoking operator be recorded
    /// beside the time. It is personal data about the operator in the same way
    /// <see cref="MintedBy"/> is, and it exists for the same reason: an
    /// invitation that stopped working is answerable to whoever stopped it or
    /// to nobody. The pair is kept whole by the constructor rather than by
    /// whoever writes the next caller, and it is one revocation rather than a
    /// history: <see cref="Revocation"/> keeps the first of them, so this names
    /// the operator who ended the invitation and not the last one to ask.
    /// </remarks>
    public Guid? RevokedBy { get; }

    /// <summary>
    /// Gets a value indicating whether this invitation has been revoked.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="RevokedAt"/> rather than stored beside it. The
    /// inventory names revoked and revoked-at as two rows, and two stored
    /// fields admit a record that says it is revoked and cannot say when, or
    /// says it is not and carries a time. A partial write and a restored store
    /// are both ways to arrive there, and neither is a state anybody would know
    /// how to read.
    /// </remarks>
    public bool IsRevoked => RevokedAt is not null;

    /// <summary>
    /// Gets the name of the template the operator picked when minting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a label an operator reads, and nothing resolves it. #61 requires
    /// that the template is copied into the invitation at minting rather than
    /// referenced by name at redemption, because a referenced template edited
    /// later retroactively changes what every live invitation grants. Decision
    /// 6 in #11 keeps several named templates, so the name is worth recording;
    /// it is worth recording only as what was chosen.
    /// </para>
    /// <para>
    /// The copy is <see cref="Template"/>, one member along. The one moment a
    /// label is turned into a grant is the mint, which reads the configured
    /// templates through <see cref="Accounts.TemplateSettings"/> and writes what
    /// it found here. Nothing may read this label to work out a grant after
    /// that: somebody holding a label and needing a template reaches for a
    /// lookup first, and a lookup is the shape the rule above forbids.
    /// </para>
    /// </remarks>
    public string TemplateLabel { get; }

    /// <summary>
    /// Gets the grant this invitation carries, as it was copied out of the
    /// configured template at minting, or <c>null</c> where no copy was taken.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the copy #61 asks for, and it is what an account is created
    /// from.</b> It is taken once, by the mint, and never refreshed: an operator
    /// who edits the named template afterwards changes what the next invitation
    /// grants and leaves this one exactly as it was minted. The value has no
    /// setter and the type it is made of is immutable, so nothing after the
    /// mint can move it, and <c>TemplateCopyTests</c> asserts the record's copy
    /// against an edit made to the configured entry it came from.
    /// </para>
    /// <para>
    /// <b><c>null</c> is a record minted before the copy existed and nothing
    /// else.</b> The mint refuses to write one: <see cref="InvitationMint"/>
    /// takes the grant as an argument and refuses its absence. What produces
    /// one is <see cref="Storage.InvitationStore"/> reading a version one
    /// document, whose records carried the label and no grant, and there is no
    /// honest value to migrate that into. Resolving the label at read time is
    /// the lookup the rule above forbids, and a grant invented for it would be
    /// one nobody decided, which is the silent widening #92 refuses in a
    /// migration. So such a record keeps its name, keeps its count, and can
    /// create nothing: a redemption that reaches for this and finds nothing has
    /// no template to hand to the creation routine, and that is the strict
    /// answer rather than a guess.
    /// </para>
    /// </remarks>
    public AccountTemplate? Template { get; }

    /// <summary>
    /// Gets the accounts this invitation has created, each with when it
    /// expires.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The link between an invitation and the accounts that came out of it.
    /// This is the most identifying row in the inventory and it is also the one
    /// an operator needs when an account they do not recognise appears, which
    /// is the trade docs/personal-data.md argues rather than assumes. It is
    /// also what the load-time consistency report in #46 compares against the
    /// server's own user list.
    /// </para>
    /// <para>
    /// A claim is an entry rather than a bare identifier, which is #468, so an
    /// expiry belongs to one account and not to the invitation that made it.
    /// <see cref="ProducedAccount"/> is where that is argued, and an absent
    /// expiry there means the account does not expire.
    /// </para>
    /// </remarks>
    public ImmutableArray<ProducedAccount> AccountsProduced { get; }

    /// <summary>
    /// Two invitations are equal when every field is.
    /// </summary>
    /// <param name="other">The invitation to compare with, or <c>null</c>.</param>
    /// <returns><c>true</c> when every field matches.</returns>
    /// <remarks>
    /// <para>
    /// Written out rather than taken from a record declaration on purpose. The
    /// equality a record synthesises would compare the two sequence fields by
    /// the identity of their backing arrays, so a record read back out of a
    /// store would never equal the one written to it however faithful the
    /// store was. That comparison is the last clause of #38 and #39 is what
    /// makes it, so an equality that could not pass it would be a trap laid for
    /// whoever writes the store.
    /// </para>
    /// <para>
    /// <b>The keyed hash is the one field compared in fixed time.</b> A
    /// sequence comparison stops at the first pair of bytes that differ, so how
    /// long it takes says how much of the stored hash the other value got
    /// right. Nothing today hands this method a hash somebody chose, so no
    /// timing leak is being closed here; what is being kept out of the tree is
    /// the spelling, because value equality is where a byte-by-byte compare of
    /// a secret gets written without anybody deciding to write one.
    /// <c>secret-compared-by-sequence</c> in .github/lint/invariants.sh refuses
    /// it coming back. The accounts are compared as a sequence and stay that
    /// way: a list of account identifiers is not a secret, and comparing it in
    /// fixed time would say this type could not tell the two fields apart.
    /// </para>
    /// </remarks>
    public bool Equals(Invitation? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Id.Equals(other.Id)
            && CryptographicOperations.FixedTimeEquals(CodeHash.AsSpan(), other.CodeHash.AsSpan())
            && MintedBy.Equals(other.MintedBy)
            && MintedAt.Equals(other.MintedAt)
            && ExpiresAt.Equals(other.ExpiresAt)
            && UsesGranted == other.UsesGranted
            && UsesRemaining == other.UsesRemaining
            && Nullable.Equals(RevokedAt, other.RevokedAt)
            && Nullable.Equals(RevokedBy, other.RevokedBy)
            && string.Equals(TemplateLabel, other.TemplateLabel, StringComparison.Ordinal)
            && Equals(Template, other.Template)
            && AccountsProduced.AsSpan().SequenceEqual(other.AccountsProduced.AsSpan());
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as Invitation);

    /// <inheritdoc/>
    /// <remarks>
    /// The two sequence fields enter as their lengths rather than as their
    /// contents. Every field that decides equality has to be visible to this,
    /// and a length is visible to it; hashing the bytes of the keyed hash into
    /// a value that ends up in a dictionary somebody dumps is the opposite of
    /// what the hash is here for. The template enters through its own hash,
    /// which <see cref="AccountTemplate"/> builds from every grant it carries.
    /// </remarks>
    public override int GetHashCode()
    {
        var first = HashCode.Combine(Id, MintedBy, MintedAt, ExpiresAt, UsesGranted, UsesRemaining, RevokedAt, RevokedBy);
        return HashCode.Combine(first, TemplateLabel, Template, CodeHash.Length, AccountsProduced.Length);
    }
}
