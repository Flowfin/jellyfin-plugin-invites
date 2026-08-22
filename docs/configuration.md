# Configuration reference

One row per setting: what it does, its default, its bounds, what happens at each
bound, and what breaks if it is set badly. The reader this is for has met a
setting and does not know what it means, so the row answers that and nothing
else. How the settings are used in sequence belongs in the operator guide, which
is #111 and is not written.

A setting that reaches the configuration type without a row here fails a check,
so this file cannot quietly fall behind the type:

    bash .github/lint/configuration-reference.sh check

The rows are checked against the type rather than generated from it. Four of the
six columns are sentences somebody has to write, and a file generated whole is
one nobody reviews the prose of. What the check reads is the property names on
both sides, and it refuses either set holding a name the other does not.

It reads one cell as well. The Default column states a fact the type also
states, so a row whose default disagrees with the initialiser beside it is
refused. That is the drift with the sharpest edge in this file: a reader who
meets a wrong sentence about what breaks argues with it, and a reader who meets
a wrong default believes it and configures against it.

What it will not judge is a setting with no initialiser whose declared type has
no unambiguous language default. A bool with no initialiser is false and an
integer is zero, and those are compared; anything else is null, and whether a
row should write that as unset, none or empty is a writing decision the check
has no business taking. It names each such setting on every run, so a green mark
is never read as every default having been compared.

The Default cell carries the value and nothing else. A unit or a qualification
belongs in Bounds, because the moment the check starts deciding that "7 days"
and "7" are the same value, that column is prose again.

Nothing else a row says is read. Whether the sentence about what breaks is true
is what the review is for.

## The settings

| Setting | What it does | Default | Bounds | At the bound | If it is set badly |
| ------- | ------------ | ------- | ------ | ------------ | ------------------ |
| `PublicBaseUrl` | The address invitation links are built from, as a stranger outside the network reaches this server. It is what the mint response writes its link against, and it is read from here and never from the request | Empty | An absolute `http` or `https` address, with an optional path prefix and no query or fragment | Empty mints as usual and returns the refusal in place of the link, naming this setting | Every link points somewhere the invited person cannot reach, or reaches a server that is not this one. Nothing is minted wrongly and no account is affected, because the address is used only to write the link down |

## The public base address

This is the setting whose misconfiguration produces links that do not work, and
it is the one a reverse proxy makes awkward, so it gets more than a row.

The address is configuration and never the incoming request. Building a link
from the request's host header is the easy way and it is the vulnerability: a
minting call carrying a forged host produces a link pointing at somebody else's
server, and the invited person types their new password into it. A link is also
minted for a person who is not on the other end of any request, so there is no
correct version of deriving one from a request even where the request is honest.
Two rules in `.github/lint/invariants.sh` refuse the two spellings, and
`InvitationLink` takes no request-shaped parameter at all, which is the shape
those rules cannot see.

Set it to what somebody outside the network types to reach this server, scheme
included. A path prefix belongs here where a proxy serves the server under a
subdirectory, and it survives into the link. A trailing slash makes no
difference, and neither does an explicit `:443` on an `https` address.

The fallback when it is not set is a refusal rather than a guess. The plugin
does not read the server's own published address, and that is a decision rather
than an omission: the case this setting exists for is a server behind a proxy,
which is exactly the case where the server's own idea of its address is the
wrong one, and the member a server answers that question with has already been
measured moving between the two lines this plugin loads on. A refusal naming this
setting is a support question with an answer. A link built from the wrong address
is a support question without one.

The refusal is not written to a log for somebody to find later. It comes back in
the mint response, in place of the link, at the moment an operator was expecting
one, which is #50 and is what `docs/api.md` describes under `POST /Invites`.

An address that is not absolute, is not `http` or `https`, or carries a query or
a fragment is refused the same way, because appending a path to any of them
produces something that looks like a link and does not reach the redemption
route.

## A fresh install

A server that installs the plugin and never opens the configuration page runs
with an empty public address, which builds no invitation links. The closed
answer for this setting is not a safe address, it is no address, and the reason
is in the row above.

`Jellyfin.Plugin.Invites.Tests.FreshInstallConfigurationTests` holds the type to
that answer, which is what #87 asks for. It is a table every setting has to be
in, so a setting arriving without a decided fresh-install value reds the suite
rather than shipping whatever the default happened to be.

## What is not in this file yet

The ceilings, because a reader needs to know that they are enforced when the
configuration loads and that an out-of-range value refuses the load rather than
being clamped quietly. They arrive with #33, which decides the three numbers and
the reasoning for each. Nothing about them is written here, because the settings
do not exist and a document describing a setting the code does not have is the
drift this file is built to refuse in the other direction.

The check refuses a setting with no row. It does not refuse a setting with no
section, because which settings need more than a row is a judgement about what a
reader will trip over rather than a fact about the type, and a check pretending
to make that judgement would turn a red mark into an argument.
