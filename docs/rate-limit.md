# The redemption limiter, and what a restart does to it

The redemption endpoint is unauthenticated by construction. A stranger has to be
able to reach it or the plugin has no purpose, so it is the one route on the
server where somebody gets free attempts at a secret. A limiter belongs there.

This page settles what the limiter is allowed to be worth, in the order the two
halves have to be taken in. First where the counter lives, how long it lives,
and what a restart does to it, which was written before the limiter existed
because a lifetime read off an implementation somebody already wrote is a
lifetime nobody decided. Then the two thresholds, which are read off that
lifetime and off the arithmetic in [docs/code-entropy.md](code-entropy.md)
rather than chosen because they looked reasonable.

Nothing here is built, and the reason has moved. This paragraph said there is no
endpoint to limit, which was true when it was written and was overtaken without
the sentence moving. There is an endpoint a stranger can reach:

    git grep -lE 'ControllerBase|ApiController|HttpGet|HttpPost' -- 'Jellyfin.Plugin.Invites/*.cs' ; echo "exit=$?"
    exit=0

`RedeemController` serves the setup page anonymously, so the address the limiter
belongs on exists.

THIS PARAGRAPH SAID THAT ROUTE READS NO INVITATION AND DECIDES NOTHING, SO THERE
IS NO ATTEMPT TO COUNT. Its post decides one on every submission, and it asks
this limiter before it does:

    git grep -n 'Decide(' -- 'Jellyfin.Plugin.Invites/*.cs' ':!*RedemptionDecision.cs'
    exit=0

    git grep -n 'RedemptionDecision.Decide' -- 'Jellyfin.Plugin.Invites/*.cs'
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:645:            var verdict = RedemptionDecision.Decide(presented, hash, contents.Invitations, now);

What is unchanged is the GET. The same bytes still come back for a code that was
never minted as for a live one, because that action reads no invitation, and its
refusal half is #75 and #77. So a browser following a link spends no allowance
and a form being posted spends one, which is the split the numbers below are
sized against.

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

That is the reason the lifetime above was settled before any number was chosen.
A number chosen first invites a lifetime chosen to make the number look
load-bearing, and the section below is written under that rule rather than
beside it.

## The two numbers

**Per source address, twenty attempts an hour. Across all sources, ten attempts
a second.** Both are counted in fixed windows read through the clock seam above,
and both leave with the process, which is the lifetime already settled.

An attempt is a presented code being judged. Fetching the setup page is not one.
That route reads no invitation and decides nothing, so counting it would count
somebody opening their link twice.

Neither number moves anything above it. The lifetime argument is about what
happens when the counter is empty, and it holds at any threshold.

### Where twenty an hour comes from

From what an invited person needs, not from the keyspace. The code arrives in
the link rather than being typed, so a redemption that fails does so because the
username is taken or the password was refused, and the person submits again.
Four people behind one household address, each meeting three refusals before
they get through, is sixteen attempts over an evening rather than inside one
hour.

Twenty an hour is above that and nowhere near what a search needs. One address
at that rate for a year is 175,200 attempts, which against ten thousand live
invitations asks for sixty-three bits where the code carries a hundred and
twenty-eight.

### Where ten a second comes from

It is the number [docs/code-entropy.md](code-entropy.md) already assumes for its
throttled row, and which it names as #31's to confirm or to move. It is
confirmed rather than moved, so that page's second row still reproduces and none
of its arithmetic has to be redone for this one.

Re-run here with the per-address rows beside it, the same model and the same
inputs:

    $ awk 'BEGIN{ l2=log(2); N=10000;
        one=20*8760; spread=10000*20*8760; all=10*31536000;
        printf "one address, 20 an hour for a year    = %.2f bits\n", (log(one)+log(N))/l2 + 32;
        printf "10^4 addresses, 20 an hour for a year = %.2f bits\n", (log(spread)+log(N))/l2 + 32;
        printf "all sources, 10 a second for a year   = %.2f bits\n", (log(all)+log(N))/l2 + 32; }'
    one address, 20 an hour for a year    = 62.71 bits
    10^4 addresses, 20 an hour for a year = 75.99 bits
    all sources, 10 a second for a year   = 73.52 bits

