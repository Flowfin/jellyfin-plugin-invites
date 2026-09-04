# Tests this plugin refuses to write

Eight obvious tests are refusals here. Each one would be a reasonable thing to
add, each one would break the headless rule in `CONTRIBUTING.md`, and each one
is therefore replaced by something narrower rather than dropped.

Writing the list down is what stops the eighth of them arriving in good faith
next year, with a comment explaining that it only needs a browser. A refusal
nobody recorded is a refusal that gets reversed by whoever did not know it was
one.

Every row below names the refusal, the part of the headless rule it breaks, and
what covers the same risk instead. Where the replacement does not exist yet, the
row says so and names the issue that builds it, because a replacement written
in the future tense covers nothing today.

Every backticked name below whose whole content is one word beginning with a
capital is a test in `Jellyfin.Plugin.Invites.Tests`, a type in the plugin, or a
member of one. Nothing else is written that way, which is why an HTTP header is
written with its colon, as `Host:`, and a rule id keeps its hyphens.

`RefusalListTests.EveryNameTheRefusalListWritesResolves` refuses a name here
that is none of the three. A row whose replacement was renamed goes on reading
as covered until somebody follows the name, and this page has gone stale twice
already in what its rows say, both times found by a person re-reading rather
than by anything going red. That leg holds the names.

The part of a status line that names an issue and a state is held too.
`.github/lint/tracker-claim.sh` reads every present-tense sentence in tracked
markdown saying an issue is open or closed and refuses one the tracker
disagrees with. It runs daily rather than on a pull request, because reading
the tracker is a network call and because a sentence here goes stale when
somebody closes an issue rather than when somebody edits this file, so the
merge is not the moment the answer changes.

A count this page states about itself is held as well, in
`TestsNotWrittenPageTests` beside the legs that hold the block of server jobs.
`TheCountOfRefusalsThePageStatesIsTheNumberOfRowsItCarries` requires the number
in the opening sentence above and the number in the heading over the rows to be
the number of rows under it.
`TheCountOfServerJobsThePageStatesIsTheNumberItNames` requires both numbers in
the real-server row's status line to be the number of jobs that row goes on to
name, so a count here reaches the workflow directory through the names rather
than reading it a second way. A third leg asks that the four sentences were
found at all, so a page reworded past them reds rather than reporting the same
silence as a page whose numbers agree.

Each of the two is one word away from being wrong. The count of server jobs is
the one that already went wrong, on the line above the block that names them,
and the count of refusals is the one this page invites: its last section tells
the next person how to add a row and says nothing about the two words that go
stale when they do.

They are not every count written here. The rows count assertions and
replacements among themselves, and nothing reads any of those, so a number
inside a row is still a number somebody has to keep true by hand. What these
legs hold is the two that count the page's own subjects.

What is still held by nobody is the prose of a status line. "Neither part
exists", and whether the replacement a row names covers the risk the row
claims, are judgements about the tree and about meaning; both of the two rows
that went stale were stale in sentences of that kind as well as in what they
called things. #100 is where that stays open.

## The rule these are refused by

The headless rule is in `CONTRIBUTING.md` and it is executed rather than
trusted: `.github/workflows/headless.yaml` runs the suite inside a container
with no network interface, as an unprivileged user. A test that reaches out
fails there rather than on somebody's next machine.

The rule refuses a test that opens a window or needs a display, asks for
elevated rights, writes outside a temporary directory it owns, reads or writes
the machine's certificate stores, opens a network connection, launches an
external binary, or sleeps on a real clock.

## The eight

### A test that drives the setup page in a browser

It would start a real server, load the redemption page, fill the form and press
the button. It needs a display or a headless browser stack, and a network to
reach the server it started. Two clauses of the rule, and the browser stack is
also an external binary.

Replaced by route-level tests over the whole flow, which is #107, plus one
manual check before each release recorded in `docs/manual-checks.md`. The route
tests are where every branch of `docs/redemption-flow.md` is asserted. What they
cannot see is whether the page renders, which is what the manual check is for
and the only thing it is for.

Status: neither part exists. #107 is open, and no manual check has been recorded
because nothing has been released.

### A test that proves the plugin loads into a real Jellyfin server

It needs a server binary, a media directory and somewhere to put both. External
binary, and writes outside a temporary directory.

