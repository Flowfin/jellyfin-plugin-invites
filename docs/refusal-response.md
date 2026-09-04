# The refusal a person sees

An invitation that is not honoured produces one page. The page is the same page
whether the code was never real, expired last week, was spent this morning or
was revoked a minute ago, and it is the same page whether the person following
the link is the one the operator invited or somebody who guessed.

That is a security property first. An endpoint that answers differently for a
code that exists and a code that does not is an endpoint a stranger can ask
whether a code exists, one guess at a time, and get an answer. It is also, if
the page is written carelessly, a dead end that tells an invited person nothing
and turns into a message to the operator. Both halves are settled here, because
they are settled in the same sentence.

THIS PARAGRAPH SAID NOTHING SERVES THIS PAGE. The post that receives the form
landed, and it answers every case below with these bytes. What is still not
served is the refusal half of `GET /redeem/{code}`, which is #75 and #77 and is
what [docs/api.md](api.md) says at that route: the setup page is served there for
every code, because that route reads no invitation.

    git grep -n 'var refusal = Content(RefusalPage.Html, RefusalPage.ContentType);' -- Jellyfin.Plugin.Invites/Controllers/RedeemController.cs
    Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:322:        var refusal = Content(RefusalPage.Html, RefusalPage.ContentType);

This document is still what the rest is built against, and it is a decision
rather than a description of behaviour: the wording, the case list and the
compared-on list were written before the route existed and the route was built
to them.

## The wording

The page says this and nothing more:

> **This invitation link cannot be used**
>
> The link you followed is no longer usable. This can happen for several
> reasons, and this page cannot tell you which one applies.
>
> If you were expecting to set up an account, contact the person who sent you
> the link. They can issue a new invitation.

Three things are deliberate about it.

It does not say why. Not because the reason is secret from the person, but
because the page cannot tell the person apart from anybody else who has the
link, and any wording that narrows the reason narrows it for both of them
equally.

It does not say whether the code was ever valid. "No such invitation" and "this
invitation was spent" are the two answers a stranger most wants, and a page that
distinguishes them hands over the more useful one for free: a code that was once
real tells an attacker the format is right and the guessing is worth continuing.

It says the one useful thing. The person's route back is the human who sent them
the link, and nothing on the server can help them. A page that says only that
the link is invalid leaves them with nowhere to go, which is how a correct
refusal becomes a support thread.

## The cases it serves

Every case below produces this page, byte for byte identical. The list is one
somebody adds to rather than a paragraph somebody reimplements, and adding a
refusal without adding it here is how the set quietly grows a distinguishable
member.

| Case | Owned by | Trail outcome |
| --- | --- | --- |
| The presented code matched no record | #28 | `NoSuchInvitation` |
| The record's expiry had passed | #51 | `Expired` |
| The record had no uses left | #55 | `Spent` |
| The operator had revoked the record | #54 | `Revoked` |
| A rate limit or lockout refused the attempt | #31 | `RefusedByRateLimit` |
| A ceiling on what the plugin may create refused it | #33 | `RefusedByCeiling` |

The outcome names are the ones in
[docs/attempt-outcomes.md](attempt-outcomes.md) rather than a second vocabulary
for the same states.

`Owned by` named #56 on four rows and #52 on one, and both are closed, so the
column sent a reader after a case's owner to work that was done. What #56 built
is the routine that tells the first four apart. What had built nothing was the
route that serves the one response for all of them, and that was the post in
#399.

THE POST HAS LANDED AND THE COLUMN NO LONGER NAMES IT. Four rows carried #399
beside their rule issue and the first carried it alone, and a reader following
the column after this change would arrive at closed work, which is this page's
own subject happening on this page for the third time. What is left on each row
is the rule that owns what the row says happened, so that is what the cells hold:
#28 for a code that matched nothing, because the rule that a code which never
existed is answered exactly as one that was spent is #28's rather than the
route's, and #51, #55 and #54 for the three states of a record that did exist.

The post is named in this prose instead, where it can be written in the past
tense. `.github/lint/issue-pointer.sh` reads this column against the tracker and
refuses a cell naming a closed issue; it reads no prose, and a past-tense
sentence is a claim about a moment rather than about this commit.

THE COLUMN NAMED #74 ON THOSE FOUR ROWS AND THE POST HAS NOT BEEN #74 SINCE
2026-08-31. #71 split the act in two that day, the post became #399 and the
routine that creates the account became #398, and this page went on sending a
reader after the one response for all six cases to the issue that landed the
setup page. The two are different work: #74's own remaining clause is whether
those bytes render in a browser, which no route serves a refusal for.

Three documents were carried over to the new number before this one was, read at
the commit this repair was written against:

    $ git grep -ln '#399' origin/master -- docs/
    origin/master:docs/configuration.md
    origin/master:docs/migration-from-jfa-go.md
    origin/master:docs/what-an-invitation-can-never-do.md

