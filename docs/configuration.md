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

## The ceilings

**None of these is a setting, and that is the first thing to know about them.**
They are constants in the source, so there is nothing here for an operator to
type and nothing on this page's table to look them up in. They are written down
here anyway, because an operator who meets one meets it as a refused request from
`POST /Invites` with a number in it, and the configuration reference is where
somebody goes to find out where a number came from.

Three numbers act at minting today, and they come from two issues rather than
one. Two of them are #33's ceilings; the validity maximum is #51's and is a
ceiling in the same sense, so it belongs beside them rather than in a section of
its own:

    git grep -n 'public const int UsesCeiling' -- Jellyfin.Plugin.Invites/Invitations/InvitationMint.cs
    Jellyfin.Plugin.Invites/Invitations/InvitationMint.cs:71:    public const int UsesCeiling = 10;

    git grep -n 'public const int LiveCeiling\|public const int MaximumValidityDays' -- Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:66:    public const int MaximumValidityDays = 90;
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:113:    public const int LiveCeiling = 500;

At most ten accounts on one invitation, at most ninety days of validity, and at
most five hundred live invitations in the store at once. The reasoning for each
number is on the constant that carries it rather than restated here, which is
where it stays true when the number moves.

The default validity is a fourth number and is not a ceiling. Seven days, and it
is what a mint that names no validity gets:

    git grep -n 'public static TimeSpan DefaultValidity' -- Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:152:    public static TimeSpan DefaultValidity => TimeSpan.FromDays(7);

**Nothing is clamped.** A request outside any of them is refused with the limit
and the value in the message, and nothing is written. A use count or a validity
outside its range is a bad request; the live ceiling is a conflict, because what
the caller sent was acceptable and the store's state was not, so the repair is
revoking an invitation rather than editing the request.

**What "live" counts is not what the file holds.** An expired, spent or revoked
record is not live, does not count against five hundred, and stays in the file.
None of the numbers above bounds how large the store grows; retention does, which
is the scheduled sweep rather than a ceiling.

**The third of the three ceilings #33 asks for does not exist.** How many
accounts the plugin may create in a given period is the one that still holds when
the other two are set badly, and nothing in the plugin creates an account:

    git grep -nE 'CreateUserAsync' -- 'Jellyfin.Plugin.Invites/*.cs' ; echo "exit=$?"
    exit=1

So five hundred live invitations at ten uses each is what the standing set can
authorise with no further operator action, and no number bounds what arrives from
it in an hour.

**What is owed here and is not written.** #113 asks that this section say the
ceilings are enforced when the configuration loads and that an out-of-range value
refuses the load rather than being clamped. Neither half can be written truthfully
today: there is no ceiling on the configuration type, so there is no load-time
comparison to describe. That arrives with #86, and when it does, a configured
value has to be bounded by the constant rather than replace it - a setting that
can be raised without limit is not a ceiling. The paragraph above says what is
enforced instead, at minting, which is a smaller claim than the one this section
will eventually carry.

## What is not in this file yet

The ceilings as SETTINGS, which is the half the section above cannot write. What
this page holds for them today is where the numbers are and what meeting one
looks like; what it does not hold is a row, because a row is a promise that an
operator can change something and none of them can.

The check refuses a setting with no row. It does not refuse a setting with no
section, because which settings need more than a row is a judgement about what a
reader will trip over rather than a fact about the type, and a check pretending
to make that judgement would turn a red mark into an argument.