What those two clauses refuse is a test in the suite. The headless rule is
executed against the suite, by a container with no network interface, so the
clauses are read against what `dotnet test` does and not against every job this
repository runs. A workflow job that starts a server on a runner breaks none of
them. That distinction was left implicit while it cost nothing, and it stopped
being free the day two such jobs landed, because this row went on describing a
plugin nobody had seen load anywhere.

Replaced by four things that each cover part of it. Every build compiles the
plugin against the package version the manifest's `targetAbi` names, which
`Directory.Build.props` derives from that field, so a call into a server member
that arrived after the floor is caught at build time rather than at redemption
time, and the assembly a server binds names the floor rather than a newer
release of the line; `.github/workflows/abi-floor.yaml` builds it that way on
every pull request and `AbiFloorBindingTests` holds the built assembly's
reference table to the same field.
The packaging job in `.github/workflows/package.yaml` builds the artefact the
manifest lists. Then a runner installs that artefact into an unmodified
published server image and puts the question to the server itself. And one
manual install per supported server line before a release, recorded in
`docs/manual-checks.md`.

The jobs that put a question to a real server are these:
`.github/workflows/e2e-authorization.yaml` asserts that the plugin's one
anonymous route answers and that every administrator route refuses an
unauthenticated request. `.github/workflows/e2e-identity.yaml` installs the
upstream template beside this plugin and asserts that the server holds both
under two identifiers with this plugin still serving its own route.
`.github/workflows/e2e-scheduled-task.yaml` completes the wizard and reads the
server's own scheduled task list back, so the retention sweep is a task the
server holds rather than a class this plugin declares.
`.github/workflows/e2e-no-web-client.yaml` starts the same image with the web
client turned off, confirms it is gone before asking for anything else, and
compares the served setup page against the tracked file byte for byte.
`.github/workflows/e2e-plugin-disabled.yaml` disables the plugin through the
server's own administrator route and reads the public redemption address twice,
once with the server still running and once after a restart, because those are
two moments an operator meets and #47 asks about both. It answers 200 at the
first and 404 at the second, which is the half of that issue no reading of this
tree could reach. `.github/workflows/abi-floor.yaml` installs the packaged
plugin on the published image of the floor itself, 10.11.0, and reads the
server's plugin list back for a status of Active, because the archive compiled
against 10.11.11 was NotSupported there while every job above, pinned to
10.11.11, was green; that reading is on #155.

That block said two jobs and named two while four had landed, which is the
drift this page is least able to afford: it is the map somebody reads to find
out what has been asked of a real server, so a job missing from it is a job
nobody counts. It was found by asking which workflows name the pinned published
server image and which of those this page writes down, and the two answers
differed by the two jobs that arrived last. `TestsNotWrittenPageTests` holds the
block in both directions now. A workflow naming that image and missing from the
block reds the suite, and a name in the block that is not such a workflow reds
it too, so the repair cannot rot the way the sentence it replaces did.

The anonymous route answering is the part those jobs share, and it is what
belongs to this row rather than to the issue each was built for. A plugin the
server failed to load answers 404 at every one of its addresses, so a job that
reads 200 there has read the loading, and `e2e-authorization.yaml` gives that as
its own reason for asserting the anonymous route before anything else.

Status: the ABI floor build, the packaging job and six server jobs exist, and
every one of the six is green where it has run. Five of them on the default
branch, read at `00854ccde17a625781cdc8e9dcf76bae0ee0faef`; the sixth, the load
on the floor in `abi-floor.yaml`, at the head of the change that landed it under
#155, where its run is quoted, because a job runs on the default branch for the
first time at the merge that lands it, which is what the paragraph after the
transcript says of the fifth:

```
$ for w in e2e-authorization e2e-identity e2e-no-web-client e2e-plugin-disabled e2e-scheduled-task; do
    printf '%s\t' "$w.yaml"
    gh run list --workflow "$w.yaml" --branch master --limit 1 \
      --json headSha,conclusion --jq '.[]|"\(.headSha[0:8]) \(.conclusion)"'
  done
e2e-authorization.yaml	00854ccd success
e2e-identity.yaml	00854ccd success
e2e-no-web-client.yaml	00854ccd success
e2e-plugin-disabled.yaml	00854ccd success
e2e-scheduled-task.yaml	00854ccd success
```

This paragraph said four of the five, named the fifth as deliberately absent from
the loop, and gave as its reason that the job had never run on the default
branch. That was true when it was written and stopped being true at the merge
that landed it, which is the same commit: the merge is a push to `master` and the
job runs on one. So a sentence explaining an absence outlived the absence by the
length of one workflow run, in the paragraph of this page most likely to be
quoted as a verdict. It was found by asking the loop for the fifth name rather
than by reading the paragraph.

