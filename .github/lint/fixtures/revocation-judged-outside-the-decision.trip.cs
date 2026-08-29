// Trips revocation-judged-outside-the-decision. A maintenance routine that asks
// for itself whether an invitation was revoked is a second answer to the
// question RedemptionDecision exists to answer alone. This one is already a
// different answer: it treats a revoked invitation as one whose retention runs
// from its expiry, so a link revoked a month before it would have expired is
// kept a month longer than docs/personal-data.md says, and it never looks at
// the use count at all.
internal static class SweepFixture
{
    public static bool MayBeRemoved(Invitation invitation, DateTimeOffset now)
    {
        if (invitation.IsRevoked)
        {
            return invitation.ExpiresAt <= now;
        }

        return invitation.RevokedAt is null && invitation.ExpiresAt <= now;
    }
}
