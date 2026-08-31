using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MediaBrowser.Model.Users;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A second place that could build or carry a user policy, written where the
/// invariant lint cannot see it. Nothing constructs this type; it is here so
/// the walk below can be shown to find a policy on a member signature when
/// there is one.
/// </summary>
internal sealed class ProbePolicyCarryingType
{
    /// <summary>
    /// Hands back a policy it made.
    /// </summary>
    /// <returns>Nothing a caller reads.</returns>
    public static UserPolicy Build() => new UserPolicy();
}

/// <summary>
/// One routine in this plugin may name a user policy, and this refuses a
/// second.
/// </summary>
/// <remarks>
/// <para>
/// #69 decided that a template reaches an account through exactly one routine,
/// and two rules in <c>.github/lint/invariants.sh</c> refuse a policy write
/// anywhere else. What those two match is a write through a policy MEMBER: an
/// assignment to a property named Policy, or to a field reached through one,
/// which is how a write arrives at the policy hanging off a user. A write
/// through a PARAMETER or a local of that type is a different spelling, neither
/// rule names it, and it is the spelling the one allowed routine itself uses.
/// So a second routine handed a policy and setting a field on it is refused by
/// neither, whatever its file is called, and the path exemption both rules
/// carry is a second reason rather than the reason.
/// </para>
/// <para>
/// Neither spelling is written out above. This file is inside what those rules
/// read, so quoting the shape a rule refuses would make this file one of its
/// matches, which is the same reason <c>SuiteDirectoryTests</c> assembles every
/// needle it names.
/// </para>
/// <para>
/// That is not a hole somebody has to be careless to walk into. A helper beside
/// the routine, a builder, a second opinion about what a template means, all
/// arrive with a policy on their surface and a field written through it, which
/// is the ordinary shape of a file added next to the one it helps.
/// </para>
/// <para>
/// <b>What this holds, and why it is a type walk rather than a text rule.</b>
/// A policy has to be named on a member signature before a routine can be
/// handed one or hand one on, and a type is not exempted by what its file is
/// called. So the subject here is every constructor, method, property and field
/// the plugin's own types declare, and the answer is a list of one. A second
/// name arrives red and its author has to place it, which is the same shape
/// <c>RouteInventoryTests</c> uses for the routes and
/// <c>AccountsAreNeverWrittenTests</c> uses for the seam over the server's
/// accounts.
/// </para>
/// <para>
/// <b>Its bound.</b> This reads signatures. A routine that names no policy type
/// on its surface and reaches one through reflection, through <c>object</c>, or
/// through a type parameter resolved at the call site walks past it, exactly as
/// it walks past the compiler. What is claimed is that no second type in this
/// plugin declares a member a policy can travel through by name.
/// </para>
/// </remarks>
public class OneRoutineNamesAUserPolicyTests
{
    /// <summary>
    /// The one type that may name a user policy, with what it does with one.
    /// A second entry here is a second place a grant can be built and is a
    /// decision rather than an addition.
    /// </summary>
    private static readonly Dictionary<string, string> Declared = new(StringComparer.Ordinal)
    {
        ["Jellyfin.Plugin.Invites.Accounts.AccountTemplateApplication"] =
            "writes the template's grants onto the policy the server made, and is the one routine #69 allows",
    };

