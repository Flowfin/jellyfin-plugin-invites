// The same routine with the violation removed. One word moves: the instant
// comes from the injected clock instead of the machine, so a test crosses the
// boundary by moving the clock rather than by waiting.
internal static class ExpiryFixture
{
    public static bool HasExpired(Invitation invitation, IClock clock)
    {
        return invitation.ExpiresAt <= clock.UtcNow;
    }
}
