# Security policy

This plugin mints invitation links that create Jellyfin accounts. A defect here
can hand somebody an account on a server they were never invited to, so security
reports are wanted and are not a nuisance.

## Reporting a vulnerability

Use GitHub's private vulnerability reporting on this repository:

<https://github.com/Flowfin/jellyfin-plugin-invites/security/advisories/new>

It is enabled, which anyone can check:

```
gh api repos/Flowfin/jellyfin-plugin-invites/private-vulnerability-reporting
{"enabled":true}
```

The name in both is the one this repository answers to today, read back rather
than copied:

```
gh repo view --json nameWithOwner --jq .nameWithOwner
Flowfin/jellyfin-plugin-invites
```

An older path still reaches here through a rename redirect, so a link written
against it works until somebody else claims that name. A reporter following one
would be checking whatever the old path resolves to on the day they run it,
which is not a check of this repository's setting.

That route is private until an advisory is published, and it is the only route
this document promises. Please do not open a public issue for something that
lets somebody in, and please do not send it to a personal address; a public
issue is a working exploit handed to every reader of this repository, and a
personal address is a route nobody else can pick up.

If the report concerns Jellyfin itself rather than this plugin, it belongs to
the Jellyfin project's own policy and not here.

## What to expect

This is a small project without a staffed security team, so the numbers below
are what I can actually hold to rather than what reads well.

- An acknowledgement within seven days that the report was received and read.
- An assessment within thirty days: whether it is accepted, what it is thought
  to affect, and either a fix or a stated reason for not fixing.
- Credit in the advisory unless you ask otherwise.

If seven days pass with no acknowledgement, the report has probably not been
seen. Say so in a public issue without describing the vulnerability, and that
is the escalation.

## What is in scope

The code in this repository, its packaging metadata, and its workflows. A
report that the plugin grants more than the invitation it came from, that a
spent or revoked invitation is honoured, that an invitation code is guessable
or enumerable, or that a code or secret appears in a log, is in scope even if
you cannot demonstrate a full exploit.

Out of scope: the Jellyfin server itself, a server whose operator configured it
to be open, and reports produced by a scanner with nothing behind them.

## What this plugin defends

Seven properties, each with the reason it exists and what holds it today. They
are stated no more strongly than what can be run, so each one names the tests
that hold it, and a property nothing holds yet says so in the same place rather
than being left out where its absence reads as silence.

Every backticked name below is either a test in `Jellyfin.Plugin.Invites.Tests`
or a type in the plugin. Which of the two a name is, is decided by the assembly
it is in rather than by how it is written. The tests are reached by:

```
dotnet test --configuration Release
```

That sentence said a name written this way is a test, and three names on this
page have never been one. `AttemptLimiter`, `HashSecret` and
`InvitationCodeHash` are plugin types, so a reader who took the sentence at its
word went looking for three tests that do not exist.

Both halves are resolved rather than promised.
`SecurityPageTests.EveryNameThisPageWritesResolves` refuses a backticked name
that is neither a test this assembly runs nor a type either assembly declares,
which is what stops a rename leaving a name here that reads as evidence and
cannot be followed. `SecurityPageTests.EveryTestTheSecurityPageNamesExists` was
already doing that for the names written as a class, a dot and a method, and it
reads only those: after the first mention of a class this page drops it, so it
saw twenty-seven names and the fourteen written bare were read by nothing.

THIS PARAGRAPH SAID NOTHING IN THIS REPOSITORY REDEEMS AN INVITATION OR CREATES
AN ACCOUNT. Both happen now. The post on the public route judges a presented
code, takes the use and creates the account:

```
git grep -n 'RedemptionDecision.Decide' -- 'Jellyfin.Plugin.Invites/*.cs'; echo "exit=$?"
exit=0
```

```
git grep -n 'RedemptionDecision.Decide' -- 'Jellyfin.Plugin.Invites/*.cs'
Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:628:            var verdict = RedemptionDecision.Decide(presented, hash, contents.Invitations, now);
```