So the stale pointer was not one page's oversight and not the whole tree's
either, which is why it was worth a change rather than a note: a reader who
checked one neighbouring page would have found the right number and concluded
this one was deliberate.

The last two are in the set for a reason worth keeping in front of whoever
implements them, because both look like cases that deserve their own message. A
limiter that answers "too many attempts" is an oracle: a stranger hits the limit
deliberately, learns where the boundary is, and learns from the difference which
of their guesses reached a lookup. A ceiling refusal that says the server has
created too many accounts today tells a stranger something true about the server
they had no other way to learn, and it does it while refusing them.

## The two refusals that are not in the set

`RefusedByAntiForgery` and `RefusedByValidation` produce their own responses and
must not be folded into this page.

ONE OF THE TWO IS SERVED NOW AND IT IS NOT THE RESPONSE THIS SECTION IMAGINES.
#78 landed the token, and a post that does not carry it is answered with the bad
request this route already gives a post it read nothing out of, under the same
five headers, rather than with a form carrying a reason. That is inside this
section's rule rather than an exception to it: what the rule refuses is folding
the case into the single indistinguishable refusal, and it is not folded. The
sentence below saying both happen after the invitation was honoured enough to
show the person a form is the half that does not fit this one, and it is the
reason the answer is a bad request rather than the form again: a post with no
good token was not necessarily made by anybody who was ever shown the form.

Both happen after the invitation was already honoured enough to show the person
a form. A password that is too short, a confirmation that does not match or a
username already taken are the person's own answers being wrong, and a page that
responds to those with "this link cannot be used" sends somebody away who was
one field from an account. Neither reveals anything about the code, because by
that point the code has already been accepted.

The username collision does disclose that a username exists on this server. That
is a disclosure with no way around it if the person is to choose their own name,
it is #67's, and it is already written down under what is not defended rather
than hidden here.

THE SHAPE HALF OF THAT REFUSAL IS BUILT AND DISCLOSES NOTHING, which is worth
separating from the collision it sits beside. A name the server's expression
refuses is refused out of the request alone, before any code is looked at and
before any use is spent, so the answer says nothing about the code and nothing
about which accounts exist:

    git grep -n 'public static string? WhyRefused' -- Jellyfin.Plugin.Invites/Setup/UsernameRules.cs
    Jellyfin.Plugin.Invites/Setup/UsernameRules.cs:122:    public static string? WhyRefused(string? username)

What is unbuilt is the collision, and it is the half that carries the
disclosure.

## What "identical" covers

Comparing the visible text is not the property. The list below is what the
response is compared on, and it is the list a test asserts rather than a
description of one:

- the status code
- the response body, byte for byte
- the `Content-Type` and the content length
- every header the route sets, by name and by value. There are five, and they
  are read off the route rather than listed from memory:

      git grep -nE 'headers\.(ContentSecurityPolicy|XFrameOptions|XContentTypeOptions|CacheControl)|headers\[ReferrerPolicy\]' -- Jellyfin.Plugin.Invites/Controllers/RedeemController.cs
      Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:354:        headers.ContentSecurityPolicy = policy;
      Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:355:        headers.XFrameOptions = "DENY";
      Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:356:        headers.XContentTypeOptions = "nosniff";
      Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:357:        headers.CacheControl = "no-store";
      Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:358:        headers[ReferrerPolicy] = "no-referrer";

  EVERY LINE NUMBER IN THAT PASTE MOVED BY THREE WHEN THE ROUTE TOOK THE
  CEILING ON HOW MANY ACCOUNTS MAY BE CREATED IN A WINDOW, and none of the five
  values changed. It is re-made from the command rather than adjusted by the
  difference, which is the rule the check enforces and the reason it refuses a
  quiet renumbering.

  THE FIRST OF THE FIVE READS A PARAMETER NOW AND USED TO NAME THE SETUP PAGE.
  When the route served one page the policy could be written where it was set; a
  route serving two pages under one set of headers has to be handed the policy of
  the page it is serving. The move is stated rather than renumbered quietly,
  because a reader comparing this paste against an older copy would otherwise see
  a value change with no reason beside it.

  Whether that is the right set is #78's. That it is the set a refusal has to
  match is this page's, and the paste above is judged by
  `.github/lint/pasted-line-reference.sh`, so a value moved in the route reddens
  this list instead of leaving it describing a response nobody serves that way.
- the presence and value of any header the plugin sets beside those five

Nothing in the response may be interpolated from the case. A body assembled from
a per-case string differs by its length even when the text is identical in a
reader's eye, and a length is the cheapest thing in the response to measure. The
page is one served resource, not a template with a reason in it.

## Timing, and what a test can honestly say about it

