using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.Invites.Codes;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Redemption;
using Xunit;
using Xunit.Abstractions;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The parser and the decision, driven with everything a stranger can put on
/// the wire, which is #21.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is fuzzed.</b> Both halves the issue names, in one run:
/// <see cref="InvitationCode.Canonicalise"/>, which is everything between the
/// wire and the lookup key, and <see cref="RedemptionDecision.Decide"/>, which
/// is the whole of what a presented string is allowed to become. A run says
/// which of the two it covered in its own output, because a job covering one of
/// them and reading as though it covered both is the trap a scheduled green mark
/// sets.
/// </para>
/// <para>
/// <b>The means.</b> An in-process generator inside the suite that already
/// exists, rather than a second project under a coverage-guided fuzzer. Both
/// targets are pure functions, no store is opened and no clock is read, so a run
/// needs no instrumentation, no corpus minimisation and no second toolchain, and
/// the whole harness is one file a reviewer can hold in their head. What that
/// gives up is coverage feedback: this generator does not learn which inputs
/// reached a new branch, so it explores the shapes written into
/// <see cref="Mutate"/> and the seed corpus and no others. That bound is why the
/// corpus is committed and grown rather than thrown away, and a coverage-guided
/// harness stays available if a branch is ever found that this cannot reach.
/// </para>
/// <para>
/// <b>Reproducibility.</b> Every run is a seed and a budget, both printed. The
/// sequence below is a fixed arithmetic one rather than the framework's random
/// type, for two reasons: <c>weak-random</c> in
/// <c>.github/lint/invariants.sh</c> refuses that type anywhere in the tree, and
/// a failing scheduled run has to be re-runnable from the seed in its log rather
/// than only describable.
/// </para>
/// <para>
/// <b>What a failure is.</b> An exception out of either routine, an outcome the
/// decision table in <see cref="RedemptionDecisionTableTests"/> does not carry
/// for the same combination, a verdict permitting an account against a record
/// that did not pass every gate, or a canonical form that is not a code. The job
/// fails and files nothing.
/// </para>
/// <para>
/// <b>What is printed on a failure.</b> The seed, the iteration and the input,
/// escaped. Every input is generated inside this process from a printed seed and
/// no run mints anything for anybody, so nothing here is the code half of a live
/// invitation, which is what #29 keeps off a disk. Without the input a failing
/// run says only that something broke.
/// </para>
/// </remarks>
[Trait("Category", "Fuzz")]
public class RedemptionFuzzTests
{
    /// <summary>
    /// The instant every run decides at. Fixed, because expiry is decided
    /// against a clock reading the caller supplies, so the harness moves each
    /// record's expiry around this instant rather than moving the clock.
    /// </summary>
    private static readonly DateTimeOffset _now = new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset _minted = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly IInvitationCodeHash _codeHash = new TestCodeHash();

    /// <summary>
    /// The decision table, read once out of the suite that owns it rather than
    /// restated here. Read once because a run judges every input against it and
    /// rebuilding it a million times is a fuzzer spending its budget on
    /// bookkeeping.
    /// </summary>
    private static readonly IReadOnlyList<DecisionRow> _table = TheTable();

    /// <summary>
    /// The characters a canonical code may contain, as this harness states them
    /// rather than as the parser holds them. A canonical form carrying anything
    /// else is a defect whatever the parser believes its alphabet to be, so the
    /// property is asserted against this string and not against the one under
    /// test.
    /// </summary>
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>
    /// The length of a code, stated here for the same reason as the alphabet.
    /// </summary>
    private const int CodeLength = 26;

    /// <summary>
    /// What the generator reaches for when it wants an input that is not a code:
    /// the separators the parser drops, the confusables it maps, and characters
    /// from outside the ASCII range that a browser will happily send.
    /// </summary>
    private const string Wild = " \t\r\n-_.,/\\|+*%$#@!?<>[]{}()'\"`~^&;:=ilouILOUéßİıА０　";

    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedemptionFuzzTests"/> class.
    /// </summary>
    /// <param name="output">Where the seed and the budget are reported.</param>
    public RedemptionFuzzTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Where a presented input stands, decided by this harness from what it
    /// built rather than by asking the routine under test.
    /// </summary>
    private sealed record Case(
        CodeStanding Code,
        ExpiryStanding Expiry,
        bool Revoked,
        int UsesLeft);

