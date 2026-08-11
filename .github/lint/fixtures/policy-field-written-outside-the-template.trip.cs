// Trips policy-field-written-outside-the-template. One field of a policy set
// outside the routine that applies an account template is a grant that never
// passed the place grants are decided, and the first line is the grant this
// plugin exists to make impossible: an invitation that mints an administrator.
//
// The whole-policy spellings are the neighbouring rule's. This one is about the
// single field, which is what somebody writes when they want to change one
// thing and leave the rest alone.
internal static class PolicyFieldFixture
{
    public static void Grant(User user)
    {
        user.Policy.IsAdministrator = true;
        user.Policy.EnableAllFolders = true;
    }
}
