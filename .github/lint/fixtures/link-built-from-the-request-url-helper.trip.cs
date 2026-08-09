// Trips link-built-from-the-request-url-helper. GetSmartApiUrl derives its
// answer from what the caller presented, so this reads as asking the server and
// behaves as reading the Host header. A minting request carrying a forged host
// still produces a link pointing somewhere the operator does not own.
internal static class LinkFixture
{
    public static string Build(IServerApplicationHost host, HttpRequest request, string code)
    {
        return host.GetSmartApiUrl(request).TrimEnd('/') + "/invite/" + code;
    }
}
