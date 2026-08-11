// The same two grants, asked for through the routine that applies a template.
// Nothing else changes.
//
// The last method reads a policy field and compares it, which this rule may not
// refuse: reading what an account already has is how the refusal to widen an
// existing account is written, and a rule reddening on it would redden on the
// guard rather than on the grant.
internal static class PolicyFieldFixture
{
    public static void Grant(User user, IAccountTemplates templates)
    {
        templates.Apply(user, "household");
    }

    public static bool AlreadyAnAdministrator(User user)
    {
        return user.Policy.IsAdministrator;
    }
}
