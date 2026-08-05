// The same comparison in constant time. Nothing else changes.
internal static class SecretEqualityFixture
{
    public static bool Matches(byte[] stored, byte[] candidate)
    {
        return CryptographicOperations.FixedTimeEquals(stored, candidate);
    }
}
