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

## What is not defended

The threat model, including what this plugin deliberately does not defend
against, is being written under milestone M3 and does not exist yet. Until it
does, this section is a placeholder and not an assurance. Do not read the
absence of a stated non-defence as a claim that everything is defended.

## Supported versions

Nothing is released yet, so there is no supported version and no backport
policy. This section is rewritten at the first release, under milestone M12.
