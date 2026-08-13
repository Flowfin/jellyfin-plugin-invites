// The same two comparisons in constant time. Nothing else changes.
//
// The last two methods are what this rule may not refuse. A template label
// compared with an ordinal comparison is not a secret, and a set of labels built
// on StringComparer.Ordinal is the ordinary way to hold one: a rule that
// reddened on either would be a rule people route around by renaming the
// variable. The set is here in particular because HashSet carries the word this
// rule's vocabulary is built on, and it is the shape the tree already has.
internal static class SecretComparerFixture
{
    public static bool Matches(string storedHash, string presented)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(storedHash),
            Encoding.UTF8.GetBytes(presented));
    }

    public static bool MatchesThroughAComparer(string presented, string storedSecret)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented),
            Encoding.UTF8.GetBytes(storedSecret));
    }

    public static bool SameLabel(string label, string other)
    {
        return string.Equals(label, other, StringComparison.Ordinal);
    }

    public static HashSet<string> LabelsLeftAlone()
    {
        return new HashSet<string>(StringComparer.Ordinal);
    }
}
