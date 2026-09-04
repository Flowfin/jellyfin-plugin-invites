namespace Jellyfin.Plugin.Invites.Controllers;

/// <summary>
/// The answers a post carried, after the server has judged them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two members where the form has three, and the third is why this type
/// exists.</b> The confirmation is an answer the server reads and nothing
/// downstream may see: it is a check that the person typed what they meant, and
/// a routine handed both copies is a routine that could pick the wrong one. What
/// leaves this judgement is the password once, under one name.
/// </para>
/// <para>
/// <b>Neither member is nullable, and that is the whole reason the judgement
/// hands back a type rather than a boolean.</b> A boolean leaves the caller
/// holding a submission whose members the compiler still reads as nullable, so
/// the caller writes a null-forgiving operator and the guarantee moves from the
/// type system into a claim about what some other function checked. This way the
/// check and the guarantee are the same thing, and a judgement that stopped
/// requiring a username could not compile.
/// </para>
/// <para>
/// <b>Nothing about the account is here.</b> No member of this type reaches the
/// template a redemption applies, which is #75's rule that nothing the redeeming
/// party sends may influence what the account gets. The grant comes off the
/// invitation record, and the only way a posted value could steer it is a member
/// added here that the creation reads.
/// </para>
/// </remarks>
/// <param name="Username">The name the person will sign in with.</param>
/// <param name="Password">
/// The password the person chose. It is handed to the server's own credential
/// routine and this plugin keeps no copy of it.
/// </param>
public sealed record AcceptedAnswers(string Username, string Password);