So the properties below are on a path a stranger can reach for the first time,
and that is the sentence to read before the rest of this page: what was held at a
routine nobody called is now held on a live route, and no server has run it.

Routes are served. Four of them are administrator-only and two are reachable
without an account, and the public pair serves the setup page for every code and
receives the form. Where that is the difference between a property and what holds
it, it is written out.

### Codes are minted from a cryptographic source at a calculated entropy

The code is the whole credential, so its size is read off a calculation rather
than picked because a number of characters looks long.
[docs/code-entropy.md](docs/code-entropy.md) requires 128 bits and derives that
from the live invitation count, the guess rate an attacker can sustain and a
stated margin. A code is twenty-six characters over a thirty-two character
alphabet, which is 130 bits, all of it random: no mint time, no operator, no
prefix, no checksum.

`InvitationCodeTests.AMintedCodeIsTwentySixCharactersFromTheAlphabet` holds the
length and the alphabet, `EveryCharacterOfTheAlphabetIsMinted` holds that the
draw reaches all thirty-two rather than a subset, and
`TwoCodesMintedInTheSameMillisecondDiffer` holds that a code is not derived from
the moment it was minted.

No test holds that the source is cryptographic, and none can. A good source and
a bad one are indistinguishable from their output at any sample size a suite can
take. What refuses a bad one is the `weak-random` rule in
`.github/lint/invariants.sh`, and it matches a spelling rather than a dataflow,
so the same source reached through a helper two files away passes it. The
compiler refuses the same spelling in the plugin project: the analyzer rule for
an insecure random source, CA5394, is raised to a warning in `jellyfin.ruleset`
and that project treats warnings as errors, so the mistake written into the
minting routine no longer builds. This paragraph said the rule was turned down
to information and the mistake built with no warning and no error, which was
true until the entry was raised under #16. The analyzer rule is a usage rule on
the same construct as the greppable one, so a source reached through a helper
passes it exactly as it passes the grep, and it does not reach the test project,
which names no ruleset. The dataflow query that would follow such a source was
run against that same mistake in that same routine and reported nothing, which
is measured in issue #16.

### A code is stored only as a hash of itself

Somebody who reads the data directory or a backup should learn nothing they can
present at the redemption page. The record carries the hash and no code, and
that is refused rather than remembered:
`InvitationRecordTests.ARecordWithoutAKeyedHashIsRefused` refuses a record with
nothing to compare against, and `NoMemberOfTheRecordHandsBackSomethingShapedLikeACode`
asserts that no member of the type hands back anything canonicalisation would
accept as one.
`InvitationModelTests.AMintedCodeIsNotRecoverableFromTheStore` writes a record
and reads the file back, asserting the longest run of code alphabet in it is
shorter than a code.

The second of those said it mints a code, and it does not. It reads a record the
test builds, with a hash made of a repeated byte, so what it holds is a property
of the type's members rather than of a record built from a live mint. The
difference matters in the direction that flatters the page: a member that leaked
the code it was constructed from would be invisible to a record constructed
without one. The minted direction is held by the other two tests in this
section, which mint through the real path and then read the store and every file
the mint left behind.

The stored form is keyed, which this paragraph said it was not.
`InvitationCodeHash` reduces a canonical code under the secret `HashSecret`
draws and holds, and the minting path is what constructs it:

```
git grep 'new InvitationCodeHash' -- 'Jellyfin.Plugin.Invites/*.cs'
Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:            var hash = new InvitationCodeHash(
```

The line number came out of that command rather than being corrected. It read
168, the construction had moved off that line, and the paste stayed as it was
because nothing re-runs a pasted output. What the sentence rests on is which
file constructs the hash, so the number was carrying nothing and can no longer
go stale under it.

