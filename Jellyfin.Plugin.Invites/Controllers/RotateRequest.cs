namespace Jellyfin.Plugin.Invites.Controllers;

/// <summary>
/// What a caller sends to the rotation route, which is either nothing or the
/// count they were shown.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two steps are one route because the shape makes the first one
/// unskippable.</b> #30 asks that rotation say what it will do before it does
/// it. A route that acted on an empty body would leave that promise to whoever
/// writes the operator interface; a route whose only way to act is to send back
/// a number it was told first cannot be called blind.
/// </para>
/// <para>
/// The number is the count of records the rotation would make unverifiable, and
/// it is checked against the store again at the moment of the write. A store
/// that gained or lost a record between the two calls refuses rather than
/// rotating against a cost nobody stated, which is
/// <see cref="Storage.HashSecretRotation.CountMoved"/>.
/// </para>
/// </remarks>
public sealed class RotateRequest
{
    /// <summary>
    /// Gets or sets the count the caller is confirming, or <c>null</c> to ask
    /// what a rotation would cost without rotating anything.
    /// </summary>
    /// <remarks>
    /// Omitted, the route reads the store and answers with the plan, and
    /// nothing on disk moves. Given, it must equal what the store holds now.
    /// </remarks>
    public int? Invalidates { get; set; }
}
