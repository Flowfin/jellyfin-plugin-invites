using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Jellyfin.Plugin.Invites.Invitations;

namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// The key behind the keyed hash: where it comes from, where it lives, what its
/// permissions are, what happens when it is not there, and what rotating it
/// costs.
/// </summary>
/// <remarks>
/// <para>
/// <b>A file of its own, beside the store and never inside the configuration.</b>
/// The configuration is serialised by the server, rendered on a page in the
/// dashboard and pasted into support threads, so a key there is a key disclosed
/// to everyone who ever helps an operator with something else. This file is
/// written by the plugin and surfaced by nothing.
/// </para>
/// <para>
/// <b>Generated on first use, and first use is decided by the store rather than
/// by this file.</b> #30 asks for two things that are the same observation seen
/// twice: a secret generated on first use, and a missing secret failing closed
/// instead of falling back to a default. Both are an absent file. What separates
/// them is whether anything was ever hashed under it. With no records, an absent
/// secret is a new installation and drawing one loses nothing. With records, an
/// absent secret means every stored hash is already unverifiable, and drawing a
/// new one would turn every live invitation into one that can never be redeemed
/// while reporting a healthy start. That is a rotation nobody asked for, so it
/// is refused: see <see cref="HashSecretRefusedException"/>.
/// </para>
/// <para>
/// <b>The records are handed in rather than read here.</b> Whoever is starting
/// the plugin has already read the store under whatever lock it holds, and a
/// second read here would be a second answer to the same question, taken at a
/// different instant. It also keeps this type testable without a store file.
/// </para>
/// <para>
/// <b>Permissions are the store's decision, not a second one.</b> The file is
/// created at <see cref="InvitationStore.CreatedMode"/> on a platform with file
/// modes, the constant is referenced rather than copied, and a mode found wider
/// than that is reported and never repaired, which is the rule
/// <see cref="InvitationStore"/> already settled for a file in the same
/// directory holding the hashes this key verifies. On Windows there is no mode
/// to set or read, and this says which of the two it did rather than reporting a
/// check that read nothing.
/// </para>
/// <para>
/// <b>What this does not do.</b> It does not hash anything.
/// <see cref="Codes.IInvitationCodeHash"/> is that surface and is #29's; this is
/// only the key's life cycle, so the two can be reviewed apart. It does not log,
/// because what this plugin logs is decided in docs/logging.md and the secret is
/// on the list of values that are never written at any level.
/// </para>
/// </remarks>
public sealed class HashSecret
{
    /// <summary>
    /// The name of the file inside the directory this secret was asked for.
    /// </summary>
    public const string FileName = "code-hash.key";

    /// <summary>
    /// The name of the file a write is built up in before it becomes the
    /// secret.
    /// </summary>
    /// <remarks>
    /// Same directory and same reason as the store's: the last step is a move
    /// over the file, and a move only replaces within one filesystem. A secret
    /// half written over its predecessor is a secret nothing can read, which
    /// fails closed but takes every live invitation with it.
    /// </remarks>
    public const string WritingFileName = FileName + ".writing";

    /// <summary>
    /// How many bytes a secret is.
    /// </summary>
    /// <remarks>
    /// Thirty-two, which is the key size of the construction this feeds and the
    /// point past which a longer key stops being the thing an attacker attacks.
    /// It is a fixed length rather than a minimum so that a truncated file is a
    /// refusal rather than a weaker key nobody notices.
    /// </remarks>
    public const int Bytes = 32;

    private HashSecret(string path, ImmutableArray<byte> value, StorePermissions permissions, bool wasCreated)
    {
        Path = path;
        Value = value;
        Permissions = permissions;
        WasCreated = wasCreated;
    }

    /// <summary>
    /// Gets the full path of the file the secret sits in.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the key, as the bytes a keyed hash is built with.
    /// </summary>
    public ImmutableArray<byte> Value { get; }