`InvitationCodeHashTests.TheValueIsNotTheUnkeyedHashOfTheCode` holds that the
stored value is not the plain digest of the code, and
`TwoKeysReduceOneCodeToDifferentValues` holds that the key is what separates two
installations. Without the key an unkeyed digest of a twenty-six character code
is a table somebody builds once and then reads every store they are ever handed.
`MintedCodeOnDiskTests.NothingTheMintLeavesOnDiskIsShapedLikeACode` mints through
that path and reads every file the mint left behind rather than the store alone.

The other direction is not held. Nothing presents a code to this plugin, so no
route reduces one and compares it against what is stored, and the property is
held at the mint and at the record rather than across a redemption. Somebody who
can read the store and the secret together is in the undefended list below, and
none of this defends against them.

### The four ways a redemption fails are one answer

A response that separates "no such invitation" from "expired" from "already
used" from "revoked" hands somebody guessing an oracle, and turns a search
nobody can afford into one they can. The reason the invitation was refused is
the operator's to see, in their own view, and not the caller's.

`RedemptionDecisionTests.RubbishAndAnUnmintedCodeAreTheSameVerdict` holds that a
code that was never minted, a string that is not a code, an empty one and none
at all are one outcome carrying nothing.
`ARecordThatIsBothRevokedAndExpiredAndSpentReadsAsRevoked` and
`AnExpiredRecordWithUsesLeftIsExpiredRatherThanSpent` hold that the order the
refusals are read in is fixed rather than incidental, and
`RedemptionDecisionTableTests.EveryReachableCombinationAnswersItsRow` holds the
whole table rather than the cases somebody thought of.

The other half of this is duration. The stored hash is compared with
`CryptographicOperations.FixedTimeEquals`, every record is compared rather than
the loop returning on the first match, and
`RedemptionDecisionTests.TheAnswerDoesNotDependOnWhereTheRecordSits` moves the
matching record through every position and requires the same answer. Three
rules refuse the spellings that would take it back:
`secret-compared-with-equality`, `secret-compared-by-sequence` and
`secret-compared-through-a-comparer`.

This paragraph named the first two and called them the two spellings. The third
had been in the lint for two days when that sentence was written, and it is the
one the other two cannot see: a comparison written as `string.Equals(stored,
presented, StringComparison.Ordinal)` puts the secret one comma along, past an
identifier-in-front-of-an-operator pattern, and it is what somebody writes after
an analyser asks them to say which comparison they meant. A page naming two of
three describes a narrower guard than the one that is there, which is the
direction this section is least able to afford: the reader who trusts it is the
reader deciding whether that spelling is available. No timing was measured, and
this is a claim about which branches the routine takes rather than about a
clock.

THIS PARAGRAPH SAID BYTE-IDENTICAL RESPONSES ARE NOT HELD BECAUSE NO ROUTE
ANSWERS A PRESENTED CODE, AND THAT NOTHING POSTS TO THE PUBLIC ROUTE. One does:

```
git grep -n 'HttpPost' -- Jellyfin.Plugin.Invites/Controllers/RedeemController.cs; echo "exit=$?"
exit=0
```

```
git grep -n 'HttpPost' -- Jellyfin.Plugin.Invites/Controllers/RedeemController.cs
Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:210:    [HttpPost("{code}")]
```

They are held on that route and the assertion is at the route level, which is
where a response the server actually sends can be read:
`RedeemPostTests.EveryRefusalThisRouteServesIsTheSameResponse` drives five of the
six cases [docs/refusal-response.md](docs/refusal-response.md) lists through the
action and compares the status, the body, the content type and every header.

Two bounds stay, and they are the halves that matter. The sixth case, a ceiling
on what the plugin may create, is refused by nothing yet, so it is not in the
comparison. And the GET still answers every code with the same setup page, so it
distinguishes nothing and refuses nothing; its refusal half is #75 and #77.

Timing is not held and no test in this repository will say otherwise. That claim
and its reason are in [docs/refusal-response.md](docs/refusal-response.md).

