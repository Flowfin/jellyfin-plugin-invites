// The same two comparisons in constant time. Nothing else changes.
//
// The last method is a sequence comparison this rule may not refuse: a list of
// account identifiers is not a secret, and a rule that reddened on it would be
// one people route around by renaming the variable.
internal static class SecretSequenceFixture
{
    public static bool Matches(ReadOnlySpan<byte> storedHash, ReadOnlySpan<byte> candidate)
    {
        return CryptographicOperations.FixedTimeEquals(storedHash, candidate);
    }

    public static bool MatchesTheOtherWayRound(ReadOnlySpan<byte> candidate, ReadOnlySpan<byte> storedHash)
    {
        return CryptographicOperations.FixedTimeEquals(candidate, storedHash);
    }

    public static bool SameAccounts(ReadOnlySpan<Guid> produced, ReadOnlySpan<Guid> other)
    {
        return produced.SequenceEqual(other);
    }
}
