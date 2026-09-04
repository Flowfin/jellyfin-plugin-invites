using System;
using System.Linq;
using Jellyfin.Plugin.Invites.Accounts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The third of #33's ceilings: how many accounts this plugin may create in a
/// window, across every invitation.
/// </summary>
/// <remarks>
/// <para>
/// It is the one that still holds when the other two are set badly. A use count
/// bounds one invitation and the live ceiling bounds how many may stand at once,
/// and both are numbers somebody chooses; five hundred live invitations at ten
/// uses each is five thousand accounts the standing set authorises with no
/// further operator action.
/// </para>
/// <para>
/// Nothing here sleeps. The window is crossed by moving the injected clock,
/// which is what the seam exists for.
/// </para>
/// </remarks>
public class CreationCeilingTests
{
    private static readonly DateTimeOffset _now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The window admits exactly its number and refuses the next.
    /// </summary>
    [Fact]
    public void TheWindowAdmitsItsNumberAndRefusesTheNext()
    {
        var ceiling = new CreationCeiling(new TestClock(_now));

        for (var created = 0; created < CreationCeiling.AccountsInAWindow; created++)
        {
            Assert.True(ceiling.MayCreate(), "Refused at " + created + ", inside the ceiling.");
        }

        Assert.False(ceiling.MayCreate());
        Assert.Equal(CreationCeiling.AccountsInAWindow, ceiling.AllowedInThisWindow);
    }

    /// <summary>
    /// A refusal does not raise the count, so the ceiling refuses from the same
    /// place however often somebody asks.
    /// </summary>
    /// <remarks>
    /// A counter that rose on a refusal would still refuse, so nothing an
    /// attacker does gets past it, and the count an operator reads would stop
    /// meaning accounts. It is the number's meaning that is protected here
    /// rather than the bound.
    /// </remarks>
    [Fact]
    public void ARefusalDoesNotRaiseTheCount()
    {
        var ceiling = new CreationCeiling(new TestClock(_now));
        for (var created = 0; created < CreationCeiling.AccountsInAWindow; created++)
        {
            Assert.True(ceiling.MayCreate());
        }

        for (var refused = 0; refused < 5; refused++)
        {
            Assert.False(ceiling.MayCreate());
        }

        Assert.Equal(CreationCeiling.AccountsInAWindow, ceiling.AllowedInThisWindow);
    }

    /// <summary>
    /// The next window starts empty, and the count says so before anything is
    /// asked of it.
    /// </summary>
    /// <remarks>
    /// The reading is taken before the first call in the new window on purpose.
    /// A count that only reset when something asked would report the last
    /// window's total to an operator looking at a quiet server, which is exactly
    /// when they would be looking.
    /// </remarks>
    [Fact]
    public void TheNextWindowStartsEmpty()
    {
        var clock = new TestClock(_now);
        var ceiling = new CreationCeiling(clock);
        for (var created = 0; created < CreationCeiling.AccountsInAWindow; created++)
        {
            Assert.True(ceiling.MayCreate());
        }

        Assert.False(ceiling.MayCreate());

        clock.Advance(CreationCeiling.Window);

        Assert.Equal(0, ceiling.AllowedInThisWindow);
        Assert.True(ceiling.MayCreate());
        Assert.Equal(1, ceiling.AllowedInThisWindow);
    }

    /// <summary>
    /// Time passing inside a window gives nothing back.
    /// </summary>
    /// <remarks>
    /// This is the half a fixed window is easiest to get wrong in the generous
    /// direction. A counter reset by any movement of the clock rather than by a
    /// window boundary would admit an unbounded number of accounts to anybody
    /// patient enough to space them out.
    /// </remarks>
    [Fact]
    public void TimePassingInsideAWindowGivesNothingBack()
    {
        var clock = new TestClock(_now);
        var ceiling = new CreationCeiling(clock);
        for (var created = 0; created < CreationCeiling.AccountsInAWindow; created++)
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            Assert.True(ceiling.MayCreate());
        }

        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.False(ceiling.MayCreate());
    }

    /// <summary>
    /// The window is aligned to the clock and not to the first request, so
    /// nearly a whole window of waiting inside one gives nothing back and the
    /// boundary arrives at the same moment for everybody.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the surprising half and it is asserted rather than left to be
    /// discovered. Filling the ceiling half an hour after a boundary and waiting
    /// twenty-three hours does not open it; crossing the next boundary does,
    /// however little of the window has been used. It is the same alignment
    /// <c>AttemptLimiter</c> has, and it is what makes a window a period rather
    /// than a timer somebody starts by asking.
    /// </para>
    /// <para>
    /// What it costs is the boundary crossing a fixed window always costs: the
    /// number is reachable twice in quick succession around one of them. That is
    /// the trade the type states, and it is why the number is chosen with a
    /// doubling in mind.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheWindowIsAlignedToTheClockRatherThanToTheFirstRequest()
    {
        var justInside = new DateTimeOffset(2026, 5, 1, 0, 30, 0, TimeSpan.Zero);
        var clock = new TestClock(justInside);
        var ceiling = new CreationCeiling(clock);
        for (var created = 0; created < CreationCeiling.AccountsInAWindow; created++)
        {
            Assert.True(ceiling.MayCreate());
        }

        clock.Advance(TimeSpan.FromHours(23));

        Assert.False(ceiling.MayCreate());

        clock.Advance(TimeSpan.FromHours(1));

        Assert.True(ceiling.MayCreate());
    }

    /// <summary>
    /// It holds no address, no code and no identifier, which is why it can count
    /// on an endpoint a stranger reaches without keeping anything about them.
    /// </summary>
    /// <remarks>
    /// Read off the type rather than asserted about the implementation in prose.
    /// A field of any other shape is a thing somebody has to argue a row for in
    /// docs/personal-data.md, and the moment to catch it is when it is added.
    /// </remarks>
    [Fact]
    public void ItHoldsNothingAboutAnybody()
    {
        var fields = typeof(CreationCeiling)
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Select(field => field.FieldType.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "IClock", "Int32", "Int64", "Object" }, fields);
    }

    /// <summary>
    /// The ceiling is offered to the server once for the whole process.
    /// </summary>
    /// <remarks>
    /// A ceiling handed out per request counts to one and bounds nothing, and it
    /// would pass every assertion above, because those drive one instance
    /// directly. That is the same assertion `LimiterRegistrationTests` makes for
    /// the same reason, and it is made here rather than there because a file
    /// about one type is the wrong place to notice the other one going missing.
    /// </remarks>
    [Fact]
    public void TheCeilingIsRegisteredForTheLifetimeItsNumberRestsOn()
    {
        var services = new ServiceCollection();

        new Jellyfin.Plugin.Invites.Startup.PluginServiceRegistrator()
            .RegisterServices(services, null!);

        var registered = services
            .Where(descriptor => descriptor.ServiceType == typeof(CreationCeiling))
            .ToArray();

        Assert.True(
            registered.Length == 1,
            "The ceiling is registered " + registered.Length + " time(s). One the server cannot resolve bounds nothing, and two of them are two counters.");

        Assert.Equal(ServiceLifetime.Singleton, registered[0].Lifetime);
    }
}