### Redemption is rate limited

**Nothing is limited today.** That sentence is first because the paragraphs under
it name a dozen tests, and a reader who takes the list for the property would be
reading the opposite of what this section says. A counter exists and no route
calls it:

```
git grep -ln 'MayJudge' -- 'Jellyfin.Plugin.Invites/*.cs'
Jellyfin.Plugin.Invites/Redemption/AttemptLimiter.cs
```

The one file is the definition. An attempt is a presented code being judged, and
no route judges one: `GET /redeem/{code}` is reachable without an account, serves
a page and reads no invitation, so there is nothing on it for a limiter to stand
in front of. Every test named below drives the counter directly and not one of
them is about a request.

This paragraph used to say that nothing in this repository limits anything and
quoted a command over the plugin's sources returning only the clock seam. That
stopped being true when `AttemptLimiter` landed, and the difference between "no
counter" and "a counter nothing calls" is the whole reason this section is worth
reading twice.

What the counter does, and what holds each part of it.

It counts to the two numbers [docs/rate-limit.md](docs/rate-limit.md) decided, in
fixed windows, per source address and across all of them.
`AttemptLimiterTests.OneAddressGetsExactlyTheDecidedNumberInItsWindow` and
`AttemptLimiterTests.AllSourcesTogetherGetTheDecidedNumberInASecond` hold the two
ceilings, and
`AttemptLimiterTests.TheNumbersInTheCodeAreTheNumbersOnThePageThatDecidedThem`
holds them against the sentence on that page rather than against a second copy of
the numbers, so moving either one in the source without moving it on the page
reddens.

A refused request is not an attempt and is not counted, which is what makes the
guarantee exact rather than approximate.
`AttemptLimiterTests.AnExhaustedAddressCannotSpendTheGlobalAllowanceByBeingRefused`
and `AttemptLimiterTests.AnAddressRefusedGloballyHasSpentNoneOfItsOwnAllowance`
hold both directions of it.

Each window turns at its boundary and not a tick before it.
`ClockBoundaryTests.ThePerAddressWindowTurnsAtTheBoundary` and
`ClockBoundaryTests.TheGlobalWindowTurnsAtTheBoundary` ask at the tick before the
boundary, at it and the tick after, and
`ClockBoundaryTests.TheFixedWindowLetsTwiceTheRateThroughAcrossABoundary` asserts
what a fixed window costs rather than leaving it on the page: twice the stated
rate across a boundary, read against the sentence that admits it.

The counter leaves with the process, which is the lifetime the two numbers were
chosen under rather than an implementation detail.
`AttemptLimiterTests.TheCounterHoldsNothingThatCouldOutliveTheProcess` holds that
nothing it keeps is a file, a stream or a path, and
`LimiterRegistrationTests.TheLimiterIsRegisteredForTheLifetimeItsNumbersRestOn`
holds that the server is handed one counter rather than one per request, which is
the difference between a limit and an empty counter per attempt.

Two costs it carries even once something calls it, asserted rather than
described.
`ClockBoundaryTests.AClockSteppingBackwardsAcrossAWindowHandsTheAllowanceBack`
holds that a clock going backwards hands the allowance back early, and
`ClockBoundaryTests.AJumpPastSeveralWindowsGivesOneAllowanceBack` holds that a
jump forward gives one allowance rather than one per window skipped. Neither is a
defect: [docs/rate-limit.md](docs/rate-limit.md) settles that the counter may
never be the thing the arithmetic rests on, because an attacker resets it by
waiting and an operator resets it by upgrading, and a backwards clock is one more
way to reset something already resettable.

What the counter holds about a person is one source address for the length of its
window, which `AttemptLimiterTests.AnAddressIsHeldForItsWindowAndNoLonger` reads
back as a count rather than as the addresses.

