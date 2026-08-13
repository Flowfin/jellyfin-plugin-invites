using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// Where this suite is allowed to write, held to the two types that own a
/// directory and remove it.
/// </summary>
/// <remarks>
/// <para>
/// #105 ends with a clause about the suite rather than about the store: every
/// test uses a temporary directory it owns and cleans up, and no test writes
/// outside it. Every other clause of that issue is a named test over a file
/// state. This one was a convention, and a convention is what the next test
/// class does not know about. The whole suite touches the filesystem through
/// <c>OwnedDirectory</c> or <c>StubApplicationPaths</c>, both of which build a
/// root under the system temporary directory and delete it again, and nothing
/// refused a third one.
/// </para>
/// <para>
/// <b>What is refused.</b> A source file of this suite that reaches a machine
/// directory for itself. The system temporary directory may be named in the two
/// files that declare the owning types and nowhere else, and the other roots a
/// test could reach for may be named nowhere at all. What that catches is the
/// shape somebody actually writes: a new test class that wants somewhere to put
/// a file and helps itself, which passes green and leaves a directory behind on
/// every machine that ever ran the suite.
/// </para>
/// <para>
/// <b>The means, and why it is not the greppable lint.</b> A source-text rule is
/// what this property wants, and this repository already has a place for those
/// in <c>.github/lint/invariants.sh</c>. Each rule there is the machine-readable
/// half of a rule about the plugin, declared against the issue that decided it,
/// and the subject here is the test project instead. It is also a file being
/// changed under an issue of its own, and a rule landed into it from here would
/// be two hands on one file. The suite can read its own sources, which
/// <c>ApiDocumentTests</c> already does for a document, so the rule lives beside
/// what it judges and runs on the same command as everything else.
/// </para>
/// <para>
/// <b>Its bound, stated rather than left to be found.</b> This reads text. A
/// test that reached a directory through an environment variable, a constant
/// assembled somewhere else, or a path handed to it by a package would walk
/// past every leg below. <c>AppContext.BaseDirectory</c> is named by four
/// classes and is the test host's own output directory, read for the committed
/// store shapes and for the fuzz corpus; nothing here can tell a read of it from
/// a write, so that it is only read is a claim and not a refusal.
/// </para>
/// </remarks>
public class SuiteDirectoryTests
{
    /// <summary>
    /// The files allowed to reach the system temporary directory, which are the
    /// two that declare a type removing what it created.
    /// </summary>
    private static readonly string[] _owners = ["InvitationStoreTests.cs", "Stubs.cs"];

    /// <summary>
    /// The system temporary directory, spelled in halves.
    /// </summary>
    /// <remarks>
    /// Every needle below is assembled rather than written out, because this
    /// file is inside what it reads. A literal here would make this class one of
    /// the matches, and the leg would then be reporting on its own text instead
    /// of on the suite.
    /// </remarks>
    private const string TemporaryDirectory = "GetTemp" + "Path";

    /// <summary>
    /// The machine directories a test could otherwise reach for, one per case,
    /// with what each one would do.
    /// </summary>
    /// <returns>The needle and the sentence naming what it reaches.</returns>
    public static TheoryData<string, string> OtherRoots() => new()
    {
        { "Environment." + "CurrentDirectory", "the directory the runner happened to start in" },
        { "Directory." + "GetCurrentDirectory", "the same directory, through the other spelling" },
        { "Environment." + "GetFolderPath", "a directory belonging to whoever is running the suite" },
        { "Environment." + "SystemDirectory", "the machine's own system directory" },
        { "Path." + "GetTempFileName", "a file under the temporary directory that nothing here removes" },
    };

