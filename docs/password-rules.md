# What this plugin requires of a password

The invited person chooses their own password during guided setup, which is
point 4 of the plan's decisions. Nobody else ever sees it, and this page is
what the form requires of it and why.

## The rules are the plugin's, not the server's

The obvious way to do this is to read the rules off the server, so that a
password the form accepts is one the server will take. The server line this
plugin builds against exposes no such thing. Neither end of the range declared
in `build.yaml` carries a member a password policy could be spelled in:

```
$ cache=~/.nuget/packages
$ names='MinimumPasswordLength|PasswordPolicy|PasswordRequirement|PasswordComplexity|MinPasswordLength|PasswordRules'
$ for v in 10.11.0 10.11.11; do
    grep -acE "$names" $cache/jellyfin.model/$v/lib/net9.0/MediaBrowser.Model.dll
    grep -acE "$names" $cache/jellyfin.controller/$v/lib/net9.0/MediaBrowser.Controller.dll
  done
0
0
0
0
```

What those assemblies do carry is `ChangePassword`, which is the path the
password is set through and which takes whatever it is handed.

So every rule below is stricter than the server, and the cost is a real one
rather than a formality: **a password this form refuses is a password the server
would have accepted.** That is the direction the issue behind this page calls
the survivable one, because the other direction fails after the account already
exists. It is still a person being told no for a reason that is this plugin's.

The rules live in `Jellyfin.Plugin.Invites/Setup/PasswordRules.cs` and nowhere
else. The numbers are read back rather than restated here:

```
$ grep -n 'public const int' Jellyfin.Plugin.Invites/Setup/PasswordRules.cs
```

## Why a length and no composition rule

Length is the whole of it. There is no rule demanding a digit, a capital or a
symbol, and that is a decision rather than an omission: a composition rule buys
a predictable substitution at the end of a word rather than entropy, and the
guidance that used to ask for one has withdrawn it. The floor is set where a
floor is set when nothing else is asked for.

The maximum is a bound on what reaches the server's hashing path, which does
work proportional to what it is given and is reachable by a stranger. It sits
far above any password a person types, so what it refuses is a submission
rather than a passphrase.

Length is counted in text elements rather than in UTF-16 units, which is what a
person means by a character: a letter carrying a combining accent is one
character and two units. Counting units would let six such letters satisfy a
rule asking for twelve, and `PasswordRulesTests.ACharacterIsWhatAPersonCounts`
is what holds that.

## The person is told before they type

The rules are on the page above the password box, in the same words the refusal
uses. A rule stated under the field is a rule read after the mistake.

The page is an embedded resource served byte for byte, so the sentences are text
in that file rather than values substituted into it, and what keeps the two from
drifting is the suite: every sentence the rules declare has to appear in the
page ahead of the field. That is a checked agreement rather than a derivation.
A rule reworded in one place reds; a rule nobody wrote into either place is
invisible to both.

## What is not defended

Nothing here reads a breach list, and nothing judges whether a password is a
common phrase. A password of twelve ordinary characters that appears in every
leak of the last ten years meets every rule on this page. That is the residual,
and the controls that stand against it are elsewhere: the invitation is single
use and expires, and the account it creates can never manage anything.

Nothing here is a claim about the strength of any particular password.

## What is not built

THIS SECTION SAID THE FORM HAS NO POST BEHIND IT, THAT NOTHING TAKES A PASSWORD,
THAT NOTHING VALIDATES ONE ON THE SERVER SIDE AND THAT NO ACCOUNT IS CREATED. A
post landed under #399 on 2026-09-04, and it asks this file rather than a second
copy of the rules:

```
$ git grep -n 'PasswordRules.WhyRefused(submission.Password)' -- 'Jellyfin.Plugin.Invites/*.cs'
Jellyfin.Plugin.Invites/Controllers/SetupAnswers.cs:117:        if (PasswordRules.WhyRefused(submission.Password) is not null)
```

So the rules above are what a submission is judged by, and the sentence at the
head of this page - that the form and the server agree because there is one place
the rules live - is a statement about a live route rather than a plan.

**The clause about a refused password leaving no account behind is held.** The
rules are asked out of the request alone, before any code is looked at and before
any use is taken, and the assertion reads the store and the write seam rather than
the response, which is what makes it a claim about what was left behind:

```
$ git grep -n 'public async Task APostWhoseAnswersAreRefusedIsAnsweredWithoutJudgingTheCode' -- Jellyfin.Plugin.Invites.Tests/RedeemPostTests.cs
Jellyfin.Plugin.Invites.Tests/RedeemPostTests.cs:282:    public async Task APostWhoseAnswersAreRefusedIsAnsweredWithoutJudgingTheCode(
```

**What the response says is the one clause still not built.** `WhyRefused` answers
with the sentence this page states and the route discards it: a refused submission
is answered with the bare bad request, so somebody one field away from an account
is told only that something was wrong. Serving the form again with a reason on it
is #77's, and until that lands the wording exists here and reaches nobody.
