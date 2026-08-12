namespace Jellyfin.Plugin.Invites.Storage;

/// <summary>
/// Where the store sits, as one thing to ask rather than a static to reach for.
/// </summary>
/// <remarks>
/// <see cref="InvitationStore.For(Plugin)"/> already says the store lives in the
/// directory the server hands this plugin. What this adds is that a caller can
/// be handed the answer instead of reaching through <see cref="Plugin.Instance"/>
/// for it. That matters for exactly one reason: the instance is a static the
/// server sets, so a routine that reads it can only be tested by a test that
/// arranges a global, and two tests arranging the same global cannot run beside
/// each other.
/// </remarks>
public interface IStoreDirectory
{
    /// <summary>
    /// Gets the directory the store sits in, or <c>null</c> where it cannot be
    /// worked out.
    /// </summary>
    string? Path { get; }
}
