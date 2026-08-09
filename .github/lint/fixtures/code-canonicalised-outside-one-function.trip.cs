// Trips code-canonicalised-outside-one-function. The route is forgiving about
// what somebody typed and does the forgiving itself, so there are now two
// answers to which codes are equal: this one and the one the store was keyed
// with. They agree until one of them learns about hyphens.
internal static class RedemptionRouteFixture
{
    public static string LookupKey(string code)
    {
        return code.Trim().ToUpperInvariant();
    }
}
