# Tests this plugin refuses to write

Six obvious tests are refusals here. Each one would be a reasonable thing to
add, each one would break the headless rule in `CONTRIBUTING.md`, and each one
is therefore replaced by something narrower rather than dropped.

Writing the list down is what stops the sixth of them arriving in good faith
next year, with a comment explaining that it only needs a browser. A refusal
nobody recorded is a refusal that gets reversed by whoever did not know it was
one.

Every row below names the refusal, the part of the headless rule it breaks, and
what covers the same risk instead. Where the replacement does not exist yet, the
row says so and names the issue that builds it, because a replacement written
in the future tense covers nothing today.

## The rule these are refused by

The headless rule is in `CONTRIBUTING.md` and it is executed rather than
trusted: `.github/workflows/headless.yaml` runs the suite inside a container
with no network interface, as an unprivileged user. A test that reaches out
fails there rather than on somebody's next machine.

The rule refuses a test that opens a window or needs a display, asks for
elevated rights, writes outside a temporary directory it owns, reads or writes
the machine's certificate stores, opens a network connection, launches an
external binary, or sleeps on a real clock.

## The six

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

Replaced by three things that each cover part of it. The ABI floor build in
`.github/workflows/abi-floor.yaml` compiles the plugin against the package
version the manifest's `targetAbi` names, so a call into a server member that
arrived after the floor is caught at build time rather than at redemption time.
The packaging job in `.github/workflows/package.yaml` builds the artefact the
manifest lists. And one manual install per supported server line before a
release, recorded in `docs/manual-checks.md`.

Status: the two workflows exist. The manual install has a place to be recorded
and nothing recorded in it.

### A test of the plugin behind a real reverse proxy with real certificates

It needs certificates in a machine trust store, which the rule refuses by name,
and a network to make the request through the proxy.

Replaced by unit tests of link construction against forged headers, which is
#50. The risk being covered is that a minting request carrying an attacker's
`Host` header produces a link pointing at the attacker's server. That is
decidable from the header alone and needs no proxy: the test builds a request
with a forged host and asserts the configured base address comes out.

The invariant lint carries the greppable half of the same rule today. The
`link-built-from-a-request-header` rule in `.github/lint/invariants.sh` refuses
the shape in source, and it has a fixture that trips it:

```
$ bash .github/lint/invariants.sh selftest
```

Status: both parts exist. The lint rule is there, and the unit test is
`AForgedHostDoesNotReachTheLink` in `InvitationLinkTests`, which builds a link
while a request carrying a forged host sits in the same process and asserts the
configured address comes out. #50 is open, and it says of that test itself that
the builder never sees the request, so nothing in the process could have
carried the forged host into the link and the test could not have failed. What
this row asks is that the replacement exist, and it does at that strength.

### A test that an invitation mail arrives

It needs a mail server and a network.

There is no mail path in the plan. The operator copies a link and sends it
themselves, and whether that ever changes is item 5 in #11, which is not
answered. So this row has nothing to replace today. If a sending path is ever
added, the replacement is a test against a fake transport that asserts what was
handed to it, and never a test that anything left the machine.

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
`TheRouteServesThePageUnchanged` asserts the response body is those exact bytes,
which is what makes an assertion about the bytes an assertion about what a
browser receives.

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

Status: the seam exists and one of the four behaviours is driven by it. #41 is
closed, `IClock` and `SystemClock` are in the tree, and
`TheExpiryBoundaryIsExclusive` in `RedemptionDecisionTests` asserts the expiry
one tick before the boundary, at it, and one tick after, without sleeping.
`ClockJumpTests` steps the clock backwards across an expiry and forwards past
several at once. #104 is open because the other three clock-driven behaviours,
the rate-limit window, the retention sweep and the optional account expiry, are
not in the tree to be driven.

## What this list is measured against

This section counted four of six replacements absent, against a suite of two
files holding two facts about the template's plugin class. Both numbers have
moved, and one number is no longer the right shape of answer: most rows carry
more than one replacement and the rows are no longer all in one state, so a
single count hides which half of a row is missing.

Row by row, which is the same thing each status line above says at more length.
The setup page has neither of its two, because #107 is open and no manual check
has been recorded. The real-server row has its two workflows and not its manual
install. The reverse-proxy row has both. The mail row has nothing to replace and
says so. The dashboard row has all three for both pages. The sleeping row has
the seam and the expiry boundary cases, and not the three behaviours that do not
exist.

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

A seventh row is added the same way. Name the test somebody would reasonably
write, name the clause of the headless rule that refuses it, name what covers
the same risk instead, and say whether that replacement exists today. A row
whose replacement column says nothing is not a refusal. It is a gap with a
justification attached, and the two are worth telling apart.
