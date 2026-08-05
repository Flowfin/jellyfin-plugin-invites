// The same routine with the violation removed. Nothing else changes.
internal static class WeakRandomFixture
{
    public static string MintCode()
    {
        var buffer = new byte[16];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer);
    }
}
