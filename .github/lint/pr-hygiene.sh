#!/usr/bin/env bash
# Deterministic pull-request hygiene for this repository.
#
# Every leg below is one a reader could have checked themselves in a few seconds.
# The check exists so nobody has to, and so the answer is the same every time. A
# leg that would need somebody to judge whether a sentence says anything is not
# here and will not be added: that judgement is what the review is for, and a
# machine pretending to make it turns a red mark into an argument.
#
# The legs are in two tiers, and the tier is part of what the leg means:
#
#   refuse   the fact is not in doubt and the repair is one edit
#   note     a convention worth seeing, never a reason to hold a change
#
# The size leg is a note rather than a refusal on purpose. A rename, a document
# migration or a first implementation legitimately exceeds any cap, so a size
# that reds is a check people learn to route around.
#
# One leg from the gate this repository is copying is deliberately absent: a
# version bump in build.yaml having to touch a changelog. There is no changelog
# in this tree yet and #124 is where the first one is written, so the leg would
# either refuse every version bump or be a rule with nothing behind it. It
# belongs with that issue.
#
# The input is plain text rather than JSON, so the checks run the same way here
# and in a workflow and need no tool beyond a shell. What produces it is
# .github/workflows/pr-hygiene.yaml.
#
# Two modes:
#   check <dir>   read the four files below out of <dir> and judge them
#   selftest      fail unless every leg fires on its own fixture, fires alone,
#                 and stays quiet on the clean one
#
# <dir> holds:
#   body.txt      the pull-request body, verbatim
#   commits.txt   one commit message per line, newlines flattened to spaces
#   files.txt     one changed file per line, "<changed-lines> <path>"
#   author.txt    one line, "<author type> <author association>"
set -uo pipefail

FIXTURES=".github/lint/fixtures/pr-hygiene"

# The number of changed lines above which a change is noted as hard to read. It
# is the inherited 400 and it is a note, so nothing turns on the exact number.
SIZE_NOTE_AT=400

# Who the refusing tier is for. The issue-reference convention is this
# repository's own, and somebody arriving from outside has no way to know which
# issue numbers it expects, so refusing their work on it is a ritual rather than
# a caught slip. A bot cannot know the convention at all. The two skips have
# different reasons and are printed separately, and neither means the linkage
# does not matter: it moves to whoever picks the change up.
INSIDE_ASSOCIATIONS="OWNER MEMBER"

# A closing keyword standing next to a hash reference, in a sentence that denies
# it. The platform reads the keyword and the reference and does not read the
# denial, so a body written to say a change leaves an issue OPEN is the thing
# that closes it. #338 is where the mechanism was measured. The window is short
# and stops at a full stop or at a second hash, so the denial and the reference
# have to be in one clause rather than merely on one line.
DENIED_CLOSING='(^|[^[:alnum:]])(not|never|nor|neither|if|whether|unless|n[^[:alnum:]]t)[^.#]{0,59}[^[:alnum:].#](close|closes|closed|fix|fixes|fixed|resolve|resolves|resolved)[[:space:]]*:?[[:space:]]*#[0-9]+'

# The same failure in the other tense. A body cannot DECLARE that a merge closed
# an issue: the merge has not happened when the body is read. So a past-tense
# closing keyword beside a hash reference is always a retrospective mention of
# some other change, and it always closes the issue it mentions. Two bodies on
# this repository carried one and both closed an issue nobody was deciding
# about. A declaration standing at the start of its own line is left alone,
# because "Fixed #12" written there is somebody saying what their change does.
RETROSPECTIVE_CLOSING='^.*[^[:space:]].*[^[:alnum:].#](closed|fixed|resolved)[[:space:]]*:?[[:space:]]*#[0-9]+'

