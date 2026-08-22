# Disaster cases

Three things happen to servers that are not attacks and not bugs, and all three
change what a live invitation means. None of them is prevented by getting the
invitation logic right, so they are written down here rather than left to be
discovered by whoever meets one.

One of the three is detected and the other two are not, which is what this
document has said from the day it was written rather than a position taken since.
The third case below, two servers over one store, is refused when the server
starts. The keyed hash secret is no longer held by an open issue: a code is
stored only as a value keyed under it, and rotating it is an operator action. The
redemption caller still is, and no invitation is spent anywhere, which is why the
first two cases are described against a store that only minting and revocation
write to. What this document fixes is what each case does and which of them the
plugin is expected to notice, because that decides work in the issues that build
the store rather than being read off them afterwards.

## The shape they share

Every one of them is the same mistake in a different costume: the plugin's state
is treated as a file, and a file can be copied, restored and shared. The
invitation store is not a cache. It is the record of which bearer credentials
are still worth something, and moving it in time or space moves that answer with
it.

## Restore from backup

An operator restores the data directory from a backup taken before some
invitations were spent or revoked.

What comes back is the store as it was, which means an invitation that was
redeemed after the backup is live again, and an invitation that was revoked
after the backup is live again. The accounts created in between are not in the
store's view at all, because the server's own user database is restored
separately and may be restored to a different point.

The revocation case is the one that matters. Revoking is the operator saying a
link must stop working, and a restore silently undoes it. That is not a
detectable event from inside the plugin: a restored store is a valid store, and
nothing in it says it is older than the accounts around it.

Consequence stated plainly: a restore can revive a revoked invitation, and the
plugin does not detect it. The operator's answer is to re-revoke after a
restore, which means the operator has to know what they revoked, which is what
makes the revocation record in #54 worth keeping beyond the invitation it
belongs to.

Detected: no, and that is unchanged by the report described next. #46 asked for
what a restore does to live invitations and has closed with that report landed;
#54 holds what a revocation leaves behind and is open.

What the load does report is narrower than detection and is not to be read as it.
When the server starts, the store is compared against the accounts the server
has, and a record claiming an account that is not there is written to the log. A
restore far enough back produces that, so does an account an operator deleted by
hand, and nothing distinguishes the two. It says the store and the server
disagree. It does not say a restore happened, and a restore that revived a
revocation without touching any account produces no disagreement at all.

## A cloned server

An operator copies a data directory onto a second machine, to move house, to
build a staging server or to try an upgrade without risk.

Now two servers hold the same keyed hash secret and the same live invitations.
An invitation redeemed on the first is still live on the second, so one link
mints one account on each machine. Both are real accounts on real servers, and
neither knows about the other.

The keyed hash secret is the part that outlasts the mistake. Deleting the clone
does not undo the fact that its secret is now somewhere else, and the secret is
what makes the stored form of a code useless to somebody who read the store.

Consequence stated plainly: a clone doubles every live invitation and copies the
secret. The plugin does not detect it, because a copied data directory is
indistinguishable from the original from inside.

The instruction that follows is to rotate the secret on whichever machine keeps
the identity, accepting that rotation invalidates every live invitation, which
is exactly what rotation is for and what #30 requires be stated before it runs.

Detected: no. Named in the operator guide with the rotation instruction, which
is #111. The rotation itself is an operator action now rather than something
#30 still owes: `POST /Invites/HashSecret/Rotate` in
[docs/api.md](api.md) says what it will invalidate before it does it, and the
control is on the plugin's configuration page.

## Two servers, one store

Some deployments will point two server processes at one shared directory, on a
network filesystem or a container volume, expecting it to work the way a
database would.

It does not. The store's atomicity is written for one process: a redemption
reads, decides and writes under a lock the process holds against itself, and a
second process holds no such lock. Two redemptions of the same invitation can
both pass the decision, and the file can be left in a state neither writer
intended.

This is the case worth detecting, and it is cheap. A lock file written at
startup carrying the host and the process that holds it turns a silent
corruption into a message an operator reads on the first run. A second process
finding a live lock refuses to start its own store rather than joining in.

Refusing is the right answer rather than warning. A warning about a shared store
is a warning nobody sees until the corruption is already there, and the failure
this refuses is one that costs accounts rather than uptime.

Two details decide whether the detection is worth having. A lock left behind by
a process that was killed must be recoverable by the operator, or the mechanism
turns an outage into a longer one; the lock file therefore carries enough for a
person to decide, and clearing it is an operator action rather than an automatic
timeout. And the check is at startup rather than per write, because a per-write
check on a network filesystem is a promise the filesystem does not keep.

Detected: yes, at startup, refusing rather than continuing. Owned by #40 for the
atomicity the lock protects and #96 for the refusal itself.

The claim is taken by the load the server starts, which is the same moment the
store is read and compared against the server's accounts. A server that arrives
second writes an error naming the holder and the file to remove, and it reads
nothing: a comparison taken out of a directory another process is writing to
describes a store that moved while it was being read, and nobody can tell that
from a store that really disagrees.

## What this document is not

It is not a backup procedure and it does not tell an operator how to take one.
It is not a recovery runbook. And it makes no claim that the three cases are
exhaustive: they are the three that follow from the store being a file, and a
fourth arrives the day the store stops being one.