    /// <summary>
    /// The system temporary directory is reached in the two files that own what
    /// they create, and in no others.
    /// </summary>
    [Fact]
    public void TheSystemTemporaryDirectoryIsReachedOnlyWhereItIsOwned()
    {
        var reached = Sources()
            .Where(source => File.ReadAllText(source).Contains(TemporaryDirectory, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(_owners.OrderBy(name => name, StringComparer.Ordinal).ToArray(), reached);
    }

    /// <summary>
    /// No other machine directory is named anywhere in the suite.
    /// </summary>
    /// <param name="needle">The spelling that would reach it.</param>
    /// <param name="what">What that spelling reaches, for the failure message.</param>
    [Theory]
    [MemberData(nameof(OtherRoots))]
    public void NoOtherMachineDirectoryIsNamedInTheSuite(string needle, string what)
    {
        var named = Sources()
            .Where(source => File.ReadAllText(source).Contains(needle, StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            named.Length == 0,
            needle
            + " reaches "
            + what
            + ", which is not a directory this suite removes afterwards. Named in: "
            + string.Join(", ", named)
            + ". Take an OwnedDirectory or a StubApplicationPaths instead.");
    }

    /// <summary>
    /// A directory the suite owns is gone once it is released, with whatever was
    /// written inside it.
    /// </summary>
    /// <remarks>
    /// Executed rather than read off the type: the clause is that the suite
    /// cleans up, and a Dispose that swallowed its own failure would satisfy
    /// every reading of the source.
    /// </remarks>
    [Fact]
    public void ADirectoryTheSuiteOwnsIsGoneOnceItIsReleased()
    {
        string path;

        using (var directory = new OwnedDirectory())
        {
            path = directory.Path;
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "written-by-a-test.txt"), "anything");

            Assert.True(Directory.Exists(path));
        }

        Assert.False(Directory.Exists(path));
    }

    /// <summary>
    /// Every path the stubbed server hands out is inside the one directory that
    /// stub removes.
    /// </summary>
    /// <remarks>
    /// The stub answers for a server's whole set of paths, so a single one of
    /// them pointing somewhere else is a test writing outside its own directory
    /// without anybody choosing to. The properties are read off the type rather
    /// than listed here, so a path added to the server's interface is covered on
    /// the day the stub gains it.
    /// </remarks>
    [Fact]
    public void EveryPathTheStubbedServerHandsOutIsUnderTheDirectoryItRemoves()
    {
        string root;

        using (var paths = new StubApplicationPaths())
        {
            root = paths.ProgramDataPath;

            var handedOut = paths.GetType()
                .GetProperties()
                .Where(property => property.PropertyType == typeof(string))
                .Select(property => (property.Name, Value: (string?)property.GetValue(paths)))
                .ToArray();

            Assert.NotEmpty(handedOut);

            foreach (var (name, value) in handedOut)
            {
                Assert.True(
                    value is not null && value.StartsWith(root, StringComparison.Ordinal),
                    name + " is " + (value ?? "null") + ", which is not under " + root + ".");
            }

            Assert.True(Directory.Exists(root));
        }

        Assert.False(Directory.Exists(root));
    }

    /// <summary>
    /// The source files of this suite.
    /// </summary>
    /// <remarks>
    /// Found by walking up from the test binary until a directory holds the
    /// solution and the test project, which is how <c>ApiDocumentTests</c>
    /// finds a document: the number of levels under the binary moves with the
    /// configuration and the target framework, and the marker does not. Build
    /// output is skipped, because the generated files under it are not sources
    /// anybody writes. Nothing is written and nothing outside the repository is
    /// read.
    /// </remarks>
    /// <returns>Every C# source file in the test project.</returns>
    private static IReadOnlyList<string> Sources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var project = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.Tests");
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            if (File.Exists(solution) && Directory.Exists(project))
            {
                var sources = Directory
                    .EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
                    .Where(path => !Generated(project, path))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList();

                Assert.NotEmpty(sources);

                return sources;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds both Jellyfin.Plugin.Invites.sln and the test project, so these legs read nothing. Failing rather than reporting a rule that ran over an empty set.");
    }

    /// <summary>
    /// Whether a path is build output rather than a source somebody wrote.
    /// </summary>
    /// <param name="project">The test project directory.</param>
    /// <param name="path">The file.</param>
    /// <returns>True when it sits under bin or obj.</returns>
    private static bool Generated(string project, string path)
    {
        var relative = Path.GetRelativePath(project, path)
            .Replace('\\', '/');

        return relative.StartsWith("bin/", StringComparison.Ordinal)
            || relative.StartsWith("obj/", StringComparison.Ordinal);
    }
}