### Why there are two of them, in the same units

The middle row is the per-address limit obeyed exactly, by ten thousand
addresses at once. That is what a spread of sources buys an attacker without
breaking any rule this page sets, and it asks for 75.99 bits against the global
row's 73.52. So the per-address limit on its own is the weaker of the two by two
and a half bits, which is a measurement rather than an argument, and the global
limit is what closes that gap.

The reverse is why the per-address limit is not dropped in favour of the global
one alone. One source able to spend the whole global allowance takes the
operator's real invitees down with it, and that is the denial of service #31
names as what a global limit alone produces.

Neither row is load-bearing. Both sit far under the hundred and twenty-eight
bits the code carries, and the unthrottled row is the harder requirement anyway,
which is the rule this section is written under rather than a happy accident.

### What a fixed window costs, in the same units

A fixed window lets somebody run at twice the stated rate across a boundary, by
spending one window's allowance at its end and the next one's at its start.
Doubling the attempts adds exactly one bit to the requirement, against the
forty-one bits of headroom the code carries. That is the whole cost of the
simpler counter, and it is why the window is fixed rather than sliding.

### A challenge is not one of the defences, and that costs something

Item 10 in #11 answers whether bot defence is in scope. It is, as the limiter on
this page and as the lockout beside it, and not as a challenge. No captcha, and
nothing else that asks the person to prove themselves to a third party.

Two reasons, and the second is the one an operator should weigh. A challenge
either binds a third party into the redemption path of a self-hosted server, or
it needs a surface this plugin does not have, and the presentation rules in
[docs/setup-never-asks.md](setup-never-asks.md) already refuse the shape it
arrives in. And it puts somebody who was invited by name in the position of
proving themselves to a party the operator did not choose, which is the wrong
thing to ask of an invitation.

What that gives up, said plainly rather than left for a support thread. Rate
limiting alone does not stop a determined attacker with many source addresses.
The per-address number is what one address gets, so an attacker spread across
enough of them buys attempts in proportion to how many they have, and the global
number is the only thing standing behind that. What actually bounds the guess is
the entropy in [docs/code-entropy.md](code-entropy.md); the limiter narrows the
window, and it is not the thing making the code hard to find.

### What is deliberately not a number here

There is no lockout that outlives its window. Being refused for the rest of the
hour is what the per-address limit is, and a lockout surviving longer than the
counter would be a durable counter under a different name, which the rule above
refuses.

What a refused attempt says. That is
[docs/refusal-response.md](refusal-response.md), and the requirement that a
throttled answer be indistinguishable from an ordinary refusal cannot be met
until something serves an ordinary refusal at all.

## What the counter holds about a person, and for how long

The source address, for as long as its window and no longer.
`docs/personal-data.md` is the page that owns this and it decides both halves:
the limiter needs the address while a request is being decided, and the attempt
trail does not carry it. Seeing a value and holding it are different things, and
only the first happens here.

    git grep -n 'memory for as long as its window' -- docs/personal-data.md
    docs/personal-data.md:215:memory for as long as its window and no longer, and that the trail does not

The line moved from 149 to 174 when the paragraph above it was repaired, from
174 to 175 when #61 added the template grant's row above it, from 175 to 186
when the post landed and the sentence about nothing taking a submission was
corrected above it, and from 186 to 193 when the post began comparing the two
copies of the password and the correction underneath that row was replaced by
the row saying so. The sentence this points at is not one of the bytes that
changed any of those times. It is re-pasted
here rather than renumbered quietly, because a corrected number with nothing
said about it reads exactly like one that was right all along.

## What this page does not decide

What a refused attempt looks like. That is `docs/refusal-response.md`, which
holds the wording and the byte-for-byte requirement for every case including
this one, and a limiter that answered differently would be the oracle the whole
set exists to close.

