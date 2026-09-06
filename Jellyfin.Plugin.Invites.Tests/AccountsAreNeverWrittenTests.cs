using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Invites.Accounts;
using MediaBrowser.Controller.Library;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// What this plugin may do to an account on the server, held to the two seams
/// that declare it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file used to say the plugin could not touch an account at all, and
/// that is no longer true.</b> #398 asked for the act the whole plan turns on:
/// a redemption that produces an account. So the property this holds has moved
/// from "no write exists" to "every write is one of three, on a seam that
/// declares them, reaching members that are named". The change is a real
/// widening and is recorded here rather than in a commit message somebody would
/// have to go looking for.
/// </para>
/// <para>
/// <b>What #91 leaned on, and what it leans on now.</b> That issue decides that
/// removing this plugin touches no account, and the reason it held was an
/// absence: there was no way to touch one. An absence is what a later change
/// removes without anything going red, which is what this file existed to stop.
/// The replacement is not an absence. Nothing on either seam removes, disables,
/// renames or re-authenticates an account, the write seam addresses only the
/// account a redemption is creating, and the last assertion below reads the
/// seam's own source against every member the server's interface carries, so a
/// fourth member reached there arrives red.
/// </para>
/// <para>
/// The reflection assertions are the ones that matter most, because both seams
/// bind late in part. A member reached by name at run time is invisible to the
/// compiler and to the invariant lint, which reads source text, so a write
/// hidden behind a looked-up name is the one shape neither of them can see.
/// </para>
/// <para>
/// This does not assert what #91's done-condition asks for. That clause wants a
/// created account still present after the plugin's own state is removed, which
/// needs a server. Nothing here stands in for it.
/// </para>
/// </remarks>
public class AccountsAreNeverWrittenTests
{
    /// <summary>
    /// The types that may be handed the server's user manager, with what each
    /// one is for. A FOURTH entry is a fourth place a write could be made from,
    /// and it arrives without touching any interface below. THIS SENTENCE SAID
    /// A THIRD ENTRY AND THERE ARE THREE: #67 added a seam that asks whether one
    /// name is taken, and it is read-only for the same reason the first is.
    /// </summary>
    private static readonly Dictionary<string, string> Seams = new(StringComparer.Ordinal)
    {
        [typeof(ServerAccounts).FullName!] =
            "reads the account identifiers and nothing else",
        [typeof(ServerAccountNames).FullName!] =
            "asks whether one name is already taken, and is the seam #67 asked for",
        [typeof(ServerAccountWrites).FullName!] =
            "makes the three writes a redemption needs, and is the seam #398 asked for",
    };

