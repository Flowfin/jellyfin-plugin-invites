// The same effect, asked for through the routine that applies a template.
// Nothing else changes.
internal static class PolicyFixture
{
    public static void Grant(User user, IAccountTemplates templates)
    {
        templates.Apply(user, "guest");
    }
}
