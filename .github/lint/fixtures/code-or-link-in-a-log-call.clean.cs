// The same three lines about the same three events, naming the invitation
// identifier rather than the value that carries the code. The signatures lose
// the strings instead of keeping them unused, so this is the shape taken out
// and not the spelling avoided. Nothing else changes.
internal static class LogFixture
{
    public static void Redeeming(ILogger logger, Guid invitationId)
    {
        logger.LogInformation("Redeeming invitation {Id}", invitationId);
    }

    public static void Minted(ILogger logger, Guid invitationId)
    {
        logger.LogDebug("Minted invitation {Id}", invitationId);
        logger.LogDebug("Handing out invitation {Id}", invitationId);
    }
}
