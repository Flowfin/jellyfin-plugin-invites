// Trips server-wide-grant-flag-set. The server can grant every library, and
// every channel, with one flag each. A flag granted today widens on its own the
// next time an operator adds a library, and the account it widens belongs to
// somebody who was invited months earlier by an operator who may since have
// left.
//
// The third line is the grant this plugin makes instead, and it is here so the
// rule is seen to leave it alone: EnabledFolders is the resolved list and is
// not the flag.
internal static class ServerWideFlagFixture
{
    public static void Grant(UserPolicy policy, AccountTemplate template)
    {
        policy.EnableAllFolders = true;
        policy.EnableAllChannels = true;
        policy.EnabledFolders = template.Libraries.ToArray();
    }
}
