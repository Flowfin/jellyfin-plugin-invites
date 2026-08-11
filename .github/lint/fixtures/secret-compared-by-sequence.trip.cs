// Trips secret-compared-by-sequence. A keyed hash is bytes, so the comparison
// somebody reaches for is a sequence comparison rather than string equality,
// and it stops at the first pair of bytes that differ. The time it takes is
// how much of the stored hash the guess got right.
//
// Both operand orders are here because either is what gets written, and the
// rule has to see the secret whichever side of the call it is on.
internal static class SecretSequenceFixture
{
    public static bool Matches(ReadOnlySpan<byte> storedHash, ReadOnlySpan<byte> candidate)
    {
        return storedHash.SequenceEqual(candidate);
    }

    public static bool MatchesTheOtherWayRound(ReadOnlySpan<byte> candidate, ReadOnlySpan<byte> storedHash)
    {
        return candidate.SequenceEqual(storedHash);
    }
}
