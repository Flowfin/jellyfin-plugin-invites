# What the guided setup may never ask

The setup page is a form on a server the person is about to trust, shown at the
moment they are most willing to type whatever it asks for. Somebody who has
just been invited to a media server by a friend will fill in a date of birth
because the page has a box for it. That willingness is the reason to fix the
question list before the page exists rather than after, and it is why this is a
refusal list rather than a guideline.

Nothing here is implemented. The page is #74, the server-side validation is
#75, the password rules are #76, and the cross-site protections are #78. This
document is what those four are built against.

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

## What none of this is yet

There is no page. Nothing in this repository serves anything, which is checkable
rather than a claim about work not yet done:

```
git grep -nE 'ControllerBase|ApiController|HttpGet|HttpPost' -- '*.cs'
exit=1
```

So no line above is enforced today, and three of them are things a test has to
say rather than a document. When the page lands, the enforcement each rule is
owed is:

- The list of questions is read by a person against the form. Nothing can
  decide whether a field asks for a legal name, and no check is owed for that.
- No resource from another host is a property of the served bytes and a check
  can decide it. #73 asks for a test that asserts the rendered page references
  no external origin, and that test lands with the page in #74.
- Every field on the form appearing in the personal-data inventory is a
  comparison between two files, and it is the one rule here that could be
  refused by a machine rather than read. Nothing does it today.

One gap is worth naming rather than leaving for whoever hits it. The inventory
in [docs/personal-data.md](personal-data.md) has rows for the invitation record
and rows for the attempt trail, and no section for what the form asks. The
username becomes the account and the password goes to the server, so neither is
held by this plugin, and the inventory says so in prose under what is never
held. But "every field on the form appears in the inventory" wants rows, and
there are none to point at. That section belongs to #34, which owns that file
and is open.
