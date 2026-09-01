# The coverage floors, and what each number is for

A single number over the whole assembly is a number people raise by testing the
easy parts. There is a number per area here, and each one is set from what the
area is worth rather than from what it happens to measure today.

`.github/lint/coverage-floors.sh` is what refuses. This page is what the number
means, and it is the file to argue with.

## What a floor is not

It is not a target. Nothing here says an area at its floor is well covered, and
an area sitting exactly on its number is an area to look at rather than one that
has passed. A floor stops a slide; it does not measure a suite.

It is also not a measure of whether the tests are any good. Coverage says a line
ran, not that a test would notice if that line were wrong. The measure that says
the second thing is mutation testing, which is #22, and where the two disagree
the mutation result is the one that means something. This gate is what stops the
coverage collapsing between mutation runs.

## The areas

Line coverage, as whole percentages. An area at 89.9 has not met a floor of 90.

| Area | Floor | Why that number |
| --- | --- | --- |
| Redemption decision | 95 | Every uncovered branch here is a rule about whether an invitation is honoured that nobody has checked. The five points below complete are for a line no test can reach, not for a decision nobody asserted. |
| Attempt trail | 95 | What an operator reads when an account they do not recognise appears, and the only place the four refusals a stranger cannot tell apart are told apart. High for the same reason as its neighbour: an uncovered branch here is either an entry nobody asserted the shape of or a drop nobody watched happen, and the bound is a defence rather than a convenience. |
| Codes | 95 | The code is the credential. An uncovered branch in minting or canonicalising is a path by which a code is weaker or a presented code is read differently than the stored one. |
| Invitations | 90 | The record and the mint. The refusals on the type are what stop a half-written record existing at all, and they are cheap to reach. |
| Account template | 90 | What an invitation is worth to the account it creates. The field-by-field assertion in #69 is the control here, and this row said the floor keeps the type's own refusals exercised while that is written. It is written: `AccountTemplateApplicationTests` asserts every field of the server's policy after a template is applied. The floor stays what it is, for the reason under `## What a floor is not` above: an area at its floor is not well covered, and the arrival of a control is not a reason to lower the number under it. |
| Store | 80 | Lower than its neighbours for a reason that is not about how much it matters. Four of its legs are file-permission tests that skip on a platform with no file modes, so the same suite measures this area differently on a developer's machine and on the runner. The floor is set to hold on the platform where it measures lowest. Migration, which #108 names separately, is the store version and its refusal, and it is inside this area. |
| Startup | 85 | The load that claims the store and reports what it disagrees with. Lower than the decision because one of its branches is a plugin with no data directory, which is a state the server is not expected to produce. |
| Server line | 90 | The comparison that decides whether this plugin runs at all on the server it was loaded into, from #97. An uncovered branch here is either a server the plugin would refuse without anybody having said so or, worse, one it would carry on against. Four lines of the area are the seam over the server's own application host, which reads one property and needs a running server to construct; those are what the ABI floor build and the end-to-end install job stand in for, and the floor is set below the measured figure by roughly that much rather than at it. |
| Clock | 90 | Two files, one of which exists to be the only place the machine clock is read and is deliberately never steered by a test. |
| Controllers | 70 | Moderate, because the logic in a route is thin by design and the authorization on it is held by the inventory test from #83 rather than by coverage. The row said no controller existed and that the gate reported the area as `empty`; routes answer there now, so the floor has an area and the sentence describing it as empty is gone rather than left to be read as current. It said five and the count was already six when rotation landed, so the number is derived by whoever wants it, with `git grep -cE '^    \[Http(Get|Post)' -- 'Jellyfin.Plugin.Invites/Controllers/*.cs'`, rather than typed here to go stale again. |
| Setup page | 90 | The page a stranger is served, and the policy it is served under. High because the whole area is a routine deriving a content security policy from bytes, and an uncovered branch in it is either a policy nobody built or a refusal nobody reached. It is separate from the settings page below because that one is markup with no decision in it and this one is markup plus the derivation that describes it. |

## What has no floor, and why

| Not measured against a floor | Reason |
| --- | --- |
| The settings class and its page | The configuration class carries values and no decisions, and the page it is edited on is markup. A floor over either is a floor met by a test that constructs a settings object, which proves nothing about the settings being right. The formatting check and the assertion that the page loads no external origin are what hold that surface. |

The exclusion is written here rather than achieved by leaving the area out of
the list. An area that is simply absent from a gate is indistinguishable from
one somebody forgot, and the difference is the whole point of writing it down.

## The mistake this gate is built against

A coverage filter that matches nothing is scored as 100% by the collector, not
as 0%. So a namespace with one character wrong reports a perfect score for an
area nobody measured, and it reports it every run, forever.

The gate refuses that: after each area's run it requires the report to name at
least one file under that area's directory. Its selftest proves both that
refusal and the floor comparison itself on every run, against the real suite,
because a fixture for a coverage number would be a fixture of the script's
arithmetic rather than of the thing it reads.

## Where the numbers came from

Each floor is a judgement, and every one of them is below what the suite reaches
today. That gap is deliberate and it is not slack to be spent: it is what stops
an ordinary refactor turning red before the test that covers its new branch
lands. Lowering a floor is a change to this file and is argued here, which is
the difference between a floor and a number somebody edited to make a run pass.

## What this page does not settle

Nothing here is required of a merge. The gate reports and does not block, because
the required set on the default branch is #24's and is still the three the
repository was created with.

Branch coverage is measured by the same run and is not compared against
anything. Line coverage is the weaker of the two and it is what these floors are
written in, which is worth knowing before reading a green run as a statement
about branches.
