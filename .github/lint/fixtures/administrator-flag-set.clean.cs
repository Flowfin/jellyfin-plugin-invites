// The same routine applying the same template, with the administrator flag not
// written at all. There is no arm reading MayManage into it, because a template
// asking for an administrator is refused before this routine is reached and the
// refusal is the control; this rule is what keeps the spelling out of the tree
// so the refusal cannot be quietly walked around later.
//
// The second method reads the flag and the rule may not refuse it. Refusing to
// touch an account that already exists is written by asking what that account
// already has, so a rule reddening on a read would redden on the guard rather
// than on the grant.
internal static class AdministratorFlagFixture
{
    public static void Apply(UserPolicy policy, AccountTemplate template)
    {
        policy.EnabledFolders = template.Libraries.ToArray();
    }

    public static bool IsAlreadyAnAdministrator(UserPolicy policy)
    {
        return policy.IsAdministrator;
    }
}