The manual install has a place to be recorded and nothing recorded in it, and
that is the half of this row which has not moved. What those jobs answer for is
one server line on two images pinned by digest: the newest release of the line
`build.yaml` names in `targetAbi`, and since #155 the floor of it, 10.11.0, which
is the version the package is compiled against and the one on which the archive
compiled against 10.11.11 did not load. Per supported line is what the manual
install is for, and a job pinned to a digest says nothing about the line above
or below it.

### A test that the account signs in with the password the person chose

It would create an account through this plugin, hand that password to the
server's own sign-in and read what came back. It needs a server binary and
somewhere to put it, and a connection to reach it: an external binary, a write
outside a temporary directory it owns, and a network connection. Three clauses.

The refusal is narrower than it first reads and the narrowing is the useful
part. This plugin does not authenticate anybody. It hands the password to the
server's own credential routine and keeps nothing, so what a sign-in then
answers is a fact about the server rather than about this repository. What IS
decidable here is that the right member is reached, with the account and the
password, in whichever of the two shapes the ends of the declared server line
differ by.

Replaced by three things, covering different halves. `ServerAccountWritesTests`
drives the credential arm against two stand-ins, one carrying each shape of the
member, so the arm binds and is called correctly on either end of the line.
`NoTraceOfThePasswordTests` reads every file the plugin wrote, the response it
sent and the trail of calls it made, for the password's bytes, which is the half
that is about this plugin keeping nothing. And one manual step per release, in
the third check of `docs/manual-checks.md`, where somebody signs in as the
created account with the password that was typed.

Status: both suite replacements exist. The manual step has never been run,
because nothing has been released.

### A test of the plugin behind a real reverse proxy with real certificates

It needs certificates in a machine trust store, which the rule refuses by name,
and a network to make the request through the proxy.

Replaced by unit tests of link construction against forged headers, which is
#50. The risk being covered is that a minting request carrying an attacker's
`Host:` header produces a link pointing at the attacker's server. That is
decidable from the header alone and needs no proxy: the test builds a request
with a forged host and asserts the configured base address comes out.

The invariant lint carries the greppable half of the same rule today. The
`link-built-from-a-request-header` rule in `.github/lint/invariants.sh` refuses
the shape in source, and it has a fixture that trips it:

```
$ bash .github/lint/invariants.sh selftest
```

Status: both parts exist, and the second one is stronger than it was when this
row was written. The lint rule is there.
`AForgedHostDoesNotReachTheLink` in `InvitationLinkTests` is still there too and
is still the weak form: it builds a link while a request carrying a forged host
sits in the same process, and says of itself that the builder never sees that
request, so nothing could have carried the host into the link and the leg could
not have failed.

What moved is that a route now answers with a link.
`AForgedHostDoesNotReachTheMintedLink` in `InvitesControllerTests` forges the
host on the request the mint action is answering and asserts the configured
address in the link that came back, which is the shape this row is about: the
value asserted is one the code produced from a request rather than one produced
beside a request. It was seen to fail, by building the link against a different
address and by returning none at all.

Its bound is one spelling. The forgery is set through the `Host:` header,
because the greppable rules refuse the request object own host member and the
forwarded header names as text anywhere in this tree and take no exemption for a
test. Those spellings are covered by the rules instead, and more widely: no file
here may name them at all.

### A test that an invitation mail arrives

It needs a mail server and a network.

There is no mail path in the plan. The operator copies a link and sends it
themselves, and item 5 in #11 is answered: this plugin never sends an invitation
itself. So this row has nothing to replace today, and the reason is a decision
rather than an open question. If a sending path is ever added it is a milestone
of its own, and the replacement here is a test against a fake transport that
asserts what was handed to it, never a test that anything left the machine.

Status: nothing to cover, and the row exists so that adding a mail path does not
also quietly add a mail server to the suite.

### A test that the dashboard page looks right

It needs a browser and a person, and the person is the part no workflow supplies.
A test that asserts the shape of rendered markup is also a test that breaks on
every styling change and catches nothing, which is the reason this one is
refused twice over.

