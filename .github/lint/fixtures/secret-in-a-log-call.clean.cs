// The same log line about the same event, naming the record rather than the
// secret. Nothing else changes.
internal static class LogFixture
{
    public static void Record(ILogger logger, Guid invitationId)
    {
        logger.LogInformation("Redeeming invitation {Id}", invitationId);
    }
}
