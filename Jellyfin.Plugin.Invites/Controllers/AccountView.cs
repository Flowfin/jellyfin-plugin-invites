using System;

namespace Jellyfin.Plugin.Invites.Controllers;

/// <summary>
/// One account an invitation claims to have created, with what became of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The presence arrives through the constructor and there is no default.</b>
/// A type where the answer could be left unset is a type where a route that
/// forgot to ask hands back a row saying the account is there, and the whole
/// point of #45 is that a row must not say that without having asked.
/// </para>
/// <para>
/// It carries the identifier and nothing else about the account. This plugin
/// asks the server for identifiers and never for a name, an address or a
/// policy, which is what keeps <c>docs/personal-data.md</c>'s inventory the size
/// it is.
/// </para>
/// </remarks>
public sealed class AccountView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AccountView"/> class.
    /// </summary>
    /// <param name="id">The account's identifier, as the record holds it.</param>
    /// <param name="presence">What became of it.</param>
    public AccountView(Guid id, AccountPresence presence)
    {
        Id = id;
        Presence = presence;
    }

    /// <summary>
    /// Gets the account's identifier, as the record holds it.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets what became of the account.
    /// </summary>
    public AccountPresence Presence { get; }
}
