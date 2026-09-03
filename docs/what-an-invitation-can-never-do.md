# What an invitation can never do

Seven sentences. Each one is a property somebody could break by accident in a
change that looks unrelated, which is why they are written down together rather
than left implied by the code that happens to hold them today.

A sentence here is worth exactly what refuses it. So every line below names the
thing that would go red, and where nothing would, it says so and names the issue
that lands the source. A line whose defence is "nobody would do that" is a line
this page would be better off without.

## How to read a line

Each line carries one of four states, and they are different claims:

- **Refused by a test.** A named test fails when the line is broken in the
  source. The test is named here, and breaking the line is how the naming was
  checked rather than a claim about what the test looks like it covers.
- **Refused by a spelling.** A rule in `.github/lint/invariants.sh` matches the
  shape of the mistake. That is narrower than the sentence: it catches the
  spelling somebody actually writes and not the same thing written another way,
  which the lint file says about itself rule by rule.
- **Refused at the capability.** A named test refuses the plugin gaining the
  ability the line is about, rather than refusing the behaviour. It is weaker
  than the first state and stronger than the last, and the difference matters in
  one direction: it says the line cannot be broken today and says nothing about
  the day the ability arrives. The test reds on that day, which is what puts the
  line's own test in the change that lands the routine rather than after it.
- **Not refused.** Nothing in the tree would notice. The line still belongs
  here, because the code it is about is being written against it, and the issue
  that will land that code is named.

The tests are named rather than pasted, and the names are what the suite prints:

    dotnet test --configuration Release --no-build

## The seven

### An invitation can never create an administrator

Refused by a test, in the routine that creates the account.
`ATemplateThatWouldManageTheServerIsRefusedBeforeAnythingIsCreated` puts a
template asking to manage the server through `AccountCreation.CreateAsync` and
requires the refusal AND that nothing was asked of the server. #62 asks for the
refusal inside the routine rather than as validation on the way in, so a later
caller that skips its own checks still meets it, and asserting the empty call
trail is what separates a refusal from a refusal after an account exists.

The state on this line moved from `Not refused by a test` to this one because
the routine it needed arrived, and the sentence below is what that state used to
rest on.

