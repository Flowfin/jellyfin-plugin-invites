# What is held about a person

This page is for somebody who was invited to a Jellyfin server through this
plugin, and for the operator answering them. It says what is held about you,
what is never held, how long each thing stays, and what makes it go away.

[docs/personal-data.md](personal-data.md) is the same inventory written for a
different reader: somebody deciding what may be stored at all, who needs each
field argued against the record type and against the issue it exists for. This
page names the same fields in the terms of the person they are about. It is not
a second decision, and where the two disagree that is a defect rather than a
nuance. `Jellyfin.Plugin.Invites.Tests.PersonalDataForAPersonTests` reads the
stored fields off both pages and refuses a disagreement in either direction, so
a field held about you with no line here reds the suite, and so does a line here
naming something that page does not hold.

## Where this plugin stops and the server begins

The account an invitation creates is an ordinary Jellyfin account. Its name, its
password, what it has watched and how far into each thing, the devices it has
signed in from - all of that belongs to the server and is held exactly as it is
for an account the operator typed in by hand. This plugin neither adds to it nor
takes it away, and removing the plugin leaves every one of those accounts
standing. A question about what the server holds about you is a question for the
server rather than for this page.

What this plugin adds is one thing the server would not otherwise have: the link
between an account and the invitation it came from. That is the line below worth
the most care, and it is the reason this page exists.

## What is held

One line per field. Whom it is about, what it means about them, and what removes
it.

| Field | Whom it is about | What it means | What removes it |
| --- | --- | --- | --- |
| Invitation identifier | The invitation | A short name for one invitation, so an operator and a log line can both point at it without either one carrying the code you were sent | The record |
| Keyed hash of the code | The invitation | A one-way fingerprint of the code, kept so a presented code can be recognised without the code itself being held anywhere | The record |
| Minted by | The operator | Which operator account made this invitation, so an invitation is answerable to somebody rather than to nobody | The record |
| Minted at | The invitation | When it was made | The record |
| Expires at | The invitation | The moment after which it stops working | The record |
| Uses granted, uses remaining | The invitation | How many accounts it was allowed to create, and how many of those are left | The record |
| Revoked, revoked at | The invitation | Whether an operator withdrew it, and when | The record |
| Revoked by | The operator | Which operator account withdrew it | The record |
| Template name | The invitation | Which set of grants the invitation carried, which is what decides the libraries and the quotas of the account it creates | The record |
| Accounts produced | You | The link between your account and the invitation it came from. This is the most identifying line on the page: it is what lets an operator who meets an account they do not recognise find out where it came from | The record |
| Invitation identifier, where one matched | You, where the attempt was yours | Which invitation a redemption attempt was made against. It is empty where the presented code matched nothing, because there is nothing to name | The trail bound |
| Outcome | You, where the attempt was yours | What happened to an attempt, as one value from a fixed set rather than as free text, so nothing anybody typed reaches it | The trail bound |
| Time | You, where the attempt was yours | When the attempt happened | The trail bound |

Four of those thirteen lines are about the operator or about the invitation and
not about you at all, and they are on this page anyway. An inventory showing only
the lines that name the reader is one the reader cannot check, because the shape
of what is kept is part of the answer.

## What is held about the operator

Two lines above, `Minted by` and `Revoked by`. They are operator account
identifiers, and they are personal data about the operator on the same footing as
`Accounts produced` is about you. They exist so that an invitation that was made,
or one that stopped working, is answerable to whoever did it.

They are kept under the same rule as the rest of the record and are removed by
the same sweep. Nothing else about the operator is held by this plugin.

## What is never held

The code you were sent. Only the keyed hash above is kept, and a hash does not
run backwards into a code.

The password you chose. It is handed to the server, which holds the credentials
of every account on it, and this plugin keeps it in no form at all: not in the
store, not in a log line, not in the record of the attempt. The same is true of
the confirmation you typed beside it.

Your username. The account has one, on the server, and this plugin points at your
account by its identifier rather than by its name, so there is no second copy
here.

A contact address for you. The guided setup asks for none, and that is a decision
rather than an omission: this plugin's job ends once an account exists, account
recovery is the server's, and an address collected here would be the one field
making this plugin hold something about you that the server does not already
hold.

The address you connected from. Redemption attempts are rate limited, which needs
to know where an attempt came from while it is being decided and needs nothing of
it afterwards, so those addresses live in memory for the length of the limiter's
window and reach no file. Writing them into the record of attempts would turn a
counter into a register of where a person was.

A free-text label describing you. There is no box for one, and the record has no
field one could be put in.

## How long each thing stays

Two named periods, and neither number is written here. A number restated in two
places is a number that goes wrong in one of them, so each is stated once, on the
page that owns it.

`record-retention` is how long an invitation record is kept once the invitation
has stopped being usable - once it is spent, expired or revoked. It is counted
from the moment it stopped being usable rather than from when it was made. The
period, and the reasoning behind the length of it, are in
[docs/personal-data.md](personal-data.md).

`trail-bound` is how many failed attempts are kept before the oldest are dropped.
The number, and what it does and does not buy, are in
[docs/attempt-outcomes.md](attempt-outcomes.md).

An invitation that can still be redeemed is touched by neither. It stays until it
is used up, until it expires, or until an operator revokes it.

## What removes it

A scheduled sweep runs daily and writes the store back without the records whose
retention period has run out. Nothing an operator has to remember to do, and
nothing that reaches an account.

Revocation removes nothing. An operator who revokes an invitation stops it
working and the record stays, because the record of a revocation is what tells an
operator that a restore from a backup has quietly undone it.

Removing the plugin removes the plugin's own state and leaves every account it
created standing. That also means it removes the only record of which account
came from which invitation, which is why an operator who wants to keep that link
takes it out before uninstalling rather than afterwards.

Deleting your account is the server's act rather than this plugin's, and it does
not remove the `Accounts produced` line above. That line stops naming an account
that exists, and it is removed by the retention sweep in its own time.

## Asking

No route on this plugin answers the question "what do you hold about me". What
answers it is the operator of the server you were invited to, reading their own
administrator page against this one. The questions an operator gets asked, and
what to answer, are in [docs/operator-guide.md](operator-guide.md).