Replaced by the formatting check over the non-C# files, which is #19, by an
assertion that the page loads no external origin, which is part of #74 for the
setup page and #84 for the configuration page, and by assertions that the page's
own wiring agrees with the code it drives. Between them they cover three things
about the page that are worth failing a build for: that it is readable, that it
fetches nothing from anywhere else, and that every route it calls and every
element it reaches for is one that exists.

The third of those is the one a reader is most likely to mistake for the refused
test, so it is worth being exact about what it sees. It reads the page as bytes
and the controller through its own attributes, and compares the two. It does not
render anything and it says nothing about what a browser makes of the markup.

The second replacement said route-level until the configuration page got one,
and that word was wrong for this half of it. A configuration page is an embedded
resource the dashboard asks for, so what is served is bytes in the assembly and
a test reads them without a route anywhere.

The same paragraph then kept the word for the other half, on the reasoning that
the setup page is a route. That was a prediction rather than a reading and it
went the other way. The setup page landed as an embedded resource too, so the
assertion that it fetches nothing from anywhere else reads compiled-in bytes and
reaches no route at all. What is route-level is the sentence joining the two:
`TheRouteServesThePageWithNothingInItButItsOwnToken` asserts the response body
is those exact bytes with one value written in, and that putting the placeholder
back where that value went gives the compiled-in page again, which is what makes
an assertion about the bytes an assertion about what a browser receives.

Status: all three parts exist for both pages. #19 is closed and the formatter
reads both served pages like every other tracked non-C# file, since nothing in
`.prettierignore` covers either one and the workflow's tree scan matches HTML:

```
$ git ls-files -- '*.html' | grep -v fixtures
Jellyfin.Plugin.Invites/Configuration/configPage.html
Jellyfin.Plugin.Invites/Setup/setupPage.html
```

For the configuration page, `PageFetchesFromNowhereElse` in
`ConfigurationPageTests` refuses four spellings of an address somewhere else and
names the line it found. `PageCallsTheRoutesTheControllerDeclares` in the same
file reads the route template off `InvitesController` and the revoke template
off its `Revoke` action and compares both against the literals the page calls,
and `PageQueriesElementsItActuallyDeclares` holds every identifier the page's
script reaches for against the markup in both directions.

For the setup page, `ThePageFetchesFromNowhereElse` in `SetupPageTests` refuses
the same four spellings, read from the same list rather than a second copy of
it. The wiring half is five assertions rather than two, because the page has no
script to query elements with and carries a form instead:
`TheRouteIsTheOneTheLinkPointsAt` holds the route template against the constant
`InvitationLink` builds its links from, `TheFormPostsBackToWhereItCameFrom`
holds that the form names no address of its own, and
`TheHashInThePolicyIsTheHashOfThePagesOwnStyle` hashes the page's style
independently and requires the served policy to carry that hash.

Two of the five arrived later, with the password rules under #76, and they are
the same half rather than a neighbouring subject: both compare the served bytes
against the type the route enforces.
`ThePageStatesEveryRuleBeforeThePasswordField` in `PasswordRulesTests` requires
every sentence the rules declare to appear on the page ahead of the box a person
types into, and `ThePageQuotesNoOtherNumbers` requires both refusal sentences to
be on the page with the minimum length inside the sentence rather than typed
beside it, so the number somebody is shown and the number they are refused by
are one value. This row said three until they were read back, which is the same
shape as the count below and is why that one is handed to a command.

#74 is open on a clause this row does not cover, and the page it built is what
these read.

### A test that waits for an invitation to expire

It sleeps on a real clock, which the rule refuses by name and for a stated
reason: four behaviours here are clock-driven, and a suite that sleeps gets
slower until people stop running it.

Replaced by the injected clock, which is #41, and the boundary cases in #104.
The clock seam is what turns every one of those four behaviours from a wait into
an assignment, so the replacement is not a weaker version of the refused test.
It is a stronger one, because a sleep can only test the far side of a boundary
and an injected clock can test the instant itself.

Status: the seam exists and three of the four behaviours are driven by it. #41
is closed, `IClock` and `SystemClock` are in the tree, and
`TheExpiryBoundaryIsExclusive` in `RedemptionDecisionTests` asserts the expiry
one tick before the boundary, at it, and one tick after, without sleeping.
`ClockJumpTests` steps the clock backwards across an expiry and forwards past
several at once.

