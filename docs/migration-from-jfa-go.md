# Coming from jfa-go

Most people who find this plugin are already running jfa-go, which is a
separate container sitting next to their Jellyfin server, and what they want to
know is whether they can stop running it. This page answers that in both
directions and does not pretend the other tool is not there. jfa-go is well
established, it does more than this plugin plans to do, and for several of the
things it does this plugin has no answer at all.

Nothing in this plugin is implemented yet. Every behaviour attributed to it
below is a planned behaviour held by an open issue, and the issue number is
where the promise is kept:

```
$ git ls-files -- '*.cs' ':!.github/lint/fixtures'
Jellyfin.Plugin.Invites.Tests/ClockSeamTests.cs
Jellyfin.Plugin.Invites.Tests/PluginPagesTests.cs
Jellyfin.Plugin.Invites.Tests/Stubs.cs
Jellyfin.Plugin.Invites/Configuration/PluginConfiguration.cs
Jellyfin.Plugin.Invites/Plugin.cs
Jellyfin.Plugin.Invites/Time/IClock.cs
Jellyfin.Plugin.Invites/Time/SystemClock.cs
$ git grep -nE 'Invitation|Redeem' -- '*.cs' ':!.github/lint/fixtures'
Jellyfin.Plugin.Invites/Plugin.cs:29:    public override string Name => "Account Invitations";
```

Seven source files, and the only one of them carrying the word invitation
carries it as the display name. So this is not a
page telling an operator to switch today. It is the page that says what
switching would and would not get them, written before the code exists so that
no feature gets claimed here and built afterwards.

## Where the claims about jfa-go come from

Every statement below about jfa-go was read out of jfa-go's own current
documentation rather than from memory or from a comparison somebody wrote
elsewhere. Two files carry all of it, and both are pinned so a reader can see
exactly what was read:

```
$ gh api repos/hrfee/jfa-go/contents/README.md --jq .sha
3994bbfc8a5eb57ced1c1e5b45d537ae81c1aa1b
$ gh api repos/hrfee/jfa-go/contents/config/config-base.yaml --jq .sha
bc5fcc489ef60a4bb2356fe951a3cbcc3e3d8baf
$ gh api repos/hrfee/jfa-go/releases/latest --jq '.tag_name + " " + .published_at'
v0.6.0 2025-11-27T20:45:12Z
```

`config/config-base.yaml` is jfa-go's own settings schema, so a feature named
there is a feature with a switch behind it rather than a line in a summary. It
is the file worth re-reading when this page is next revised, because a claim
about another project ages the moment that project ships.

## What this plugin plans to do instead

An operator mints a link from the Jellyfin dashboard, sends it to somebody by
whatever means they already use, and that person follows it and answers a short
setup that ends in an account the operator scoped in advance. The whole of that
happens inside the Jellyfin server process.

| Planned behaviour | Issue |
| --- | --- |
| An invitation with a validity period and a use count | #51, #52 |
| An account template applied at creation, deciding libraries and permissions | #61, #63, #64 |
| A guided setup page served by the plugin, where the person picks a username and a password | #74, #76 |
| Revocation that takes effect on the next redemption, including one already in flight | #54 |
| An administrator view of what was invited and what became of it | #89 |

The one thing that is structurally different is that there is no second
service. No container, no separate process, no reverse proxy entry, no second
thing to upgrade, and no second thing to notice has stopped. jfa-go's own
feature set is the price of being a separate application, and running inside
the server is the reason this plugin's is smaller.

## What jfa-go does that this plugin does not plan to do

Each row is a real capability of the other tool and a real absence here. None
of these is on a roadmap in this repository.

