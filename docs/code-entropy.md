# How long an invitation code is

An invitation code is a bearer credential for account creation. Whoever holds
one gets an account, so its length is not a matter of taste and it is not picked
because a number of characters looks long. It is read off a calculation whose
inputs are written down, so that a later disagreement is a disagreement about an
input rather than about a preference.

This page is the calculation, and #28 is where the requirement it states is
stated. The routine that requirement is on exists now. #49 landed
`InvitationCode.Mint`, which draws from the platform's cryptographic source and
carries the number below as a length and an alphabet rather than as a comment:

    $ git grep -n 'public const int Length\|private const string Alphabet\|RandomNumberGenerator.Fill' -- Jellyfin.Plugin.Invites/Codes/InvitationCode.cs
    Jellyfin.Plugin.Invites/Codes/InvitationCode.cs:62:    public const int Length = 26;
    Jellyfin.Plugin.Invites/Codes/InvitationCode.cs:68:    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    Jellyfin.Plugin.Invites/Codes/InvitationCode.cs:77:        RandomNumberGenerator.Fill(draws);

Twenty-six independent uniform draws over thirty-two characters is 130 bits,
which clears the requirement below with two bits to spare. What the page still
does not do is enforce anything, and the last section says exactly how far the
suite reaches instead.

## The model

One attacker making blind guesses at the redemption endpoint. Codes are drawn
uniformly and independently from a space of `2^b`, and `N` of them are live at
the same moment, so any one guess hits with probability `N/2^b`. Over `A`
attempts the chance of at least one hit is

    1 - (1 - N/2^b)^A

which is `A*N/2^b` to well within a bit for as long as that quantity stays far
below one, and it does at every size considered here. Reading the required `b`
off that gives

    b = log2(A) + log2(N) + margin

Every input enters as a logarithm, which is why being wrong about one of them by
a factor of ten costs three bits rather than thirty. It is also why the answer is
insensitive to the arguments most likely to be had about it.

The model deliberately ignores two things that would make the attacker's job
harder, so that the number survives both being wrong. It assumes every guess is
against the full live set at once, and it assumes the attacker never has to wait
for anything but the rate limit.

## The inputs

Each is an upper bound rather than a likely value, and each names who owns it.

`N`, the live invitations on a busy server, taken as `10^4`. This is an
assumption until #33 lands, which is the issue that bounds what one operator
action can create and what the store can grow into. If that issue sets a ceiling
lower than ten thousand, the requirement below falls; if it allows more, the
requirement rises by a bit for every doubling.

`A`, the attempts, in two scenarios, because the answer has to survive the rate
limiter being absent as well as present.

The first assumes no limiter at all and a determined attacker sustaining `10^4`
guesses a second for ten years. The second assumes the limiter from #31 holds the
endpoint to ten guesses a second across all sources for one year. #31 owns the
second of those numbers and the requirement moves with it.

The margin, `2^-32`, meaning about one chance in four thousand million that any
guess ever lands. This one is chosen here rather than owned elsewhere, and it is
the input a reader is most likely to want to argue with. It is stated as a
separate term so that arguing with it costs exactly its own bits and nothing
else moves.

## The arithmetic

    $ awk 'BEGIN{ l2=log(2); N=10000; A1=10000*315360000; A2=10*31536000;
        printf "required bits, unthrottled = %.2f\n", (log(A1)+log(N))/l2 + 32;
        printf "required bits, throttled   = %.2f\n", (log(A2)+log(N))/l2 + 32;
        printf "P at 128 bits, unthrottled = 2^%.1f\n", (log(A1)+log(N))/l2 - 128;
        printf "P at  64 bits, unthrottled = 2^%.1f\n", (log(A1)+log(N))/l2 - 64;
        printf "P at  64 bits, throttled   = 2^%.1f\n", (log(A2)+log(N))/l2 - 64; }'
    required bits, unthrottled = 86.81
    required bits, throttled   = 73.52
    P at 128 bits, unthrottled = 2^-73.2
    P at  64 bits, unthrottled = 2^-9.2
    P at  64 bits, throttled   = 2^-22.5

Eighty-seven bits with no limiter, seventy-four with one.

## The number, and the two conditions that come with it

A code carries 128 bits drawn from a cryptographic source.

It clears the harder of the two requirements by forty-one bits, it is the next
size that maps onto a whole number of bytes and onto the source #49 draws from,
and the headroom is what absorbs `N` being wrong by several orders of magnitude
without anybody having to redo this page.