THIS PARAGRAPH SAID NOTHING IN THIS PLUGIN CREATES AN ACCOUNT, AND SOMETHING
DOES. #398 landed the routine that turns an honoured redemption into an account,
so the reason given here for there being no execution path to reach has gone:

    git grep -n 'public static async Task<Guid> CreateAsync' -- Jellyfin.Plugin.Invites/Accounts/AccountCreation.cs
    Jellyfin.Plugin.Invites/Accounts/AccountCreation.cs:95:    public static async Task<Guid> CreateAsync(

That number was 72 when this paragraph was written under #398 and the routine is
the same routine: what moved it is the remarks #62 added above it, in the change
that put the refusal at the top of this section. The line-reference check refused
the old number before this branch was pushed rather than a reader finding it.

What is still missing is the caller. Nothing in the plugin reaches that routine
outside the suite, which is #399's half of the split #398's body records, so the
line is untested for the same reason as before written the other way round: the
path exists and nothing walks it from a request.

THIS PARAGRAPH ALSO SAID NOTHING HERE WRITES A USER POLICY, AND SOMETHING DOES.
The routine that applies an account template landed under #69 and writes fifteen
fields of one:

    git grep -c '^        policy\.' -- Jellyfin.Plugin.Invites/Accounts/AccountTemplateApplication.cs
    Jellyfin.Plugin.Invites/Accounts/AccountTemplateApplication.cs:15

`IsAdministrator` is not among the fifteen, and that is refused over the whole
plugin rather than chosen by the routine: `administrator-flag-set` carries no
exemption at all and the routine's own file is not outside it. So what is left of
this paragraph's reason is the first half of what it claimed. There is a policy
to write and there is still no account for it to be written to.

THE CAPABILITY STATE THIS LINE CARRIED IS GONE, AND IT IS THE ONE STATE THIS
PAGE WARNS EXPIRES. It said creating an administrator means creating an account
and the plugin cannot create one. It can, since #398, so the argument that the
line cannot be broken without something first going red no longer rests on the
plugin being unable to make an account.

What is left of it is narrower and is worth stating exactly, because the two
assertions it named are both still there.
`TheSeamOverTheServersAccountsDeclaresNothingThatWrites` still refuses a member
on the READ seam that takes an argument or hands nothing back, and
`OnlyTheDeclaredSeamsCanBeHandedTheServersUserManager` still refuses a type
beyond the two declared seams being handed the user manager at all. Beside them,
`TheWriteSeamDeclaresOnlyTheThreeActsARedemptionNeeds` holds the write seam to
three acts and `TheWriteSeamReachesNoMemberBeyondTheFiveItNeeds` holds it to
five members of the server's own interface. None of the four is about
administration. What they buy this line is that the account-creating capability
cannot grow a fifth shape without something going red, which is a fact about the
surface and not about what is written to a policy.

THE TEST THIS PARAGRAPH SAID THE LINE OWED IS AT THE TOP OF THIS SECTION, AND IT
DID NOT WAIT FOR THE POST. It said the day the redemption post lands is the day
this line owes a test of its own. That was wrong about which arrival mattered:
what a refusal needs is a routine to live in, not a caller to reach it, and #62
put it in the routine the moment there was one. The post is still #399's and the
line is refused before it.

Beside it stand the spelling below and the routine that applies a template,
which writes fifteen policy fields and no administrator flag.

Refused by a spelling, in part.
`policy-field-written-outside-the-template` matches a policy field assigned
outside the routine that applies an account template, and the failure it prints
names `IsAdministrator` as the field this plugin may never set. It sees one
statement, so the same write through a local is invisible to it, which the lint
file records against #69.

The template carries `MayManage` as a stated grant rather than an absence, so
the value an invitation is worth says in as many words whether it manages
anything. Whether a grant may say yes is #62, and it is refused there rather
than on the template, so that one rule does not live in two places.

### An invitation can never modify an account that already exists

Not refused by a test.

THIS LINE'S OWN REASON SAID NO ACCOUNT IS CREATED AND NONE IS WRITTEN, AND HALF
OF THAT IS NOW WRONG. An account is created and one is written to, under #398.
What holds this line is the narrower half: everything the write seam does is
addressed to the account the redemption is creating, and the identifier it is
addressed by comes out of the creation rather than out of a lookup, so there is
no shape in which the routine reaches an account that was already there.

This paragraph said the plugin named the server's user operations nowhere, and
that stopped being true. The command it offered as evidence returned two lines
when it was written, returned five after #91, and returns nine:

    git grep -n 'IUserManager' -- '*.cs' ':!.github'
    Jellyfin.Plugin.Invites.Tests/AccountsAreNeverWrittenTests.cs:141:            foreach (var property in typeof(IUserManager).GetProperties().Where(candidate => candidate.Name == name))
    Jellyfin.Plugin.Invites.Tests/AccountsAreNeverWrittenTests.cs:148:            foreach (var method in typeof(IUserManager).GetMethods().Where(candidate => candidate.Name == name && !candidate.IsSpecialName))
    Jellyfin.Plugin.Invites.Tests/AccountsAreNeverWrittenTests.cs:177:            .Where(type => Members(type).Any(parameter => typeof(IUserManager).IsAssignableFrom(parameter)))
    Jellyfin.Plugin.Invites.Tests/AccountsAreNeverWrittenTests.cs:221:        var named = typeof(IUserManager)
    Jellyfin.Plugin.Invites.Tests/RevocationTests.cs:132:    /// something after the next edit. Add an <c>IUserManager</c> parameter and
    Jellyfin.Plugin.Invites/Accounts/ServerAccountWrites.cs:41:/// implement <see cref="IUserManager"/>, and <c>ChangePassword</c> is a member
    Jellyfin.Plugin.Invites/Accounts/ServerAccountWrites.cs:75:    private readonly IUserManager _users;
    Jellyfin.Plugin.Invites/Accounts/ServerAccountWrites.cs:81:    public ServerAccountWrites(IUserManager users)
    Jellyfin.Plugin.Invites/Accounts/ServerAccounts.cs:56:    public ServerAccounts(IUserManager users)

Five of the nine are in the suite and four are the plugin, and the direction the
count moved in has changed meaning. The three that arrived under #91 were the
refusal naming the type in order to refuse it, so that move was the capability
being held. Three of the four that arrived under #398 are the write seam itself,
and that move is the plugin reaching further.

The last line is the plugin. `ServerAccounts` asks the user manager for the
identifier of every account and for nothing else, which is what
`IServerAccounts` declares and what the load-time comparison landed under #46
reads. It is not a type waiting for a caller: it is registered, and the hosted
service that reads it runs when the server starts.

    git grep -n 'IServerAccounts, ServerAccounts\|AddHostedService<LoadOnStart>' -- Jellyfin.Plugin.Invites/Startup/PluginServiceRegistrator.cs
    Jellyfin.Plugin.Invites/Startup/PluginServiceRegistrator.cs:50:        serviceCollection.AddSingleton<IServerAccounts, ServerAccounts>();
    Jellyfin.Plugin.Invites/Startup/PluginServiceRegistrator.cs:59:        serviceCollection.AddHostedService<LoadOnStart>();

Both numbers moved twice in one night and this paste is the second re-run: first
by two, when the retention sweep from #59 was registered and its namespace
imported above, and then by one and by two again when the redemption limiter from
#31 was. The registrations themselves are the same two lines and nothing this
page says about them has changed. Neither move was noticed by a reader; the
line-reference check refused both before the branch carrying them was pushed.

Both numbers moved again with this revision, by two and by six, and the sentence
they support did not. Four registrations landed between them, the server-line
comparison from #97 and the refusal it attaches to this plugin's controllers.
Neither line is a different line of code; each is the same call further down the
same method.

And both moved once more, by one each, when the seam over the configured
account templates from #86 was registered above them. The two calls are
unchanged and so is what this page says about them.

That narrows the reason this line is undefended without moving it. An identifier
is not an account: the READ seam hands back no user object to modify and has no
member that writes. What is gone is the argument that no path in the plugin
could reach an account at all, and what is left is the smaller one, that the
path which reads reads identifiers.

Refused at the capability, since #91, AND THE CAPABILITY IS WIDER THAN IT WAS.
That the read seam reads and never writes was an observation, and
`AccountsAreNeverWrittenTests` makes it a refusal: a member on that interface
that takes an argument or hands nothing back, a name it reaches by reflection
that is not a read on the server's own interface, and a type beyond the two
declared seams able to take the user manager are each refused. The middle one is
the one a reader should notice, because both seams bind late in part and a write
behind a looked-up name is invisible to the compiler and to a lint that reads
source text.

The second seam is what #398 added, and the same file bounds it in two
directions. `TheWriteSeamDeclaresOnlyTheThreeActsARedemptionNeeds` holds it to
creating an account, setting that account's credential and applying a template
to it, and `TheWriteSeamReachesNoMemberBeyondTheFiveItNeeds` reads the seam's
own source against every member the server's interface carries and refuses a
sixth. So there is nothing on either seam that removes, disables, renames or
re-authenticates an account, and the reason this line still stands is that every
one of the three acts is addressed to the account the redemption just made.

The fourth line is prose inside a test file, and the test it belongs to is the
nearest thing in the tree to a defence of this sentence.
`NothingHereCanBeHandedAnAccount` holds the parameters of the revocation routine
to a fixed set, so a later change that starts passing an account into it goes
red before anything is written with one. It covers that one routine, and this
line is about every routine.

The line has two halves and they need different mechanisms. That the routine
does not modify an account it was handed is a test, and this sentence named #69
for the routine it waits on. That routine exists and it is never handed an
account: what it takes is a policy and a template, which is why the test this
half wants is still absent.

THIS SENTENCE SAID THAT HALF WAITS ON SOMETHING THAT CREATES THE ACCOUNT THE
POLICY WOULD BELONG TO, AND THAT HAS ARRIVED. #398 landed it, and what the half
waits on now is the recorded-call contract in #103 and the post in #399: the
routine is reached from the suite and from nowhere else, so a test asserting
that a redemption modifies no existing account has no request to make.

The second half, that no other
path in the plugin ever reaches the server's update method, said here that it is
not a test at all and is a greppable invariant.
`OnlyTheDeclaredSeamsCanBeHandedTheServersUserManager` is that half as a test, and it
is stronger than the grep it replaces: it reads the compiled assembly rather
than source text, so a spelling the lint's pattern does not match is still a
type able to take the user manager and is still refused.
`policy-written-outside-the-template` stays where it is and covers the
neighbouring shape, a policy written outside the routine that applies a
template.

### An invitation can never grant a library, a permission or a quota that its template does not name

Refused by a test, on the template. `EveryGrantTheIssueNamesIsAPropertyAndThereAreNoOthers`
holds the grant to exactly the fields the plan named.
`NoCeilingIsAStatedGrantRatherThanAnAbsentOne` refuses a field that means
whatever the server does by default, which is a grant that changes under an
upgrade. `AGrantOfNoLibraryIsKeptAsOne` keeps the empty list as a decision
rather than as a missing one. `EveryQuotaOnTheTemplateHasARowAndEveryRowIsAQuota`
ties every quota the template carries to a field of the server's policy, read
off the assembly rather than off a document.

Refused by a test on the policy as well, since #69. THIS PARAGRAPH SAID THE LINE
IS DEFENDED ON THE VALUE AND NOT ON THE RESULT, and the middle of those two
arrived. `EveryFieldIsTheTemplatesGrantOrTheValueTheServerHadSet` asserts every
field of the server's user policy after a template is applied: the fifteen the
template grants carry what it granted, and the twenty-nine this plugin writes
nothing to carry exactly the value they had. It runs twice with the markers
reversed, so a routine writing a constant into a field it should leave alone is
caught by one of the two runs rather than by neither.
`MovingOneGrantMovesExactlyTheFieldsItIsWrittenTo` moves one grant at a time and
asserts which fields moved with it, which is what refuses a grant handed to the
wrong field; a swap of two of them passes the run above and reddens this one.

Not refused on the account, AND ON THE ACCOUNT THE LIBRARY HALF OF THIS LINE IS
FALSE ON EVERY SERVER THE PLUGIN LOADS ON. THIS PARAGRAPH SAID THERE IS NO
CREATION PATH AND THAT THE WRITE SIDE OF THE SEAM IS #103'S. Both landed under
#398, and the routine that applies a template writes through them:

    git grep -n 'GetUserDto(created)?.Policy\|ApplyTo(granted, template)\|UpdatePolicyAsync(account, granted)' -- Jellyfin.Plugin.Invites/Accounts/ServerAccountWrites.cs
    Jellyfin.Plugin.Invites/Accounts/ServerAccountWrites.cs:162:        var granted = _users.GetUserDto(created)?.Policy
    Jellyfin.Plugin.Invites/Accounts/ServerAccountWrites.cs:165:        AccountTemplateApplication.ApplyTo(granted, template);
    Jellyfin.Plugin.Invites/Accounts/ServerAccountWrites.cs:167:        await _users.UpdatePolicyAsync(account, granted).ConfigureAwait(false);

The policy at 162 is the one the server made, and the server makes every one
with the all-libraries flag on, at both ends of the supported line. That is read
off the server's own source at the two tags this build resolves, because the
server's user manager is in no package this tree restores:

    gh api "repos/jellyfin/jellyfin/contents/Jellyfin.Data/UserEntityExtensions.cs?ref=v10.11.0" --jq .content | base64 -d | grep -n 'EnableAllFolders'
    177:        entity.Permissions.Add(new Permission(PermissionKind.EnableAllFolders, true));
    gh api "repos/jellyfin/jellyfin/contents/Jellyfin.Data/UserEntityExtensions.cs?ref=v10.11.11" --jq .content | base64 -d | grep -n 'EnableAllFolders'
    177:        entity.Permissions.Add(new Permission(PermissionKind.EnableAllFolders, true));

`server-wide-grant-flag-set` refuses this plugin writing that field in either
direction and exempts no file, so the routine hands the policy back with the
flag as it arrived and the server writes it onto the account. An account this
plugin creates therefore sees every library whatever `Libraries` names, and
every test above is green over a policy whose flag a test set rather than one
the server made. Whether the way out is a named exemption for the one routine
that applies a template, or a disclosure that the list does not bound what an
account sees, is #63's, where the reading is written in full, and this page
takes neither. Nothing reaches the routine from a request yet, which is #399,
so no account has been created this way; the sentence above is about what the
first one will carry.

### An invitation can never be redeemed after expiry, after revocation, or with no uses left

Refused by a test, three times, and this is the line the plugin actually holds
today.

`TheExpiryBoundaryIsExclusive` decides the instant itself, which is the case an
implementation gets right by accident and a later change gets wrong in silence.
`ARevokedInvitationIsRefused` and `AnInvitationWithNoUsesLeftIsSpent` hold the
other two. `RedemptionDecisionTableTests` carries every reachable combination of
the four dimensions as a row, so the three refusals are asserted against each
other rather than one at a time, and `RedemptionFuzzTests` drives generated
input through the same routine and refuses any verdict the table does not carry.

The three are one routine on purpose, which is #56, and
`expiry-or-use-count-judged-outside-the-decision` refuses an expiry or a use
count judged anywhere else. A second place that decides whether an invitation
may be honoured is a second answer, and the two drift.

The line has a second way of being broken, and it does not go through that
routine at all. A record read back from a file that does not mention a member
arrives with that member at its default, and the default for a revocation is an
invitation nobody revoked, so a store written by a build that spelled the
revocation some other way hands the decision a record it will honour. That is
#93 and it is refused by a test: `RemovingAMemberNeverMakesAnUnusableInvitationUsable`
removes each member of a written document in turn and refuses any removal that
turns an unusable invitation into a usable one, and
`RemovingBothRevocationMembersNeverMakesARevokedInvitationUsable` covers the
pair together, which is the shape an older store would have. The store requires
the revocation members to be present rather than defaulting them, and
`ARecordCarryingAnEmptyRevocationStillReadsAndIsUsable` is what keeps that from
refusing every live invitation.

### An invitation can never be recovered from the store, a log, a backup or an error message

Refused by a test, for the store. The record holds the keyed hash of the code
and never the code: `ARecordWithoutAKeyedHashIsRefused` refuses a record with
nothing to compare against, `NoMemberOfTheRecordHandsBackSomethingShapedLikeACode`
mints a code and asks the type for one back, and
`EveryPublicMemberOfTheRecordIsARowInThePersonalDataInventory` refuses a member
that nobody placed in `docs/personal-data.md`. The store writes that record, so
there is no code in the object graph for a file to carry.

Refused by a spelling, for the log. `secret-in-a-log-call` matches a code, a
password or the hash secret handed to a log call. `docs/logging.md` is where the
never-list for logging is decided and where the rule's narrowness is written
down.

THIS PARAGRAPH SAID NOTHING IN THE PLUGIN LOGS ANYTHING YET, AND THAT THE RULE IS
AHEAD OF ITS SUBJECT. The plugin writes log lines, in `LoadOnStart` and
`RetentionSweep`. That is not a reading taken here: `docs/logging.md` names those
two routines and `LoggingPageTests` holds the sentence naming them against the
tree in both directions, so a third routine that starts logging reddens the suite
rather than ageing that page.

    git grep -n 'The routines in this plugin that write log lines today' -- docs/logging.md
    docs/logging.md:9:The routines in this plugin that write log lines today are `LoadOnStart` and

How many calls those two hold is deliberately not written on either page, for the
reason `docs/logging.md` gives where it hands the reader the command instead: the
number moved once while a document carried it. So this line rests on a rule with
a subject, scanning calls that exist, rather than on a rule with nothing to scan.

What that does not change is the rule's narrowness, and this is the half a reader
should leave with. It matches a spelling, so a secret reaching a log call through
a local or through a string built two files away is invisible to it, which
`docs/logging.md` says rule by rule. Neither of the two routines is the redemption
path, and the redemption path is where a code is in scope to be logged at all;
that path is #399's, so this half of the line has never been put to the case it
exists for.

Not refused, for a backup. No test in this repository sees a backup: it is a
file another tool made, on a machine this suite never runs on. What stands
behind the line is the store holding only keyed hashes, above, and a check
somebody makes by hand. `docs/manual-checks.md` is the register such a check is
recorded in and it does not carry this one, and #100 is where what belongs in it
is decided.

Not refused, for an error message. The single indistinguishable refusal is #77
and there is no response to inspect.

### An invitation can never be extended by anything the redeeming party sends

Refused by a test, at the decision, which is the only surface a stranger reaches
in this tree. `RedemptionDecision.Decide` takes what was presented and reads
everything else from the record and from one clock reading the caller supplies,
and `RedemptionFuzzTests` asserts over generated input that nothing reaches
account creation without passing every gate, against what the harness built each
record to be rather than against the members the routine read.

Not refused, at the route, and the reason has narrowed since this paragraph was
first written. It said there is no redemption route, no form and no field list.
All three exist. The route serves a page, the page carries a form, and the form
asks three named things:

    git grep -nE '<form|name="(username|password|confirmation)"' -- Jellyfin.Plugin.Invites/Setup/setupPage.html
    Jellyfin.Plugin.Invites/Setup/setupPage.html:74:            <form method="post">
    Jellyfin.Plugin.Invites/Setup/setupPage.html:78:                    name="username"
    Jellyfin.Plugin.Invites/Setup/setupPage.html:96:                    name="password"
    Jellyfin.Plugin.Invites/Setup/setupPage.html:105:                    name="confirmation"

and the list is held rather than written down: `SetupPageTests.TheFormAsksForThreeThingsAndNoFourth`
counts the questions, `SetupFormInventoryTests.EveryFieldOnTheFormHasARowInThePersonalDataInventory`
refuses a field nobody placed in `docs/personal-data.md`, and
`SetupFormInventoryTests.TheThreeQuestionsTheRefusalListNamesAreTheOnesRead`
holds those three against `docs/setup-never-asks.md`. A fourth field added to the
form reddens all three.

What is absent is the post that receives them:

    git grep -nE '\[Http(Get|Post)' -- Jellyfin.Plugin.Invites/Controllers/RedeemController.cs
    Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:59:    [HttpGet("{code}")]

So this half of the line cannot be broken today, and for the weakest of reasons:
nothing reads a submitted field because nothing receives one. The form posts back
to an address with no post action behind it, which
`SetupPageTests.TheFormPostsBackToWhereItCameFrom` holds and which is worth
reading as the state it is rather than as a working flow. What the field list
does NOT bound is what a post handler will read - a handler binding a request
body to a model wider than the form is exactly this line broken, and no
assertion above sees it. That is #75.

### An invitation can never create more than one account per use

Not refused. Nothing creates an account.

The line has three parts and they are three issues. The record is the only
authority for the count, which is #52. The crash between creating the account
and writing the count back is #53, and its direction is decided there: lose an
invitation rather than grant an extra account. Two redemptions at once are #40's
lock and #106's deterministic test of it.

## Where this list is not defended today

In one place, so that a reader after the gaps does not have to read seven
sections:

| Line | What is missing | Where it lands |
| --- | --- | --- |
| Never create an administrator | Account creation, and the decision that a grant may not manage | #62 |
| Never modify an existing account | Account creation, and the write side of the seam a test would drive it through | #103 |
| Never grant beyond the template | The account the policy is written to, on which the all-libraries flag the server sets stays on because this plugin may not clear it | #63, #399 |
| Never be recovered, for a backup | A check somebody makes by hand. The register it would be recorded in is in the tree and carries no row for it | #100 |
| Never be recovered, for an error message | The refusal response | #77 |
| Never be extended by what is sent, at the route | The post that receives the form. The route, the form and the field list are all in the tree | #75 |
| Never create more than one account per use | Account creation, and the redemption path that spends a use under the lock. The count itself is authoritative already | #40, #53 |

Four of the seven lines are undefended in whole or in part, and that is the
state of the plugin rather than a gap in this page. The rule the page is for is
the other direction: a line's source arriving with no test for the line is what
this table exists to make visible, so a change that lands one of the issues
above lands the line's test in the same change and moves its row out of this
table.

#69 LEFT THE THIRD COLUMN OF THREE ROWS AND NO ROW LEFT THE TABLE. That issue is
closed as completed, so a reader following the column to find where the missing
thing lands arrived at finished work, which is the defect this page is for
happening on this page. What the routine it landed does is written in the second
and third sections above; what none of the three rows was ever about is the
routine, and each one still names account creation or the seam that would drive
it. A row leaves this table when the line has a test, not when an issue named
beside it closes.

Nothing in the table moves for the capability refusal added under #91, and that
is deliberate. What is missing in each row is a routine and the test over it,
and refusing the plugin the ability to reach an account removes neither. What it
changes is the first two rows' failure mode rather than their content: those
lines cannot be broken quietly today, because the change that would break them
has to add a write to the seam or a second type able to take the user manager,
and both are red before a line of the routine is written. The row leaves this
table when the routine and its test arrive, not before.

THE BACKUP ROW SAID THE REGISTER WAS MISSING AND THE SECTION ABOVE IT SAID THE
REGISTER IS IN THE TREE, at the same commit. Both sentences are about
`docs/manual-checks.md`, one names it and one does not, and a reader who took
the table for the summary it advertises itself as read that a check of this kind
has nowhere to be written down:

    git log --oneline --diff-filter=A -1 -- docs/manual-checks.md
    617c9b7 List the tests this plugin refuses to write, and what replaces each

What that costs is the row's own subject. The repair for this line is somebody
running a check against a backup once per release and writing down what they
found; a reader told the place to write it does not exist stops there, and one
told the register exists and carries no such row is one edit away from the whole
repair. So the cell names only the check now, which is what is actually absent:

    grep -c '^| Setup page renders' docs/manual-checks.md
    1
    grep -ci 'backup' docs/manual-checks.md
    0

The register carries five checks and none of them reads a backup. That is
unchanged by this edit and what belongs in it is #100's.

NOTHING FOUND THIS AND NOTHING WOULD HAVE. The two disagreeing sentences are on
one page, the section names the path and the cell does not, and the check that
refuses a moved `path:line:text` paste reads neither, because a prose cell in a
table is not a reference. It was found by reading the section and the table
against each other, which is a person rather than a route, and the same shape
can be written again tomorrow with every workflow here green.
