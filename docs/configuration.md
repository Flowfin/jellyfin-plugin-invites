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
both sides, and it refuses either set holding a name the other does not. It does
not read what a row says. Whether the sentence about what breaks is true is what
the review is for.

## The settings

There are none. The configuration type carries no settings today:

    git grep -n 'get;' -- '*/Configuration/PluginConfiguration.cs'
    exit=1

The table below is the shape a row takes. It stays empty until a setting lands,
and the check above is what makes the first one arrive with its row rather than
without it.

| Setting | What it does | Default | Bounds | At the bound | If it is set badly |
| ------- | ------------ | ------- | ------ | ------------ | ------------------ |

## A fresh install

A server that installs the plugin and never opens the configuration page runs
with no settings, because there are none to run with. The class exists so the
plumbing is in place and the page exists so the operator can find it, and there
is nothing on that page to set. Nothing is minted and no account is created,
because none of that is built yet.

That is a closed posture by absence rather than by decision, and it is not the
one #87 asks for. That issue wants the fresh-install configuration asserted
field by field in a test, so that moving a default generously turns a test red
and somebody has to say so. A test asserting the absence of fields would go
green today and would keep going green while fields were added around it, which
is the shape of guard this repository refuses. It lands with the fields.

## What is not in this file yet

Two settings will need more than a row when they exist, and #113 asks for a
section for each. Neither is written here, because neither setting exists and a
document describing a setting the code does not have is the drift this file is
built to refuse in the other direction.

The public base address is one, because it is the setting whose misconfiguration
produces links that do not work, and its interaction with a reverse proxy is
where most support questions will come from. It arrives with #50, which decides
that a link is never built from what the request says the host is.

The ceilings are the other, because a reader needs to know that they are
enforced when the configuration loads and that an out-of-range value refuses the
load rather than being clamped quietly. They arrive with #33, which decides the
three numbers and the reasoning for each.

The check refuses a setting with no row. It does not refuse a setting with no
section, because which settings need more than a row is a judgement about what a
reader will trip over rather than a fact about the type, and a check pretending
to make that judgement would turn a red mark into an argument.
