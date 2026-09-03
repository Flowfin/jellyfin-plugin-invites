using System;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.Versioning;
using Jellyfin.Plugin.Invites.Invitations;
using Jellyfin.Plugin.Invites.Storage;
using Xunit;

namespace Jellyfin.Plugin.Invites.Tests;

/// <summary>
/// The keyed hash secret's life cycle, against real files in a real directory.
/// </summary>
/// <remarks>
/// The whole of this issue is what happens to a file: whether it is drawn, what
/// mode it is drawn at, what is done when it is not there and what replacing it
/// costs. A fake filesystem here would prove that the fake behaves, so every leg
/// below writes and reads a directory it owns and deletes.
/// </remarks>
public class HashSecretTests
{
    private static readonly Invitation[] _none = Array.Empty<Invitation>();

    /// <summary>
    /// A first run has nothing in the store, so the absent file is a new
    /// installation and a key is drawn.
    /// </summary>
    [Fact]
    public void AFirstRunDrawsTheSecret()
    {
        using var directory = new OwnedDirectory();

        var opened = HashSecret.OpenOrCreate(directory.Path, _none);

        Assert.True(opened.WasCreated);
        Assert.Equal(HashSecret.Bytes, opened.Value.Length);
        Assert.True(File.Exists(opened.Path));
        Assert.Equal(HashSecret.Bytes, new FileInfo(opened.Path).Length);
    }

    /// <summary>
    /// It sits beside the store, in the directory the plugin was given, under
    /// its own name and not inside the configuration.
    /// </summary>
    [Fact]
    public void ItSitsInTheDirectoryUnderItsOwnName()
    {
        using var directory = new OwnedDirectory();

        var opened = HashSecret.OpenOrCreate(directory.Path, _none);

        Assert.Equal(Path.Combine(directory.Path, HashSecret.FileName), opened.Path);
        Assert.Equal(HashSecret.PathIn(directory.Path), opened.Path);
        Assert.NotEqual(InvitationStore.FileName, HashSecret.FileName);
    }

    /// <summary>
    /// A second run reads what the first drew, byte for byte. Anything else is
    /// a rotation happening on every start.
    /// </summary>
    [Fact]
    public void ASecondRunReadsWhatTheFirstDrew()
    {
        using var directory = new OwnedDirectory();

        var first = HashSecret.OpenOrCreate(directory.Path, _none);
        var second = HashSecret.OpenOrCreate(directory.Path, _none);

        Assert.False(second.WasCreated);
        Assert.Equal(first.Value.AsSpan().ToArray(), second.Value.AsSpan().ToArray());
    }

    /// <summary>
    /// Two installations do not share a key. This is the property a shipped
    /// default would break, and it is asserted rather than assumed because the
    /// failure looks like nothing at all.
    /// </summary>
    [Fact]
    public void TwoInstallationsDrawDifferentSecrets()
    {
        using var here = new OwnedDirectory();
        using var there = new OwnedDirectory();

        var mine = HashSecret.OpenOrCreate(here.Path, _none);
        var yours = HashSecret.OpenOrCreate(there.Path, _none);

        Assert.NotEqual(mine.Value.AsSpan().ToArray(), yours.Value.AsSpan().ToArray());
    }

    /// <summary>
    /// The fail-closed path. The store holds a record and the key is gone, so
    /// every stored hash is already unverifiable and drawing a fresh key would
    /// report a healthy start while making that permanent.
    /// </summary>
    [Fact]
    public void AMissingSecretWithRecordsIsRefused()
    {
        using var directory = new OwnedDirectory();
        Directory.CreateDirectory(directory.Path);

        var refused = Assert.Throws<HashSecretRefusedException>(
            () => HashSecret.OpenOrCreate(directory.Path, new[] { AnInvitation() }));

        Assert.Contains(HashSecret.PathIn(directory.Path), refused.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(HashSecret.PathIn(directory.Path)));
    }

    /// <summary>
    /// And it refuses without writing anything, which is what makes the repair
    /// possible. A refusal that had drawn a key first would have destroyed the
    /// state an operator restores into.
    /// </summary>
    [Fact]
    public void ARefusalLeavesTheDirectoryAsItWas()
    {
        using var directory = new OwnedDirectory();
        Directory.CreateDirectory(directory.Path);

        Assert.Throws<HashSecretRefusedException>(
            () => HashSecret.OpenOrCreate(directory.Path, new[] { AnInvitation() }));

        Assert.Empty(Directory.GetFiles(directory.Path));
    }

    /// <summary>
    /// A file that is there and is not a key is refused rather than read
    /// short, and it is left exactly as it was found.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(HashSecret.Bytes - 1)]
    [InlineData(HashSecret.Bytes + 1)]
    public void AFileOfTheWrongLengthIsRefusedAndLeftAlone(int length)
    {
        using var directory = new OwnedDirectory();
        Directory.CreateDirectory(directory.Path);
        var path = HashSecret.PathIn(directory.Path);
        var planted = new byte[length];
        Array.Fill(planted, (byte)0x5a);
        File.WriteAllBytes(path, planted);

        var refused = Assert.Throws<HashSecretRefusedException>(
            () => HashSecret.OpenOrCreate(directory.Path, _none));

        Assert.Contains(path, refused.Message, StringComparison.Ordinal);
        Assert.Equal(planted, File.ReadAllBytes(path));
    }

