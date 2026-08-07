// Trips weak-random. An invitation code drawn from System.Random is predictable
// from a few observed codes, which makes every unredeemed invitation guessable.
internal static class WeakRandomFixture
{
    public static string MintCode()
    {
        var rng = new Random();
        return rng.Next().ToString();
    }
}
