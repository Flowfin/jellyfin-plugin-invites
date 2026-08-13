// Trips administrator-flag-set. Both #69 rules exempt the routine that applies
// an account template, because that routine is the one place a policy is
// allowed to be written. Inside that exemption the ceiling has nothing holding
// it: a template whose MayManage says yes becomes an administrator on the first
// line below, and every other rule in this file stays green while it happens.
//
// The second line is the grant this plugin does make, and it is here so the
// rule is seen to leave it alone: a resolved library list is not the flag.
//
// The second method is the same violation written by somebody being careful.
// Setting the flag false is this plugin deciding a field it has decided not to
// decide, and the template carries ServerDefaultsLeftAlone so that such a field
// is named as left alone rather than written.
internal static class AdministratorFlagFixture
{
    public static void Apply(UserPolicy policy, AccountTemplate template)
    {
        policy.IsAdministrator = template.MayManage;
        policy.EnabledFolders = template.Libraries.ToArray();
    }

    public static void BeExplicitAboutIt(UserPolicy policy)
    {
        policy.IsAdministrator = false;
    }
}
