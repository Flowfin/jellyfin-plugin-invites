// Trips expiry-or-use-count-judged-outside-the-decision. A route that reads the
// expiry and the use count for itself is a second answer to the one question
// this plugin has to get right, and the two drift the first time either rule
// changes. This one is already a different answer: it honours an invitation at
// the exact instant of its expiry, which docs/expiry-rules.md refuses, and it
// never looks at whether the invitation was revoked.
internal static class RedeemFixture
{
    public static bool MayCreateAnAccount(Invitation invitation, DateTimeOffset now)
    {
        if (invitation.ExpiresAt < now)
        {
            return false;
        }

        return invitation.UsesRemaining > 0;
    }
}
