// The same routine with the violation removed. It asks the decision routine
// instead of judging for itself, so the expiry direction, the revocation and the
// count are one answer in one place, and this caller cannot disagree with it.
internal static class RedeemFixture
{
    public static bool MayCreateAnAccount(Invitation invitation, DateTimeOffset now)
    {
        var verdict = RedemptionDecision.Decide(presented, codeHash, new[] { invitation }, now);

        return verdict.MayCreateAnAccount;
    }
}
