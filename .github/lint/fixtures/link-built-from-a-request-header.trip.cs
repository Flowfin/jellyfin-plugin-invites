// Trips link-built-from-a-request-header. The Host header is chosen by whoever
// sent the request, so an invitation link built from it can be pointed at a
// server the operator does not own.
internal static class LinkFixture
{
    public static string Build(HttpRequest request, string code)
    {
        return "https://" + request.Headers["Host"] + "/invite/" + code;
    }
}
