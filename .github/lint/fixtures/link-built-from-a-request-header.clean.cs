// The same link built from configuration the operator set. Nothing else
// changes.
internal static class LinkFixture
{
    public static string Build(PluginConfiguration configuration, string code)
    {
        return configuration.PublicBaseUrl.TrimEnd('/') + "/invite/" + code;
    }
}
