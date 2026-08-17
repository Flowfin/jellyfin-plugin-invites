using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Storage;
using Jellyfin.Plugin.Invites.Time;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// The four operations an operator has over invitations: mint one, list them,
/// look at one, revoke one.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is where the work happens and the routes are where it is translated.</b>
/// docs/api.md fixes four administrator routes and says of them that they
/// validate, call the routine that does the work and translate the result, and
/// that no route makes a judgement of its own about expiry, uses or revocation.
/// Keeping the work here rather than in the controller is what makes that
/// sentence checkable: a controller with no store and no clock on it cannot
/// decide anything about a record, and the suite can drive every one of these
/// operations without a request.
/// </para>
/// <para>
/// <b>One writer at a time, inside this process.</b> #40 chose the granularity:
/// one lock over the whole store rather than one per invitation, because a
/// redemption is a person following a link, the store is one file, and the work
/// inside the lock is a read, a comparison and a write. The cost is named rather
/// than hidden - two operations against two different invitations arriving
/// together are serialised, and the second waits for the first.
/// </para>
/// <para>
/// <b>What that lock does not cover.</b> It is an object in this process, so it
/// serialises this server and says nothing about a second one. Two servers over
/// one directory is the case <see cref="StoreLock"/> refuses at startup instead,
/// and the refusal is the answer there because two processes cannot share this
/// gate however it is written.
/// </para>
/// </remarks>
public sealed class InvitationOperations
{
    /// <summary>
    /// The longest validity an operator may ask for, in days.
    /// </summary>
    /// <remarks>
    /// <para>
    /// docs/expiry-rules.md decides the number and the argument for it, and
    /// names this issue as where it acts: enforced at minting rather than at
    /// redemption, so an operator who asks for longer is told at once instead of
    /// finding out later that an invitation they thought would last quietly
    /// died.
    /// </para>
    /// <para>
    /// It is a constant here rather than a configured value for the same reason
    /// <see cref="InvitationMint.UsesCeiling"/> is: nothing in this tree carries
    /// configuration yet, and #86 is where the settings live. A ceiling is the
    /// half that must not become configurable without somebody deciding it, so
    /// this one moving to configuration is a decision rather than a port.
    /// </para>
    /// </remarks>
    public const int MaximumValidityDays = 90;

    private readonly IStoreDirectory _directory;
    private readonly IClock _clock;
    private readonly object _gate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="InvitationOperations"/> class.
    /// </summary>
    /// <param name="directory">Where the store sits.</param>
    /// <param name="clock">The one time source, so a test can move it.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public InvitationOperations(IStoreDirectory directory, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(clock);

