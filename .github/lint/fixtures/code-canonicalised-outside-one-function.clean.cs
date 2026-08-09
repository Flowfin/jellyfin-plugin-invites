// The same routine with the violation removed. The normalisation is asked for
// rather than repeated, so hyphens, case and the confusable characters are
// decided in one place and the refusal of anything that is not a code comes
// back with it.
internal static class RedemptionRouteFixture
{
    public static string? LookupKey(string presented)
    {
        return InvitationCode.Canonicalise(presented);
    }
}