#77 asks that the timing not vary with whether the code exists. That clause
cannot be discharged by comparing two recorded responses, and a test that
measures durations on a shared runner measures the runner.

What a test can assert is that the same work is done either way: a well-formed
code is canonicalised, hashed and looked up whether or not it matches, and the
comparison against a stored value is the constant-time one from #29 rather than
an equality that returns on the first differing byte. Those are properties of
the routine in #56 and are assertable without a stopwatch.

What is left after that is a claim rather than a measurement, and it stays
written as one. The plugin does not defend against an attacker who can measure
the difference between a store hit and a store miss through the noise of a media
server, and no test in this repository will say otherwise.

## Where the difference does live

The operator sees what the person cannot. Every case above appends its own
outcome to the attempt trail, which is #43, and the operator's view of it is
#89. An invitation presented eleven times and never redeemed is the row worth
looking at, and it exists precisely because the page in front of the stranger
said nothing.

## One assertion, not four

The byte-for-byte comparison is a done-condition clause in this issue, in #55,
in #31 and in #107. Written four times it is four tests that drift apart, and
the first to be relaxed is the one nobody remembers is load-bearing. The likely
split is one test comparing bodies and another comparing bodies and headers,
both green, while the responses differ by a header only one of them looked at.
That is the oracle all four exist to close, reintroduced by the way they were
tested.

The comparison lives once, at the route level, because that is the only place
that sees the response the server actually sends. It exists:

    git grep -n 'public async Task EveryRefusalThisRouteServesIsTheSameResponse' -- Jellyfin.Plugin.Invites.Tests/RedeemPostTests.cs
    Jellyfin.Plugin.Invites.Tests/RedeemPostTests.cs:176:    public async Task EveryRefusalThisRouteServesIsTheSameResponse()

It drives five of the six cases through the action and compares what came back on
everything the list above names, reading the headers off the response rather than
naming them, so a header the route starts setting joins the comparison without
anybody adding it. #107 widens it rather than writing a second one, and the other
three issues add their case to it.

THIS PARAGRAPH SAID THE SIXTH CASE IS NOT REACHED, BECAUSE NOTHING REFUSED A
REDEMPTION FOR A CEILING. Something does: #33's third ceiling landed and the
comparison drives all six.

That case is the one which would most obviously deserve a message of its own and
most obviously must not have one. A page saying the server has created too many
accounts today tells a stranger something true about the server they had no other
way to learn, and it does it while refusing them. The person who meets it is
usually not an attacker, and telling them nothing is the price of not telling the
one who is.

This document is where the case list and the compared-on list are kept, so adding
a case is an edit here and a case there rather than a new test.

## The page is served by this plugin and nothing else

The presentation rules for the setup page apply to this one unchanged: nothing
loaded from another host, no third-party script, no font service, no analytics.
They are in [docs/setup-never-asks.md](setup-never-asks.md) and are not repeated
here.

The page carries no code, no invitation identifier, and nothing that varies
between the cases. It may sit in a browser history on a shared machine, which is
the same reason the completion page in #79 carries nothing either.

## What is settled and what is not

Settled here: the wording, the case list, what identical covers, which two
refusals are outside the set, and where the comparison lives.

THIS SECTION SAID THE EXACT SECURITY HEADER VALUES ARE NOT SETTLED BY ANYBODY
YET. Five are set on the route that serves the page, and the suite asserts four
of them by name and value and the policy by the hash of the page's own style, so
a reader who came here to find out what a refusal has to carry was told nobody
had chosen while the tree had chosen. The list above carries them now.

What that does not settle is whether the set of headers is right or complete,
which is #78's and is where its clauses live rather than here.

THE STATUS CODE WAS THE ONE THING THIS SECTION LEFT UNSETTLED AND IT IS SETTLED.
It belonged to the route that first serves a refusal, that route is the post, and
the post picked `403 Forbidden`:

    git grep -n 'refusal.StatusCode = StatusCodes.Status403Forbidden;' -- Jellyfin.Plugin.Invites/Controllers/RedeemController.cs
    Jellyfin.Plugin.Invites/Controllers/RedeemController.cs:323:        refusal.StatusCode = StatusCodes.Status403Forbidden;

Why that one, in the terms this page argues everything else in. It is true of
every case in the table without narrowing any of them: the server understood the
request and will not act on it, which is as true of a code that never existed as
of one a limit refused. It is not a redirect, so nothing about it suggests that
somewhere else would work. And it says nothing about whether the address ever
named an invitation, which `404 Not Found` would: that address is served, so a
not-found there would be a claim about the address rather than about the
invitation, and a stranger would read it as the more useful of the two answers.

The choice binds all six cases at once and binds the refusal half of the GET with
them, which is what #75 and #77 build against rather than choose again.
