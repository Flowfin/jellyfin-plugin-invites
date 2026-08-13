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

Status: the lint rule exists. #50 is open, so the unit test does not.

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

Replaced by the formatting check over the non-C# files, which is #19, and by an
assertion that the page loads no external origin, which is part of #74 for the
setup page and #84 for the configuration page. Between them they cover the two
things about the page that are worth failing a build for: that it is readable,
and that it fetches nothing from anywhere else.

The second replacement said route-level until the configuration page got one,
and that word was wrong for this half of it. A configuration page is an embedded
resource the dashboard asks for, so what is served is bytes in the assembly and
a test reads them without a route anywhere. The setup page is a route and its
half of this row stays route-level.

Status: both parts exist for the configuration page and neither does for the
setup page. #19 is closed and the formatter reads the configuration page like
every other tracked non-C# file, since nothing in `.prettierignore` covers it.
`PageFetchesFromNowhereElse` in `ConfigurationPageTests` refuses four spellings
of an address somewhere else and names the line it found. The setup page has no
test because it has no page, which is #74 and is open.

### A test that waits for an invitation to expire

It sleeps on a real clock, which the rule refuses by name and for a stated
reason: four behaviours here are clock-driven, and a suite that sleeps gets
slower until people stop running it.

Replaced by the injected clock, which is #41, and the boundary cases in #104.
The clock seam is what turns every one of those four behaviours from a wait into
an assignment, so the replacement is not a weaker version of the refused test.
It is a stronger one, because a sleep can only test the far side of a boundary
and an injected clock can test the instant itself.

Status: neither exists. #41 and #104 are open.

## What this list is measured against

Four of the six replacements do not exist. That is a statement about the state
of this repository rather than about the list, and it is written here rather
than left to be discovered:

```
$ git ls-files -- '*.cs' ':!.github/lint/fixtures' | grep -c 'Tests/'
2
```

Two test files, holding two facts about the template's plugin class. This list
is what the suite is built against as it grows, and #100 is where the count of
existing replacements is checked again.

## When a refusal is added

A seventh row is added the same way. Name the test somebody would reasonably
write, name the clause of the headless rule that refuses it, name what covers
the same risk instead, and say whether that replacement exists today. A row
whose replacement column says nothing is not a refusal. It is a gap with a
justification attached, and the two are worth telling apart.
