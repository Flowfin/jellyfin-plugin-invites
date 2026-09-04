using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Configuration;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Maintenance;
using Jellyfin.Plugin.Invites.Server;
using Jellyfin.Plugin.Invites.Startup;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// A redemption happens and no code-shaped value reaches a line an operator
/// could read.
/// </summary>
/// <remarks>
/// <para>
/// <b>The code is the credential, so a log line carrying one is every live
/// invitation in every copy of that log.</b> docs/logging.md carries the never
/// list and the argument; this is the clause of #32 that asks for it to be
/// driven rather than read.
/// </para>
/// <para>
/// <b>Two readings, because the surfaces divide.</b> Nothing on the redemption
/// path holds a logger, so there is no line for a code to reach there, and that
/// is asserted as a shape rather than watched for. What DOES log is the startup
/// load and the retention sweep, and both of them read invitation records, so
/// both are driven here against a store a redemption has just written and every
/// line they produce is read.
/// </para>
/// <para>
/// <b>What a code-shaped value is.</b> A run of the code alphabet as long as a
/// code, wherever it appears and however it is grouped, rather than the exact
/// string the mint handed back. A line carrying the code with its separators
/// moved, or one carrying somebody else's code, is the same disclosure, and a
/// comparison against the one minted string would see neither.
/// </para>
/// <para>
/// <b>What this does not reach.</b> The level. Every line these two write is
/// captured whatever level it was written at, which is the strongest reading
/// available and is not the same as observing a server configured to the most
/// verbose one: what a server does with these lines is measured nowhere here.
/// And a line written by the server itself, about a request this plugin
/// answered, is the server's and is outside anything this repository reads.
/// </para>
/// </remarks>
public class NoCodeReachesALogLineTests
{
    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A run of the code alphabet as long as a code. Separators are dropped
    /// before the match, so a grouped code and a bare one are the same shape.
    /// </summary>
    private static readonly Regex CodeShaped = new(
        "[0123456789ABCDEFGHJKMNPQRSTVWXYZ]{" + InvitationCode.Length + "}",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    /// <summary>
    /// The two routines that write log lines are driven over a store a
    /// redemption has just written, and neither emits anything shaped like a
    /// code.
    /// </summary>
    /// <remarks>
    /// Both are driven rather than one, because both read records: the load
    /// reports what it disagrees with at startup and the sweep reports what it
    /// removed, and a record is the thing a code hashes to.
    /// </remarks>
    /// <returns>Nothing a caller reads.</returns>
    [Fact]
    public async Task NeitherRoutineThatLogsEmitsACodeShapedValueAfterARedemption()
    {
        using var directory = new OwnedDirectory();
        var clock = new TestClock(_minted);
        var minted = RedeemRoute.Mint(directory.Path, clock, uses: 1);
        var seam = new ARecordingWriteSeam();

        await RedeemRoute
            .Over(directory.Path, clock, seam, RedeemRoute.Request())
            .Submit(minted.Code, RedeemRoute.Filled("newcomer", "a password long enough"));

        Assert.True(CodeShaped.IsMatch(Bare(minted.Code)), "The minted code is not shaped like one, so this reading is looking for the wrong thing.");

        var lines = await LinesWrittenOver(directory.Path, clock, seam.Answers);

        Assert.NotEmpty(lines);

        var carrying = lines
            .Where(line => CodeShaped.IsMatch(Bare(line)))
            .ToList();

        Assert.True(
            carrying.Count == 0,
            "A routine that logs wrote something shaped like an invitation code after a redemption: "
            + string.Join(" | ", carrying)
            + ". The code is the credential, and a log line carrying one is every live invitation in every copy of that log.");
    }

    /// <summary>
    /// The reading finds a code that is in a line.
    /// </summary>
    /// <remarks>
    /// A scan for a shape is the assertion that passes hardest when it has
    /// stopped working: a pattern that never matches and a capture that
    /// collected nothing report the same green as a plugin that writes clean
    /// lines. So the same reading is run over a line the test writes itself,
    /// in three groupings a code is handed about in.
    /// </remarks>
    [Fact]
    public void TheReadingFindsACodeInALine()
    {
        var code = InvitationCode.Mint();

        Assert.All(
            new[]
            {
                "the code is " + code,
                "the code is " + Grouped(code) + " and that is all",
                Grouped(code),
            },
            line => Assert.True(
                CodeShaped.IsMatch(Bare(line)),
                "This reading did not find a code in: " + line));
    }

    /// <summary>
    /// Nothing that is handed a presented code can write a log line, because
    /// none of those types holds a logger.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the half the capture above cannot reach. The redemption path is
    /// where a code exists at all, and the reason no line from it appears in the
    /// capture is that there is no line: the routines that handle a code have
    /// nothing to log with.
    /// </para>
    /// <para>
    /// The population is derived rather than listed, so a routine written
    /// tomorrow that takes a presented code is in it on the day it is written.
    /// What it does not reach is a routine that receives the value under another
    /// name and a type that gets at a logger through something it was not
    /// handed; <c>code-or-link-in-a-log-call</c> refuses the spelling from the
    /// other direction and the two are a floor rather than a proof.
    /// </para>
    /// </remarks>
    [Fact]
    public void NothingHandedACodeHoldsALoggerToWriteItTo()
    {
        var carrying = typeof(InvitationCode).Assembly
            .GetTypes()
            .Where(type => type
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Any(method => method
                    .GetParameters()
                    .Any(parameter => parameter.Name is not null
                        && (parameter.Name.Equals("code", StringComparison.OrdinalIgnoreCase)
                            || parameter.Name.Equals("presented", StringComparison.OrdinalIgnoreCase)))))
            .ToList();

        Assert.NotEmpty(carrying);

        var holding = carrying
            .Where(HoldsALogger)
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            holding.Count == 0,
            "These are handed an invitation code and hold a logger, so the credential is one interpolation away from a line at any level: "
            + string.Join(", ", holding));
    }

