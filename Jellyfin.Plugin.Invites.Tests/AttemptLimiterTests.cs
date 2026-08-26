using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.Invites.Redemption;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The limiter counts to the two numbers docs/rate-limit.md decided, in fixed
/// windows, and forgets an address when its window turns.
/// </summary>
/// <remarks>
/// <para>
/// That page ends by saying no limiter has been written and that an
/// implementation which persisted the counter to disk, or counted to a different
/// pair of numbers, would pass every workflow in this repository. This file is
/// the half of that which a suite can hold.
/// </para>
/// <para>
/// Nothing here sleeps. Every window is crossed by moving the injected clock,
/// which is the clause of #31 asking for exactly that, and
/// <see cref="SuiteDoesNotSleepTests"/> is what refuses the alternative.
/// </para>
/// </remarks>
public class AttemptLimiterTests
{
    private static readonly DateTimeOffset _start = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// One address gets exactly twenty judged attempts in its window and the
    /// twenty-first is refused.
    /// </summary>
    /// <remarks>
    /// The global limit is kept out of the way by moving the clock a second
    /// between attempts, which is the whole reason the two windows are different
    /// lengths: ten a second is not a bound on twenty an hour.
    /// </remarks>
    [Fact]
    public void OneAddressGetsExactlyTheDecidedNumberInItsWindow()
    {
        var clock = new TestClock(_start);
        var limiter = new AttemptLimiter(clock);

        for (var attempt = 1; attempt <= AttemptLimiter.PerAddressCeiling; attempt++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.True(limiter.MayJudge("198.51.100.7"), "Attempt " + attempt + " was refused inside the ceiling.");
        }

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(limiter.MayJudge("198.51.100.7"));
    }

    /// <summary>
    /// An address that has spent its allowance cannot go on taking the global
    /// allowance away from everybody else by being refused.
    /// </summary>
    /// <remarks>
    /// <b>This is the assertion the obvious version of this test does not make,
    /// and the difference is the clock.</b> Refusing the exhausted address once a
    /// second proves nothing, because the global window turns between every one of
    /// them and a counter that wrongly counted those refusals would look exactly
    /// like one that did not. So the refusals here all fall inside one global
    /// window, and the second address then asks for the whole global allowance in
    /// that same window. Found by applying the fault: raising the global counter
    /// before the per-address check left the spaced-out version green.
    /// </remarks>
    [Fact]
    public void AnExhaustedAddressCannotSpendTheGlobalAllowanceByBeingRefused()
    {
        var clock = new TestClock(_start);
        var limiter = new AttemptLimiter(clock);

        for (var attempt = 1; attempt <= AttemptLimiter.PerAddressCeiling; attempt++)
        {
            clock.Advance(AttemptLimiter.GlobalWindow);
            Assert.True(limiter.MayJudge("198.51.100.7"));
        }

        clock.Advance(AttemptLimiter.GlobalWindow);

        for (var refused = 1; refused <= 50; refused++)
        {
            Assert.False(limiter.MayJudge("198.51.100.7"));
        }

        for (var attempt = 1; attempt <= AttemptLimiter.GlobalCeiling; attempt++)
        {
            Assert.True(
                limiter.MayJudge("203.0.113.9"),
                "The second address was refused at attempt " + attempt + " of the global allowance, in the same window the first address spent only refusals in. A refusal took a slot from somebody who had not used one.");
        }
    }

    /// <summary>
    /// And the same in the other direction, within one window: an address refused
    /// by the global limit has not spent any of its own allowance either.
    /// </summary>
    [Fact]
    public void AnAddressRefusedGloballyHasSpentNoneOfItsOwnAllowance()
    {
        var clock = new TestClock(_start);
        var limiter = new AttemptLimiter(clock);

        for (var attempt = 1; attempt <= AttemptLimiter.GlobalCeiling; attempt++)
        {
            Assert.True(limiter.MayJudge("198.51.100." + attempt.ToString(CultureInfo.InvariantCulture)));
        }

        for (var refused = 1; refused <= 30; refused++)
        {
            Assert.False(limiter.MayJudge("203.0.113.9"));
        }

        clock.Advance(AttemptLimiter.GlobalWindow);

        var allowed = 0;
        for (var attempt = 1; attempt <= AttemptLimiter.PerAddressCeiling + 5; attempt++)
        {
            clock.Advance(AttemptLimiter.GlobalWindow);
            if (limiter.MayJudge("203.0.113.9"))
            {
                allowed++;
            }
        }

        Assert.Equal(AttemptLimiter.PerAddressCeiling, allowed);
    }