    /// <summary>
    /// Gets what the file's permissions were found to be at the moment it was
    /// opened or written.
    /// </summary>
    /// <remarks>
    /// Returned rather than acted on, for the reason
    /// <see cref="StorePermissions"/> already gives: a caller that ignores a
    /// finding has to write the line that ignores it.
    /// </remarks>
    public StorePermissions Permissions { get; }

    /// <summary>
    /// Gets a value indicating whether this call drew the secret rather than
    /// reading one that was already there.
    /// </summary>
    /// <remarks>
    /// A caller that wants to tell an operator a key was generated needs to
    /// know, and asking the file afterwards cannot answer it.
    /// </remarks>
    public bool WasCreated { get; }

    /// <summary>
    /// Where the secret sits inside a directory.
    /// </summary>
    /// <param name="directory">The directory the plugin keeps its data in.</param>
    /// <returns>The full path of the secret file.</returns>
    /// <exception cref="ArgumentException">The directory is null or blank.</exception>
    public static string PathIn(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("A secret needs a directory to sit in.", nameof(directory));
        }

        return System.IO.Path.Combine(directory, FileName);
    }

    /// <summary>
    /// Reads the secret, drawing one only where the store has never held
    /// anything.
    /// </summary>
    /// <param name="directory">The directory the plugin keeps its data in.</param>
    /// <param name="records">
    /// The records the caller read out of the store, handed in as they were
    /// read. An empty set is what makes an absent secret a first run rather than
    /// a loss.
    /// </param>
    /// <returns>The secret, with what its permissions were found to be.</returns>
    /// <exception cref="ArgumentException">The directory is null or blank.</exception>
    /// <exception cref="ArgumentNullException">The records are null.</exception>
    /// <exception cref="HashSecretRefusedException">
    /// The file is not there and the store holds records, or the file is there
    /// and is not a secret this build can use. Nothing is written in either
    /// case.
    /// </exception>
    public static HashSecret OpenOrCreate(string directory, IReadOnlyCollection<Invitation> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var path = PathIn(directory);
        if (File.Exists(path))
        {
            return ReadFrom(path);
        }

        if (records.Count > 0)
        {
            throw HashSecretRefusedException.Missing(path, records.Count);
        }

        return Draw(directory, path);
    }

    /// <summary>
    /// Works out what rotating the secret would cost, without rotating it.
    /// </summary>
    /// <param name="directory">The directory the plugin keeps its data in.</param>
    /// <param name="records">The records the caller read out of the store.</param>
    /// <returns>The plan, carrying the count and the sentence to show first.</returns>
    /// <exception cref="ArgumentException">The directory is null or blank.</exception>
    /// <exception cref="ArgumentNullException">The records are null.</exception>
    public static HashSecretRotation PlanRotation(string directory, IReadOnlyCollection<Invitation> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        return new HashSecretRotation(directory, PathIn(directory), records.Count);
    }

    /// <summary>
    /// Rotates the secret, against a plan an operator has already been shown.
    /// </summary>
    /// <param name="plan">The plan from <see cref="PlanRotation"/>.</param>
    /// <param name="records">
    /// The records the caller read out of the store when it confirmed. They are
    /// counted again here, so a rotation confirmed against a store that has
    /// moved is refused rather than paid at a price nobody stated.
    /// </param>
    /// <returns>The new secret.</returns>
    /// <remarks>
    /// <para>
    /// There is no overload that rotates without a plan, which is what makes
    /// "says how many invitations it will invalidate before it does it" a
    /// property of this surface rather than a convention an operator interface
    /// is trusted to follow.
    /// </para>
    /// <para>
    /// The old secret is not kept. A rotation that left the previous key beside
    /// the new one would leave the invitations it was rotated away from
    /// redeemable by anybody who reads the directory, which is the opposite of
    /// what the operator asked for.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">The plan or the records are null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The store holds a different number of records than the plan was made
    /// against. Nothing is written.
    /// </exception>
    public static HashSecret Rotate(HashSecretRotation plan, IReadOnlyCollection<Invitation> records)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count != plan.Invalidates)
        {
            throw HashSecretRotation.CountMoved(plan.Invalidates, records.Count);
        }

        return Draw(plan.Directory, plan.Path);
    }

    private static HashSecret ReadFrom(string path)
    {
        var held = File.ReadAllBytes(path);
        if (held.Length != Bytes)
        {
            throw HashSecretRefusedException.WrongLength(path, held.Length, Bytes);
        }

        return new HashSecret(path, ImmutableArray.Create(held), Inspect(path), wasCreated: false);
    }

    private static HashSecret Draw(string directory, string path)
    {
        CreateDirectory(directory);

        var drawn = RandomNumberGenerator.GetBytes(Bytes);
        try
        {
            WriteBesideAndMove(directory, path, drawn);
            return new HashSecret(path, ImmutableArray.Create(drawn), Inspect(path), wasCreated: true);
        }
        finally
        {
            // The key is in the returned value either way, so this buys one
            // thing and it is worth saying how little: the loose buffer stops
            // being a second copy sitting in whatever the allocator hands out
            // next. It says nothing about the copy the caller now holds, and
            // nothing about a page that reached swap before this ran.
            CryptographicOperations.ZeroMemory(drawn);
        }
    }

    private static void WriteBesideAndMove(string directory, string path, byte[] value)
    {
        var writingPath = System.IO.Path.Combine(directory, WritingFileName);

        // The mode is set as the file is created rather than afterwards, so
        // there is no instant in which the key exists and is readable by
        // everybody. On a platform without file modes the option is not passed
        // at all: asking for one there throws rather than being ignored.
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = InvitationStore.CreatedMode;
        }

        using (var file = new FileStream(writingPath, options))
        {
            file.Write(value, 0, value.Length);

            // Pushed to the device before the move, so a machine losing power
            // just after it does not come back to a directory entry pointing at
            // a key whose bytes never arrived.
            file.Flush(true);
        }

        // A secret that is already there keeps the mode it has, which is the
        // rule the store file follows: a mode an operator widened is reported
        // and never repaired, and one they tightened is theirs. Only the first
        // draw, where there is nothing to carry, leaves the created mode in
        // place.
        if (!OperatingSystem.IsWindows() && File.Exists(path))
        {
            File.SetUnixFileMode(writingPath, File.GetUnixFileMode(path));
        }

        File.Move(writingPath, path, overwrite: true);
    }

    private static void CreateDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
            return;
        }

        // The owner also needs to enter the directory, which read and write
        // alone do not allow, so the execute bit is the difference between this
        // mode and the file's.
        Directory.CreateDirectory(directory, InvitationStore.CreatedMode | UnixFileMode.UserExecute);
    }

    private static StorePermissions Inspect(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return new StorePermissions(
                StorePermissionState.NotCheckedOnThisPlatform,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The mode of {0} was not read. File modes are a POSIX concept and this platform has none; what protects the key here is the access control the data directory already carries, which this plugin does not set and does not understand well enough to judge.",
                    path));
        }

        var found = File.GetUnixFileMode(path);
        if (!InvitationStore.WiderThanCreated(found))
        {
            return new StorePermissions(
                StorePermissionState.AsWritten,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} is {1}, which is no wider than the {2} it is created with.",
                    path,
                    found,
                    InvitationStore.CreatedMode));
        }

        // Reported and not repaired, for the reason the store file gives.
        // Tightening a mode silently changes something an operator may have set
        // deliberately, and refusing to start turns a permissions nit into an
        // outage of the redemption path.
        return new StorePermissions(
            StorePermissionState.WiderThanWritten,
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} is {1}, which is wider than the {2} it is created with. Whoever reads it can verify a code against every stored hash and can compute the hash of any code they like. This is reported and not repaired: the mode is left as it was found.",
                path,
                found,
                InvitationStore.CreatedMode));
    }
}