This property is on the list because the entropy calculation quotes a figure
that assumes a limiter, and reading the code length as sufficient on its own
would be reading the unthrottled row of that page, not the throttled one. Both
rows are on it, and the unthrottled one is what holds today, because nothing
calls the counter.

### An invitation expires, at an instant the plugin does not argue with

Expiry is judged against one absolute instant, read through the one clock seam
the plugin has, so the boundary is a comparison somebody can point at rather
than a behaviour that depends on when the code ran.
`RedemptionDecisionTests.TheExpiryBoundaryIsExclusive` asserts the answer one
tick before the boundary, at it, and one tick after, which is the difference
between "expires at" and "expires after" and the only place it shows up before a
support thread.

`ClockJumpTests.AClockSteppingBackwardsAcrossTheExpiryMakesTheInvitationUsableAgain`
holds the accepted cost of a clock that goes backwards,
`AClockSteppingBackwardsRevivesNeitherARevokedNorASpentInvitation` bounds that
cost to expiry alone, and `AJumpPastSeveralExpiriesRefusesEveryOneOfThem` holds
a jump forward across several at once.
[docs/expiry-rules.md](docs/expiry-rules.md) is where the backwards jump is
argued rather than defended.

`clock-read-outside-the-seam` refuses the machine clock anywhere but the one
file, so a later change cannot quietly acquire a second opinion about the time.
What it refuses is a fixed vocabulary rather than the idea: `DateTime.UtcNow`,
`DateTime.Now`, `DateTime.Today`, `DateTimeOffset.UtcNow`,
`DateTimeOffset.Now`, `Environment.TickCount`, `Stopwatch.GetTimestamp` and
`TimeProvider.System`. This paragraph said it refuses a machine clock read,
which is wider than what it does, and the difference is reachable: a
`Stopwatch.StartNew()` written into the plugin and read for the time it has
counted leaves the rule green. That was probed rather than argued, and the probe
was taken out again.

### An invitation can be revoked, and revoking it twice costs nothing

Revocation is the control an operator reaches for at the worst moment, so it has
to work on the first attempt and it has to be safe to repeat.
`RevocationTests.RevokingRecordsTheOperatorAndTheTime` holds the trail,
`RevokingTwiceKeepsTheFirstTimeAndTheFirstOperator` and
`RevokingTwiceHandsBackTheRecordItWasGiven` hold that a second revocation
changes nothing and is visible to a caller as nothing to write, and
`RedemptionDecisionTests.ARevokedInvitationIsRefused` holds that the decision
refuses it.

Two things revocation deliberately does not do, both asserted rather than
described. `RevocationTests.TheAccountsAlreadyCreatedAreStillNamed` holds that
revoking does not disown the accounts already made from that invitation, and
`NothingHereCanBeHandedAnAccount` holds that no account manager can be passed
into any of it, so a later change cannot start deleting accounts through this
routine.

The first of those was credited here with holding that revoking stops future
accounts as well, and it does not. It compares one record's list of accounts
before and against after, which says nothing about what a later redemption is
answered with. What stops the future ones is
`RedemptionDecisionTests.ARevokedInvitationIsRefused`, named in the paragraph
above, and reading one test as holding both is how a property ends up with less
behind it than the page says.

Immediacy is not held. A revocation taking effect against a redemption that is
already in flight follows from the lock covering read, decide and write as one
unit, and nothing in this repository holds that lock because nothing redeems.

### No invitation mints an administrator or widens an existing account

This is the ceiling, and it is the property worth a refusal in more than one
place. An invitation that could mint an administrator is the server. An
invitation that could widen an account that already exists is worse, because it
needs no new account at all: a feature that reuses an account whose name matches
turns the link into a privilege-editing tool for whoever holds it.

THIS SECTION SAID NO TEST HOLDS EITHER HALF, BECAUSE THERE IS NO ROUTINE THAT
CREATES AN ACCOUNT. There is one, under #398, and both halves are held inside it
under #62. What each test does and does not reach is set out here rather than
left to the name, because this was the last property on this page with nothing
behind it and a name alone would be the weakest form of the repair.

