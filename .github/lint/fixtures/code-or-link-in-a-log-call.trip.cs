// Trips code-or-link-in-a-log-call. All three lines are the ordinary spelling
// rather than an invented one. A redemption route names its parameter after
// what the route is about, and a mint route names the address it just built
// after what it is. None of the three names holds the word secret, none of them
// reads as a credential, and all three values are one.
internal static class LogFixture
{
    public static void Redeeming(ILogger logger, string code)
    {
        logger.LogInformation("Redeeming {Code}", code);
    }

    public static void Minted(ILogger logger, string url, string link)
    {
        logger.LogDebug("Minted {Url}", url);
        logger.LogDebug("Handing out {Link}", link);
    }
}