THIS PAGE LISTED WHETHER A THROTTLED ATTEMPT APPENDS A TRAIL ENTRY AMONG WHAT IT
DOES NOT DECIDE, AND THAT QUESTION IS ANSWERED. It still is not decided here:
`docs/attempt-outcomes.md` carries the answer and it is one answer for the two
pages rather than one each. What that page settles, in one sentence so a reader
of this one need not go and get it, is that the trail records the throttling
rather than the requests - one entry when a source starts being refused against
an invitation, carrying how many attempts it covers, and not another until that
episode ends.

The half that is this page's is the consequence for the limiter: the write
happens on the state transition rather than on the event, so the limiter's own
refusals do not become the write path the trail's bound exists to close.
`AttemptLimiter` does not append anything today. THAT USED TO REST ON NOTHING
CALLING IT, and the post calls it now; what is left is that there is no trail on
disk to append to, and that whether a throttled attempt appends an entry at all
is one answer between #31 and #43 that nobody has given.

## What is written now, and what of this page it holds

This paragraph said no limiter had been written, that nothing enforced any of
this, and that an implementation counting to a different pair of numbers would
pass every workflow in this repository. `AttemptLimiter` landed under #31 and two
of those three have moved.

    git grep -n 'public const int PerAddressCeiling\|public const int GlobalCeiling' -- Jellyfin.Plugin.Invites/Redemption/AttemptLimiter.cs
    Jellyfin.Plugin.Invites/Redemption/AttemptLimiter.cs:85:    public const int PerAddressCeiling = 20;
    Jellyfin.Plugin.Invites/Redemption/AttemptLimiter.cs:90:    public const int GlobalCeiling = 10;

Something reads this page as well. `AttemptLimiterTests` matches the sentence
under `## The two numbers` above, resolves the words in it, and compares them
against those two constants, so a number moved in the source without being moved
here turns the suite red. Its bound is stated on itself and is the part to read
before that is trusted: it reads one sentence matched by its shape, and nothing
judges whether the argument around the sentence still supports the number.

The lifetime is held too, by there being nothing durable to hold: the type takes
the clock and nothing else, and a test reads its own members back to say so. A
registration handing out a limiter per request would give every attempt an empty
counter while passing every assertion about one instance, so the registration's
lifetime is asserted separately.

**THIS PARAGRAPH SAID NOTHING CALLS IT, AND THE POST CALLS IT.** An attempt is a
presented code being judged, the post on the redemption route judges one on every
submission, and it asks here first:

    git grep -n 'if (!_limiter.MayJudge(from)' -- Jellyfin.Plugin.Invites/Controllers/RedeemController.cs
    Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:249:        if (!_limiter.MayJudge(from) || !_operations.StoreIsAvailable)

So this page no longer describes a component that is built and unreached. Three
things about the call are worth having here rather than in the route, because
this page is where the numbers are argued.

The order is asked before judged. Nothing is looked up until the limiter has
allowed the attempt and counted it, which is what makes the two numbers bound
guesses rather than describe them after the fact. A test drives the global
ceiling and then presents a live code, and the invitation comes back with its use
untouched.

A request the server cannot place is refused rather than counted. `MayJudge`
refuses to count an attempt naming no address, which this page's own decision
about the counter's key requires, and the route answers such a request with the
ordinary refusal instead of judging it outside the limit.

What is counted is a submission and never a page view. The route serves the setup
page to anybody who asks, unchanged and without reading a code, so a browser
loading a link spends nothing. The allowance is spent by a form being posted.

## What is still not claimed

Nothing here has been measured against a running server, and no rate has been
observed. The byte-identity requirement is not held by anything: what a refused
attempt looks like is `docs/refusal-response.md`, and there is no ordinary
refusal for a throttled one to be compared against yet.

The arithmetic quoted above is re-run from `docs/code-entropy.md` and is the same
model, with the same inputs and the same two assumptions it names about an
attacker. Nothing here measures an attempt rate anybody has observed against a
Jellyfin server.

The household behind twenty an hour is reasoned rather than counted. Nobody has
watched four people redeem from one address, so that number is an upper bound on
an imagined case, and the thing to re-run when it is wrong is the row above it
rather than this sentence.
