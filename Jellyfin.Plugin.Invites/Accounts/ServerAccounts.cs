using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.Invites.Accounts;

/// <summary>
/// The account identifiers, read off the server's own user manager.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the one place in the plugin that binds late, and the reason is
/// measured rather than defensive.</b> The member that answers this question
/// changed shape inside the server line this plugin claims to load on. On the
/// floor the manifest declares, which the plugin compiles against, it is a
/// property, and on the newest release of the line it is a method:
/// </para>
/// <para>
/// <c>IEnumerable&lt;Guid&gt; UsersIds { get; }</c> on 10.11.0, against
/// <c>IEnumerable&lt;Guid&gt; GetUsersIds()</c> on 10.11.11.
/// </para>
/// <para>
/// There is no source form that compiles against both, and a call compiled
/// against either one throws on the other at run time rather than at build time.
/// The ABI floor build found this while the plugin still compiled against the
/// newest release of the line: a direct call built clean against that package
/// and failed against the floor. The plugin compiles against the floor since
/// #155, so a direct call would now throw on the newest server instead.
/// </para>
/// <para>
/// <b>What this does not become.</b> It reads two names and nothing else. It is
/// not a general shim, it does not paper over a member that was removed rather
/// than moved, and where neither name is there it answers <c>null</c> and lets
/// the caller say so. A server whose user manager answers in some third shape is
/// a server this plugin has not been built for, and reporting that is better
/// than guessing at an account list.
/// </para>
/// </remarks>
public sealed class ServerAccounts : IServerAccounts
{
    /// <summary>
    /// The member names this reads, newest first.
    /// </summary>
    private const string TheMethod = "GetUsersIds";
    private const string TheProperty = "UsersIds";

    private readonly object _users;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerAccounts"/> class.
    /// </summary>
    /// <param name="users">The server's user manager.</param>
    public ServerAccounts(IUserManager users)
    {
        _users = users;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Guid>? Identifiers => Of(_users);

    /// <summary>
    /// Reads the account identifiers off whatever was handed in.
    /// </summary>
    /// <param name="users">The user manager.</param>
    /// <returns>
    /// The identifiers, or <c>null</c> where neither known member is there or
    /// either one answers something that is not a list of identifiers.
    /// </returns>
    /// <remarks>
    /// It takes an <see cref="object"/> so the two shapes can be driven by a
    /// test without a server: the whole question is what a member reflection
    /// finds, and a stand-in carrying one shape asks it exactly.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The user manager is null.</exception>
    public static IReadOnlyCollection<Guid>? Of(object users)
    {
        ArgumentNullException.ThrowIfNull(users);

        var type = users.GetType();

        var method = type.GetMethod(TheMethod, BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        if (method is not null)
        {
            return AsIdentifiers(method.Invoke(users, null));
        }

        var property = type.GetProperty(TheProperty, BindingFlags.Public | BindingFlags.Instance);
        if (property is not null)
        {
            return AsIdentifiers(property.GetValue(users));
        }

        return null;
    }

    /// <summary>
    /// What this plugin was built to read, named so a caller reporting the
    /// absence can say what it was looking for.
    /// </summary>
    /// <returns>The two member names, as a sentence fragment.</returns>
    public static string WhatWasLookedFor()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}() or {1}",
            TheMethod,
            TheProperty);
    }

    private static Guid[]? AsIdentifiers(object? answered)
    {
        return answered is IEnumerable<Guid> identifiers ? identifiers.ToArray() : null;
    }
}