    /// <summary>
    /// One record, beside the code it was built from and the combination it
    /// stands in.
    /// </summary>
    private sealed record Built(Invitation Record, string Code, Case Case);

    /// <summary>
    /// The corpus, mutations of it, and generated inputs, each decided and each
    /// judged against the table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the first clause of #21: one run over the parser and the
    /// decision. The budget and the seed come from the environment so the
    /// scheduled job can spend minutes where the pull-request suite spends
    /// milliseconds, and an unreadable value is refused rather than replaced by
    /// the default, because a job that asked for a million inputs and silently
    /// ran two thousand reports the same green as one that ran them all.
    /// </para>
    /// </remarks>
    [Fact]
    public void NoInputProducesAnOutcomeTheTableDoesNotCarry()
    {
        var iterations = Budget("INVITES_FUZZ_ITERATIONS", 2_000);
        var seed = (ulong)Budget("INVITES_FUZZ_SEED", 20_260_812);
        var corpus = SeedCorpus();

        _output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Fuzzing InvitationCode.Canonicalise and RedemptionDecision.Decide: {corpus.Count} committed seeds, {iterations} generated inputs, seed {seed}."));

        var sequence = new Sequence(seed);
        var reached = new HashSet<RedemptionOutcome>();

        for (var input = 0; input < corpus.Count + iterations; input++)
        {
            var store = Store(sequence);
            var presented = input < corpus.Count
                ? corpus[input]
                : Generate(sequence, corpus, store);

            reached.Add(DriveOne(presented, store, seed, input));
        }

        // A run that never reached a refusal, or never reached a permission, is
        // a run whose generator has drifted off the target. It would stay green
        // for ever and stand for nothing, which is the failure this harness
        // exists against.
        Assert.Equal(
            Enum.GetValues<RedemptionOutcome>().OrderBy(outcome => outcome),
            reached.OrderBy(outcome => outcome));
    }

    /// <summary>
    /// The committed corpus on its own, with no generated input at all.
    /// </summary>
    /// <remarks>
    /// A corpus that has stopped matching the parser is a directory of files
    /// nobody notices, so what it reaches is asserted rather than trusted: the
    /// seeds alone have to reach a canonical form and to reach a refusal.
    /// </remarks>
    [Fact]
    public void TheSeedCorpusStillReachesBothAnswersOfTheParser()
    {
        var corpus = SeedCorpus();

        Assert.NotEmpty(corpus);
        Assert.Contains(corpus, seed => InvitationCode.Canonicalise(seed) is not null);
        Assert.Contains(corpus, seed => InvitationCode.Canonicalise(seed) is null);
    }

    /// <summary>
    /// The alphabet and the length this harness states are the ones the parser
    /// mints to, which is what stops the properties above being asserted against
    /// a specification that has quietly moved.
    /// </summary>
    [Fact]
    public void TheSpecificationThisHarnessHoldsIsTheOneTheParserMints()
    {
        Assert.Equal(Alphabet.Length, Alphabet.Distinct().Count());
        Assert.Equal(CodeLength, InvitationCode.Length);

        for (var draw = 0; draw < 64; draw++)
        {
            var minted = InvitationCode.Mint();

            Assert.Equal(CodeLength, minted.Length);
            Assert.All(minted, character => Assert.True(
                Alphabet.Contains(character),
                "A minted code carries a character this harness does not hold in the alphabet."));
            Assert.Equal(minted, InvitationCode.Canonicalise(minted));
        }
    }