    /// <summary>
    /// Every line the two routines that log write, over one store.
    /// </summary>
    /// <param name="store">The store directory.</param>
    /// <param name="clock">The clock both are handed.</param>
    /// <param name="account">The account the redemption produced.</param>
    /// <returns>The messages, in the order they were written.</returns>
    private static async Task<IReadOnlyList<string>> LinesWrittenOver(string store, TestClock clock, Guid account)
    {
        var load = new RecordingLogger<LoadOnStart>();
        using (var starting = new LoadOnStart(
            new ServerLineGate("42.7", new StubRunningServer(new Version(42, 7, 3))),
            new StubStoreDirectory(store),
            new StubPublicAddress(null),
            new StubConfiguredTemplates([]),
            new StubServerAccounts([account]),
            clock,
            load))
        {
            await starting.StartAsync(CancellationToken.None);
            await starting.StopAsync(CancellationToken.None);
        }

        var swept = new RecordingLogger<RetentionSweep>();
        var operations = RedeemRoute.Operations(store, clock);
        clock.Advance(Retention.RecordRetention + TimeSpan.FromDays(400));
        await new RetentionSweep(operations, swept)
            .ExecuteAsync(new Progress<double>(), CancellationToken.None);

        return load.Lines.Select(line => line.Message)
            .Concat(swept.Lines.Select(line => line.Message))
            .ToList();
    }

    /// <summary>
    /// Whether a type was handed a logger, in a field, a property or a
    /// constructor.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><c>true</c> where it holds one.</returns>
    private static bool HoldsALogger(Type type)
    {
        const BindingFlags Declared =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        return type.GetFields(Declared).Select(field => field.FieldType)
            .Concat(type.GetProperties(Declared).Select(property => property.PropertyType))
            .Concat(type.GetConstructors(Declared).SelectMany(constructor => constructor.GetParameters()).Select(parameter => parameter.ParameterType))
            .Any(held => held.Name.StartsWith("ILogger", StringComparison.Ordinal));
    }

    /// <summary>
    /// A line with the characters a code is never written with taken out, so a
    /// grouped code and a bare one read the same.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The line without separators.</returns>
    private static string Bare(string line) =>
        line.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// A code written in groups of four, which is how one is read down a
    /// telephone and how a person retypes it.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>The grouped form.</returns>
    private static string Grouped(string code) =>
        string.Join(
            "-",
            Enumerable
                .Range(0, (code.Length + 3) / 4)
                .Select(group => code.Substring(group * 4, Math.Min(4, code.Length - (group * 4)))));
}
