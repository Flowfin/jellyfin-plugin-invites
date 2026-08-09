// The same link built from the address an operator configured. The request and
// the server host are gone from the signature, because a link minted for
// somebody who is not on the other end of any request cannot be derived from
// one.
internal static class LinkFixture
{
    public static string Build(PluginConfiguration configuration, string code)
    {
        return configuration.PublicBaseUrl.TrimEnd('/') + "/invite/" + code;
    }
}