        _directory = directory;
        _clock = clock;
    }

    /// <summary>
    /// Gets the validity an invitation is minted for when the caller names none.
    /// </summary>
    /// <remarks>
    /// The number is the threat model's and is not restated here. What this
    /// property is, is the place it acts until #86 gives an operator a setting
    /// to change it within <see cref="MaximumValidityDays"/>.
    /// </remarks>
    public static TimeSpan DefaultValidity => TimeSpan.FromDays(7);

    /// <summary>
    /// Gets a value indicating whether there is a store to work against.
    /// </summary>
    /// <remarks>
    /// A server that has not told this plugin where its data lives is not a
    /// failed operation, it is an operation that must not be attempted. The
    /// caller asks first rather than being handed an exception to interpret.
    /// </remarks>
    public bool StoreIsAvailable => !string.IsNullOrWhiteSpace(_directory.Path);

    /// <summary>
    /// Mints one invitation, writes it, and hands back the code once.
    /// </summary>
    /// <param name="mintedBy">The operator answerable for it.</param>
    /// <param name="templateLabel">The name of the template it carries.</param>
    /// <param name="validity">
    /// How long the link lasts, or <c>null</c> for <see cref="DefaultValidity"/>.
    /// </param>
    /// <param name="uses">
    /// How many accounts it is good for, or <c>null</c> for one.
    /// </param>
    /// <returns>The code and the record that was stored.</returns>
    /// <exception cref="ArgumentException">The template label is null or blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The validity is zero or negative or above <see cref="MaximumValidityDays"/>,
    /// or the use count is outside what <see cref="InvitationMint"/> allows.
    /// </exception>
    /// <exception cref="InvalidOperationException">There is no store directory.</exception>
    public Minting Mint(Guid mintedBy, string templateLabel, TimeSpan? validity, int? uses)
    {
        if (string.IsNullOrWhiteSpace(templateLabel))
        {
            throw new ArgumentException(
                "An invitation carries the name of the template it grants, and a blank one names no grant.",
                nameof(templateLabel));
        }

        var lasts = validity ?? DefaultValidity;

        // An invitation with no expiry at all is refused, and so is one that has
        // already expired when it is handed over. docs/expiry-rules.md argues
        // the first: the bound on what a leaked link is worth is written per
        // remaining validity, and a link with no left makes that sentence say
        // nothing rather than weakening it.
        if (lasts <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(validity),
                lasts,
                "An invitation lasts for some length of time. Zero or less produces a link that is expired when it is copied.");
        }

        if (lasts > TimeSpan.FromDays(MaximumValidityDays))
        {
            throw new ArgumentOutOfRangeException(
                nameof(validity),
                lasts,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "An invitation may be minted to last at most {0} days, and {1} were asked for. Minting again costs one action and puts the decision in front of the operator a second time.",
                    MaximumValidityDays,
                    lasts.TotalDays));
        }

        var now = _clock.UtcNow;
        var code = InvitationCode.Mint();

        lock (_gate)
        {
            var directory = Directory();
            var store = new InvitationStore(directory);
            var contents = store.Read();

            var hash = new InvitationCodeHash(
                HashSecret.OpenOrCreate(directory, contents.Invitations).Value);

            var minted = InvitationMint.Mint(
                id: Guid.NewGuid(),
                codeHash: hash.Of(InvitationCode.Canonicalise(code)!),
                mintedBy: mintedBy,
                mintedAt: now,
                expiresAt: now + lasts,
                uses: uses ?? 1,
                templateLabel: templateLabel);

            store.Write(contents.Invitations.Add(minted));

            return new Minting(code, minted);
        }
    }

    /// <summary>
    /// Every invitation the store holds.
    /// </summary>
    /// <returns>The records, in the order the store holds them.</returns>
    /// <exception cref="InvalidOperationException">There is no store directory.</exception>
    public ImmutableArray<Invitation> All()
    {
        lock (_gate)
        {
            return Store().Read().Invitations;
        }
    }

    /// <summary>
    /// One invitation, by its non-secret identifier.
    /// </summary>
    /// <param name="id">The identifier a log line and a view both name it by.</param>
    /// <returns>The record, or <c>null</c> where the store holds no such one.</returns>
    /// <exception cref="InvalidOperationException">There is no store directory.</exception>
    public Invitation? One(Guid id)
    {
        lock (_gate)
        {
            return Store().Read().Invitations.FirstOrDefault(invitation => invitation.Id == id);
        }
    }

    /// <summary>
    /// Revokes one invitation.
    /// </summary>
    /// <param name="id">The non-secret identifier.</param>
    /// <param name="revokedBy">The operator who revoked it.</param>
    /// <returns>
    /// The record as it now stands, or <c>null</c> where the store holds no such
    /// one.
    /// </returns>
    /// <remarks>
    /// Idempotent, and the idempotence is <see cref="Revocation"/>'s rather than
    /// this routine's: a second revocation hands back the record it was given,
    /// so the comparison below sees nothing to write and the first timestamp and
    /// the first operator stay where they are. Nothing here reads the record's
    /// expiry or its use count, because revoking a spent or expired invitation
    /// is not an error and deciding that it is would be a second place with an
    /// opinion about those fields.
    /// </remarks>
    /// <exception cref="InvalidOperationException">There is no store directory.</exception>
    public Invitation? Revoke(Guid id, Guid revokedBy)
    {
        var now = _clock.UtcNow;

        lock (_gate)
        {
            var store = Store();
            var contents = store.Read();

            var found = contents.Invitations.FirstOrDefault(invitation => invitation.Id == id);
            if (found is null)
            {
                return null;
            }

            var revoked = Revocation.Of(found, revokedBy, now);
            if (ReferenceEquals(revoked, found))
            {
                return found;
            }

            store.Write(contents.Invitations.Replace(found, revoked));
            return revoked;
        }
    }

    private InvitationStore Store() => new(Directory());

    private string Directory()
    {
        var directory = _directory.Path;
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                "This plugin has no data directory, so there is no store to work against. Ask StoreIsAvailable before calling an operation.");
        }

        return directory;
    }
}
