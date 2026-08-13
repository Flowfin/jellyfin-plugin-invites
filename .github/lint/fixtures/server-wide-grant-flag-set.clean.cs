// The same account, granted the resolved list the template carries. Neither
// server-wide flag is set at all, in either direction: a policy field this
// plugin declines to decide is named in the template's ServerDefaultsLeftAlone
// list, so a reader can tell a considered omission from a forgotten field, and
// writing the flag false would be this plugin deciding it after all.
//
// The last method reads one of the flags and the rule may not refuse it.
// Reading what an account already has is how a refusal to widen an existing
// account gets written, and a rule reddening on it would redden on the guard
// rather than on the grant.
internal static class ServerWideFlagFixture
{
    public static void Grant(UserPolicy policy, AccountTemplate template)
    {
        policy.EnabledFolders = template.Libraries.ToArray();
    }

    public static bool AlreadySeesEveryLibrary(UserPolicy policy)
    {
        return policy.EnableAllFolders;
    }
}
