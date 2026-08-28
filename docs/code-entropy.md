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
which clears the requirement below with two bits to spare. The suite reads this
page rather than a reader having to: the links between an input below and that
constant are held by `CodeEntropyPageTests`, and the last two sections say
exactly how far that reaches and where it stops.

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

`N`, the live invitations on a busy server, taken as `10^4`. #33 is the issue
that bounds what one operator action can create and what the store can grow
into, it is open, and this input is therefore not read off a decided ceiling.

It is not unbounded in the tree either, which is worth knowing before anybody
argues about the number. Minting refuses at five hundred live invitations, and
the refusal is a branch rather than a comment:

    $ git grep -n 'public const int LiveCeiling = \|if (live >= LiveCeiling)' -- Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:113:    public const int LiveCeiling = 500;
    Jellyfin.Plugin.Invites/Invitations/InvitationOperations.cs:247:            if (live >= LiveCeiling)

The input stays at ten thousand rather than moving to five hundred, and that is
a decision rather than an oversight. The requirement on this page is what a
later ceiling gets checked against, so an input read out of the constant it is
meant to bound would move with that constant and check nothing. What the
constant buys is headroom, and how much is derived rather than asserted:

    $ awk 'BEGIN{ l2=log(2); A1=10000*315360000; A2=10*31536000;
        printf "required bits at N=10^4, unthrottled = %.2f\n", (log(A1)+log(10000))/l2 + 32;
        printf "required bits at N=500,  unthrottled = %.2f\n", (log(A1)+log(500))/l2 + 32;
        printf "required bits at N=10^4, throttled   = %.2f\n", (log(A2)+log(10000))/l2 + 32;
        printf "required bits at N=500,  throttled   = %.2f\n", (log(A2)+log(500))/l2 + 32; }'
    required bits at N=10^4, unthrottled = 86.81
    required bits at N=500,  unthrottled = 82.49
    required bits at N=10^4, throttled   = 73.52
    required bits at N=500,  throttled   = 69.20

Four bits and a third, which is `log2(20)` and is what a factor of twenty costs
when every input enters as a logarithm. The number below clears every row
above, so nothing on this page moves for it.

If #33 sets a ceiling above ten thousand, the requirement rises by a bit for
every doubling and this input moves with it. If it confirms a ceiling below,
the requirement falls and the headroom above is what it falls by.

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

## What reads this page

Three links stand between an input above and a constant in the source. The
figures pasted under the arithmetic have to be the ones the inputs produce, the
requirement stated in words has to clear those figures, and the bits a minted
code carries have to clear that requirement. `CodeEntropyPageTests` holds all
three, and each was proven by breaking it on its own.

This section used to say that nothing reads this page, and that raising the
requirement here while leaving the constants where they are passes every route.
Raising the stated number from 128 to 131 reds one test and nothing else:

    $ dotnet test Jellyfin.Plugin.Invites.sln --nologo --configuration Release --no-build
    Jellyfin.Plugin.Invites.Tests.CodeEntropyPageTests.TheCodeThisPluginMintsClearsTheRequirementTheEntropyPageStates [FAIL]

With that same edit in place and that leg's condition neutralised the whole
suite is green, so nothing else in this tree refuses the direction and the leg
is the whole of what the sentence above rests on.

Raising an input instead, and re-running the block so the pastes agree, reds
`TheRequirementTheEntropyPageStatesClearsItsOwnArithmetic` alone. Editing a
pasted figure without touching the inputs reds
`TheFiguresTheEntropyPagePastesAreTheOnesItsInputsProduce` alone. Renaming an
input so the patterns stop matching reds
`TheScanFindsTheInputsTheEntropyPageDeclares`, which is there so that a page
which has stopped being read is not mistaken for one that agrees.

What is read is numerals in prose. An input moved into a sentence written some
other way stops being read, which is what the scan leg reds on rather than
passes over, and whether the model above is the right model is a judgement no
reading of this tree makes.

## What is not claimed

The bits a code carries are computed from the length and from the number of
distinct characters a run of mints produces, so what is held is the alphabet's
size and not the draw's uniformity. A mint reaching every character but reaching
some of them far more often than others would carry less than the figure above
and is invisible here. That the draw is uniform rests on the source and on a
mask over a byte, and no test asserts it.

The near-miss that mask is one character from is halving the alphabet, which
costs a bit a character without touching the length, the shape or anything else
on this page. Measured by making that one-character change and running the
suite:

    $ dotnet test Jellyfin.Plugin.Invites.sln --nologo --configuration Release --no-build
    Jellyfin.Plugin.Invites.Tests.CodeEntropyPageTests.TheCodeThisPluginMintsClearsTheRequirementTheEntropyPageStates [FAIL]
    Jellyfin.Plugin.Invites.Tests.InvitationCodeTests.EveryCharacterOfTheAlphabetIsMinted [FAIL]

The totals that used to sit under a line like that are gone rather than
corrected. They read 342 passed of 351 and the suite is larger every week, so a
reader re-running this got different numbers and no way to tell whether the
finding had moved with them. What the sentence rests on is which tests red, and
the lines above are that.

`AMintedCodeIsTwentySixCharactersFromTheAlphabet` asserts the length against the
literal twenty-six and every character of a minted code against the alphabet,
and `EveryCharacterOfTheAlphabetIsMinted` asserts the draw reaches all
thirty-two. Those two hold the constants the number was encoded as. The three
above hold the number.

The greppable rule that exists nearby refuses a non-cryptographic source, which
is a different failure again:

    $ bash .github/lint/invariants.sh selftest | grep weak-random
    bites weak-random (#49): .github/lint/fixtures/weak-random.trip.cs trips it, .github/lint/fixtures/weak-random.clean.cs does not

The two scenarios are assumptions about an attacker, not measurements of one.
Nothing here has been checked against an attempt rate anybody has observed
against a Jellyfin server, and the ten thousand live invitations is a bound
chosen before #33 has set one.