    /// <summary>
    /// The members of the server's user manager the write seam may reach.
    /// </summary>
    /// <remarks>
    /// Two of them create and change the account being made, one hands back the
    /// policy the server gave it and one hands that policy back to the server,
    /// and the fifth is the lookup the older shape of the credential member
    /// needs. Every other member the server's interface carries is refused,
    /// including the four that would let this plugin remove, rename, disable or
    /// authenticate somebody.
    /// </remarks>
    private static readonly string[] Reachable =
    {
        "ChangePassword",
        "CreateUserAsync",
        "GetUserById",
        "GetUserDto",
        "UpdatePolicyAsync",
    };

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
    /// Every member the read seam declares hands something back and takes
    /// nothing. A method, a setter or an argument is the shape a write arrives
    /// in.
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
    /// The names the read seam reaches by reflection resolve to reads on the
    /// server's own interface. A looked-up name is not judged by the compiler or
    /// by the source lint, so it is judged here.
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
    /// The types in the plugin that can be handed the server's user manager are
    /// the declared seams and nothing else. One more is one more place a write
    /// could be made from, and it arrives without touching any of the
    /// interfaces. THIS SENTENCE COUNTED TWO AND THE POPULATION IS READ OFF THE
    /// DICTIONARY RATHER THAN COUNTED HERE NOW, so it does not go stale the next
    /// time a seam is argued for.
    /// </summary>
    [Fact]
    public void OnlyTheDeclaredSeamsCanBeHandedTheServersUserManager()
    {
        var reached = typeof(Jellyfin.Plugin.Invites.Plugin).Assembly
            .GetTypes()
            .Where(type => Members(type).Any(parameter => typeof(IUserManager).IsAssignableFrom(parameter)))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Seams.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray(), reached);
    }

    /// <summary>
    /// The write seam declares three acts, and they are the three a redemption
    /// needs. A fourth is a power somebody has to argue for.
    /// </summary>
    [Fact]
    public void TheWriteSeamDeclaresOnlyTheThreeActsARedemptionNeeds()
    {
        var declared = typeof(IServerAccountWrites)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(member => member.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "ApplyTemplateAsync", "CreateAccountAsync", "SetCredentialAsync" },
            declared);
    }

    /// <summary>
    /// The write seam names five members of the server's user manager and no
    /// others. The population is read off the server's own interface rather than
    /// listed here, so a member the server grows tomorrow is in it on the day it
    /// arrives.
    /// </summary>
    /// <remarks>
    /// It reads source text, which is what the compiler cannot help with for the
    /// arm that binds by name, and its bound is the same as every rule of that
    /// kind: a member reached through a name assembled at run time walks past
    /// it. What it catches is the shape somebody writes, which is the member
    /// spelled out.
    /// </remarks>
    [Fact]
    public void TheWriteSeamReachesNoMemberBeyondTheFiveItNeeds()
    {
        var source = WriteSeamSource();

        var named = typeof(IUserManager)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(member => !(member is MethodInfo method && method.IsSpecialName))
            .Select(member => member.Name)
            .Distinct(StringComparer.Ordinal)
            .Where(name => Regex.IsMatch(source, @"\b" + Regex.Escape(name) + @"\b"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(Reachable.OrderBy(name => name, StringComparer.Ordinal).ToArray(), named);
    }

    /// <summary>
    /// The names seam reaches one member of the server's user manager, and it is
    /// a question rather than a command.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same reading as the write seam's leg above and the same bound: it
    /// judges the member spelled out in the source, and a name assembled at run
    /// time walks past it. What it buys is that the seam #67 added cannot grow a
    /// second reach without somebody saying so here, which matters more on this
    /// one than on the read seam over identifiers, because this one is the only
    /// place in the plugin that takes an argument from an unauthenticated
    /// stranger and hands it to the server's user table.
    /// </para>
    /// <para>
    /// <b>It reads the code and not the comments, which the leg above does
    /// not.</b> That seam's argument does not have to name a member it may not
    /// reach; this one's does, because the reason to trust a collision answer is
    /// that it asks the same question <c>CreateUserAsync</c> asks. A leg reading
    /// the whole file would refuse that evidence, which is the shape of a
    /// checker refusing its own documentation. The bound it adds is that a reach
    /// hidden in a comment is not seen, and a comment does not call anything.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheNamesSeamReachesOnlyTheOneQuestionItAsks()
    {
        var source = WithoutComments(NamesSeamSource());

        var named = typeof(IUserManager)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(member => !(member is MethodInfo method && method.IsSpecialName))
            .Select(member => member.Name)
            .Distinct(StringComparer.Ordinal)
            .Where(name => Regex.IsMatch(source, @"\b" + Regex.Escape(name) + @"\b"))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { ServerAccountNames.TheMember }, named);

        var reached = typeof(IUserManager)
            .GetMethods()
            .Where(method => string.Equals(method.Name, ServerAccountNames.TheMember, StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(reached);
        Assert.All(reached, method => Assert.True(
            method.ReturnType != typeof(void),
            ServerAccountNames.TheMember + " hands nothing back, which is the shape of a command rather than a question."));
    }

    /// <summary>
    /// The names seam declares one member and it answers with a yes or a no.
    /// </summary>
    /// <remarks>
    /// A member handing back an account, a name or a list of either would be
    /// this plugin reading the server's user table rather than comparing one
    /// value against it, and docs/personal-data.md's row for this seam rests on
    /// the answer being a boolean.
    /// </remarks>
    [Fact]
    public void TheNamesSeamAnswersWithNothingButAYesOrANo()
    {
        var declared = typeof(IServerAccountNames)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OfType<MethodInfo>()
            .Where(method => !method.IsSpecialName)
            .ToArray();

        var only = Assert.Single(declared);
        Assert.Equal(typeof(bool), only.ReturnType);
        Assert.Equal(typeof(string), Assert.Single(only.GetParameters()).ParameterType);

        Assert.Empty(typeof(IServerAccountNames)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OfType<PropertyInfo>());
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

    /// <summary>
    /// One seam's source with its comment lines removed.
    /// </summary>
    /// <param name="source">The file's text.</param>
    /// <returns>The lines that are not comments, joined again.</returns>
    private static string WithoutComments(string source) =>
        string.Join(
            Environment.NewLine,
            source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    /// <summary>
    /// Reads the names seam's own source out of the working tree.
    /// </summary>
    /// <returns>The text of the file.</returns>
    private static string NamesSeamSource() => SeamSource("ServerAccountNames.cs");

    /// <summary>
    /// Reads the write seam's own source out of the working tree.
    /// </summary>
    /// <returns>The text of the file.</returns>
    private static string WriteSeamSource() => SeamSource("ServerAccountWrites.cs");

    /// <summary>
    /// Reads one seam's source out of the working tree.
    /// </summary>
    /// <remarks>
    /// The file is found by walking up from the test binary until a directory
    /// holds both the solution and it, rather than by counting how many levels
    /// of output directory sit under it. The count changes with a configuration
    /// or a target framework and the marker does not. Nothing is written and
    /// nothing outside the repository is read, so this stays inside the headless
    /// rule.
    /// </remarks>
    /// <param name="file">The file name under the accounts directory.</param>
    /// <returns>The text of the file.</returns>
    private static string SeamSource(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var seam = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites", "Accounts", file);
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            if (File.Exists(seam) && File.Exists(solution))
            {
                return File.ReadAllText(seam);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds both Jellyfin.Plugin.Invites.sln and "
            + file
            + ", so this comparison read nothing. Failing rather than passing over an empty file.");
    }
}