| jfa-go does | Here |
| --- | --- |
| Sends invitations itself, by email and through Discord, Telegram and Matrix bots | Nothing. The operator copies a link and sends it. Whether sending is ever in scope is item 5 in #11 and it is not answered |
| Confirms an email address before the account is made, through its `email_confirmation` setting | Nothing. Whether the setup form collects any contact address at all is item 9 in #11, and `docs/personal-data.md` is the argument for collecting none |
| Offers a CAPTCHA on the account creation form, including Google reCAPTCHA | Nothing. Item 10 in #11 records why: a challenge means a third-party script on the redemption page and a runtime dependency, and this plugin has none |
| Expires accounts a set time after they are created, deleting or disabling them, with reminder messages before it happens | Planned as disable only and off by default, in #68, and it waits on item 3 in #11. Deletion is not planned in any form |
| Gives a user their own limited invite to pass on, which it calls referrals | Nothing, and nothing planned |
| Runs a "My Account" page where a person changes their own password and contact details | Nothing. The plugin's job ends when the account exists |
| Resets passwords, working with Jellyfin's own forgot-password flow | Nothing. Account recovery is the server's job |
| Manages existing users in bulk, enabling, disabling, deleting and applying profiles across them | Nothing, and the opposite is a rule here. #62 makes it a refusal inside the creation routine that an invitation ever touches an account that already exists |
| Syncs usernames, passwords and contact details with Ombi and Jellyseerr | Nothing, and no integration with anything outside the Jellyfin server is planned |
| Sends Markdown announcements to users, and lets an operator edit every message a user sees | Nothing. The only text this plugin shows a person is the setup page and its refusal |

The last two rows in the table above are worth reading as a pair with the one
about bulk user management. jfa-go is described by its own repository as "a
bit-of-everything user management app for Jellyfin". This plugin is one link
turning into one account, and every row above is a thing it declines to be
rather than a thing it has not got round to.

## What is not decided yet

Two rows above would move if the maintainer answered #11 differently, and
saying so here is cheaper than an operator discovering it.

Item 3 decides whether an invited account expires at all. Until it is answered
#68 is written as disable, never delete, and off by default, so an operator
coming from jfa-go's `delete_user` behaviour would find no equivalent.

Item 5 decides whether this plugin ever sends an invitation itself. The plan
assumes it does not, because the Jellyfin server has no mail path this plugin
should be building, and adding one brings a mail or webhook configuration
surface with it.

## Moving over

There is no import path, and there is no plan for one. Nothing in this plugin
reads a jfa-go database, its invite list, its profiles or its user records. An
operator moving over recreates their profiles as account templates by hand, and
whatever invitations are outstanding in jfa-go stay in jfa-go until they expire
or are cancelled there.

That sounds worse than it is for the accounts, because the accounts were never
jfa-go's to hold. An account jfa-go created is an ordinary Jellyfin account in
the server's own user database, and it stays exactly as it is when jfa-go
stops. This plugin will not touch it: it creates accounts and never modifies
one that already exists, which is the refusal in #62 rather than a habit.

The one thing that does not survive is the link between an account and the
invitation it came from, because that link lives in jfa-go's own store. After a
switch, accounts made by jfa-go are accounts with no invitation record here,
and the administrator view in #89 will show nothing for them. There is no way
around that without an importer, and an importer that guesses at the mapping
would be worse than the gap.

Anything jfa-go was doing to accounts on a schedule stops when jfa-go stops.
This matters most for account expiry: if jfa-go was set to disable or delete
accounts after a period, removing it leaves those accounts alive forever,
because the thing that would have acted on them is gone. Check what is
outstanding there before turning it off.

## Running both at once

It is possible, and the two do not know about each other.

Nothing here reads jfa-go's state and nothing here registers a Jellyfin route,
a scheduled task or a configuration key that jfa-go also uses, so there is no
collision to describe at the level of the server. What there is instead is two
things creating accounts on one server, with two separate ideas of who was
invited and by whom.

The consequences are worth stating plainly. An operator investigating an
unfamiliar account has to look in two places, and one of them will not have it.
The two tools have separate ideas of what a new account may reach, so a person
invited through one gets jfa-go's profile and a person invited through the
other gets this plugin's account template, and keeping those two in agreement
is manual work nothing checks. Revoking a link in one has no effect on the
other. And a username taken by an account one of them created is simply taken
for the other, which surfaces as a refusal partway through somebody's setup.

None of that is a failure mode either tool can fix, and none of it corrupts
anything. It is the cost of two things owning the same job, and it is a
reasonable cost to pay for a week while an operator moves across. It is not a
configuration to settle into.

## What this page does not do

It does not compare quality, maturity or the pace of either project. jfa-go has
shipped for years and this plugin has shipped nothing, which is the only
comparison of that kind worth making today.

It does not tell an operator which to run. The absences above are the material
facts, and an operator who needs mail sending or referrals needs jfa-go and
should keep it.