    /// <summary>
    /// One input, decided and judged.
    /// </summary>
    /// <param name="presented">What the generator produced.</param>
    /// <param name="store">The records the decision reads.</param>
    /// <param name="seed">The seed of this run, for the failure message.</param>
    /// <param name="input">Which input this is, for the failure message.</param>
    /// <returns>The outcome, so the caller can assert the run reached them all.</returns>
    private static RedemptionOutcome DriveOne(
        string? presented,
        IReadOnlyList<Built> store,
        ulong seed,
        int input)
    {
        var where = string.Create(
            CultureInfo.InvariantCulture,
            $" [seed {seed}, input {input}, presented {Escape(presented)}]");

        var canonical = Guarded(
            () => InvitationCode.Canonicalise(presented),
            "The parser threw." + where);

        // The parser's own properties, stated here rather than read back off it.
        // The length one is what a relaxed bound in the guard breaks: an input
        // carrying more characters of the alphabet than a code has must be
        // refused, rather than truncated to something a record could match.
        if (canonical is not null)
        {
            Assert.True(
                canonical.Length == CodeLength,
                "A canonical form is not the length of a code." + where);
            Assert.All(canonical, character => Assert.True(
                Alphabet.Contains(character),
                "A canonical form carries a character the alphabet does not." + where));
            Assert.True(
                string.Equals(canonical, InvitationCode.Canonicalise(canonical), StringComparison.Ordinal),
                "Canonicalising a canonical form changed it, so which codes are equal depends on how many times a string has been through the parser." + where);
        }

        var records = store.Select(built => built.Record).ToList();
        var verdict = Guarded(
            () => RedemptionDecision.Decide(presented, _codeHash, records, _now),
            "The decision threw." + where);

        Assert.NotNull(verdict);

        var actual = Classify(canonical, store);
        var expected = TableAnswer(actual, where);

        Assert.True(
            expected == verdict.Outcome,
            string.Create(
                CultureInfo.InvariantCulture,
                $"The decision answered {verdict.Outcome} where the table carries {expected} for {actual}.{where}"));

        Assert.True(
            (verdict.Invitation is null) == (verdict.Outcome == RedemptionOutcome.NoSuchInvitation),
            "A verdict carries a record exactly when it matched one." + where);

        // The second half of #21's property: nothing reaches account creation
        // without passing every gate. It is asserted against what this harness
        // built each record to be, so it does not read the record back through
        // the same members the decision judged.
        if (verdict.MayCreateAnAccount)
        {
            Assert.True(
                canonical is not null
                && actual.Code == CodeStanding.Minted
                && actual.Expiry == ExpiryStanding.Before
                && !actual.Revoked
                && actual.UsesLeft >= 1,
                "An account was permitted against a record that did not pass every gate." + where);
        }

        return verdict.Outcome;
    }

    /// <summary>
    /// What the table says about a combination.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rows are read out of <see cref="RedemptionDecisionTableTests"/>
    /// rather than restated, so this is #21's property in the words it asks for
    /// it: no input produces a redemption the table does not also produce. A
    /// combination with no row fails here rather than being skipped, because an
    /// input reaching a combination nobody wrote a row for is exactly what a
    /// fuzzer is for.
    /// </para>
    /// <para>
    /// Where no record matched, only the code dimension is compared. The table
    /// collapses the other three there for the reason written on it: with no
    /// record there is no expiry, no revocation and no count for a row to vary.
    /// </para>
    /// </remarks>
    /// <param name="actual">The combination this input reached.</param>
    /// <param name="where">The seed, the input and its number.</param>
    /// <returns>The outcome the table carries for it.</returns>
    private static RedemptionOutcome TableAnswer(Case actual, string where)
    {
        var answers = _table
            .Where(row => actual.Code == CodeStanding.Minted
                ? row.Code == CodeStanding.Minted
                    && row.Expiry == actual.Expiry
                    && row.Revoked == actual.Revoked
                    && row.UsesLeft == actual.UsesLeft
                : row.Code == actual.Code)
            .Select(row => row.Expected)
            .Distinct()
            .ToList();

        Assert.True(
            answers.Count == 1,
            string.Create(
                CultureInfo.InvariantCulture,
                $"The decision table carries {answers.Count} answers for {actual}, so this input has no single row to be judged against.{where}"));

        return answers[0];
    }

