// Trips secret-compared-with-equality. String equality returns as soon as two
// bytes differ, so the time it takes tells an attacker how much of the stored
// hash their guess got right.
internal static class SecretEqualityFixture
{
    public static bool Matches(string storedHash, string candidateHash)
    {
        return storedHash == candidateHash;
    }
}
