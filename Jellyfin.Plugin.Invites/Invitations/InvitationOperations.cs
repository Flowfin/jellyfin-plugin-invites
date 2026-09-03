using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.Invites.Accounts;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Configuration;
using Jellyfin.Plugin.Invites.Redemption;
using Jellyfin.Plugin.Invites.Storage;
using Jellyfin.Plugin.Invites.Time;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// What an operator may do: mint an invitation, list them, look at one, revoke
/// one, and rotate the key the stored hashes are computed under.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is where the work happens and the routes are where it is translated.</b>
/// docs/api.md fixes the administrator routes and says of them that they
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

    /// <summary>
    /// The most invitations that may be live at once on one server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// #33 asks for three ceilings and this is the second of them. Live means
    /// what <see cref="RedemptionDecision.IsLive"/> means and nothing else: a
    /// record that a presented code could still turn into an account. A revoked,
    /// expired or spent invitation is not live and does not count against this
    /// number, so an operator makes room by revoking rather than by deleting.
    /// </para>
    /// <para>
    /// <b>Where five hundred comes from.</b> docs/code-entropy.md sizes the code
    /// against a live set it takes as ten thousand, says that is an assumption
    /// until this ceiling exists, and says the requirement falls if the ceiling
    /// is lower. Five hundred is a twentieth of that, so every figure on that
    /// page stays an upper bound and none of its arithmetic is redone here.
    /// </para>
    /// <para>
    /// <b>What it bounds.</b> With <see cref="InvitationMint.UsesCeiling"/> at
    /// ten, the standing set can authorise five thousand accounts with no
    /// further operator action. That number is the argument for #33's third
    /// ceiling rather than against this one: this ceiling bounds what is
    /// outstanding at an instant, and a bound on how many accounts may be
    /// created in a period is the one that still holds when this one is set
    /// badly. That third ceiling is not in this tree.
    /// </para>
    /// <para>
    /// <b>What it does not bound, said plainly because the issue's own body
    /// invites the wrong reading.</b> It is not a bound on how large the store
    /// file may grow. An expired or spent record stays where it is, which
    /// docs/limits.md holds as its own entry, and both the file and the lookup
    /// that walks every record on a presented code count those too. Removing
    /// them is retention, which is <see cref="Sweep"/> under #59 and runs on a
    /// schedule rather than at a ceiling.
    /// </para>
    /// <para>
    /// <b>Not measured.</b> Nobody has counted how many invitations a real
    /// server holds. Five hundred is an upper bound on a household or a group of
    /// friends rather than an observation, and an operator who meets it has a
    /// case for making the number configurable, which is #86, rather than a
    /// fault. A configured value has to be bounded by this constant rather than
    /// replace it, or the number stops bounding anything.
    /// </para>
    /// </remarks>
    public const int LiveCeiling = 500;

    private readonly IStoreDirectory _directory;
    private readonly IClock _clock;
    private readonly IPublicAddress _address;
    private readonly IConfiguredTemplates _templates;
    private readonly object _gate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="InvitationOperations"/> class.
    /// </summary>
    /// <param name="directory">Where the store sits.</param>
    /// <param name="clock">The one time source, so a test can move it.</param>
    /// <param name="address">
    /// The configured public address a minted link is written against, and the
    /// only place an address is read from. It is a seam for the same reason the
    /// two above are: the value lives on a static the server sets, so a routine
    /// reading it directly could only be tested by a test that arranges a
    /// global.
    /// </param>
    /// <param name="templates">
    /// The configured account templates a minted grant is copied out of, and
    /// the only place the mint reads them from. A seam for the reason the
    /// address is one, and it is read at the mint and nowhere later: once the
    /// copy is on the record, nothing here looks a template up again.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public InvitationOperations(IStoreDirectory directory, IClock clock, IPublicAddress address, IConfiguredTemplates templates)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(templates);

        _directory = directory;
        _clock = clock;
        _address = address;
        _templates = templates;
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
    /// <param name="templateLabel">
    /// The name of the configured template it carries. The grant behind that
    /// name is copied onto the record here and now, which is #61's rule: what
    /// the record holds afterwards is the copy, and an edit to the named
    /// template changes the next invitation and not this one.
    /// </param>
    /// <param name="validity">
    /// How long the link lasts, or <c>null</c> for <see cref="DefaultValidity"/>.
    /// </param>
    /// <param name="uses">
    /// How many accounts it is good for, or <c>null</c> for one.
    /// </param>
    /// <returns>
    /// The code, the record that was stored, and the link the two of them make
    /// against the configured public address.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The template label is null or blank, or no configured template carries
    /// it. Nothing is written.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The validity is zero or negative or above <see cref="MaximumValidityDays"/>,
    /// or the use count is outside what <see cref="InvitationMint"/> allows.
    /// </exception>
    /// <exception cref="ConfiguredTemplatesRefusedException">
    /// The configured templates are a list <see cref="TemplateSettings"/>
    /// refuses, so no name in it can be copied from. Nothing is written.
    /// </exception>
    /// <exception cref="LiveCeilingReachedException">
    /// The store already holds <see cref="LiveCeiling"/> live invitations.
    /// Nothing is written.
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

        // Resolved before the clock is read, before a code is minted and
        // before the store is opened, so a name that finds no grant costs
        // nothing and leaves nothing. This is the one moment a label becomes a
        // grant. The list is judged whole first, by the routine that judges it
        // at load, so the mint and the load cannot disagree about which lists
        // are usable.
        AccountTemplate? template;
        try
        {
            template = TemplateSettings.Named(_templates.Templates, templateLabel);
        }
        catch (ArgumentException refused)
        {
            throw new ConfiguredTemplatesRefusedException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The account templates this plugin is configured with cannot be used as they stand, so no grant can be copied onto an invitation and nothing was minted. The setting is {0} on this plugin's own configuration page. {1}",
                    TemplateSettings.SettingName,
                    refused.Message),
                refused);
        }

        if (template is null)
        {
            throw new ArgumentException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "No configured template is named {0}, so there is no grant to copy onto the invitation and nothing was minted. Names are compared ignoring case, and the templates are the {1} setting on this plugin's own configuration page.",
                    templateLabel,
                    TemplateSettings.SettingName),
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

            // Counted inside the gate, against the records this mint is about to
            // be added to, and against the same clock reading the record will
            // carry. Counting before the lock would count a store somebody else
            // has since written to, which is the whole reason the gate is here.
            var live = 0;
            foreach (var record in contents.Invitations)
            {
                if (RedemptionDecision.IsLive(record, now))
                {
                    live++;
                }
            }

            if (live >= LiveCeiling)
            {
                throw new LiveCeilingReachedException(live, LiveCeiling);
            }

            var hash = new InvitationCodeHash(
                HashSecret.OpenOrCreate(directory, contents.Invitations).Value);

            var minted = InvitationMint.Mint(
                id: Guid.NewGuid(),
                codeHash: hash.Of(InvitationCode.Canonicalise(code)!),
                mintedBy: mintedBy,
                mintedAt: now,
                expiresAt: now + lasts,
                uses: uses ?? 1,
                templateLabel: templateLabel,
                template: template);

            store.Write(contents.Invitations.Add(minted));

            return new Minting(code, minted, _address.PublicBaseUrl);
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
    /// Every invitation whose record claims it created a given account.
    /// </summary>
    /// <param name="account">The server's own identifier for the account.</param>
    /// <returns>
    /// The records that claim it, in the order the store holds them, and empty
    /// where none does.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Empty is an ordinary answer here and not a missing one.</b> This
    /// plugin puts no mark on an account, so an account it never created is
    /// indistinguishable from one an operator made by hand, and on any real
    /// server most accounts are the second. A lookup that treated "no record
    /// claims this" as a failure would make the common case an error.
    /// </para>
    /// <para>
    /// <b>Every claimant comes back rather than the first.</b> Two records
    /// claiming one account is a store disagreeing with itself, and answering
    /// "where did this account come from" with one of the two and no sign of
    /// the other hides exactly the thing an operator asked the question to
    /// find. <see cref="ConsistencyReport"/> takes the same position on the
    /// same data, and says on itself that it reports what the store says
    /// rather than tidying it.
    /// </para>
    /// <para>
    /// <b>Nothing is stored to make this answerable.</b> The claim already sits
    /// on the record as <see cref="Invitation.AccountsProduced"/>, so this walks
    /// what the listing already reads. One read serves the whole walk, under the
    /// same monitor every other operation takes, so two entries of one answer
    /// cannot come from two states of the file.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">There is no store directory.</exception>
    public ImmutableArray<Invitation> AllClaiming(Guid account)
    {
        lock (_gate)
        {
            return Store().Read().Invitations
                .Where(invitation => invitation.AccountsProduced.Contains(account))
                .ToImmutableArray();
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

    /// <summary>
    /// Removes every record whose retention period has run out.
    /// </summary>
    /// <returns>
    /// The identifiers of the records that were removed, in the order the store
    /// held them, and empty where nothing was.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Under the same monitor as everything else, which is the clause of #59
    /// that is easiest to satisfy by accident and easiest to lose.</b> A sweep
    /// that opened the store for itself would be the second writer this store has
    /// never had, arriving on a schedule, and it would race a mint that had
    /// already read the records it is about to write back. Being a method here
    /// rather than a routine beside the scheduled task is what makes it
    /// impossible to hold the file without holding the gate.
    /// </para>
    /// <para>
    /// <b>One clock reading for the whole sweep.</b> Reading the clock per record
    /// would let two records with the same expiry be judged differently in one
    /// run, which is a difference nobody could reproduce afterwards.
    /// </para>
    /// <para>
    /// <b>Nothing is written where nothing is removed.</b> A sweep that rewrote
    /// the file every night would change the bytes on disk daily for no reason,
    /// which costs an operator watching a backup differ and costs this plugin the
    /// ability to say that the file only moves when something happened.
    /// </para>
    /// <para>
    /// <b>No field of a surviving record is touched.</b> #59 asks in as many words
    /// that the sweep never mark anything expired, and the shape here is stronger
    /// than remembering not to: the kept records are the ones the filter passed
    /// through, so there is no code path that could write a changed one.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">There is no store directory.</exception>
    public ImmutableArray<Guid> Sweep()
    {
        var now = _clock.UtcNow;

        lock (_gate)
        {
            var store = Store();
            var held = store.Read().Invitations;

            var kept = held.Where(record => !Retention.MayBeRemoved(record, now)).ToImmutableArray();
            if (kept.Length == held.Length)
            {
                return ImmutableArray<Guid>.Empty;
            }

            store.Write(kept);

            return held
                .Where(record => Retention.MayBeRemoved(record, now))
                .Select(record => record.Id)
                .ToImmutableArray();
        }
    }

    /// <summary>
    /// Works out what rotating the keyed hash secret would cost, without
    /// rotating it.
    /// </summary>
    /// <returns>The plan, carrying the count and the sentence to show first.</returns>
    /// <remarks>
    /// Read under the same gate as everything else, so the number is the store
    /// as it stood at one instant rather than a count assembled while somebody
    /// else was minting.
    /// </remarks>
    /// <exception cref="InvalidOperationException">There is no store directory.</exception>
    public HashSecretRotation PlanRotation()
    {
        lock (_gate)
        {
            var directory = Directory();

            return HashSecret.PlanRotation(directory, Store().Read().Invitations);
        }
    }

    /// <summary>
    /// Rotates the keyed hash secret, against a count the caller was already
    /// shown.
    /// </summary>
    /// <param name="invalidates">
    /// The count from a <see cref="PlanRotation"/> the caller has seen. It must
    /// still be what the store holds.
    /// </param>
    /// <returns>What the rotation cost, as it was actually paid.</returns>
    /// <remarks>
    /// <para>
    /// The plan is made again here rather than carried in from the caller, so
    /// what is rotated against is the store inside this gate and not a value
    /// that travelled over a network. The number the caller sends is compared
    /// against it, and a store that moved between the two calls refuses.
    /// </para>
    /// <para>
    /// No record is removed. Rotation makes every stored hash unverifiable,
    /// which is what the operator asked for, and deleting the records as well
    /// would take away the trail of what those invitations produced. That is
    /// retention rather than rotation and this plugin offers no route for it.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// There is no store directory, or the store holds a different number of
    /// records than the caller confirmed. Nothing is written in either case.
    /// </exception>
    public HashSecretRotation Rotate(int invalidates)
    {
        lock (_gate)
        {
            var directory = Directory();
            var records = Store().Read().Invitations;
            var plan = HashSecret.PlanRotation(directory, records);

            if (plan.Invalidates != invalidates)
            {
                throw HashSecretRotation.CountMoved(invalidates, plan.Invalidates);
            }

            HashSecret.Rotate(plan, records);

            return plan;
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
