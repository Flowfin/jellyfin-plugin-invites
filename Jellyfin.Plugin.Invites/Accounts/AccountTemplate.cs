using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;

namespace Jellyfin.Plugin.Invites.Accounts;

/// <summary>
/// What an invitation grants the account it creates, as one value.
/// </summary>
/// <remarks>
/// <para>
/// A grant assembled out of settings applied in an order nobody can
/// reconstruct is a grant nobody can read back. This type is the whole of what
/// an invitation is worth to the account it creates, so an operator can look at
/// one value and a test can assert against one value. #69 is the single place
/// it is applied to a user policy, and #103 is where the resulting policy is
/// asserted field by field against the template that produced it.
/// </para>
/// <para>
/// <b>Every field has a value, and none of them means whatever the server does
/// by default.</b> A field standing for the server's default is a grant that
/// changes when somebody upgrades the server, which would silently change what
/// invitations minted months earlier are worth. The constructor takes every
/// field and none of its parameters has a default value, so a field added later
/// cannot arrive unset at an existing call site.
/// </para>
/// <para>
/// The server's own policy carries more fields than this template names, and
/// leaving those alone is a decision rather than an oversight. It is written
/// down as a value, in <see cref="ServerDefaultsLeftAlone"/>, so that the
/// field-by-field assertion in #103 has a source to name for every field of the
/// policy it looks at: either this template said what it should be, or this
/// template said in as many words that it was not going to.
/// </para>
/// <para>
/// <b>The value is immutable and it is copied rather than referenced.</b>
/// Decision 6 in #11 keeps several named templates, and an invitation that
/// resolved a name at redemption would have its grant rewritten by whoever
/// edited that name in the meantime. So an invitation carries the value and
/// keeps the name only as something an operator reads. Nothing here reads a
/// name, and there is no lookup on this type for anything to reach for.
/// </para>
/// <para>
/// <b>What this type does not decide.</b> Which libraries a template should
/// grant is #63, and the ceilings and their bounds are #65. Whether a grant may
/// make an account that manages anything is #62, and it is refused there rather
/// than here: a rule enforced in two places is a rule that drifts in one of
/// them. This type refuses only the states that are not a grant at all, which
/// is the same line <see cref="Invitations.Invitation"/> draws.
/// </para>
/// </remarks>
public sealed class AccountTemplate : IEquatable<AccountTemplate>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AccountTemplate"/> class.
    /// </summary>
    /// <param name="libraries">The libraries the account may see. See <see cref="Libraries"/>.</param>
    /// <param name="mayDownload">Whether it may download. See <see cref="MayDownload"/>.</param>
    /// <param name="mayPlayFromOutsideTheNetwork">Whether it may play remotely. See <see cref="MayPlayFromOutsideTheNetwork"/>.</param>
    /// <param name="mayManage">Whether it may manage anything. See <see cref="MayManage"/>.</param>
    /// <param name="remoteBitrateCeiling">The remote bitrate ceiling in bits a second, or <c>null</c>. See <see cref="RemoteBitrateCeiling"/>.</param>
    /// <param name="simultaneousStreamCeiling">How many streams at once, or <c>null</c>. See <see cref="SimultaneousStreamCeiling"/>.</param>
    /// <param name="parentalRatingCeiling">The parental rating ceiling, or <c>null</c>. See <see cref="ParentalRatingCeiling"/>.</param>
    /// <param name="serverDefaultsLeftAlone">The policy fields this template does not set. See <see cref="ServerDefaultsLeftAlone"/>.</param>
    /// <exception cref="ArgumentException">
    /// A list arrived in its uninitialized state, which is nobody having
    /// decided rather than a decision to grant nothing; a library is named
    /// twice; or a left-alone field is blank or named twice.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A ceiling is negative. Where each ceiling may sit inside the
    /// non-negative numbers is #65.
    /// </exception>
    public AccountTemplate(
        ImmutableArray<Guid> libraries,
        bool mayDownload,
        bool mayPlayFromOutsideTheNetwork,
        bool mayManage,
        int? remoteBitrateCeiling,
        int? simultaneousStreamCeiling,
        int? parentalRatingCeiling,
        ImmutableArray<string> serverDefaultsLeftAlone)
    {
        // An uninitialized list and an empty one are opposite statements. An
        // empty list is a template granting no library, which is a grant
        // somebody chose; the uninitialized one is the state a caller reaches
        // by leaving the argument out of a call it was added to. Normalising it
        // to empty, which is what the record type does with the accounts it
        // produced, would turn every such call site into a silent grant of
        // nothing.
        if (libraries.IsDefault)
        {
            throw new ArgumentException(
                "A template says which libraries it grants, and granting none is written as an empty list. An uninitialized one is nobody having decided.",
                nameof(libraries));
        }

        if (serverDefaultsLeftAlone.IsDefault)
        {
            throw new ArgumentException(
                "A template says which of the server's policy fields it deliberately leaves alone, and leaving none alone is written as an empty list. An uninitialized one is nobody having decided.",
                nameof(serverDefaultsLeftAlone));
        }

        // A library named twice is a list nobody meant to write, and it makes
        // the count of what a template grants disagree with the count of what
        // is in it. The same goes for a field left alone twice.
        RefuseARepeat(libraries, nameof(libraries), "library");

        var namesLeftAlone = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in serverDefaultsLeftAlone)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "A blank entry among the fields left alone names no field, so nothing can tell whether that field was decided or forgotten.",
                    nameof(serverDefaultsLeftAlone));
            }

            if (!namesLeftAlone.Add(name))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The policy field {0} is named twice among the fields left alone.",
                        name),
                    nameof(serverDefaultsLeftAlone));
            }
        }

        RefuseANegativeCeiling(remoteBitrateCeiling, nameof(remoteBitrateCeiling));
        RefuseANegativeCeiling(simultaneousStreamCeiling, nameof(simultaneousStreamCeiling));
        RefuseANegativeCeiling(parentalRatingCeiling, nameof(parentalRatingCeiling));

        Libraries = libraries;
        MayDownload = mayDownload;
        MayPlayFromOutsideTheNetwork = mayPlayFromOutsideTheNetwork;
        MayManage = mayManage;
        RemoteBitrateCeiling = remoteBitrateCeiling;
        SimultaneousStreamCeiling = simultaneousStreamCeiling;
        ParentalRatingCeiling = parentalRatingCeiling;
        ServerDefaultsLeftAlone = serverDefaultsLeftAlone;
    }

    /// <summary>
    /// Gets the libraries the account may see, by identifier.
    /// </summary>
    /// <remarks>
    /// Named one at a time rather than as a flag meaning all of them. A flag is
    /// a grant that widens on its own the next time an operator adds a library,
    /// and the account it widens belongs to somebody who was invited months
    /// earlier. An empty list grants no library, which is a usable template for
    /// a server whose libraries are all restricted. Which libraries a template
    /// should carry, and what happens to a template naming one that has since
    /// been removed, are #63 and #70.
    /// </remarks>
    public ImmutableArray<Guid> Libraries { get; }

    /// <summary>
    /// Gets a value indicating whether the account may download.
    /// </summary>
    /// <remarks>
    /// Downloading takes a copy of the file off the server, so it is the one
    /// playback permission whose effect outlives the account being disabled.
    /// The value an operator should get by default is #64.
    /// </remarks>
    public bool MayDownload { get; }

    /// <summary>
    /// Gets a value indicating whether the account may play from outside the
    /// network.
    /// </summary>
    /// <remarks>
    /// The permission that decides whether an invitation is worth anything to
    /// somebody who never reaches the operator's network. #64 owns the value.
    /// </remarks>
    public bool MayPlayFromOutsideTheNetwork { get; }

    /// <summary>
    /// Gets a value indicating whether the account may manage anything at all.
    /// </summary>
    /// <remarks>
    /// Present so that a template says the word rather than leaving it to
    /// whatever the server does, which is the rule this whole type is built
    /// on. It is not refused here. #62 is the issue that never lets an
    /// invitation mint an administrator, and putting the refusal in two places
    /// is how one of them ends up not being the one that runs.
    /// <c>policy-field-written-outside-the-template</c> in
    /// .github/lint/invariants.sh is the other half of that ground.
    /// </remarks>
    public bool MayManage { get; }

    /// <summary>
    /// Gets the remote bitrate ceiling in bits a second, or <c>null</c> where
    /// the template grants no ceiling.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>null</c> is a stated grant of an unlimited bitrate and is not the
    /// server's default creeping back in: a field this template declines to
    /// decide is named in <see cref="ServerDefaultsLeftAlone"/> instead, and
    /// the two are different answers a reader can tell apart. #65 owns the
    /// number and the bound on it.
    /// </para>
    /// <para>
    /// It is handed to <c>RemoteClientBitrateLimit</c> on the server's own user
    /// policy, which is an <c>Int32</c> rather than a nullable one, so no
    /// ceiling is written there as zero. The plugin is never in the request
    /// path for it. What being over it costs is processor time on the server
    /// rather than a refusal, because the answer to a client asking for more
    /// than the ceiling is a transcode down to it.
    /// </para>
    /// </remarks>
    public int? RemoteBitrateCeiling { get; }

    /// <summary>
    /// Gets how many streams the account may run at once, or <c>null</c> where
    /// the template grants no ceiling.
    /// </summary>
    /// <remarks>
    /// Reads the same way as <see cref="RemoteBitrateCeiling"/>: <c>null</c> is
    /// unlimited by decision, and #65 owns the number. It is handed to
    /// <c>MaxActiveSessions</c> on the server's user policy, which is also an
    /// <c>Int32</c> where zero is the unlimited value. Being over this one
    /// costs the person their evening rather than the server its processor: the
    /// stream that would have been the next one is refused.
    /// </remarks>
    public int? SimultaneousStreamCeiling { get; }

    /// <summary>
    /// Gets the parental rating the account may reach up to, or <c>null</c>
    /// where the template sets no limit.
    /// </summary>
    /// <remarks>
    /// Reads the same way as the two ceilings above, and #65 owns the number.
    /// It is handed to <c>MaxParentalRating</c> on the server's user policy,
    /// which is the one of the three that is nullable, so no limit travels as
    /// <c>null</c> at both ends. Being over it hides the item rather than
    /// refusing a stream. The server's line also carries
    /// <c>MaxParentalSubRating</c> beside it, which this template does not set
    /// and which is named in <see cref="ServerDefaultsLeftAlone"/> by whoever
    /// builds a template rather than being left unmentioned.
    /// </remarks>
    public int? ParentalRatingCeiling { get; }

    /// <summary>
    /// Gets the names of the server's own policy fields this template
    /// deliberately does not set.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The server's user policy is wider than the grants above, and a template
    /// that simply said nothing about the rest would leave a reader unable to
    /// tell a considered omission from a forgotten field. Naming them makes the
    /// omission a value: #103 asserts the resulting policy field by field with
    /// a source for each field, and a field named here has the server's own
    /// default as its stated source.
    /// </para>
    /// <para>
    /// Whether each name is a field the server actually carries is checked
    /// where the policy is applied, which is #69. Nothing here reflects over a
    /// server type, because a template is a value this plugin can build, read
    /// and store without a server being present.
    /// </para>
    /// </remarks>
    public ImmutableArray<string> ServerDefaultsLeftAlone { get; }

    /// <summary>
    /// Two templates are equal when they grant the same things.
    /// </summary>
    /// <param name="other">The template to compare with, or <c>null</c>.</param>
    /// <returns><c>true</c> when every field matches.</returns>
    /// <remarks>
    /// Written out rather than synthesised by a record declaration, for the
    /// reason <see cref="Invitations.Invitation.Equals(Invitations.Invitation)"/>
    /// gives: a synthesised equality compares the two list fields by the
    /// identity of their backing arrays, so a template read back off disk would
    /// never equal the one written. It is also what makes "editing a named
    /// template leaves live invitations unchanged" a sentence a test can
    /// assert, since the assertion is that a copy taken earlier still equals
    /// what it was.
    /// </remarks>
    public bool Equals(AccountTemplate? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Libraries.AsSpan().SequenceEqual(other.Libraries.AsSpan())
            && MayDownload == other.MayDownload
            && MayPlayFromOutsideTheNetwork == other.MayPlayFromOutsideTheNetwork
            && MayManage == other.MayManage
            && RemoteBitrateCeiling == other.RemoteBitrateCeiling
            && SimultaneousStreamCeiling == other.SimultaneousStreamCeiling
            && ParentalRatingCeiling == other.ParentalRatingCeiling
            && ServerDefaultsLeftAlone.AsSpan().SequenceEqual(other.ServerDefaultsLeftAlone.AsSpan(), StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as AccountTemplate);

    /// <inheritdoc/>
    /// <remarks>
    /// The two list fields enter as their lengths rather than as their
    /// contents, which is what <see cref="Invitations.Invitation"/> does and is
    /// enough for a bucket. Every field that decides equality is visible here.
    /// </remarks>
    public override int GetHashCode()
    {
        return HashCode.Combine(
            Libraries.Length,
            MayDownload,
            MayPlayFromOutsideTheNetwork,
            MayManage,
            RemoteBitrateCeiling,
            SimultaneousStreamCeiling,
            ParentalRatingCeiling,
            ServerDefaultsLeftAlone.Length);
    }

    private static void RefuseARepeat(ImmutableArray<Guid> libraries, string parameterName, string what)
    {
        var seen = new HashSet<Guid>();
        foreach (var library in libraries)
        {
            if (!seen.Add(library))
            {
                throw new ArgumentException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The {0} {1} is named twice, so what this template grants and what is written in it are two different counts.",
                        what,
                        library),
                    parameterName);
            }
        }
    }

    private static void RefuseANegativeCeiling(int? ceiling, string parameterName)
    {
        if (ceiling is < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                ceiling,
                "A ceiling below zero is not a smaller allowance, it is a number nothing can be measured against. No ceiling at all is written as null.");
        }
    }
}
