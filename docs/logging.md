# What is logged, and what may never be

A server log is copied further than the data directory it sits next to. It goes
into a support thread with a screenshot attached, into a log collector somebody
set up once, into a backup, and to whoever is helping with an unrelated problem
that day. Whatever this plugin writes there is disclosed to all of them, so the
never list is decided before there is a log call to argue with.

Nothing in this plugin writes a log line about an invitation today. Every rule
below is a constraint on code that #43, #56 and the routes in M8 have yet to
write, and the one greppable half of it is in `.github/lint/invariants.sh`.

## The never list

Four values, in any form, at any level, including a level nobody turns on in
production. The second row is the first one spelled out, because a truncated
code reads as a safe thing to write and is not.

| Never logged | Why, and the shape somebody writes by accident |
| --- | --- |
| The invitation code | It is a bearer credential for account creation. A log holding one is that credential written to disk in clear, readable by everybody the log reaches, for as long as the invitation is live. |
| A part of the code | A prefix, a suffix or a truncated form written so a line is greppable. `docs/threat-model.md` sets the entropy from a search space, and every character disclosed comes off that exponent. Half a code in a log is not half a disclosure. |
| The full invitation link | A link is the code with a host in front of it. This is the row that gets missed, because the variable is usually called something like `url` and reads as a location rather than as a secret. |
| The hash secret | It is what makes a stored hash unusable to whoever reads the store. In a log it is next to nothing else that matters, and #30 owns its life cycle. |
| The password an invited person chooses | The plugin hands it to the server and never holds it. A log line is holding it. This includes an error message and an exception carrying the value it refused. |

At any level is part of the rule rather than an aside. `Trace` and `Debug` are
where this goes wrong, because the line is written while somebody is debugging
and it survives into a release nobody re-read.

## What is logged instead

Logging nothing is also a failure. An operator who finds an account they do not
recognise has one question and it has to be answerable from the log alone:
which invitation produced it, when, and what the plugin decided.

| Logged | Where the shape is settled |
| --- | --- |
| The invitation identifier | `docs/personal-data.md`, first row of the invitation record |
| The outcome of the attempt | `docs/attempt-outcomes.md`, one value from a fixed set |
| When it happened | `docs/personal-data.md`, the attempt trail |
| The operator who minted, where a line is about minting | `docs/personal-data.md`, the invitation record |

Those are pointers rather than a second copy of the field list. A log line may
carry a value only where that value is a row in the inventory, and the
inventory is the authority for what the plugin holds at all.

The outcome in particular is a value from a fixed set and never free text. That
is decided in `docs/attempt-outcomes.md` for the trail, and it holds for the log
for the same reason: nothing anybody types reaches the field, so nothing typed
by a stranger reaches a log a support thread will paste.

## The identifier is what makes both halves possible

The never list and the useful log line only coexist because an invitation has a
name that is not its code. That identifier is decided in
`docs/personal-data.md`, and the rule here is the consequence: every log line
and every administrator view names an invitation by its identifier, and there is
no other way for either to point at one.

## The source address is not logged

`docs/personal-data.md` refuses to hold the address a redemption arrived from.
#31 needs it while it decides whether to refuse an attempt and needs nothing of
it afterwards, so it is seen and not kept.

A log line carrying it would keep it, and keep it somewhere copied more widely
and pruned less carefully than the trail. A field refused in the record cannot
be admitted through the log, or the log is the store with none of the store's
decisions attached to it.

## The greppable rule is narrower than this list

`secret-in-a-log-call` in `.github/lint/invariants.sh` is decided by #32 and is
what a machine refuses. It matches spellings, so it is not this document and
does not become it:

```
$ git grep -n 'secret-in-a-log-call@' -- .github/lint/invariants.sh
```

The alternation holds four words. A log call whose argument is named `code`,
`url` or `link` passes it, and those are the ordinary names for the first three
rows above. So a green `Invariant lint` says none of the four spellings appear,
which is a much smaller claim than the never list holding.

Widening the pattern is deliberately not done here. There is no invitation code
in the tree to measure a wider rule against, and `code` appears in ordinary C#
that has nothing to do with an invitation, a status code first. A rule that
fires on a status code is a rule somebody switches off, and switching it off
costs the four spellings it does catch. The measurement to make it wider is
available once #43 and #56 have written real log calls, and #32 is where it is
held.

## What this document does not settle

Nothing enforces the list beyond the one rule above. No check reads this file,
no check counts log calls, and a line written against every rule here would
reach the mainline if it avoided four words.

It also does not decide the log level of anything, or whether the trail and the
log are the same write. Both belong with #43, which builds the trail, and both
are constrained by this document rather than described by it.
