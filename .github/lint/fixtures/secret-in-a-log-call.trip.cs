// Trips secret-in-a-log-call. The code is the credential, so a log line holding
// it is that credential written to disk in clear, on a path an operator ships
// to somebody else when they ask for help.
internal static class LogFixture
{
    public static void Record(ILogger logger, string invitationCode)
    {
        logger.LogInformation("Redeeming {Code}", invitationCode);
    }
}