    /// <summary>
    /// The refusal names the file and the two lengths and nothing else. A
    /// message quoting any part of the value would put the key into whatever
    /// reads the message.
    /// </summary>
    [Fact]
    public void TheRefusalCarriesNoPartOfTheFile()
    {
        using var directory = new OwnedDirectory();
        Directory.CreateDirectory(directory.Path);
        var path = HashSecret.PathIn(directory.Path);
        File.WriteAllBytes(path, new byte[] { 0xde, 0xad, 0xbe, 0xef });

        var refused = Assert.Throws<HashSecretRefusedException>(
            () => HashSecret.OpenOrCreate(directory.Path, _none));

        Assert.DoesNotContain("deadbeef", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("3q2+7w", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A rotation says what it will cost before anything happens, and planning
    /// it changes nothing on disk.
    /// </summary>
    [Fact]
    public void PlanningARotationSaysWhatItCostsAndWritesNothing()
    {
        using var directory = new OwnedDirectory();
        var drawn = HashSecret.OpenOrCreate(directory.Path, _none);
        var records = new[] { AnInvitation(), AnotherInvitation() };

        var plan = HashSecret.PlanRotation(directory.Path, records);

        Assert.Equal(2, plan.Invalidates);
        Assert.Contains("2 record(s)", plan.Detail, StringComparison.Ordinal);
        Assert.Equal(drawn.Path, plan.Path);
        var reopened = HashSecret.OpenOrCreate(directory.Path, records);
        Assert.Equal(drawn.Value.AsSpan().ToArray(), reopened.Value.AsSpan().ToArray());
    }

    /// <summary>
    /// The count is every record held, including the ones that were already
    /// expired, spent or revoked, and the sentence an operator reads says so.
    /// Which of them could still have been redeemed is the redemption
    /// decision's judgement and is not made a second time here.
    /// </summary>
    [Fact]
    public void TheCountIsEveryRecordAndTheSentenceSaysSo()
    {
        using var directory = new OwnedDirectory();

        var plan = HashSecret.PlanRotation(directory.Path, new[] { AnInvitation(), AnotherInvitation() });

        Assert.Contains("expired, spent or revoked", plan.Detail, StringComparison.Ordinal);
        Assert.Contains("No account is touched", plan.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// Confirming a plan replaces the key, and the old one is not left beside
    /// the new one for whoever reads the directory.
    /// </summary>
    [Fact]
    public void RotatingReplacesTheSecretAndKeepsNoCopy()
    {
        using var directory = new OwnedDirectory();
        var before = HashSecret.OpenOrCreate(directory.Path, _none);
        var records = new[] { AnInvitation() };
        var plan = HashSecret.PlanRotation(directory.Path, records);

        var after = HashSecret.Rotate(plan, records);

        Assert.NotEqual(before.Value.AsSpan().ToArray(), after.Value.AsSpan().ToArray());
        Assert.Equal(HashSecret.Bytes, after.Value.Length);
        Assert.Equal(before.Path, after.Path);
        Assert.Equal(new[] { before.Path }, Directory.GetFiles(directory.Path));
    }

    /// <summary>
    /// A confirmation made against a store that has moved is refused. What the
    /// operator agreed to was a number, and that number is no longer the cost.
    /// </summary>
    [Fact]
    public void AConfirmationAgainstAMovedStoreIsRefused()
    {
        using var directory = new OwnedDirectory();
        var before = HashSecret.OpenOrCreate(directory.Path, _none);
        var plan = HashSecret.PlanRotation(directory.Path, new[] { AnInvitation(), AnotherInvitation() });

        var refused = Assert.Throws<InvalidOperationException>(
            () => HashSecret.Rotate(plan, new[] { AnInvitation() }));

        Assert.Contains("Nothing was rotated", refused.Message, StringComparison.Ordinal);
        var unchanged = HashSecret.OpenOrCreate(directory.Path, _none);
        Assert.Equal(before.Value.AsSpan().ToArray(), unchanged.Value.AsSpan().ToArray());
    }

    /// <summary>
    /// Nothing is rotated by accident. There is no way to reach the write
    /// except through a plan, so the count exists before the key is replaced.
    /// </summary>
    [Fact]
    public void RotationRefusesWithoutAPlan()
    {
        using var directory = new OwnedDirectory();
        var records = new[] { AnInvitation() };

        Assert.Throws<ArgumentNullException>(() => HashSecret.Rotate(null!, records));
        Assert.Throws<ArgumentNullException>(() => HashSecret.Rotate(HashSecret.PlanRotation(directory.Path, records), null!));
        Assert.Throws<ArgumentNullException>(() => HashSecret.OpenOrCreate(directory.Path, null!));
    }

    /// <summary>
    /// A directory that is not one is refused before any file is touched.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ADirectoryThatIsNotOneIsRefused(string? directory)
    {
        Assert.Throws<ArgumentException>(() => HashSecret.PathIn(directory!));
        Assert.Throws<ArgumentException>(() => HashSecret.OpenOrCreate(directory!, _none));
        Assert.Throws<ArgumentException>(() => HashSecret.PlanRotation(directory!, _none));
    }

    /// <summary>
    /// The key is created for its owner alone, at the mode the store file
    /// already decided on, and the finding says the mode was read and is no
    /// wider than that.
    /// </summary>
    [UnixOnlyFact]
    [UnsupportedOSPlatform("windows")]
    public void TheSecretIsCreatedForItsOwnerOnly()
    {
        using var directory = new OwnedDirectory();

        var opened = HashSecret.OpenOrCreate(directory.Path, _none);

        Assert.Equal(InvitationStore.CreatedMode, File.GetUnixFileMode(opened.Path));
        Assert.Equal(StorePermissionState.AsWritten, opened.Permissions.State);
        Assert.False(opened.Permissions.IsAProblem);
    }

    /// <summary>
    /// A key somebody widened afterwards is reported, with the file and the
    /// mode in the sentence, and it is not repaired: the mode is left as it was
    /// found, which is the rule the store file follows for the same reason.
    /// </summary>
    [UnixOnlyFact]
    [UnsupportedOSPlatform("windows")]
    public void AWiderModeIsReportedAndNotRepaired()
    {
        using var directory = new OwnedDirectory();
        var path = HashSecret.OpenOrCreate(directory.Path, _none).Path;
        var widened = InvitationStore.CreatedMode | UnixFileMode.OtherRead;
        File.SetUnixFileMode(path, widened);

        var reopened = HashSecret.OpenOrCreate(directory.Path, _none);

        Assert.Equal(StorePermissionState.WiderThanWritten, reopened.Permissions.State);
        Assert.True(reopened.Permissions.IsAProblem);
        Assert.Contains(path, reopened.Permissions.Detail, StringComparison.Ordinal);
        Assert.Equal(widened, File.GetUnixFileMode(path));
    }

    /// <summary>
    /// And a mode an operator tightened survives a rotation, because a fresh
    /// file carries a fresh file's mode unless something puts the old one back.
    /// </summary>
    [UnixOnlyFact]
    [UnsupportedOSPlatform("windows")]
    public void RotationKeepsTheModeItFound()
    {
        using var directory = new OwnedDirectory();
        var path = HashSecret.OpenOrCreate(directory.Path, _none).Path;
        File.SetUnixFileMode(path, UnixFileMode.UserRead);
        var records = new[] { AnInvitation() };

        HashSecret.Rotate(HashSecret.PlanRotation(directory.Path, records), records);

        Assert.Equal(UnixFileMode.UserRead, File.GetUnixFileMode(path));
    }

    /// <summary>
    /// Where the platform has no file modes, the finding says the mode was not
    /// read rather than that it was read and found fine. A check that read
    /// nothing must not report as one that found nothing.
    /// </summary>
    [WindowsOnlyFact]
    public void OnAPlatformWithNoModesItSaysTheModeWasNotRead()
    {
        using var directory = new OwnedDirectory();

        var opened = HashSecret.OpenOrCreate(directory.Path, _none);

        Assert.Equal(StorePermissionState.NotCheckedOnThisPlatform, opened.Permissions.State);
        Assert.False(opened.Permissions.IsAProblem);
        Assert.Contains(opened.Path, opened.Permissions.Detail, StringComparison.Ordinal);
    }

    private static Invitation AnInvitation()
    {
        return new Invitation(
            id: Guid.Parse("6f0a2d1e-6b3c-4f8a-9f1d-2a7c5e8b0d34"),
            codeHash: ImmutableArray.Create<byte>(0x01, 0x7f, 0x80),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.FromHours(2)),
            expiresAt: new DateTimeOffset(2026, 3, 11, 5, 6, 7, TimeSpan.FromHours(2)),
            usesGranted: 3,
            usesRemaining: 1,
            revokedAt: null,
            revokedBy: null,
            templateLabel: "Household",
            template: TestTemplates.Household,
            accountsProduced: ImmutableArray<Guid>.Empty);
    }

    private static Invitation AnotherInvitation()
    {
        return new Invitation(
            id: Guid.Parse("2c9f7b41-0d5e-4a63-8b12-7e4d3f6a9c05"),
            codeHash: ImmutableArray.Create<byte>(0xaa, 0xbb, 0xcc),
            mintedBy: Guid.Parse("11112222-3333-4444-5555-666677778888"),
            mintedAt: new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero),
            expiresAt: new DateTimeOffset(2026, 4, 8, 10, 0, 0, TimeSpan.Zero),
            usesGranted: 1,
            usesRemaining: 1,
            revokedAt: null,
            revokedBy: null,
            templateLabel: "Friends",
            template: TestTemplates.Household,
            accountsProduced: ImmutableArray<Guid>.Empty);
    }
}
