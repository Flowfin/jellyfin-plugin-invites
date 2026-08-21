using System;

namespace Jellyfin.Plugin.Invites.Invitations;

/// <summary>
/// What one minting produced: the record that was stored, the code that was
/// not, and the link the two of them make.
/// </summary>
/// <remarks>
/// <para>
/// The record and the code travel together for exactly one moment and then
/// separate for good. The record goes to the store; the code goes into the
/// response and is forgotten here. Returning them as one value is what makes
/// the separation visible at the call site rather than leaving a caller to
/// notice that the record it was handed has no code on it.
/// </para>
/// <para>
/// <b>The link is built here because this is the only moment it can be.</b> It
/// is the code with the configured address in front of it, and nothing after
/// this holds the code. #50 decided that the mint response carries it: the
/// alternative was the configuration page composing it from the setting and the
/// code it had just been handed, which would put a second place in charge of
/// what a link looks like, in a language the greppable rules do not read.
/// </para>
/// <para>
/// <b>An address that cannot carry a link is not a failed minting.</b> The
/// invitation is written either way and the code is handed over either way,
/// because the address is used only to write the link down and getting it wrong
/// affects no record and no account. What the caller gets instead of a link is
/// the refusal, naming the setting, so an operator learns why there is no link
/// at the moment they were expecting one.
/// </para>
/// </remarks>
public sealed class Minting
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Minting"/> class.
    /// </summary>
    /// <param name="code">The code, in the form it goes into a link.</param>
    /// <param name="invitation">The record that was stored.</param>
    /// <param name="publicBaseUrl">
    /// The configured public address, from
    /// <see cref="Configuration.IPublicAddress.PublicBaseUrl"/>. Nothing about a
    /// request reaches this.
    /// </param>
    /// <exception cref="ArgumentException">The code is null or blank.</exception>
    /// <exception cref="ArgumentNullException">The record is null.</exception>
    public Minting(string code, Invitation invitation, string? publicBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A minting that produced no code is a link nobody can follow.", nameof(code));
        }

        ArgumentNullException.ThrowIfNull(invitation);

        Code = code;
        Invitation = invitation;

        // The builder refuses by exception, which is right for every other
        // caller: a link that cannot be built is a fault there and nothing
        // sensible follows it. This is the one caller for which it is not a
        // fault, so the refusal is caught and carried rather than thrown, and
        // the message is the builder's own so that the setting is named in one
        // place.
        try
        {
            Link = InvitationLink.For(publicBaseUrl!, code);
        }
        catch (ArgumentException refused)
        {
            Link = null;
            LinkRefusal = refused.Message;
        }
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

    /// <summary>
    /// Gets the link to hand to the invited person, or <c>null</c> where the
    /// configured address cannot carry one.
    /// </summary>
    /// <remarks>
    /// It contains the code, so it is the same credential as
    /// <see cref="Code"/> with a host in front of it and it is subject to every
    /// rule that value is. Exactly one of this and
    /// <see cref="LinkRefusal"/> is set.
    /// </remarks>
    public string? Link { get; }

    /// <summary>
    /// Gets why no link was built, or <c>null</c> where one was.
    /// </summary>
    /// <remarks>
    /// A fresh install has no configured address, so this is what an operator
    /// meets first, and it names the setting to fill in rather than reporting
    /// that something went wrong.
    /// </remarks>
    public string? LinkRefusal { get; }
}
