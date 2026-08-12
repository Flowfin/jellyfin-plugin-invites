using System;

namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// Raised when a store directory is already held by somebody else.
/// </summary>
/// <remarks>
/// It carries what the lock file said and where that file is, because the two
/// together are the whole of what an operator needs: who has it, and what to
/// delete if that answer is a process which is no longer running. A refusal that
/// only said the store was busy would leave them with nothing to act on and a
/// server that will not start.
/// </remarks>
public sealed class StoreInUseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StoreInUseException"/> class.
    /// </summary>
    /// <param name="heldBy">What the lock file says about its holder.</param>
    /// <param name="path">The full path of the lock file.</param>
    public StoreInUseException(string heldBy, string path)
        : base(Sentence(heldBy, path))
    {
        HeldBy = heldBy;
        Path = path;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreInUseException"/> class.
    /// </summary>
    public StoreInUseException()
        : base("The store directory is held by another process.")
    {
        HeldBy = string.Empty;
        Path = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreInUseException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public StoreInUseException(string message)
        : base(message)
    {
        HeldBy = string.Empty;
        Path = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreInUseException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">What went wrong underneath.</param>
    public StoreInUseException(string message, Exception innerException)
        : base(message, innerException)
    {
        HeldBy = string.Empty;
        Path = string.Empty;
    }

    /// <summary>
    /// Gets what the lock file says about who holds it.
    /// </summary>
    public string HeldBy { get; }

    /// <summary>
    /// Gets the full path of the lock file.
    /// </summary>
    public string Path { get; }

    private static string Sentence(string heldBy, string path)
    {
        return "This store directory is already held: "
            + heldBy
            + ". Two servers over one store corrupt it rather than sharing it, so this one is refusing to start on it. If that holder is a process which is no longer running, delete "
            + path
            + " and start again. Nothing removes it on a timer, because a timer would eventually let the two servers meet.";
    }
}