    /// <summary>
    /// Runs one call against the target and turns anything it throws into a
    /// failure that names the run.
    /// </summary>
    /// <remarks>
    /// A crash out of either routine is what a fuzzer is looking for, and a
    /// crash reported as a bare stack trace is one nobody can reproduce: the
    /// input that caused it is in this process and nowhere else. Every exception
    /// is caught for that reason, including the ones a narrower catch would let
    /// past, since the shape of the next one is not knowable in advance.
    /// </remarks>
    /// <typeparam name="T">What the call answers.</typeparam>
    /// <param name="call">The call.</param>
    /// <param name="what">The seed, the input and its number.</param>
    /// <returns>The answer.</returns>
    private static T Guarded<T>(Func<T> call, string what)
    {
        try
        {
            return call();
        }
        catch (Exception thrown)
        {
            throw new InvalidOperationException(what, thrown);
        }
    }

    /// <summary>
    /// The rows of the decision table, as the suite that owns them supplies
    /// them.
    /// </summary>
    /// <returns>The rows.</returns>
    private static IReadOnlyList<DecisionRow> TheTable()
    {
        var rows = new List<DecisionRow>();
        foreach (var row in RedemptionDecisionTableTests.Reachable())
        {
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Where the presented input stands against the records this harness built.
    /// </summary>
    /// <remarks>
    /// The match is decided by comparing the canonical form with the code each
    /// record was built from, rather than by hashing and comparing the way the
    /// routine under test does. Both halves of a differential comparison
    /// agreeing because they call the same code is the failure a harness like
    /// this is easiest to write.
    /// </remarks>
    /// <param name="canonical">The canonical form, or null.</param>
    /// <param name="store">The records and the codes they were built from.</param>
    /// <returns>The combination.</returns>
    private static Case Classify(string? canonical, IReadOnlyList<Built> store)
    {
        if (canonical is null)
        {
            return new Case(CodeStanding.NotACode, ExpiryStanding.Before, false, 1);
        }

        foreach (var built in store)
        {
            if (string.Equals(built.Code, canonical, StringComparison.Ordinal))
            {
                return built.Case;
            }
        }

        return new Case(CodeStanding.Unminted, ExpiryStanding.Before, false, 1);
    }

    /// <summary>
    /// A small store of records, each in a combination this harness chose.
    /// </summary>
    /// <param name="sequence">The run's sequence.</param>
    /// <returns>The records, their codes and their combinations.</returns>
    private static IReadOnlyList<Built> Store(Sequence sequence)
    {
        var store = new List<Built>();
        var codes = new HashSet<string>(StringComparer.Ordinal);

        var wanted = 1 + sequence.Next(3);
        while (store.Count < wanted)
        {
            var code = ACode(sequence);
            if (!codes.Add(code))
            {
                // Two records under one code would make the lookup's answer
                // depend on the order of the list rather than on the code, and
                // that is a property of the store rather than of the decision.
                continue;
            }

            var standing = (ExpiryStanding)sequence.Next(3);
            var revoked = sequence.Next(2) == 0;
            var granted = 1 + sequence.Next(3);
            var left = sequence.Next(granted + 1);

            var record = new Invitation(
                id: AnIdentifier(sequence),
                codeHash: _codeHash.Of(code),
                mintedBy: AnIdentifier(sequence),
                mintedAt: _minted,
                expiresAt: ExpiryFor(standing),
                usesGranted: granted,
                usesRemaining: left,
                revokedAt: revoked ? _minted : null,
                revokedBy: revoked ? Guid.Parse("44445555-6666-7777-8888-99990000aaaa") : null,
                templateLabel: "Household",
                template: TestTemplates.Household,
                accountsProduced: ImmutableArray<Guid>.Empty);

            // The table's third count is "more than one" rather than a number,
            // so anything above one is that row.
            store.Add(new Built(record, code, new Case(
                CodeStanding.Minted,
                standing,
                revoked,
                Math.Min(left, 2))));
        }

        return store;
    }

    /// <summary>
    /// The expiry a record needs in order to stand where it was asked to,
    /// against the one clock reading the run decides at.
    /// </summary>
    /// <param name="standing">Where the clock is wanted.</param>
    /// <returns>The instant to store on the record.</returns>
    private static DateTimeOffset ExpiryFor(ExpiryStanding standing) => standing switch
    {
        ExpiryStanding.Before => _now.AddTicks(1),
        ExpiryStanding.AtTheInstant => _now,
        _ => _now.AddTicks(-1),
    };

    /// <summary>
    /// One generated input.
    /// </summary>
    /// <remarks>
    /// Some inputs start from a code the store actually holds, because an input
    /// that never nearly matches anything only ever exercises one branch, and
    /// some of those are presented unmutated, because a code arriving exactly as
    /// it was minted is the ordinary case rather than an edge.
    /// </remarks>
    /// <param name="sequence">The run's sequence.</param>
    /// <param name="corpus">The committed seeds.</param>
    /// <param name="store">The records for this input.</param>
    /// <returns>The input, which may be null.</returns>
    private static string? Generate(
        Sequence sequence,
        IReadOnlyList<string> corpus,
        IReadOnlyList<Built> store)
    {
        if (sequence.Next(64) == 0)
        {
            return null;
        }

        var input = sequence.Next(4) switch
        {
            0 => corpus[sequence.Next(corpus.Count)],
            1 => Junk(sequence),
            _ => store[sequence.Next(store.Count)].Code,
        };

        var mutations = sequence.Next(4);
        for (var applied = 0; applied < mutations; applied++)
        {
            input = Mutate(sequence, input);
        }

        return input;
    }

    /// <summary>
    /// One mutation, from the shapes an input actually arrives in.
    /// </summary>
    /// <param name="sequence">The run's sequence.</param>
    /// <param name="input">What to mutate.</param>
    /// <returns>The mutated input.</returns>
    private static string Mutate(Sequence sequence, string input)
    {
        var at = input.Length == 0 ? 0 : sequence.Next(input.Length);

        return sequence.Next(11) switch
        {
            // One character too many, which is the shape a relaxed length guard
            // walks into.
            0 => input.Insert(at, Alphabet[sequence.Next(Alphabet.Length)].ToString()),
            1 => input.Length == 0 ? input : input.Remove(at, 1),
            2 => input.Insert(at, Wild[sequence.Next(Wild.Length)].ToString()),
            3 => input.ToUpperInvariant(),
            4 => input.ToLowerInvariant(),
            5 => input[..at],
            6 => input + new string(Alphabet[sequence.Next(Alphabet.Length)], 1 + sequence.Next(4)),
            7 => input + input,
            8 => string.Empty,
            9 => new string(' ', sequence.Next(4)) + input + new string('-', sequence.Next(4)),
            _ => Junk(sequence),
        };
    }

    /// <summary>
    /// A string of the alphabet, at the length a code has.
    /// </summary>
    /// <param name="sequence">The run's sequence.</param>
    /// <returns>The string.</returns>
    private static string ACode(Sequence sequence)
    {
        var characters = new char[CodeLength];
        for (var position = 0; position < characters.Length; position++)
        {
            characters[position] = Alphabet[sequence.Next(Alphabet.Length)];
        }

        return new string(characters);
    }

    /// <summary>
    /// A string of anything at all, at any length up to a couple of hundred.
    /// </summary>
    /// <param name="sequence">The run's sequence.</param>
    /// <returns>The string.</returns>
    private static string Junk(Sequence sequence)
    {
        var length = sequence.Next(200);
        var characters = new char[length];
        for (var position = 0; position < length; position++)
        {
            characters[position] = sequence.Next(2) == 0
                ? Alphabet[sequence.Next(Alphabet.Length)]
                : Wild[sequence.Next(Wild.Length)];
        }

        return new string(characters);
    }

    /// <summary>
    /// An identifier drawn from the run's sequence, so nothing in a run comes
    /// from anywhere but the seed.
    /// </summary>
    /// <param name="sequence">The run's sequence.</param>
    /// <returns>The identifier.</returns>
    private static Guid AnIdentifier(Sequence sequence)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, sequence.NextValue());
        BitConverter.TryWriteBytes(bytes[8..], sequence.NextValue());

        return new Guid(bytes);
    }