The rate-limit window and the retention sweep arrived after this line was last
read against the tree, and both are driven by the seam rather than by a wait.
`ClockBoundaryTests` holds them. `ThePerAddressWindowTurnsAtTheBoundary` and
`TheGlobalWindowTurnsAtTheBoundary` sit on the two limiter windows,
`TheRetentionBoundaryIsTheDirectionThisPageStates` sits on the retention
boundary and takes the direction off the page that decides it rather than off
the routine, and the same class carries a backwards step and a jump past
several boundaries for each of the two.

#104 is open on the fourth behaviour rather than on three. There is no account
expiry in this plugin, optional or otherwise, and whether one is built at all
is #68.

### A test that this plugin works beside every supported sibling

It would install the whole supported set of plugins into one server and look for
collisions: two plugins claiming one route, two scheduled tasks with one name,
two writers over one configuration key. It needs a server binary and a media
directory, like the row above, and it needs every sibling built and installed
beside this one.

The reason it is refused today is narrower than the headless rule and it is
worth reading as the whole reason: there is no sibling. The supported set is
empty, so the run would install one plugin, find no collision, and report the
colour of a run that looked at nothing. That is the failure this page exists
against, in the register that is least able to afford it: a green square whose
subject was empty.

Replaced, for now, by the half that is decidable without a second plugin. This
plugin consumes no sibling, which is what makes the absence of one a
non-event rather than a degradation to handle, and
`NoSiblingIsConsumedTests` refuses the two ways that could stop being true:
a reference to another plugin assembly, and a second file in the artefact list
`build.yaml` ships. The alone case is the row above, with its ABI floor
build, its packaging job and its manual install.

**This refusal names its own end.** It expires the day the first supported
sibling ships. On that day the set is no longer empty, a run over it is a run
over something, and this row is deleted rather than reworded. Nothing else on
this page has an end condition that specific, which is what makes this one a
refusal rather than a gap with a justification attached.

The list of siblings, when there is one, lives in this repository. Not on a
tracker: a test that fetches what to test over the network fails for reasons
unrelated to the code, and the headless rule that `headless.yaml` executes
refuses the fetch outright.

Status: the replacement exists. #44 is where the refusal was decided and what it
is waiting for is a sibling rather than any further work here.

## What this list is measured against

This section counted four of six replacements absent, against a suite of two
files holding two facts about the template's plugin class. Both numbers have
moved, and one number is no longer the right shape of answer: most rows carry
more than one replacement and the rows are no longer all in one state, so a
single count hides which half of a row is missing.

Row by row, which is the same thing each status line above says at more length.
The setup page has neither of its two, because #107 is open and no manual check
has been recorded. The real-server row has its ABI floor build, its
packaging job and the jobs that install the packaged artefact into a published
server, and not its manual install. The sign-in row has both of its suite
replacements and not its manual step, and it is the row whose two halves are
furthest apart: what the suite holds is that the right member is called, and
what the manual step would answer is the only question a person actually asks of
an invitation, which is whether they can sign in afterwards. The reverse-proxy
row has both. The mail row has nothing to replace and
says so. The dashboard row has all three for both pages. The sleeping row has
the seam and the boundary cases for three of its four behaviours, and not the
fourth, which does not exist. The sibling row has its replacement, and that
replacement is deliberately smaller than the test it stands for: it says
nothing about a collision and everything about there being nothing to collide
with.

Two rows name the setup page and they are in opposite states, which is the pair
a reader is most likely to collapse. The browser row is about whether the page
renders, and nothing in the suite can see that. The dashboard row is about
whether the page fetches from somewhere else and agrees with the code it drives,
and that is decidable from bytes. A page existing moved the second row and left
the first exactly where it was.

The suite those replacements join is not the two files this section used to
count:

```
$ git ls-files -- '*.cs' ':!.github/lint/fixtures' | grep -c 'Tests/'
```

The output is not written here. It was, as thirty-four, and the command answered
with a larger number by the time it was next run. Nothing reads a count in a
document, the check that re-runs a pasted command judges a pasted exit status
and says of itself that it does not judge pasted output. This section's point is
that the suite has grown rather than that it is any particular size, and the
command says that better than a number somebody has to keep true.

This list is what the suite is built against as it grows, and #100 is where the
statuses are read against the tree again.

## When a refusal is added

Another row is added the same way. Name the test somebody would reasonably
write, name the clause of the headless rule that refuses it, name what covers
the same risk instead, and say whether that replacement exists today. A row
whose replacement column says nothing is not a refusal. It is a gap with a
justification attached, and the two are worth telling apart.