`AccountCreationTests.ATemplateThatWouldManageTheServerIsRefusedBeforeAnythingIsCreated`
puts a template asking to manage the server through the creation routine and
requires two things: that it is refused, and that nothing was asked of the
server. The second is what separates this from a refusal raised after an account
already exists, which would hold the ceiling and still leave somebody with an
account they were not meant to get.

`AccountCreationTests.NothingHereCanBeHandedAnAccountThatAlreadyExists` holds the
other half by shape rather than by a check at run time. Every parameter of the
routine is the write seam, a name, a credential or a template, so there is no
account identifier to hand in and no account for it to address except the one the
creation just made. Asserting after a call that no other account had moved would
pass for a routine that reached one and left it as it was, which is the version
that changes something after the next edit.

What neither test reaches is worth as much here as what they hold. The first
reads one field of one type at one moment, so a template that reached the
server's administrator flag by a route that does not pass through the creation
routine is outside it. The second says the routine cannot be pointed at an
account; whether a redemption presented by somebody already signed in creates
nothing is a question about a request, and there is no post on the redemption
route, so nothing in this repository answers it.

The half of the ceiling that is configuration is not held by anything and there
is nothing for it to hold. #62 asks that a configuration asking for an
administrator account be rejected at load; the configuration type carries one
setting and it is an address, so there is no such value to reject. That is #86's
ground and it is named here so the absence is not read as a refusal.

Beside the two, `administrator-flag-set` in `.github/lint/invariants.sh` refuses
the administrator flag being written anywhere in the tree, in either direction,
with no exemption for the file the other policy rules exempt. It matches an
assignment on the line the field is named on, so a write through a local, a
policy built by a helper handed a boolean, or a whole policy object handed to
the server with the flag already set all pass it.

[docs/what-an-invitation-can-never-do.md](docs/what-an-invitation-can-never-do.md)
is where the ceiling is written down line by line, and it marks each line with
whether a test refuses it, a spelling refuses it, or nothing does.

## What a leaked link costs

An invitation link is a bearer credential. Anybody holding it can create an
account, so a link in a forwarded message or a browser history on a shared
machine is worth whatever the invitation behind it was worth. This is the bound
on that, in the same words as
[docs/threat-model.md](docs/threat-model.md), which is where each clause is
placed against the issue that keeps it:

> Somebody who holds a leaked link, and reaches the server before the invited
> person does, gets one account for each use the invitation had left, with
> exactly the template that invitation carried, valid for no longer than the
> invitation had left, listed for the operator as a redemption of that
> invitation, and removable by deleting that account.

Nothing in this repository redeems an invitation yet, so today the sentence is
what this plugin is being built to, and not a report of what it does. A report
that the plugin exceeds this bound once the code exists is a report this policy
wants, and the list above is what to measure it against.

The default validity is seven days, and a spent invitation is spent for good.
The threat model carries the reasoning for both, and both are the shortest
window that still works rather than the longest one an operator would tolerate.

## What the guided setup will never ask

The page that turns an invitation into an account asks for a username, a
password, and a password confirmation. That is the whole form.

It will never ask for a password to another service, a password already used on
this server, a payment detail, a date of birth, a postal address, a legal name,
a security question, anything phrased as optional that the plugin has no field
for, or anything the operator could ask outside the plugin. It loads no script,
font, image or analytics from another host.

A page bearing this plugin's name that asks for any of those is not this
plugin's page, and a report that it does is a report this policy wants. The
reason for each refusal is in
[docs/setup-never-asks.md](docs/setup-never-asks.md), and a field added to the
form needs a row in [docs/personal-data.md](docs/personal-data.md) first.

The page exists and is served, which this section said it did not, so the two
halves of it are now in different states and are worth reading apart.