    /// <summary>
    /// The committed seed corpus, one input per file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The corpus lives in the repository rather than on the runner, which is
    /// #21's own requirement: a corpus a scheduled job keeps in a cache is one
    /// nobody can read, review or shrink, and it disappears the first time that
    /// cache is cleared.
    /// </para>
    /// <para>
    /// A file's input is its text with one trailing newline removed, because
    /// every editor following this repository's own configuration writes one. A
    /// seed that was about a trailing newline would therefore be a seed nobody
    /// could keep; a leading one survives, and is what the corpus carries
    /// instead.
    /// </para>
    /// </remarks>
    /// <returns>The seeds, ordered by file name so a run is reproducible.</returns>
    private static IReadOnlyList<string> SeedCorpus()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var corpus = Path.Combine(directory.FullName, "fuzz", "corpus");
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            if (Directory.Exists(corpus) && File.Exists(solution))
            {
                return Directory.GetFiles(corpus, "*.txt")
                    .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
                    .Select(path => TrimOneNewline(File.ReadAllText(path)))
                    .ToList();
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds both Jellyfin.Plugin.Invites.sln and fuzz/corpus, so this run read no seeds. Failing rather than fuzzing from nothing.");
    }

    /// <summary>
    /// Removes the one trailing newline a file ends with, if it has one.
    /// </summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The seed.</returns>
    private static string TrimOneNewline(string text)
    {
        if (text.EndsWith('\n'))
        {
            text = text[..^1];
        }

        if (text.EndsWith('\r'))
        {
            text = text[..^1];
        }

        return text;
    }