# A close that nobody weighed. The two legs above refuse a close somebody did not
# mean; this refuses one nobody said anything about. #338 measured twelve issues
# closed as completed with their Done-when unmet, and the branch name was
# innocent in all twelve: what closed them was a keyword in a body, read by the
# platform and by nobody else. A machine cannot judge whether a Done-when is met -
# that is a reading - but it can make the close a deliberate act instead of a
# reflex, by asking for the sentence that says why. That decision is recorded on
# #338 and this is it.
#
# So a body that would close an issue carries a line declaring it and saying
# something after the reference, and a body that only refers to an issue writes
# "Part of #N" or "Refs #N" and closes nothing. The price of a close is one
# honest sentence.
CLOSING_REFERENCE='(close|closes|closed|fix|fixes|fixed|resolve|resolves|resolved)[[:space:]]*:?[[:space:]]*#[0-9]+'
CLOSING_DECLARATION='^[[:space:]]*(Close|Closes|Closing|Fix|Fixes|Fixing|Resolve|Resolves|Resolving)[[:space:]]*:?[[:space:]]*#[0-9]+'

# How much has to follow the reference on the declaring line. It counts
# characters and cannot judge whether they say anything, which is the review's
# job; what it buys is that "Closes #17." on its own is not a reason and does not
# pass for one.
REASON_AFTER_THE_REFERENCE=20

fail=0
fired=""

# Records that a leg fired. severity is refuse or note; only refuse moves the
# exit status.
fire() {
  local severity="$1" id="$2" detail="$3"
  fired="${fired} ${id}"
  if [ "$severity" = "refuse" ]; then
    echo "::error::${id}: ${detail}"
    fail=1
  else
    echo "::warning::${id}: ${detail}"
  fi
}

# Reads a file out of the input directory, empty if it is not there, so a
# missing file is judged rather than crashing the run.
slurp() {
  [ -f "$1" ] && cat -- "$1" || true
}