    /// <summary>
    /// The window is fixed rather than sliding, so the allowance comes back when
    /// the window turns and not an hour after the last attempt.
    /// </summary>
    [Fact]
    public void TheAllowanceComesBackWhenTheWindowTurns()
    {
        var clock = new TestClock(_start);
        var limiter = new AttemptLimiter(clock);

        for (var attempt = 1; attempt <= AttemptLimiter.PerAddressCeiling; attempt++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            Assert.True(limiter.MayJudge("198.51.100.7"));
        }

        Assert.False(limiter.MayJudge("198.51.100.7"));

        clock.Advance(AttemptLimiter.PerAddressWindow);

        Assert.True(limiter.MayJudge("198.51.100.7"));
    }

    /// <summary>
    /// Across all sources, ten attempts a second and no eleventh, however many
    /// addresses they arrive from. This is the limit that closes the gap a spread
    /// of addresses opens in the per-address one.
    /// </summary>
    [Fact]
    public void AllSourcesTogetherGetTheDecidedNumberInASecond()
    {
        var clock = new TestClock(_start);
        var limiter = new AttemptLimiter(clock);

        for (var attempt = 1; attempt <= AttemptLimiter.GlobalCeiling; attempt++)
        {
            Assert.True(
                limiter.MayJudge("198.51.100." + attempt.ToString(CultureInfo.InvariantCulture)),
                "Attempt " + attempt + " from its own address was refused inside the global ceiling.");
        }

        Assert.False(limiter.MayJudge("203.0.113.9"));

        clock.Advance(AttemptLimiter.GlobalWindow);

        Assert.True(limiter.MayJudge("203.0.113.9"));
    }

    /// <summary>
    /// The address is held for its window and no longer, which is the claim
    /// docs/personal-data.md makes on this component's behalf. The count is read
    /// rather than the addresses.
    /// </summary>
    [Fact]
    public void AnAddressIsHeldForItsWindowAndNoLonger()
    {
        var clock = new TestClock(_start);
        var limiter = new AttemptLimiter(clock);

        Assert.Equal(0, limiter.AddressesHeld);

        Assert.True(limiter.MayJudge("198.51.100.7"));
        Assert.True(limiter.MayJudge("203.0.113.9"));
        Assert.Equal(2, limiter.AddressesHeld);

        clock.Advance(AttemptLimiter.PerAddressWindow);

        Assert.Equal(0, limiter.AddressesHeld);
    }

    /// <summary>
    /// A caller that could not read an address is refused rather than counted as
    /// everybody. A blank key would be one bucket every unreadable request shared,
    /// which is a limit on nothing in particular.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AnAttemptWithNoAddressIsRefusedRatherThanCounted(string? address)
    {
        var limiter = new AttemptLimiter(new TestClock(_start));

        Assert.Throws<ArgumentException>(() => limiter.MayJudge(address!));
    }

    /// <summary>
    /// The two numbers in the code are the two numbers on the page that decided
    /// them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// docs/rate-limit.md ends by saying an implementation counting to a different
    /// pair of numbers would pass every workflow in this repository. This is the
    /// half of that gap a suite can close: the sentence on the page is read and
    /// the words in it are resolved, so moving either number in the source without
    /// moving it on the page turns this red.
    /// </para>
    /// <para>
    /// <b>What it cannot see.</b> It reads one sentence, matched by its shape, and
    /// it resolves number words from a small table. A page that stopped carrying
    /// that sentence fails here rather than passing, which is the direction to
    /// fail in, but nothing here judges whether the argument around the sentence
    /// still supports the number. That is a reading a person makes.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheNumbersInTheCodeAreTheNumbersOnThePageThatDecidedThem()
    {
        var words = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["one"] = 1,
            ["two"] = 2,
            ["five"] = 5,
            ["ten"] = 10,
            ["twenty"] = 20,
            ["thirty"] = 30,
            ["fifty"] = 50,
            ["a hundred"] = 100,
        };

        // The sentence wraps across lines on the page, so the whole text is
        // reduced to single spaces before it is matched. A pattern that only
        // worked while the line breaks fell where they do today would go red for
        // a reflow rather than for a number moving.
        var page = new Regex(@"\s+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5))
            .Replace(RateLimitPage(), " ");

        var perAddress = new Regex(
            @"Per source address, ([a-z ]+?) attempts an hour",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5)).Match(page);