    /// <summary>
    /// One number from the environment, or the default when it is not set.
    /// </summary>
    /// <param name="name">The variable.</param>
    /// <param name="fallback">What an unset variable means.</param>
    /// <returns>The number.</returns>
    private static int Budget(string name, int fallback)
    {
        var set = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrEmpty(set))
        {
            return fallback;
        }

        if (!int.TryParse(set, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            throw new ArgumentException(
                name + " is set to '" + set + "', which is not a positive number. Refusing rather than running the default, because a run that spent a thousandth of the budget it was given reports the same green as one that spent all of it.",
                name);
        }

        return value;
    }

    /// <summary>
    /// One input, rendered so a failure message can be read and pasted back.
    /// </summary>
    /// <param name="input">The input, which may be null.</param>
    /// <returns>The rendering.</returns>
    private static string Escape(string? input)
    {
        if (input is null)
        {
            return "<null>";
        }

        var rendered = new StringBuilder(input.Length + 2);
        rendered.Append('"');
        foreach (var character in input)
        {
            if (character is >= ' ' and <= '~' && character != '"' && character != '\\')
            {
                rendered.Append(character);
            }
            else
            {
                rendered.Append(string.Create(CultureInfo.InvariantCulture, $"\\u{(int)character:x4}"));
            }
        }

        rendered.Append('"');
        return rendered.ToString();
    }

    /// <summary>
    /// The run's arithmetic sequence.
    /// </summary>
    /// <remarks>
    /// SplitMix64, which is four lines and carries no state between runs beyond
    /// the seed. It is not a cryptographic source and nothing here wants one:
    /// what a fuzzer needs is a sequence that can be replayed from a number in a
    /// log. The source an invitation code is minted from is
    /// <see cref="RandomNumberGenerator"/> inside the plugin, and
    /// <c>weak-random</c> is what keeps it that way.
    /// </remarks>
    private sealed class Sequence
    {
        private ulong _state;

        internal Sequence(ulong seed)
        {
            _state = seed;
        }

        /// <summary>
        /// The next number below a bound.
        /// </summary>
        /// <param name="exclusiveBound">One above the largest number wanted.</param>
        /// <returns>The number.</returns>
        internal int Next(int exclusiveBound)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(exclusiveBound, 1);

            return (int)(NextValue() % (ulong)exclusiveBound);
        }

        /// <summary>
        /// The next draw, whole.
        /// </summary>
        /// <returns>The draw.</returns>
        internal ulong NextValue()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                var drawn = _state;
                drawn = (drawn ^ (drawn >> 30)) * 0xBF58476D1CE4E5B9UL;
                drawn = (drawn ^ (drawn >> 27)) * 0x94D049BB133111EBUL;
                return drawn ^ (drawn >> 31);
            }
        }
    }
}
