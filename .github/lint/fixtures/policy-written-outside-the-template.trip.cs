// Trips policy-written-outside-the-template. A policy written here is a grant
// that never passed the one routine where grants are decided and reviewed.
internal static class PolicyFixture
{
    public static void Grant(User user, IUserManager users)
    {
        user.Policy.EnableAllFolders = true;
        users.UpdatePolicy(user.Id, user.Policy);
    }
}
