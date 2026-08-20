# What the guided setup may never ask

The setup page is a form on a server the person is about to trust, shown at the
moment they are most willing to type whatever it asks for. Somebody who has
just been invited to a media server by a friend will fill in a date of birth
because the page has a box for it. That willingness is the reason to fix the
question list before the page exists rather than after, and it is why this is a
refusal list rather than a guideline.

Half of what this document is built against is in the tree. The page is served
from the plugin, and what it requires of a password is one value in the source
with [docs/password-rules.md](password-rules.md) behind it. The other half is
not: nothing takes a submission, so there is no server-side validation and no
anti-forgery token, which are #75 and #78.

What the form asks for is held against the list below rather than read against
it by hand. A fourth field arriving at all reds the suite:

```
$ grep -n 'public void TheFormAsksForThreeThingsAndNoFourth' Jellyfin.Plugin.Invites.Tests/SetupPageTests.cs
```

Which questions are refusals is still a person's reading. No check can decide
whether a box asks for a legal name.

## The list

Each line is a refusal, and each carries the reason it is a refusal rather than
a preference.

| Never asked | Why |
| --- | --- |
| A password for any other service | There is no reason for this form to see a credential to somewhere else, and a page that asks for one has taught the person that this kind of page asks for one. |
| A password the person already uses on this server | There is no account yet. A field that asks for an existing password on this server is asking for a credential that either does not exist or belongs to somebody else, and both answers are worse than no field. |
| A payment detail of any kind | The plugin takes no money and has nowhere to put such a value. A field for one on a page that creates a free account is the shape of a phishing page. |
| A date of birth | Nothing in the plugin reads it and no policy field it sets depends on it. Parental controls on the server are set by the operator against an account, not by the account holder against themselves. |
| A postal address | Same test, same answer, and it is the field most likely to be typed in full by somebody being polite. |
| A legal name | The account has a username. A legal name is a second identifier the plugin would hold about a person for no reader. |
| A security question | It is a shared secret that is weaker than the password beside it, and it would have to be stored to be useful. |
| Anything phrased as optional that the plugin has no field for | An optional box is still a box somebody fills in. If nothing reads the value, the honest form does not ask, and the plugin never holds a value it cannot say why it holds. |
| Anything the operator could ask outside the plugin | If the operator wants to know something about the person they invited, they can ask the person. Routing that question through the setup form turns the plugin into a collector on the operator's behalf and puts the answer in this plugin's store. |

## What it does ask

A username, a password, and a password confirmation. That is the whole form.

Anything else needs two things before it exists: a row in
[docs/personal-data.md](personal-data.md) naming what reads it and what deletes
it, and a reason written where the change is argued. One candidate is named in
the plan and is not decided here, a contact address for the invited person,
which is decision 9 in #11.

## The presentation rules

These carry the same weight as the question list, because a page that asks
three safe questions and loads a script from somewhere else has handed the
answers to whoever controls that script.

The page loads nothing from another host. No third-party script, no font from a
font service, no analytics of any kind, and no image from anywhere but this
server. Everything the page needs is served by the plugin from the server the
person is already talking to.

The page says which server it belongs to, so the person can tell they are where
they think they are. A page that could be any server is a page that is easy to
imitate.

## What is enforced, and what is still read by a person

This section said there was no page and quoted a command returning nothing.
There is one. #74 landed it as an embedded resource served at
`GET /redeem/{code}`, and the paragraphs below say which of the rules above the
tree now refuses and which it does not.

- The list of questions is read by a person against the form. Nothing can
  decide whether a field asks for a legal name, and no check is owed for that.
  What `SetupPageTests` does say is narrower and worth having beside it: the
  form carries the three fields this page names and no fourth, so a question
  added to it cannot arrive without somebody moving that assertion.
- No resource from another host is a property of the served bytes, and it is
  refused twice. The page is read for four spellings of an address somewhere
  else, which is the same reading the configuration page gets, and the response
  carries a content security policy of `default-src 'none'` that names no
  origin at all. The one thing opened back up is the page's own style element,
  by hash rather than by an allowance, and the hash is computed from the page as
  it is served so the two cannot drift apart.
- Every field on the form appearing in the personal-data inventory is a
  comparison between two files, and it is the one rule here that could be
  refused by a machine rather than read. `SetupFormInventoryTests` is that
  comparison. It reads the field names out of the form region of the page this
  plugin serves and the field names out of the setup-form table in
  [docs/personal-data.md](personal-data.md), and requires the two sets to be
  equal, so neither a question added to the form nor a row left behind after a
  question is dropped passes unread.

Two things the page does not do yet, said here rather than left to be found.

It does not say which server it belongs to, which the presentation rules above
ask for. The page is served as bytes nothing is written into, which is what
leaves it with no place a presented code could reach the markup, and naming the
server means putting a value into it. Which value, and where it comes from, is
not decided here.

It reads no invitation. The same page is served for a code that was never
minted as for a live one, so nothing about a code is disclosed and nothing is
refused either. The refusal a spent, expired or revoked invitation is owed is
#75 and #77.

The gap this section used to name, that the inventory held rows for the
invitation record and for the attempt trail and no section for what the form
asks, is closed. [docs/personal-data.md](personal-data.md) carries a setup-form
table with a row per field, and for all three the row says this plugin stores
nothing: the username becomes the account in the server's own user database, and
the password and its confirmation are never held here at all. The prose under
what is never held said as much before. What was missing was rows, and rows are
what the comparison above reads.
