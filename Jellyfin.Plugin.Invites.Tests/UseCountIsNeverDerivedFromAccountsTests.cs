using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Invites.Accounts;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// Nothing that judges or holds an invitation can be handed the server's own
/// account list.
/// </summary>
/// <remarks>
/// <para>
/// #95 names the trap this refuses. A single-use invitation whose account was
/// deleted looks, to an implementation that counts accounts rather than uses,
/// exactly like an invitation that was never used. The guard against it is not
/// a behaviour somebody adds later. It is that the count is a field of the
/// record and the server's account list is never an input to it.
/// </para>
/// <para>
/// That issue asks for the sentence to be carried into #52 and #53 rather than
/// discovered from a failing test after they land. This is the sentence as a
/// refusal, so the routine that consumes a use meets it while it is being
/// written.
/// </para>
/// <para>
/// It is scoped to the two namespaces that judge an invitation and hold what it
/// has left, and deliberately not to the whole plugin. The comparison between
/// what the store claims and what the server has exists on purpose, under #46,
/// and it is a report rather than a count: it reads both sides and reconciles
/// neither. A rule refusing the account list everywhere would refuse the one
/// place the two are supposed to meet.
/// </para>
/// <para>
/// <b>What it does not reach, and this is worth reading before trusting it.</b>
/// It judges by type, so what is refused is the seam. A caller that read the
/// identifiers off the seam and handed the bare collection in would pass. That
/// is not an oversight a wider rule closes: a collection of identifiers is
/// exactly the shape of the record's own list of the accounts it produced, and
/// the first draft of this test matched on the parameter name and refused
/// <see cref="Invitations.Invitation"/>'s constructor for carrying one. The two
/// are indistinguishable by type, so the narrow rule is the one that can be
/// stated without a name-shaped exemption, and the rest is the review's.
/// </para>
/// <para>
/// THIS SAID IT DOES NOT ASSERT #95'S DONE-CONDITION, AND NAMED TWO THINGS THAT
/// WOULD BE NEEDED FIRST. It still does not assert it, and the reason is now the
/// narrow one rather than an absence: what this refuses is a shape, and a
/// done-condition about behaviour is asserted by driving the behaviour.
/// <c>ADeletedAccountKeepsTheUseSpentTests</c> does that, over records a
/// redemption really spent, and neither of the two things named here turned out
/// to be what it needed. No seam creates or deletes an account for it - a
/// deletion outside the plugin is the read seam no longer reporting an
/// identifier - and the routine that consumes a use arrived under #399 rather
/// than #52.
/// </para>
/// </remarks>
public class UseCountIsNeverDerivedFromAccountsTests
{
    /// <summary>
    /// The namespaces that decide whether an invitation may be honoured and
    /// hold what it has left.
    /// </summary>
    private static readonly string[] _judging =
    {
        "Jellyfin.Plugin.Invites.Invitations",
        "Jellyfin.Plugin.Invites.Redemption",
    };

    /// <summary>
    /// The server's account list never reaches the code that judges an
    /// invitation, so a use count cannot be derived from it however the routine
    /// is written.
    /// </summary>
    [Fact]
    public void NothingThatJudgesAnInvitationCanBeHandedTheServersAccounts()
    {
        var reached = Judging()
            .SelectMany(type => Parameters(type).Select(parameter => new { Type = type, Parameter = parameter }))
            .Where(pair => IsTheServersAccounts(pair.Parameter))
            .Select(pair => pair.Type.FullName + " takes " + pair.Parameter.Name)
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            reached.Length == 0,
            "The server's account list reaches the code that judges an invitation: " + string.Join("; ", reached)
            + ". A use count derived from it reads a deleted account as a use nobody spent.");
    }

    /// <summary>
    /// The enumeration sees something. A test over an empty set of types passes
    /// because it looked at nothing, which is the way this one would go wrong.
    /// </summary>
    [Fact]
    public void TheEnumerationSeesBothNamespacesItJudges()
    {
        var seen = Judging()
            .Select(type => type.Namespace!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(_judging.OrderBy(name => name, StringComparer.Ordinal).ToArray(), seen);
    }

    /// <summary>
    /// Every type the plugin declares in a judging namespace.
    /// </summary>
    /// <returns>The types.</returns>
    private static IEnumerable<Type> Judging()
    {
        return typeof(Jellyfin.Plugin.Invites.Plugin).Assembly
            .GetTypes()
            .Where(type => type.Namespace is not null && _judging.Contains(type.Namespace, StringComparer.Ordinal));
    }

    /// <summary>
    /// Every parameter of every constructor and method a type declares.
    /// </summary>
    /// <param name="type">The type to read.</param>
    /// <returns>The parameters.</returns>
    private static IEnumerable<ParameterInfo> Parameters(Type type)
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        return type.GetConstructors(Any).Cast<MethodBase>()
            .Concat(type.GetMethods(Any))
            .SelectMany(member => member.GetParameters());
    }

    /// <summary>
    /// Whether a parameter carries the server's own account list, which is the
    /// seam and nothing else. The bound on that is on the type above.
    /// </summary>
    /// <param name="parameter">The parameter to judge.</param>
    /// <returns><c>true</c> where it carries the server's accounts.</returns>
    private static bool IsTheServersAccounts(ParameterInfo parameter)
    {
        return typeof(IServerAccounts).IsAssignableFrom(parameter.ParameterType);
    }
}
