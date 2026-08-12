using System;
using System.IO;
using Jellyfin.Plugin.Invites.Storage;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The claim one process makes on a store directory, against a real directory
/// the test owns.
/// </summary>
/// <remarks>
/// No second process is started and none is needed. What a second server does
/// on arrival is take the claim, and a second call here is that arrival: the
/// mechanism is a file that must not already exist, so the case is reproduced
/// exactly rather than approximated by threads.
/// </remarks>
public class StoreLockTests
{
    private static readonly DateTimeOffset _started = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A free directory is claimed, and the file left behind names who has it.
    /// </summary>
    [Fact]
    public void AFreeDirectoryIsClaimedAndTheFileSaysWhoHasIt()
    {
        using var directory = new OwnedDirectory();

        using var held = StoreLock.Take(directory.Path, "kitchen-server", 4242, _started);

        Assert.True(File.Exists(held.Path));
        Assert.Equal(Path.Combine(directory.Path, StoreLock.FileName), held.Path);

        var written = File.ReadAllText(held.Path);
        Assert.Contains("kitchen-server", written, StringComparison.Ordinal);
        Assert.Contains("4242", written, StringComparison.Ordinal);
        Assert.Contains("2026-05-01", written, StringComparison.Ordinal);
    }

    /// <summary>
    /// The case this exists for. A second server pointed at a directory the
    /// first is using refuses to start on it rather than joining in.
    /// </summary>
    [Fact]
    public void ASecondClaimOnAHeldDirectoryIsRefused()
    {
        using var directory = new OwnedDirectory();
        using var first = StoreLock.Take(directory.Path, "kitchen-server", 4242, _started);

        var refusal = Assert.Throws<StoreInUseException>(
            () => StoreLock.Take(directory.Path, "attic-server", 99, _started.AddMinutes(1)));

        Assert.Contains("kitchen-server", refusal.HeldBy, StringComparison.Ordinal);
        Assert.Contains("4242", refusal.HeldBy, StringComparison.Ordinal);
        Assert.Equal(first.Path, refusal.Path);
    }

    /// <summary>
    /// The refusal tells an operator what to do, because the person reading it
    /// has a server that will not start.
    /// </summary>
    [Fact]
    public void TheRefusalNamesTheFileToDeleteAndWhy()
    {
        using var directory = new OwnedDirectory();
        using var first = StoreLock.Take(directory.Path, "kitchen-server", 4242, _started);

        var refusal = Assert.Throws<StoreInUseException>(
            () => StoreLock.Take(directory.Path, "attic-server", 99, _started));

        Assert.Contains(first.Path, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("no longer running", refusal.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A claim left behind does not expire. Arriving a year later still refuses,
    /// because a claim that timed out would eventually let the two servers meet.
    /// </summary>
    [Fact]
    public void AClaimDoesNotExpireOnItsOwn()
    {
        using var directory = new OwnedDirectory();
        using var first = StoreLock.Take(directory.Path, "kitchen-server", 4242, _started);

        Assert.Throws<StoreInUseException>(
            () => StoreLock.Take(directory.Path, "attic-server", 99, _started.AddYears(1)));
    }

    /// <summary>
    /// A released claim leaves the directory free, and the file it wrote is
    /// gone.
    /// </summary>
    [Fact]
    public void AReleasedClaimLeavesTheDirectoryFree()
    {
        using var directory = new OwnedDirectory();

        var path = Path.Combine(directory.Path, StoreLock.FileName);
        var first = StoreLock.Take(directory.Path, "kitchen-server", 4242, _started);
        first.Dispose();

        Assert.False(File.Exists(path));

        using var second = StoreLock.Take(directory.Path, "attic-server", 99, _started.AddMinutes(1));
        Assert.True(File.Exists(second.Path));
    }

    /// <summary>
    /// Releasing twice is not a way to remove somebody else's claim, and neither
    /// is releasing after an operator cleared a stale one by hand.
    /// </summary>
    /// <remarks>
    /// The sequence is the one that happens after a machine is killed: the
    /// operator deletes the file the refusal named, a server takes the directory,
    /// and the first process finally gets round to letting go. What it lets go of
    /// must not be the second server's claim.
    /// </remarks>
    [Fact]
    public void ReleasingDoesNotRemoveAClaimItDidNotWrite()
    {
        using var directory = new OwnedDirectory();

        var abandoned = StoreLock.Take(directory.Path, "kitchen-server", 4242, _started);
        File.Delete(abandoned.Path);

        using var second = StoreLock.Take(directory.Path, "attic-server", 99, _started.AddMinutes(1));

        abandoned.Dispose();

        Assert.True(File.Exists(second.Path));
        Assert.Contains("attic-server", File.ReadAllText(second.Path), StringComparison.Ordinal);
    }

    /// <summary>
    /// A directory that is not there yet is a first run rather than a failure.
    /// </summary>
    [Fact]
    public void ADirectoryThatIsNotThereYetIsCreated()
    {
        using var directory = new OwnedDirectory();
        var below = Path.Combine(directory.Path, "not-created-yet");

        using var held = StoreLock.Take(below, "kitchen-server", 4242, _started);

        Assert.True(File.Exists(held.Path));
    }

    /// <summary>
    /// A claim needs a directory to be about.
    /// </summary>
    [Fact]
    public void AClaimOnNoDirectoryIsRefused()
    {
        Assert.Throws<ArgumentException>(() => StoreLock.Take(" ", "kitchen-server", 4242, _started));
    }
}
