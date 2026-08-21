using System;
using Jellyfin.Plugin.Invites.Storage;

namespace Jellyfin.Plugin.Invites.Controllers;

/// <summary>
/// What the rotation route answers with, in both of its steps.
/// </summary>
/// <remarks>
/// <para>
/// One shape for the plan and for what was done, because the operator reads the
/// same three facts either way and a second type would let the two sentences
/// drift. <see cref="Rotated"/> is what separates them, and it is the field an
/// interface branches on.
/// </para>
/// <para>
/// <b>Nothing here carries the secret.</b> There is no field it could be
/// expressed in, which is the same construction <see cref="InvitationView"/>
/// uses against the code: a response that returned the key would put it in the
/// operator's browser, in its cache and in whatever the dashboard logs.
/// </para>
/// </remarks>
public sealed class RotationView
{
    private RotationView(HashSecretRotation plan, bool rotated)
    {
        Invalidates = plan.Invalidates;
        Detail = plan.Detail;
        Rotated = rotated;
    }

    /// <summary>
    /// Gets how many stored records the rotation makes unverifiable.
    /// </summary>
    /// <remarks>
    /// It is every record the store holds rather than the ones that could still
    /// be redeemed, and <see cref="Detail"/> says so. Narrowing it would mean a
    /// second routine deciding whether an invitation may be honoured.
    /// </remarks>
    public int Invalidates { get; }

    /// <summary>
    /// Gets the sentence to put in front of an operator.
    /// </summary>
    /// <remarks>
    /// The routine's own, rather than one written again here. A confirmation
    /// prompt and a receipt that disagree about what happened are worse than
    /// either alone.
    /// </remarks>
    public string Detail { get; }

    /// <summary>
    /// Gets a value indicating whether the secret was rotated.
    /// </summary>
    /// <remarks>
    /// <c>false</c> is the plan: the store was read, nothing was written, and
    /// what is above is what confirming would cost.
    /// </remarks>
    public bool Rotated { get; }

    /// <summary>
    /// Reads a plan into the response shape, without anything having been done.
    /// </summary>
    /// <param name="plan">What the rotation would cost.</param>
    /// <returns>The response.</returns>
    /// <exception cref="ArgumentNullException">The plan is null.</exception>
    public static RotationView Planned(HashSecretRotation plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new RotationView(plan, rotated: false);
    }

    /// <summary>
    /// Reads a plan that was carried out into the response shape.
    /// </summary>
    /// <param name="plan">What the rotation cost.</param>
    /// <returns>The response.</returns>
    /// <exception cref="ArgumentNullException">The plan is null.</exception>
    public static RotationView Done(HashSecretRotation plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new RotationView(plan, rotated: true);
    }
}