judge() {
  local dir="$1"
  local body commits files author author_type association tier
  body=$(slurp "$dir/body.txt")
  commits=$(slurp "$dir/commits.txt")
  files=$(slurp "$dir/files.txt")
  author=$(slurp "$dir/author.txt")
  author_type=$(printf '%s' "$author" | awk '{print $1}')
  association=$(printf '%s' "$author" | awk '{print $2}')

  tier=refuse
  if [ "$author_type" = "Bot" ]; then
    echo "note  the issue-reference legs are advisory here: the author is a bot, which cannot know this repository's issue numbers."
    tier=note
  elif ! printf '%s' " $INSIDE_ASSOCIATIONS " | grep -qF " ${association:-NONE} "; then
    echo "note  the issue-reference legs are advisory here: the author is outside this repository (${association:-NONE}), and the convention is this repository's own. The linkage is still wanted and moves to whoever picks the change up."
    tier=note
  fi

  # --- the pull request says which issue it is for -------------------------
  # A change nobody can trace to an issue is a change whose reason exists only
  # in somebody's head, and the body is the one place a reader looks first.
  if printf '%s' "$body" | grep -qE '#[0-9]+'; then
    echo "ok    body-names-an-issue"
  else
    fire "$tier" body-names-an-issue \
      "the pull-request body names no issue. Write Closes #N or Refs #N in it, so the reason for the change is one click away from the change."
  fi

  # --- every commit says which issue it is for ------------------------------
  # The body is edited and the commits are what survives into the mainline. A
  # commit that names no issue is the one somebody reads a year later with no
  # way back to why it happened.
  local unnamed
  unnamed=$(printf '%s' "$commits" | sed '/^$/d' | grep -vE '#[0-9]+' || true)
  if [ -z "$commits" ]; then
    fire "$tier" commits-name-an-issue \
      "no commit messages were read, so nothing here proves they name an issue. Failing rather than reporting a check that looked at nothing."
  elif [ -n "$unnamed" ]; then
    fire "$tier" commits-name-an-issue \
      "these commit messages name no issue: $(printf '%s' "$unnamed" | tr '\n' '|')"
  else
    echo "ok    commits-name-an-issue"
  fi

  # --- a disclaimed closing keyword still closes ---------------------------
  # The failure this refuses is not a slip of the pen. It is the sentence a
  # careful body carries on purpose: "This does not close #28", written to tell
  # a reader which clause is unmet. The platform closes #28 on it, as
  # COMPLETED, with no commit behind the close, and somebody reopens it by hand
  # afterwards. Measured over every merged pull request on this repository,
  # twelve bodies carried such a sentence and every one of the twelve closed the
  # issue it was denying.
  #
  # The repair is one edit and it is in the message: write the number without a
  # hash. "It does not close issue 28" says the same thing to a reader and
  # nothing at all to the platform.
  #
  # This leg refuses for every author. The others above are advisory outside
  # this repository because they enforce a local convention a stranger cannot
  # know; this one is a platform behaviour that reaches everybody equally, and
  # an outside contributor whose body silently closes an issue is the same
  # defect with nobody watching for it.
  local denied
  denied=$(printf '%s' "$body" | grep -inE "$DENIED_CLOSING" || true)
  if [ -n "$denied" ]; then
    fire refuse closing-keyword-is-deliberate \
      "this body denies a closing keyword standing next to a hash reference. The platform reads the keyword and not the denial, so merging this closes that issue as completed with its Done-when unmet. Write the number without a hash, as \"does not close issue N\": $(printf '%s' "$denied" | tr '\n' '|')"
  else
    echo "ok    closing-keyword-is-deliberate"
  fi

  # --- a retrospective closing keyword also closes --------------------------
  # The same failure in the other tense, and it is a separate leg rather than a
  # second arm on the one above, because the selftest compares SETS of leg ids:
  # two shapes firing one id are indistinguishable to it, and the second could
  # be lost without anything going red.
  local retrospective
  retrospective=$(printf '%s' "$body" | grep -inE "$RETROSPECTIVE_CLOSING" || true)
  if [ -n "$retrospective" ]; then
    fire refuse closing-keyword-is-not-retrospective \
      "this body mentions a close that already happened, with a closing keyword beside a hash reference. The platform does not read the tense, so merging this closes that issue too. Write the number without a hash, as \"the commit that closed issue N\": $(printf '%s' "$retrospective" | tr '\n' '|')"
  else
    echo "ok    closing-keyword-is-not-retrospective"
  fi

  # --- a close is declared and given a reason -------------------------------
  # Every number this body would close, against the numbers a line declares with
  # something after the reference. The two legs above own the denied and the
  # retrospective shapes, so a number either of them has already named is left to
  # them rather than reported twice: their patterns end at the reference, so the
  # last hash of each match is the number they are about, and this leg and they
  # cannot disagree about which one that is.
  local closes declared spoken_for undeclared
  closes=$(printf '%s' "$body" | grep -oiE "$CLOSING_REFERENCE" \
    | grep -oE '#[0-9]+$' | tr -d '#' | sort -u)
  spoken_for=$( { printf '%s' "$body" | grep -oiE "$DENIED_CLOSING" || true
                  printf '%s' "$body" | grep -oiE "$RETROSPECTIVE_CLOSING" || true
                } | grep -oE '#[0-9]+$' | tr -d '#' | sort -u )
  declared=$(printf '%s' "$body" | awk -v min="$REASON_AFTER_THE_REFERENCE" -v pat="$CLOSING_DECLARATION" '
    $0 ~ pat {
      if (match($0, /#[0-9]+/)) {
        number = substr($0, RSTART + 1, RLENGTH - 1)
        rest = substr($0, RSTART + RLENGTH)
        if (length(rest) >= min) { print number }
      }
    }' | sort -u)
  undeclared=$(comm -23 \
    <(comm -23 <(printf '%s\n' "$closes" | sed '/^$/d') <(printf '%s\n' "$spoken_for" | sed '/^$/d')) \
    <(printf '%s\n' "$declared" | sed '/^$/d'))
  if [ -n "$undeclared" ]; then
    fire "$tier" closing-keyword-is-declared \
      "this body closes an issue and says nothing about it: $(printf '%s' "$undeclared" | tr '\n' '|'). The platform reads the keyword and closes the issue as completed whether or not anybody weighed its Done-when, which happened twelve times here before #338 measured it. Write the close on its own line and say why, as \"Closes #N because ...\", or refer to the issue without a closing word, as \"Part of #N\"."
  else
    echo "ok    closing-keyword-is-declared"
  fi

  # --- the change is small enough to read ----------------------------------
  local total
  total=$(printf '%s' "$files" | sed '/^$/d' | awk '{s+=$1} END {print s+0}')
  if [ "$total" -ge "$SIZE_NOTE_AT" ]; then
    fire note change-is-readable \
      "${total} changed lines, at or above ${SIZE_NOTE_AT}. Nothing is held on this. If the change is one topic it is fine, and if it is two it is easier to review split."
  else
    echo "ok    change-is-readable (${total} changed lines)"
  fi

  # --- code and its tests move together ------------------------------------
  # Matched on the .Tests directory rather than on a project name, so the rename
  # off the template does not silently stop the leg from seeing anything.
  local code tests
  code=$(printf '%s' "$files" | sed '/^$/d' | awk '{print $2}' | grep -E '\.cs$' | grep -vE '(^|/)[^/]*\.Tests/' || true)
  tests=$(printf '%s' "$files" | sed '/^$/d' | awk '{print $2}' | grep -E '(^|/)[^/]*\.Tests/' || true)
  if [ -n "$code" ] && [ -z "$tests" ]; then
    fire note tests-follow-the-plugin \
      "plugin code changed and nothing under a .Tests directory did: $(printf '%s' "$code" | tr '\n' '|')"
  else
    echo "ok    tests-follow-the-plugin"
  fi

  return $fail
}

cmd_check() {
  local dir="${1:-}"
  if [ -z "$dir" ] || [ ! -d "$dir" ]; then
    echo "::error::pr-hygiene: no input directory. Failing rather than reporting a check that read nothing." >&2
    return 2
  fi
  judge "$dir"
}

# Runs one fixture and prints the ids that fired, so the selftest can compare a
# set rather than an exit status. A leg that reds for the wrong reason is a leg
# that will red on somebody else's change for the wrong reason.
fired_ids() {
  fail=0
  fired=""
  judge "$1" >/dev/null 2>&1
  printf '%s' "$fired" | tr ' ' '\n' | sed '/^$/d' | sort -u | tr '\n' ' ' | sed 's/ $//'
}

cmd_selftest() {
  local legs="body-names-an-issue commits-name-an-issue closing-keyword-is-deliberate closing-keyword-is-not-retrospective closing-keyword-is-declared change-is-readable tests-follow-the-plugin"
  local selftest_fail=0 leg got

  got=$(fired_ids "$FIXTURES/clean")
  if [ -n "$got" ]; then
    echo "::error::clean: the clean fixture fires [${got}], so a change that breaks no leg would still be marked."
    selftest_fail=1
  else
    echo "quiet clean: nothing fires"
  fi

  for leg in $legs; do
    if [ ! -d "$FIXTURES/${leg}.trip" ]; then
      echo "::error::${leg}: missing ${FIXTURES}/${leg}.trip. Every leg owns a fixture that fires it alone."
      selftest_fail=1
      continue
    fi
    got=$(fired_ids "$FIXTURES/${leg}.trip")
    if [ "$got" != "$leg" ]; then
      echo "::error::${leg}: its fixture fires [${got}] rather than [${leg}] alone, so a change breaking this leg is not reported for this leg alone."
      selftest_fail=1
    else
      echo "bites ${leg}: its fixture fires it and nothing else"
    fi
  done

  # The tier skip is a leg of its own: the same input that refuses for a member
  # must only note for somebody outside, or the skip is not there.
  if [ -d "$FIXTURES/outside-author" ]; then
    fail=0; fired=""
    judge "$FIXTURES/outside-author" >/dev/null 2>&1
    if [ "$fail" != "0" ]; then
      echo "::error::outside-author: the fixture refuses, so the tier skip does not hold and an outside contribution is failed on this repository's own convention."
      selftest_fail=1
    else
      echo "holds outside-author: the refusing tier is advisory for an outside author"
    fi
  else
    echo "::error::outside-author: missing ${FIXTURES}/outside-author, so nothing proves the tier skip."
    selftest_fail=1
  fi

  return $selftest_fail
}

case "${1:-}" in
  check)    shift; cmd_check "$@" ;;
  selftest) cmd_selftest ;;
  *)        echo "usage: $0 check <dir> | $0 selftest" >&2; exit 2 ;;
esac
