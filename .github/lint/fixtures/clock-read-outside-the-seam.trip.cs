// Trips clock-read-outside-the-seam. An expiry decided against the machine
// clock can only be tested by a test that waits for the boundary to arrive, so
// in practice the boundary is never tested at all.
internal static class ExpiryFixture
{
    public static bool HasExpired(Invitation invitation)
    {
        return invitation.ExpiresAt <= DateTimeOffset.UtcNow;
    }
}
