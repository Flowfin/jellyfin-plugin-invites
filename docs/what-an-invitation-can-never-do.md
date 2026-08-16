# What an invitation can never do

Seven sentences. Each one is a property somebody could break by accident in a
change that looks unrelated, which is why they are written down together rather
than left implied by the code that happens to hold them today.

A sentence here is worth exactly what refuses it. So every line below names the
thing that would go red, and where nothing would, it says so and names the issue
that lands the source. A line whose defence is "nobody would do that" is a line
this page would be better off without.

## How to read a line

Each line carries one of three states, and they are different claims:

- **Refused by a test.** A named test fails when the line is broken in the
  source. The test is named here, and breaking the line is how the naming was
  checked rather than a claim about what the test looks like it covers.
- **Refused by a spelling.** A rule in `.github/lint/invariants.sh` matches the
  shape of the mistake. That is narrower than the sentence: it catches the
  spelling somebody actually writes and not the same thing written another way,
  which the lint file says about itself rule by rule.
- **Not refused.** Nothing in the tree would notice. The line still belongs
  here, because the code it is about is being written against it, and the issue
  that will land that code is named.

The tests are named rather than pasted, and the names are what the suite prints:

    dotnet test --configuration Release --no-build

## The seven

### An invitation can never create an administrator

Not refused by a test. Nothing in this plugin creates an account or writes a
user policy, so there is no execution path for a test to reach.

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

Not refused by a test. No account is created and none is written.

This paragraph said the plugin named the server's user operations nowhere, and
that stopped being true. The command it offered as evidence returns two lines:

    git grep -n 'IUserManager' -- '*.cs' ':!.github'
    Jellyfin.Plugin.Invites.Tests/RevocationTests.cs:132:    /// something after the next edit. Add an <c>IUserManager</c> parameter and
    Jellyfin.Plugin.Invites/Accounts/ServerAccounts.cs:56:    public ServerAccounts(IUserManager users)

The second is the plugin. `ServerAccounts` asks the user manager for the
identifier of every account and for nothing else, which is what
`IServerAccounts` declares and what the load-time comparison landed under #46
reads. It is not a type waiting for a caller: it is registered, and the hosted
service that reads it runs when the server starts.

    git grep -n 'IServerAccounts, ServerAccounts\|AddHostedService<LoadOnStart>' -- Jellyfin.Plugin.Invites/Startup/PluginServiceRegistrator.cs
    Jellyfin.Plugin.Invites/Startup/PluginServiceRegistrator.cs:36:        serviceCollection.AddSingleton<IServerAccounts, ServerAccounts>();
    Jellyfin.Plugin.Invites/Startup/PluginServiceRegistrator.cs:37:        serviceCollection.AddHostedService<LoadOnStart>();

That narrows the reason this line is undefended without moving it. An identifier
is not an account: nothing here hands back a user object to modify, and the type
has no member that writes. What is gone is the argument that no path in the
plugin could reach an account at all, and what is left is the smaller one, that
the path which exists reads identifiers.

The first line is prose inside a test file, and the test it belongs to is the
nearest thing in the tree to a defence of this sentence.
`NothingHereCanBeHandedAnAccount` holds the parameters of the revocation routine
to a fixed set, so a later change that starts passing an account into it goes
red before anything is written with one. It covers that one routine, and this
line is about every routine.

The line has two halves and they need different mechanisms. That the routine
does not modify an account it was handed is a test, and it waits on the routine
in #69 and the write side of the seam in #103. That no other path in the plugin
ever calls the server's update method is not a test at all: it is a greppable
invariant, and `policy-written-outside-the-template` is the half that exists.

### An invitation can never grant a library, a permission or a quota that its template does not name

Refused by a test, on the template. `EveryGrantTheIssueNamesIsAPropertyAndThereAreNoOthers`
holds the grant to exactly the fields the plan named.
`NoCeilingIsAStatedGrantRatherThanAnAbsentOne` refuses a field that means
whatever the server does by default, which is a grant that changes under an
upgrade. `AGrantOfNoLibraryIsKeptAsOne` keeps the empty list as a decision
rather than as a missing one. `EveryQuotaOnTheTemplateHasARowAndEveryRowIsAQuota`
ties every quota the template carries to a field of the server's policy, read
off the assembly rather than off a document.

Not refused on the account. What no test here reaches is the account afterwards,
because the routine that applies a template is #69 and the fake user manager it
would be applied through is #103. Until then this line is defended on the value
and not on the result.

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
down. Nothing in the plugin logs anything yet, so the rule is ahead of its
subject rather than behind it.

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

Not refused, at the route. There is no redemption route, no form and no field
list, so what a stranger may send is undecided rather than bounded. That list is
#75, and it is the half of this line that a page a browser posts to will need.

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
| Never create an administrator | Any execution path, and the decision that a grant may not manage | #62, #69 |
| Never modify an existing account | The routine, and the write side of the seam a test would drive it through | #69, #103 |
| Never grant beyond the template | The account the template was applied to | #69, #103 |
| Never be recovered, for a backup | A check somebody makes by hand, and a register to record it in | #100 |
| Never be recovered, for an error message | The refusal response | #77 |
| Never be extended by what is sent, at the route | The route, and the list of fields a form may carry | #75, #82 |
| Never create more than one account per use | Account creation, the count, and the lock around both | #52, #53, #40 |

Four of the seven lines are undefended in whole or in part, and that is the
state of the plugin rather than a gap in this page. The rule the page is for is
the other direction: a line's source arriving with no test for the line is what
this table exists to make visible, so a change that lands one of the issues
above lands the line's test in the same change and moves its row out of this
table.
