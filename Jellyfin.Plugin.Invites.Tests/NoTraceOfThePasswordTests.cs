using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The password the person chose is handed to the server and kept nowhere: not
/// on disk, not in the response, and not in what the plugin asked the server
/// for.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read as bytes rather than as fields.</b> A test that asked each record
/// type whether it holds a password would be asking the shape somebody would
/// have had to change on purpose, and would say nothing about a field added
/// beside it, a value written into a name, or a serializer that carried
/// something the type did not declare. What this reads is every file the plugin
/// left behind, whole, and asks whether the password's bytes are anywhere in
/// them. That covers the shapes nobody has thought of, which is the useful half
/// of the claim.
/// </para>
/// <para>
/// <b>Three surfaces, because the credential can leak into any of them.</b> What
/// is on disk after a redemption is what an operator's backup carries. What the
/// response carries is what a proxy log and a browser history carry. What the
/// write seam was asked is what would reach the server's own logging if the
/// plugin ever handed a password anywhere but the one call that needs it.
/// </para>
/// <para>
/// <b>What this does not reach.</b> Whether the server stores the password
/// safely once it has been handed over is the server's, and this plugin's whole
/// position on it is that it hands the value to the server's own credential
/// routine and keeps nothing. Whether an account created this way can then sign
/// in with that password needs a running server, which the headless rule
/// refuses; docs/tests-not-written.md carries that refusal and what stands in
/// for it.
/// </para>
/// <para>
/// <b>Nothing is logged by this plugin on this path</b>, which is a claim about
/// the source rather than about a run: no file under the accounts area makes a
/// logging call at all, and <c>secret-in-a-log-call</c> in
/// <c>.github/lint/invariants.sh</c> refuses one whose argument is spelled like
/// a password. Neither is read here, and neither is a substitute for the other.
/// </para>
/// </remarks>
public class NoTraceOfThePasswordTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The password the person types. Distinctive on purpose: a value that
    /// could occur in a file for some other reason would make a passing run
    /// mean less than it says.
    /// </summary>
    private const string Chosen = "Zqx-Chosen-By-The-Invited-Person-9174";

    /// <summary>
    /// After a redemption that created an account, nothing the plugin wrote to
    /// disk carries the password.
    /// </summary>
    /// <remarks>
    /// Every file under the store directory is read, not only the record file,
    /// because the hash secret and anything a later change puts beside it live
    /// in the same place and an operator's backup takes all of it.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task NothingTheRedemptionWroteToDiskCarriesThePassword()
    {
        using var directory = new OwnedDirectory();
        var written = await Redeem(directory.Path);

        Assert.Equal(StatusCodes.Status303SeeOther, written.Status);

        var carrying = Files(directory.Path)
            .Where(file => Carries(File.ReadAllBytes(file.Path), Chosen))
            .Select(file => file.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(Files(directory.Path));
        Assert.True(
            carrying.Count == 0,
            "The password the person chose is in what this plugin left on disk, in: "
            + string.Join(", ", carrying)
            + ". It is handed to the server's own credential routine and kept nowhere here, so a copy in the store is a credential in every backup of it.");
    }

    /// <summary>
    /// The reading above finds a password that IS on disk.
    /// </summary>
    /// <remarks>
    /// A scan over a directory is the shape of assertion that passes hardest
    /// when it has stopped working: a walk that returned nothing, an encoding
    /// nobody writes, or a comparison that never matches all report the same
    /// green as a store that is clean. So the same reading is run against a file
    /// planted under the store directory, in both encodings, and has to name it.
    /// The planted file is written by this test into a directory it owns and
    /// removed with it; nothing tracked carries the value.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task TheReadingFindsAPasswordThatIsThere()
    {
        using var directory = new OwnedDirectory();
        await Redeem(directory.Path);

        var eight = Path.Combine(directory.Path, "planted-utf8.bin");
        var sixteen = Path.Combine(directory.Path, "planted-utf16.bin");
        File.WriteAllBytes(eight, Encoding.UTF8.GetBytes("before " + Chosen + " after"));
        File.WriteAllBytes(sixteen, Encoding.Unicode.GetBytes("before " + Chosen + " after"));

        var carrying = Files(directory.Path)
            .Where(file => Carries(File.ReadAllBytes(file.Path), Chosen))
            .Select(file => file.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(new[] { "planted-utf8.bin", "planted-utf16.bin" }.OrderBy(name => name, StringComparer.Ordinal), carrying);
    }

    /// <summary>
    /// Nothing the response carries holds the password, in its body or in any
    /// header this route set.
    /// </summary>
    /// <remarks>
    /// The redirect is the answer to the one request that ever carries a
    /// password, and a value echoed into a header travels to a proxy log and
    /// into a browser's history the same way a code in a referrer would.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task NothingTheResponseCarriesHoldsThePassword()
    {
        using var directory = new OwnedDirectory();
        var written = await Redeem(directory.Path);

        Assert.NotEmpty(written.Headers);
        Assert.DoesNotContain(
            Chosen,
            string.Join("\n", written.Headers.Select(header => header.Key + ": " + header.Value)),
            StringComparison.Ordinal);
        Assert.DoesNotContain(Chosen, written.Body ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// The password reaches the server through one call and appears in nothing
    /// else the plugin asked for.
    /// </summary>
    /// <remarks>
    /// The recording seam writes the LENGTH of what the credential arm was
    /// handed rather than the value, which is the shape a trail of calls has to
    /// have if it is to be readable at all. This says the value itself is in no
    /// entry: an arm that passed the password on to the creation call or to the
    /// grant would be visible here and nowhere else in the suite.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task ThePasswordAppearsInNothingTheServerWasAskedForBesideTheOneCall()
    {
        using var directory = new OwnedDirectory();
        var written = await Redeem(directory.Path);

        Assert.NotEmpty(written.Asked);
        Assert.DoesNotContain(
            Chosen,
            string.Join("|", written.Asked),
            StringComparison.Ordinal);
        Assert.Contains(
            written.Asked,
            entry => entry.StartsWith("credential ", StringComparison.Ordinal));
    }

    /// <summary>
    /// Nothing that is handed a password can write a log line, because none of
    /// those types holds a logger.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the clause about log levels held by construction rather than by
    /// a run. A test that drove every level and read what came out would have to
    /// enumerate the levels and the call sites; this asks the narrower question
    /// that makes all of them impossible at once, which is whether the routine
    /// has anything to log WITH.
    /// </para>
    /// <para>
    /// The population is derived rather than listed: every routine in the plugin
    /// assembly that takes a parameter called a password, and the types those
    /// routines belong to. So a routine added tomorrow that takes one is in this
    /// set on the day it is written, and a list somebody has to remember to
    /// extend is not what stands between a password and a log line.
    /// </para>
    /// <para>
    /// What it does not reach is a routine that receives the value under another
    /// name, and a type that reaches a logger through something it was not
    /// handed. <c>secret-in-a-log-call</c> in <c>.github/lint/invariants.sh</c>
    /// refuses the spelling from the other direction, and the two together are a
    /// floor rather than a proof.
    /// </para>
    /// </remarks>
    [Fact]
    public void NothingHandedAPasswordHoldsALoggerToWriteItTo()
    {
        var carrying = TypesHandedAPassword();

        Assert.NotEmpty(carrying);

        var holding = carrying
            .Where(type => HoldsALogger(type))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            holding.Count == 0,
            "These are handed a password and hold a logger, so the password is one interpolation away from a log line at any level: "
            + string.Join(", ", holding)
            + ". The password is handed to the server's own credential routine and kept nowhere here, and a routine that can log is a routine where that stops being structural.");
    }

    /// <summary>
    /// Every type in the plugin declaring a routine that takes a password.
    /// </summary>
    /// <returns>The types.</returns>
    private static IReadOnlyList<Type> TypesHandedAPassword() =>
        typeof(Setup.PasswordRules).Assembly
            .GetTypes()
            .Where(type => type
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Any(method => method
                    .GetParameters()
                    .Any(parameter => parameter.Name is not null
                        && parameter.Name.Contains("password", StringComparison.OrdinalIgnoreCase))))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Whether a type was handed a logger, in a field, a property or a
    /// constructor.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><c>true</c> where it holds one.</returns>
    /// <remarks>
    /// Matched on the interface's name rather than on a type reference, so the
    /// generic and the bare form are one question and the suite does not have to
    /// take a dependency on the logging package to ask it.
    /// </remarks>
    private static bool HoldsALogger(Type type)
    {
        const BindingFlags Declared =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var named = type.GetFields(Declared).Select(field => field.FieldType)
            .Concat(type.GetProperties(Declared).Select(property => property.PropertyType))
            .Concat(type.GetConstructors(Declared).SelectMany(constructor => constructor.GetParameters()).Select(parameter => parameter.ParameterType));

        return named.Any(held => held.Name.StartsWith("ILogger", StringComparison.Ordinal));
    }

    /// <summary>
    /// One redemption through the route, with the store directory the caller
    /// owns.
    /// </summary>
    /// <param name="store">Where the store sits.</param>
    /// <returns>What the response carried and what the seam was asked.</returns>
    private static async Task<(int Status, string? Body, IHeaderDictionary Headers, IReadOnlyList<string> Asked)> Redeem(string store)
    {
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(store, clock, uses: 1);
        var seam = new ARecordingWriteSeam();
        var context = RedeemRoute.Request();

        var answer = await RedeemRoute
            .Over(store, clock, seam, context)
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", Chosen));

        var status = answer switch
        {
            StatusCodeResult redirect => redirect.StatusCode,
            ContentResult refusal => refusal.StatusCode ?? 0,
            _ => 0,
        };

        return (status, (answer as ContentResult)?.Content, context.Response.Headers, seam.Asked.ToList());
    }

    /// <summary>
    /// Every file under a directory, however deep.
    /// </summary>
    /// <param name="root">The directory.</param>
    /// <returns>The path and the name each file is reported by.</returns>
    private static IReadOnlyList<(string Path, string Name)> Files(string root) =>
        Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => (path, Path.GetRelativePath(root, path)))
            .ToList();

    /// <summary>
    /// Whether a file's bytes carry a string, in either of the two encodings a
    /// .NET writer would use.
    /// </summary>
    /// <param name="bytes">The file.</param>
    /// <param name="looked">What to look for.</param>
    /// <returns><c>true</c> where the bytes carry it.</returns>
    /// <remarks>
    /// UTF-8 is what everything here writes. UTF-16 is checked as well because
    /// a value that reached a file through a writer nobody chose deliberately is
    /// exactly the case this is for, and reading only the expected encoding
    /// would be assuming the answer.
    /// </remarks>
    private static bool Carries(byte[] bytes, string looked) =>
        Contains(bytes, Encoding.UTF8.GetBytes(looked))
        || Contains(bytes, Encoding.Unicode.GetBytes(looked));

    /// <summary>
    /// Whether one byte sequence occurs in another.
    /// </summary>
    /// <param name="haystack">The bytes to search.</param>
    /// <param name="needle">The bytes to find.</param>
    /// <returns><c>true</c> where it occurs.</returns>
    private static bool Contains(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return false;
        }

        for (var at = 0; at <= haystack.Length - needle.Length; at++)
        {
            if (haystack.AsSpan(at, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }
}
