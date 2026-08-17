# The redemption limiter, and what a restart does to it

The redemption endpoint is unauthenticated by construction. A stranger has to be
able to reach it or the plugin has no purpose, so it is the one route on the
server where somebody gets free attempts at a secret. A limiter belongs there.

This page settles one thing about that limiter, and it is the thing that decides
what the limiter is allowed to be worth: where the counter lives, how long it
lives, and what a restart does to it. It is written before the limiter exists,
because the answer changes what numbers may later be chosen, and a lifetime read
off an implementation somebody already wrote is a lifetime nobody decided.

Nothing here is built, and the reason has moved. This paragraph said there is no
endpoint to limit, which was true when it was written and was overtaken without
the sentence moving. There is an endpoint a stranger can reach:

    git grep -lE 'ControllerBase|ApiController|HttpGet|HttpPost' -- 'Jellyfin.Plugin.Invites/*.cs' ; echo "exit=$?"
    exit=0

`RedeemController` serves the setup page anonymously, so the address the limiter
belongs on exists. What it still does not do is read an invitation or decide
anything, so there is no attempt to count: the same bytes come back for a code
that was never minted as for a live one, and the routine that would tell them
apart has no caller.

    git grep -n 'Decide(' -- 'Jellyfin.Plugin.Invites/*.cs' ':!*RedemptionDecision.cs'
    exit=1

## The counter lives in memory, in the plugin's process

Not in the invitation store, not in a file beside it, and not in the server's
database.

The store is the wrong home for two reasons that are separate. It is the file
the redemption path writes under, so a counter in it turns every refused guess
into a write, and an endpoint a stranger can hammer becomes a disk the stranger
can drive. It is also the file `docs/personal-data.md` holds to an inventory,
and the counter's key is the source address, which that page keeps out of
anything durable on purpose.

The server's database is not this plugin's to grow a table in, and a limiter is
the last thing that should be reaching for one.

So the counter is a process-lifetime structure, keyed by source address for the
per-address limit and by nothing for the global one, and its windows are read
through the clock seam rather than off the machine, in the shape every other
timed behaviour here already takes:

    git grep -n 'interface IClock' -- Jellyfin.Plugin.Invites/Time/IClock.cs
    Jellyfin.Plugin.Invites/Time/IClock.cs:15:public interface IClock

## A restart clears it, and so does anything that recycles the process

A server restart, a plugin reload, an application pool recycle and a crash all
produce the same state: an empty counter. Whoever was three guesses from a
lockout is back to zero, and nothing anywhere records that they were ever
counted.

That is stated here rather than discovered, because it is the property somebody
meets first as a bug report. An operator who watches a lockout disappear after a
restart is looking at the design and not at a fault.

The same sentence covers a case that is not a restart at all. Two processes in
front of one server do not share a counter, so a limit of ten is a limit of ten
per process. `docs/disaster-cases.md` refuses two servers over one store at
startup for reasons of its own, and a limiter that each of them would count
separately is one more thing that refusal keeps out rather than a second defence
against it.

## Why a counter anybody can reset is still the right one

Because the limiter is not what keeps the keyspace out of reach. The code is.

`docs/code-entropy.md` sizes a code against two scenarios, one with a limiter
and one without, so the answer survives the limiter being absent. Re-run at the
commit this page lands on:

    $ awk 'BEGIN{ l2=log(2); N=10000; A1=10000*315360000; A2=10*31536000;
        printf "required bits, unthrottled = %.2f\n", (log(A1)+log(N))/l2 + 32;
        printf "required bits, throttled   = %.2f\n", (log(A2)+log(N))/l2 + 32;
        printf "P at 128 bits, unthrottled = 2^%.1f\n", (log(A1)+log(N))/l2 - 128; }'
    required bits, unthrottled = 86.81
    required bits, throttled   = 73.52
    P at 128 bits, unthrottled = 2^-73.2

A code carries 128 bits. The unthrottled requirement is eighty-seven, so the
code clears the harder of the two by forty-one bits, and the chance that any
guess ever lands over a ten-year run at ten thousand guesses a second with no
limiter at all is `2^-73.2`.

An attacker who resets the counter by waiting for a restart therefore buys
themselves the unthrottled row, and the unthrottled row is already the one the
code was sized against. That is what makes an in-memory counter honest here. It
would not be honest on a code chosen to be read out over the phone, and
`docs/code-entropy.md` says the same thing from the other end: a short code is
unsafe by dependency rather than by arithmetic, because it moves the guarantee
onto a runtime control that can be misconfigured, restarted, or outrun.

## What the limiter is for instead

Three things, none of which is the keyspace.

It keeps the trail readable. Every attempt that reaches the decision appends an
entry, and `docs/attempt-outcomes.md` bounds the failures for exactly this
reason, dropping the oldest first. The limiter is what keeps that bound from
being reached, so the entries an operator wanted to see are still there.

It keeps the server's own work bounded. Every guess that gets past the shape
check costs a lookup, and a lookup is a keyed hash the plugin has to compute.

It turns a grind into something an operator can see. An invitation refused
eleven times is a row in the operator's view, and a limiter is what makes eleven
the number rather than eleven million.

## The rule that follows from all of this

**The limiter may never become the thing the arithmetic rests on.** If `N` moves
because #33 sets a higher ceiling on live invitations, or if a shorter code is
ever wanted, the repair is the code length and not a counter that survives a
restart. Making the counter durable would move the guarantee onto a control that
an attacker resets by waiting, an operator resets by upgrading, and a second
instance never had.

That is the reason to write this page before the numbers exist rather than
after. A number chosen first invites a lifetime chosen to make the number look
load-bearing.

## What the counter holds about a person, and for how long

The source address, for as long as its window and no longer.
`docs/personal-data.md` is the page that owns this and it decides both halves:
the limiter needs the address while a request is being decided, and the attempt
trail does not carry it. Seeing a value and holding it are different things, and
only the first happens here.

    git grep -n 'memory for as long as its window' -- docs/personal-data.md
    docs/personal-data.md:96:memory for as long as its window and no longer, and that the trail does not

## What this page does not decide

The two numbers. The per-address limit and the global limit are chosen with the
endpoint that enforces them, and `docs/code-entropy.md` already consumes one of
them as an assumption, ten guesses a second across all sources, which it names
as #31's to confirm or move. Nothing above depends on either number: the
lifetime argument holds at any threshold, because it is an argument about what
happens when the counter is empty.

What a refused attempt looks like. That is `docs/refusal-response.md`, which
holds the wording and the byte-for-byte requirement for every case including
this one, and a limiter that answered differently would be the oracle the whole
set exists to close.

Whether a throttled attempt appends a trail entry at all.
`docs/attempt-outcomes.md` carries both directions and what each costs, and it
is one answer for the two pages rather than one each.

## What is not claimed

No limiter has been written, so nothing here has been measured against one. This
is a decision about a component that does not exist, and it is enforced by
nothing: no check reads this page, and an implementation that persisted the
counter to disk would pass every workflow in this repository.

The arithmetic quoted above is re-run from `docs/code-entropy.md` and is the same
model, with the same inputs and the same two assumptions it names about an
attacker. Nothing here measures an attempt rate anybody has observed against a
Jellyfin server.
