// Trips secret-compared-through-a-comparer. Both spellings put the secret in an
// argument rather than in front of the operator, which is where the two rules
// beside this one look for it.
//
// The first is what somebody writes after an analyser tells them to say which
// comparison they meant. It reads as the careful version and is the same
// early-returning comparison as ==, with a culture argument added.
//
// The second reaches the same comparison through a comparer object, which is
// what a lookup keyed by a stored hash is built out of.
internal static class SecretComparerFixture
{
    public static bool Matches(string storedHash, string presented)
    {
        return string.Equals(storedHash, presented, StringComparison.Ordinal);
    }

    public static bool MatchesThroughAComparer(string presented, string storedSecret)
    {
        return StringComparer.Ordinal.Equals(presented, storedSecret);
    }
}