    /// <summary>
    /// The types a signature mentions, with every generic argument and every
    /// element type opened, so a policy inside a collection or a task is seen.
    /// </summary>
    /// <param name="type">The type to open.</param>
    /// <returns>It and every type beneath it.</returns>
    internal static IEnumerable<Type> Opened(Type type)
    {
        yield return type;

        if (type.HasElementType)
        {
            foreach (var inner in Opened(type.GetElementType()!))
            {
                yield return inner;
            }
        }

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var inner in Opened(argument))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    /// Every type of an assembly that names <typeparamref name="TSubject"/> on a
    /// member it declares.
    /// <para>
    /// Constructors, methods, properties and fields, public and not, because a
    /// private field holding a policy is a policy the type carries and a
    /// non-public method taking one is a routine that writes one. Compiler
    /// generated types are left out: they carry the signatures of the members
    /// they were generated for, so counting them reports the same site twice
    /// under a name nobody wrote.
    /// </para>
    /// </summary>
    /// <typeparam name="TSubject">The type a member may not mention.</typeparam>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The full names of those types, ordered.</returns>
    private static IReadOnlyList<string> TypesNaming<TSubject>(Assembly assembly)
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        return assembly.GetTypes()
            .Where(type => !type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), inherit: false))
            .Where(type => Mentioned(type, Any).Any(mentioned => mentioned == typeof(TSubject)))
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Every type named on every member a type declares.
    /// </summary>
    /// <param name="type">The type to read.</param>
    /// <param name="flags">Which members to read.</param>
    /// <returns>The types those members mention, with duplicates left in.</returns>
    private static IEnumerable<Type> Mentioned(Type type, BindingFlags flags)
    {
        var signatures = new List<Type>();

        foreach (var member in type.GetConstructors(flags).Cast<MethodBase>().Concat(type.GetMethods(flags)))
        {
            signatures.AddRange(member.GetParameters().Select(parameter => parameter.ParameterType));
            if (member is MethodInfo method)
            {
                signatures.Add(method.ReturnType);
            }
        }

        signatures.AddRange(type.GetProperties(flags).Select(property => property.PropertyType));
        signatures.AddRange(type.GetFields(flags).Select(field => field.FieldType));

        return signatures.SelectMany(Opened);
    }

    /// <summary>
    /// Exactly the declared routine names a user policy. A second type is a
    /// second place a grant can be built, and it arrives without touching the
    /// routine the field-by-field assertions read.
    /// </summary>
    [Fact]
    public void OnlyTheDeclaredRoutineNamesAUserPolicy()
    {
        var naming = TypesNaming<UserPolicy>(typeof(Plugin).Assembly);

        var undeclared = naming.Where(name => !Declared.ContainsKey(name)).ToList();

        Assert.True(
            undeclared.Count == 0,
            "These types name a user policy on a member they declare and are not listed in "
            + nameof(OneRoutineNamesAUserPolicyTests)
            + ": "
            + string.Join(", ", undeclared)
            + ". #69 allows one routine to write a template onto a policy. The two lint rules that hold that "
            + "match a write through a policy member rather than through a parameter of that type, so a "
            + "routine handed a policy and setting a field on it is invisible to them; this list is what a "
            + "second place has to be argued into.");
    }

    /// <summary>
    /// And the declared list holds nothing that has gone, or it would pass for a
    /// routine that no longer exists and admit a new one under the old name.
    /// </summary>
    [Fact]
    public void TheDeclaredRoutineIsStillATypeThisPluginHolds()
    {
        var naming = TypesNaming<UserPolicy>(typeof(Plugin).Assembly);

        var gone = Declared.Keys
            .Where(name => !naming.Contains(name, StringComparer.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            gone.Count == 0,
            "These types are declared in " + nameof(OneRoutineNamesAUserPolicyTests)
            + " as naming a user policy and no type in the plugin does: " + string.Join(", ", gone)
            + ". Take the entry out in the change that takes the routine out.");
    }

    /// <summary>
    /// The walk finds a policy on a member signature when one is there. Without
    /// this the assertions above would report the same thing for a plugin with
    /// one such routine and for a walk that had stopped seeing anything.
    /// </summary>
    [Fact]
    public void TheWalkFindsATypeThatNamesAUserPolicy()
    {
        var naming = TypesNaming<UserPolicy>(typeof(ProbePolicyCarryingType).Assembly);

        Assert.Contains(typeof(ProbePolicyCarryingType).FullName!, naming, StringComparer.Ordinal);

        // And it leaves a type that names no policy alone, or it would report
        // every type ever written and its green mark over the plugin would say
        // nothing.
        Assert.DoesNotContain(typeof(CodeShape).FullName!, naming, StringComparer.Ordinal);
    }
}
