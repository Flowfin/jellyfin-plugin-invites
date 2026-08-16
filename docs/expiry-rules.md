# The expiry rules

Expiry reads like one comparison and is seven decisions. Each one below is
settled, with the reason it went that way, so the code that enforces it is built
against a decision rather than reconciled with one afterwards.

Two of these rules are enforced and the rest are not, and this paragraph said
none of them were. The record these rules judge is in the tree, as `Invitation`
under #38, and so is the routine that judges it:

    git grep -n 'public static class RedemptionDecision' -- 'Jellyfin.Plugin.Invites/*.cs'
    Jellyfin.Plugin.Invites/Redemption/RedemptionDecision.cs:46:public static class RedemptionDecision

The exclusive boundary is the comparison that routine makes, asserted at the
exact instant, and the one clock reading is the argument it takes rather than a
read it performs, with the lint refusing a second read anywhere but the seam.
Both rules say so in place below.

What is absent is a caller. The plugin serves no route, and nothing hands the
decision a clock reading or does anything with its verdict:

    git grep -nE 'ControllerBase|ApiController|HttpGet|HttpPost' -- '*.cs' ':!Jellyfin.Plugin.Invites.Tests'
    exit=1
    git grep -n 'RedemptionDecision.Decide' -- 'Jellyfin.Plugin.Invites/*.cs' | wc -l
    0

So the five rules that are not the boundary and the one reading are decisions
with nothing standing behind them, and each of them names the issue that will
enforce it. The clock seam they read through is in the tree already, as `IClock`
under #41.

## The clock starts at minting

The validity window runs from the moment the invitation is minted, not from the
first time somebody opens the link.

Expiry bounds how long the link has been loose in the world, and the link is
loose from the moment it is created. A window that started at first use would
make an unused leaked link immortal, which is precisely the case the window
exists for: the link nobody intended to receive is also the link nobody opens
until it is useful to them.

It also means the operator can answer "when does this stop working" at the
moment they send it, rather than never. Enforced at minting under #82.

## The boundary is exclusive

An invitation whose expiry is the instant T is honoured strictly before T and
refused at T.

One tick separates the two readings and only one direction of it can be argued
from the security side, so the tie goes to refusing. What matters more than
which side wins is that the exact instant is asserted, because this is the
clause an implementation gets right by accident and a later change gets wrong in
silence. The comparison lives in the decision routine under #56 and the instant
is asserted by a test under #102.

## The default validity is seven days

The number and the whole argument for it are in the threat model, under
[the default validity, and the reason for the number](threat-model.md). This
page does not restate them, because a number written in two places is a number
that will be changed in one.

What belongs here is where it acts: it is the default an operator may change
within the maximum below, defaulted and validated in the configuration schema
under #86.

## The maximum an operator may set is ninety days

An invitation valid for a year is a bearer credential valid for a year, and no
amount of care about the rest of the plugin survives that.

The longest legitimate case is inviting somebody who cannot set anything up now,
because they are travelling or otherwise away from the thing they would set it
up on. Three months covers that with room to spare. Past it, minting again costs
one click, and the operator is present to make the decision a second time, which
is the moment a person looks at the number and the whole value of a ceiling.

Enforced at minting rather than at redemption, under #82. An operator who asks
for longer is told immediately, instead of finding out later that an invitation
they thought would last quietly died.

## An invitation with no expiry at all is refused

There is no never-expiring invitation and no setting that produces one.

The bound written under #27 promises that an account created from an invitation
is exposed for no longer than the invitation had left. That sentence says nothing
at all when the invitation has no left, so a never-expiring invitation does not
weaken the bound, it makes it meaningless.

An operator who wants control without a deadline already has revocation, which
is #54, and revocation is the better instrument for that purpose: it is a
decision made at the moment they want it rather than one made once at minting
and then forgotten about.

## One clock reading serves a whole redemption

The clock is read once at the top of the decision routine, and every comparison
inside that redemption uses the same value.

An invitation whose expiry passes midway through a redemption is otherwise
decided differently by two comparisons in the same request, and which one wins
depends on how long the machine took. That is the shape the seam under #41 was
built for, and its suite already asserts that a controlled clock does not move
between two reads inside one decision, so the property has a test before it has
a caller. The routine is #56.

## A backwards clock jump is accepted, and this is what it costs

A server whose clock steps backwards after a time synchronisation un-expires
every invitation whose expiry falls inside the jump, for the duration of the
jump. The plugin does not prevent this.

It is accepted rather than handled because both available handlings are worse
than the fault. A monotonic source does not survive a restart, and a restart is
the case where a wrong clock is most likely in the first place. A persisted
high-water mark turns the one endpoint a stranger can hammer into one that
writes on every read, which is the disk-filling shape the attempt trail under
#43 is already bounded against.

What is cheap is a report rather than a repair. The store already writes on a
successful redemption, so it can carry the latest instant it has observed at no
extra cost, and a clock read earlier than that instant is reported to the
operator. That tells them their server's clock moved backwards, which is worth
knowing for reasons that reach well past this plugin, and it does not pretend to
fix anything. The store is #39 and #40, and where the report goes is #32.

This is a negative disclosure. If the security page under #112 carries it, it
should link here rather than copy the sentence, so there is one place where the
consequence can be edited.

## What is not settled here

How long expired and spent records are kept once they stop being redeemable is
the retention question, and it is answered: ninety days from the moment an
invitation stops being usable, in
[docs/personal-data.md](personal-data.md#retention). That page owns the number
and this one does not restate the reasoning. Nothing on this page moves because
of it, because expiry is not deletion, and the difference is an entry in
[docs/limits.md](limits.md).

Whether the account an invitation created expires along with it is a separate
question, decision 3 in #11, and it is #68 rather than anything on this page.
