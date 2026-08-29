// The same routine with the violation removed. It asks the decision routine
// when retention starts instead of reading the revocation for itself, so the
// revoked case and the expired case are one answer in one place and this caller
// cannot disagree with it.
internal static class SweepFixture
{
    public static bool MayBeRemoved(Invitation invitation, DateTimeOffset now)
    {
        var startsAt = RedemptionDecision.RetentionStartsAt(invitation, now);

        return startsAt is { } from && from <= now;
    }
}