The form and what it loads are a description of something serving today.
`SetupPageTests.TheFormAsksForThreeThingsAndCarriesOneOfItsOwn` holds that the
served bytes carry the three fields named above, the anti-forgery token, and
nothing else, so a question added to the form cannot arrive without somebody
moving that assertion.
`SetupFormInventoryTests.TheThreeQuestionsTheRefusalListNamesAreTheOnesRead`
holds the narrower half, that the three above are the only controls a person
answers, and its neighbour holds that the token is the only one they do not.
`ThePageFetchesFromNowhereElse` reads the same bytes for four spellings of an
address somewhere else, `ThePageRunsNoScript` refuses a script element, a
`javascript:` address and a handler attribute, and
`ThePolicyNamesNoOriginAndAllowsNoInlineScript` holds that the response is served
under `default-src 'none'`, which names no origin at all. Nothing here can decide
whether a field asks for a legal name, and that reading stays a person's.

The sentence about naming the server was removed rather than left standing,
because the page does not do it:

```
git grep -n 'It does not say which server it belongs to' docs/setup-never-asks.md
docs/setup-never-asks.md:121:It does not say which server it belongs to, which the presentation rules above
```

The line moved from 100 to 113 when that page stopped saying nothing takes a
submission, which added a paragraph above it. The sentence this points at is not
one of the bytes that changed, and the number is re-pasted rather than corrected
quietly.

Naming the server means writing a value into markup that nothing is written
into, which is what leaves the page with no place a presented code could reach
it, and which value and where it comes from is not decided.
[docs/setup-never-asks.md](docs/setup-never-asks.md) still asks for it and says
in the same place that it is not done.

Nothing posts to the form, so what a completed setup does is still what this
plugin is being built to rather than a report of anything.

## What is not defended

These are the entries the threat model in [docs/threat-model.md](docs/threat-model.md)
marks as undefended, in the same words. That file is where each one is placed
against the attack it belongs to.

This paragraph said every mitigation the threat model names is a promise held by
an open issue rather than by code. That is no longer the reading. Some of them
have landed, the keyed hash above being one, and the grid in that document names
the issue each one sits on rather than this page repeating them. What is
unchanged is that there is no redemption path here, so no mitigation in the grid
has been exercised on a path a stranger can reach. Read this section as what will
still not be defended once the rest land, and not as a claim that everything else
already is.

A leaked link within its validity, before the intended person uses it, is an
account for whoever found it. This is what a bearer credential means and no
mitigation in this plugin changes it. What the plugin offers instead is a
smaller window and a smaller blast radius: a validity the operator chooses, a
use count the operator chooses, and revocation that works the moment the
operator reaches for it.

An operator with administrator rights can mint whatever the ceilings allow.
Nothing here defends the server against the person the server already trusts
with it. The ceilings bound what any single invitation can grant, and they are
configuration an administrator can also change.

A restored backup revives spent invitations. The invitations redeemed since the
backup are live again with their uses restored, and the revocations made since
are undone. The plugin cannot prevent this. What it does is compare, on load,
the accounts the store claims to have created against the accounts the server
actually has, and report the disagreement in both directions rather than
reconciling it silently.

A cloned data directory produces two servers that both honour the same live
invitations. Redeeming on one leaves the other still willing, because neither
knows the other exists. The operator guide says to rotate the hash secret after
a clone, and rotation is a revoke-everything operation offered deliberately.

Username availability is disclosed by the setup form. A form that has to tell
somebody their chosen name is taken is a form that tells anybody holding a code
which names exist. The disclosure is bounded by needing a valid code first.

Somebody who can read both the store and the hash secret can test a guess
offline, without meeting the rate limit. The keyed hash and the code's entropy
are what stand between them and a code, and neither is a defence against
somebody who is already reading the data directory.

## Supported versions

Nothing is released yet, so there is no supported version and no backport
policy. This section is rewritten at the first release, under milestone M12.
