using System;
using System.Linq;
using System.Reflection;
using Jellyfin.Plugin.Invites.Accounts;
using MediaBrowser.Controller.Library;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The plugin cannot change an account on the server, and this is where that is
/// refused rather than observed.
/// </summary>
/// <remarks>
/// <para>
/// #91 decides that removing the plugin touches no account, and the reason it
/// holds today is that there is no way to touch one: the seam over the server's
/// accounts carries a single member and it reads. That is an absence, and an
/// absence is what a later change removes without anything going red. A create,
/// a disable or a delete added to that interface would pass every check in this
/// repository.
/// </para>
/// <para>
/// The reflection assertion is the one that matters most, because the seam binds
/// late. A member reached by name at run time is invisible to the compiler and
/// to the invariant lint, which reads source text, so a write hidden behind a
/// looked-up name is the one shape neither of them can see.
/// </para>
/// <para>
/// This does not assert what #91's done-condition asks for. That clause wants a
/// created account still present after the plugin's own state is removed, which
/// needs a seam that can create one. Nothing here stands in for it.
/// </para>
/// </remarks>
public class AccountsAreNeverWrittenTests
{
    /// <summary>
    /// The names <see cref="ServerAccounts"/> looks up on the server's user
    /// manager. They are read off the private constants rather than typed here,
    /// so a third name added to the binder is a name this test sees.
    /// </summary>
    private static string[] BoundNames()
    {
        return typeof(ServerAccounts)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();
    }

    /// <summary>
    /// Every member the seam declares hands something back and takes nothing.
    /// A method, a setter or an argument is the shape a write arrives in.
    /// </summary>
    [Fact]
    public void TheSeamOverTheServersAccountsDeclaresNothingThatWrites()
    {
        var members = typeof(IServerAccounts).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        var properties = members.OfType<PropertyInfo>().ToArray();
        Assert.NotEmpty(properties);

        Assert.All(properties, property =>
        {
            Assert.True(property.CanRead, property.Name + " hands nothing back.");
            Assert.False(property.CanWrite, property.Name + " has a setter, which is a value the plugin can put onto the server.");
            Assert.Empty(property.GetIndexParameters());
        });

        var methods = members.OfType<MethodInfo>().Where(method => !method.IsSpecialName).ToArray();
        Assert.True(
            methods.Length == 0,
            "The seam declares " + string.Join(", ", methods.Select(method => method.Name))
            + ". A method on this interface is how a create, a disable or a delete reaches the server's user table.");
    }

    /// <summary>
    /// The names the seam reaches by reflection resolve to reads on the server's
    /// own interface. A looked-up name is not judged by the compiler or by the
    /// source lint, so it is judged here.
    /// </summary>
    [Fact]
    public void EveryNameTheSeamLooksUpOnTheServerIsARead()
    {
        var names = BoundNames();
        Assert.NotEmpty(names);

        var resolved = 0;

        foreach (var name in names)
        {
            foreach (var property in typeof(IUserManager).GetProperties().Where(candidate => candidate.Name == name))
            {
                resolved++;
                Assert.True(property.CanRead, name + " is reached as a property and hands nothing back.");
                Assert.False(property.CanWrite, name + " is reached as a property with a setter, which is a value the plugin can put onto the server.");
            }

            foreach (var method in typeof(IUserManager).GetMethods().Where(candidate => candidate.Name == name && !candidate.IsSpecialName))
            {
                resolved++;
                Assert.True(
                    method.GetParameters().Length == 0,
                    name + " is reached as a call taking " + method.GetParameters().Length
                    + " argument(s). A member the plugin hands a value to is a member it changes something with.");
                Assert.True(
                    method.ReturnType != typeof(void),
                    name + " is reached as a call that hands nothing back, which is the shape of a command rather than a question.");
            }
        }

        Assert.True(
            resolved > 0,
            "None of " + string.Join(", ", names) + " is on the server's user manager at the version this suite runs against, "
            + "so nothing here judged what the seam reaches.");
    }

    /// <summary>
    /// One type in the plugin can be handed the server's user manager, and it is
    /// the read seam. A second one is a second place a write could be made from,
    /// and it arrives without touching the interface this file's first assertion
    /// holds.
    /// </summary>
    [Fact]
    public void OnlyTheReadSeamCanBeHandedTheServersUserManager()
    {
        var reached = typeof(Jellyfin.Plugin.Invites.Plugin).Assembly
            .GetTypes()
            .Where(type => Members(type).Any(parameter => typeof(IUserManager).IsAssignableFrom(parameter)))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { typeof(ServerAccounts).FullName }, reached);
    }

    /// <summary>
    /// Every parameter type of every constructor and method a type declares.
    /// </summary>
    /// <param name="type">The type to read.</param>
    /// <returns>The parameter types, with duplicates left in.</returns>
    private static Type[] Members(Type type)
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        return type.GetConstructors(Any).Cast<MethodBase>()
            .Concat(type.GetMethods(Any))
            .SelectMany(member => member.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();
    }
}