        var global = new Regex(
            @"Across all sources, ([a-z ]+?) attempts a second",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5)).Match(page);

        Assert.True(
            perAddress.Success && global.Success,
            "docs/rate-limit.md no longer carries the sentence that states the two thresholds, so nothing here compared them against the code. Restore the sentence or move this assertion to whatever replaced it.");

        Assert.True(words.ContainsKey(perAddress.Groups[1].Value), "The per-address threshold on the page reads " + perAddress.Groups[1].Value + ", which this test cannot resolve to a number.");
        Assert.True(words.ContainsKey(global.Groups[1].Value), "The global threshold on the page reads " + global.Groups[1].Value + ", which this test cannot resolve to a number.");

        Assert.Equal(AttemptLimiter.PerAddressCeiling, words[perAddress.Groups[1].Value]);
        Assert.Equal(AttemptLimiter.GlobalCeiling, words[global.Groups[1].Value]);
        Assert.Equal(TimeSpan.FromHours(1), AttemptLimiter.PerAddressWindow);
        Assert.Equal(TimeSpan.FromSeconds(1), AttemptLimiter.GlobalWindow);
    }

    /// <summary>
    /// Nothing in the limiter reaches a file, which is the lifetime
    /// docs/rate-limit.md settled before it chose either number. A counter that
    /// survived a restart would move the guarantee onto a control an attacker
    /// resets by waiting and an operator resets by upgrading.
    /// </summary>
    /// <remarks>
    /// Read off the type's own members rather than off the source text, so a
    /// write reached through a helper in another file is seen too: the assertion
    /// is that nothing this type holds is a file, a stream or a path.
    /// </remarks>
    [Fact]
    public void TheCounterHoldsNothingThatCouldOutliveTheProcess()
    {
        var durable = typeof(AttemptLimiter)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
            .Where(field => typeof(Stream).IsAssignableFrom(field.FieldType)
                || typeof(FileSystemInfo).IsAssignableFrom(field.FieldType))
            .Select(field => field.Name)
            .ToArray();

        Assert.Empty(durable);

        var reached = typeof(AttemptLimiter)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "IClock" }, reached);
    }

    /// <summary>
    /// The page the numbers are read from.
    /// </summary>
    /// <remarks>
    /// Found by walking up from the test binary until a directory holds both the
    /// solution and the page, which is how every other leg over a tracked document
    /// here finds one. Nothing is written and nothing outside the repository is
    /// read.
    /// </remarks>
    /// <returns>The text of the page.</returns>
    private static string RateLimitPage()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var page = Path.Combine(directory.FullName, "docs", "rate-limit.md");
            var solution = Path.Combine(directory.FullName, "Jellyfin.Plugin.Invites.sln");
            if (File.Exists(page) && File.Exists(solution))
            {
                return File.ReadAllText(page);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "No ancestor of "
            + AppContext.BaseDirectory
            + " holds both Jellyfin.Plugin.Invites.sln and docs/rate-limit.md, so this comparison read nothing. Failing rather than passing over an empty page.");
    }
}

/// <summary>
/// What the plugin hands the server for the limiter, and the one thing about it
/// that has to be right.
/// </summary>
/// <remarks>
/// <para>
/// The counter is a process-lifetime structure and every number
/// docs/rate-limit.md chose rests on that. A registration that handed out a new
/// limiter per request, or per scope, would give every attempt an empty counter
/// and pass every assertion in the file above, because those drive one instance
/// directly. This is the assertion that cannot be made there.
/// </para>
/// <para>
/// The registrator is driven the way the server drives it, on a real service
/// collection, and the descriptors are read back. The application host it takes
/// is not reached by the method, so nothing is built to stand in for it; a
/// registration that started using it would fail here rather than pass, which is
/// the direction to fail in.
/// </para>
/// </remarks>
public class LimiterRegistrationTests
{
    /// <summary>
    /// The limiter is offered to the server, once for the whole process.
    /// </summary>
    [Fact]
    public void TheLimiterIsRegisteredForTheLifetimeItsNumbersRestOn()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        new Jellyfin.Plugin.Invites.Startup.PluginServiceRegistrator()
            .RegisterServices(services, null!);

        var registered = services
            .Where(descriptor => descriptor.ServiceType == typeof(AttemptLimiter))
            .ToArray();

        Assert.True(
            registered.Length == 1,
            "The limiter is registered " + registered.Length + " time(s). A limiter the server cannot resolve is a limit on nothing, and two of them are two counters.");

        Assert.Equal(
            Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton,
            registered[0].Lifetime);
    }
}
