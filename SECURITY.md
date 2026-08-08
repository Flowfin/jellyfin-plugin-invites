# Security policy

This plugin mints invitation links that create Jellyfin accounts. A defect here
can hand somebody an account on a server they were never invited to, so security
reports are wanted and are not a nuisance.

## Reporting a vulnerability

Use GitHub's private vulnerability reporting on this repository:

<https://github.com/iderex/jellyfin-plugin-invites/security/advisories/new>

It is enabled, which anyone can check:

```
gh api repos/iderex/jellyfin-plugin-invites/private-vulnerability-reporting
{"enabled":true}
```

That route is private until an advisory is published, and it is the only route
this document promises. Please do not open a public issue for something that
lets somebody in, and please do not send it to a personal address; a public
issue is a working exploit handed to every reader of this repository, and a
personal address is a route nobody else can pick up.

If the report concerns Jellyfin itself rather than this plugin, it belongs to
the Jellyfin project's own policy and not here.

## What to expect

This is a small project without a staffed security team, so the numbers below
are what one maintainer can actually hold to rather than what reads well.

- An acknowledgement within seven days that the report was received and read.
- An assessment within thirty days: whether it is accepted, what it is thought
  to affect, and either a fix or a stated reason for not fixing.
- Credit in the advisory unless you ask otherwise.

If seven days pass with no acknowledgement, the report has probably not been
seen. Say so in a public issue without describing the vulnerability, and that
is the escalation.

## What is in scope

The code in this repository, its packaging metadata, and its workflows. A
report that the plugin grants more than the invitation it came from, that a
spent or revoked invitation is honoured, that an invitation code is guessable
or enumerable, or that a code or secret appears in a log, is in scope even if
you cannot demonstrate a full exploit.

Out of scope: the Jellyfin server itself, a server whose operator configured it
to be open, and reports produced by a scanner with nothing behind them.

## What a leaked link costs

An invitation link is a bearer credential. Anybody holding it can create an
account, so a link in a forwarded message or a browser history on a shared
machine is worth whatever the invitation behind it was worth. This is the bound
on that, in the same words as
[docs/threat-model.md](docs/threat-model.md), which is where each clause is
placed against the issue that keeps it:

> Somebody who holds a leaked link, and reaches the server before the invited
> person does, gets one account for each use the invitation had left, with
> exactly the template that invitation carried, valid for no longer than the
> invitation had left, listed for the operator as a redemption of that
> invitation, and removable by deleting that account.

Nothing in this repository redeems an invitation yet, so today the sentence is
what this plugin is being built to, and not a report of what it does. A report
that the plugin exceeds this bound once the code exists is a report this policy
wants, and the list above is what to measure it against.

The default validity is seven days, and a spent invitation is spent for good.
The threat model carries the reasoning for both, and both are the shortest
window that still works rather than the longest one an operator would tolerate.

## What the guided setup will never ask

The page that turns an invitation into an account asks for a username, a
password, and a password confirmation. That is the whole form.

It will never ask for a password to another service, a password already used on
this server, a payment detail, a date of birth, a postal address, a legal name,
a security question, anything phrased as optional that the plugin has no field
for, or anything the operator could ask outside the plugin. It loads no script,
font, image or analytics from another host, and it says which server it belongs
to.

A page bearing this plugin's name that asks for any of those is not this
plugin's page, and a report that it does is a report this policy wants. The
reason for each refusal is in
[docs/setup-never-asks.md](docs/setup-never-asks.md), and a field added to the
form needs a row in [docs/personal-data.md](docs/personal-data.md) first.

The page does not exist yet, so this is what it is being built to rather than a
description of something serving today.

## What is not defended

These are the entries the threat model in [docs/threat-model.md](docs/threat-model.md)
marks as undefended, in the same words. That file is where each one is placed
against the attack it belongs to.

Every mitigation the threat model names is a promise held by an open issue
rather than by code, because there is no redemption path in this repository yet.
Read this section as what will still not be defended once those issues land, and
not as a claim that everything else already is.

A leaked link within its validity, before the intended person uses it, is an
account for whoever found it. This is what a bearer credential means and no
mitigation in this plugin changes it. What the plugin offers instead is a
smaller window and a smaller blast radius: a validity the operator chooses, a
use count the operator chooses, and revocation that works the moment the
operator reaches for it.

An operator with administrator rights can mint whatever the ceilings allow.
Nothing here defends the server against the person the server already trusts
with it. The ceilings bound what any single invitation can grant, and they are
configuration an administrator can also change.

A restored backup revives spent invitations. The invitations redeemed since the
backup are live again with their uses restored, and the revocations made since
are undone. The plugin cannot prevent this. What it does is compare, on load,
the accounts the store claims to have created against the accounts the server
actually has, and report the disagreement in both directions rather than
reconciling it silently.

A cloned data directory produces two servers that both honour the same live
invitations. Redeeming on one leaves the other still willing, because neither
knows the other exists. The operator guide says to rotate the hash secret after
a clone, and rotation is a revoke-everything operation offered deliberately.

Username availability is disclosed by the setup form. A form that has to tell
somebody their chosen name is taken is a form that tells anybody holding a code
which names exist. The disclosure is bounded by needing a valid code first.

Somebody who can read both the store and the hash secret can test a guess
offline, without meeting the rate limit. The keyed hash and the code's entropy
are what stand between them and a code, and neither is a defence against
somebody who is already reading the data directory.

## Supported versions

Nothing is released yet, so there is no supported version and no backport
policy. This section is rewritten at the first release, under milestone M12.