Two conditions are part of the number rather than separate good practice.

The whole 128 bits are random. No embedded mint time, no operator identifier, no
prefix that groups codes by anything, no checksum that narrows the search.
Every such field is subtracted from the exponent above, and a field that looks
like metadata to the person who added it looks like a shorter code to the
attacker.

Nothing outside the code may shorten the search. That is what makes the listing
surface in #85 part of this requirement rather than a neighbouring concern: a
route that returns codes to a caller makes the arithmetic on this page
irrelevant, whatever the length is.

## What a short code costs, in the same units

A code short enough to be read over the phone is a real request and it has a
price, and the two rows above put a figure on it rather than leaving it as a
judgement.

At sixty-four bits with no limiter, the chance of a hit over that ten-year run is
`2^-9.2`, about one in six hundred. That is not a margin. At sixty-four bits with
the limiter from #31 holding, it is `2^-22.5`.

So a short code is not unsafe by arithmetic. It is unsafe by dependency: it moves
the whole guarantee onto the limiter, and a limiter is a runtime control that can
be misconfigured, restarted, or outrun by an attacker spread across enough
sources. A code length cannot be any of those things.

If a shorter code is wanted anyway, the honest form of the trade is a line in
[docs/threat-model.md](threat-model.md) saying the guess is mitigated by #31
alone, rather than two defences where there is one.

## What this page does not decide

The character set, and therefore the printed length. Both candidates carry the
full 128 bits and differ only in what they do to somebody transcribing one:

    $ awk 'BEGIN{ printf "128 bits at 5 bits a character = %d characters\n", int((128+4)/5);
        printf "128 bits at 6 bits a character = %d characters\n", int((128+5)/6); }'
    128 bits at 5 bits a character = 26 characters
    128 bits at 6 bits a character = 22 characters

That choice was #49's and it took the first row: thirty-two characters, one
case, with the four most transcribable-wrong letters left out, and twenty-six of
them. The reasoning for the alphabet is at the routine rather than here, because
it is about somebody reading a code off a screen and not about the arithmetic.

Recording the answer changes nothing this page requires. The requirement is the
entropy and not the length, so an encoding that adds characters without adding
bits satisfies nothing here, and a later change to either constant is checked
against the arithmetic above rather than against the number in this paragraph.

The comparison the code is subjected to once it arrives is #29, and the four
failures being indistinguishable to the caller is #55 and
[docs/refusal-response.md](refusal-response.md). Neither is what this page is
about, and both matter to the same attacker: the arithmetic above is the cost of
one blind guess, and an oracle that says which guess was close turns guessing
into something else entirely.

## What is not claimed

Nothing reads this page. No check derives the requirement from the arithmetic
above, so raising the requirement here and leaving the constants where they are
passes every route, and that direction is the one nothing covers.

The other direction is covered, and it is worth saying how far rather than
leaving it as "there are tests". `AMintedCodeIsTwentySixCharactersFromTheAlphabet`
asserts the length against the literal twenty-six and every character of a
minted code against the alphabet, and `EveryCharacterOfTheAlphabetIsMinted`
asserts the draw reaches all thirty-two. The second is the one that catches the
near-miss, because the mask that turns a byte into a position is one character
away from halving the alphabet without touching the length, the shape or
anything else on this page. Measured by making that one-character change and
running the suite:

    $ dotnet test --configuration Release --nologo
    Jellyfin.Plugin.Invites.Tests.InvitationCodeTests.EveryCharacterOfTheAlphabetIsMinted [FAIL]

The totals that used to sit under that line are gone rather than corrected. They
read 342 passed of 351 and the suite is larger every week, so a reader re-running
this got different numbers and no way to tell whether the finding had moved with
them. What the sentence rests on is which test reds, and the line above is that.

One test, and it names the alphabet rather than the entropy, so what the suite
holds is the two constants the number was encoded as. That the twenty-six draws
are independent and uniform is a property of the source and of a mask over a
byte, and no test asserts it.

The greppable rule that exists nearby refuses a non-cryptographic source, which
is a different failure again:

    $ bash .github/lint/invariants.sh selftest | grep weak-random
    bites weak-random (#49): .github/lint/fixtures/weak-random.trip.cs trips it, .github/lint/fixtures/weak-random.clean.cs does not

The two scenarios are assumptions about an attacker, not measurements of one.
Nothing here has been checked against an attempt rate anybody has observed
against a Jellyfin server, and the ten thousand live invitations is a bound
chosen before #33 has set one.
