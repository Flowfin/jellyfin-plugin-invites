using System;
using System.Globalization;
using System.IO;

namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// One process's claim on a store directory, taken once and held for as long as
/// that process is using it.
/// </summary>
/// <remarks>
/// <para>
/// Some deployments point two server processes at one shared directory and
/// expect it to behave like a database. It does not: the store's atomicity is
/// written for one process, so two redemptions of the same invitation can both
/// pass the decision and the file can be left in a state neither writer
/// intended. docs/disaster-cases.md is where that case is argued and it commits
/// to refusing rather than warning, because a warning about a shared store is
/// one nobody reads until the accounts are already wrong.
/// </para>
/// <para>
/// <b>Taken once, not per write.</b> A check on every write would be a promise
/// about a network filesystem that no network filesystem keeps, and it would
/// cost a round trip on the path a person is waiting on. This is the claim a
/// process makes when it starts using the directory, and it is released when
/// that process is finished with it.
/// </para>
/// <para>
/// <b>Nothing here expires.</b> A lock left behind by a process that was killed
/// stays until a person removes the file, and the refusal says which file and
/// why. A timeout would turn this into a mechanism that eventually lets the two
/// servers meet anyway, which is the failure it exists to prevent, arriving
/// later and with nobody watching.
/// </para>
/// <para>
/// <b>What is claimed, and what is not.</b> The claim is made by creating a file
/// that must not already exist, so two processes racing on one local filesystem
/// produce one winner and one refusal. Whether that is still true on a network
/// filesystem is a property of that filesystem rather than of this code, and it
/// has not been measured. What this detects reliably is the ordinary case, which
/// is a second server started against a directory the first is already using,
/// and that is the case the document describes.
/// </para>
/// </remarks>
public sealed class StoreLock : IDisposable
{
    /// <summary>
    /// The name of the lock file inside the store directory.
    /// </summary>
    /// <remarks>
    /// It sits beside the store rather than in a temporary directory, because
    /// what is being claimed is that directory, and a claim held somewhere else
    /// would not survive the directory being copied to a second machine, which
    /// is the neighbouring case in the same document.
    /// </remarks>
    public const string FileName = "invitations.lock";

    private readonly string _written;
    private bool _released;

    private StoreLock(string path, string heldBy, string written)
    {
        Path = path;
        HeldBy = heldBy;
        _written = written;
    }

    /// <summary>
    /// Gets the full path of the lock file this claim wrote.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the line describing the holder, as it was written into the file.
    /// </summary>
    public string HeldBy { get; }

    /// <summary>
    /// Claims a store directory for one process.
    /// </summary>
    /// <param name="directory">The directory the store sits in. Created when it is not there.</param>
    /// <param name="host">The machine making the claim.</param>
    /// <param name="process">The process making the claim.</param>
    /// <param name="takenAt">
    /// The instant of the claim, read by the caller through
    /// <see cref="Time.IClock"/>. It is an argument for the reason every other
    /// instant in this plugin is: a routine that read the machine clock could
    /// not be driven by a test without waiting for real time to pass.
    /// </param>
    /// <returns>The claim. Release it by disposing it.</returns>
    /// <exception cref="ArgumentException">The directory is null or blank.</exception>
    /// <exception cref="StoreInUseException">Somebody else holds the directory.</exception>
    public static StoreLock Take(string directory, string host, int process, DateTimeOffset takenAt)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("A claim is made on a directory, so there has to be one.", nameof(directory));
        }

        Directory.CreateDirectory(directory);

        var path = System.IO.Path.Combine(directory, FileName);
        var heldBy = string.Format(
            CultureInfo.InvariantCulture,
            "host {0}, process {1}, since {2:yyyy-MM-dd HH:mm:ssK}",
            string.IsNullOrWhiteSpace(host) ? "unnamed" : host,
            process,
            takenAt);

        // Enough for a person to decide, which is what the document asks of it:
        // the holder on one line, and what to do about a holder that is gone on
        // the next. Whoever finds this file is looking at a server that will not
        // start, and the answer has to be in the file rather than in a document
        // they would have to go and find.
        var written = heldBy
            + Environment.NewLine
            + "This file says a server is using this store directory. Two servers over one store corrupt it. Delete this file only when that process is no longer running."
            + Environment.NewLine;

        try
        {
            // CreateNew is the whole mechanism: it fails rather than truncating
            // when the file is there, so the process that finds an existing
            // claim never overwrites it. Create, or an exists check followed by
            // a write, both hand the directory to whoever arrived second.
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(file);
            writer.Write(written);
        }
        catch (IOException) when (File.Exists(path))
        {
            throw new StoreInUseException(Holder(path), path);
        }

        return new StoreLock(path, heldBy, written);
    }

    /// <summary>
    /// Releases the claim, removing the file this claim wrote.
    /// </summary>
    /// <remarks>
    /// A file whose contents are not the ones this claim wrote is left where it
    /// is. That is the case where an operator cleared a stale claim by hand and
    /// another process took the directory afterwards: removing that process's
    /// file on the way out would hand the directory to a third one while the
    /// second is still writing to it.
    /// </remarks>
    public void Dispose()
    {
        if (_released)
        {
            return;
        }

        _released = true;

        try
        {
            if (File.Exists(Path) && string.Equals(File.ReadAllText(Path), _written, StringComparison.Ordinal))
            {
                File.Delete(Path);
            }
        }
        catch (IOException)
        {
            // A claim that cannot be removed is a stale claim, which is a state
            // this design already has an answer for: the operator deletes the
            // file, and the refusal tells them so. Raising from a release path
            // would replace that with a shutdown failure.
        }
    }

    private static string Holder(string path)
    {
        try
        {
            var first = File.ReadLines(path).GetEnumerator();

            return first.MoveNext() && !string.IsNullOrWhiteSpace(first.Current)
                ? first.Current
                : "the holder is not named in the file";
        }
        catch (IOException)
        {
            return "the file could not be read";
        }
    }
}
