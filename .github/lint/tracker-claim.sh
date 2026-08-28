#!/usr/bin/env bash
# A document saying an issue is open or closed is held to the tracker.
#
# `docs/tests-not-written.md` is a list of tests this plugin refuses to write and
# what replaces each one, and every row carries a status line saying where its
# replacement stands. Two of those rows went stale in exactly that sentence: the
# row went on reading as covered while what it said about its replacement had
# stopped being true. The leg that landed under #100 reads the NAMES a page
# writes and would have caught neither, and its own note says so.
#
# This is the other half, for the part of a status line a machine can decide. A
# sentence saying an issue is open or closed is a claim about a state something
# holds, so it is a fact rather than a judgement, and it ages the same way a
# pasted exit status does: silently, in well-formed prose, in a document nobody
# re-reads because nothing asks them to.
#
# What this refuses, over tracked markdown:
#
#   A present-tense claim that disagrees with the tracker.
#
#   A claim naming a number the listing does not carry. An issue that never
#   existed and a listing that did not reach far enough are the same bytes here,
#   so this refuses rather than passing over the one it cannot tell from the
#   other.
#
#   A run in which the pattern found no claim at all. A page reworded past the
#   pattern would otherwise report the same silence as a tree that makes no such
#   claim, which is how a check goes green forever.
#
# What it does NOT refuse, stated so a green run is not read as more than it is:
#
#   A PAST-TENSE sentence. "#26 was closed on 2026-08-06" is a claim about a
#   moment rather than about this commit, and the tracker cannot refute it: an
#   issue reopened since makes that sentence more worth keeping, not less. So the
#   pattern is present tense only and a past-tense mention is invisible to it.
#
#   The rest of a status line. "Neither part exists" and "the replacement covers
#   the risk" are judgements about the tree and about meaning, and no reading of
#   either subject makes them. The two rows that went stale were stale in
#   sentences of that kind as well, and this reaches only the clause that names a
#   number and a state.
#
#   A state other than open or closed. Whether a closed issue was completed or
#   dropped is a distinction the tracker draws and no document here writes, so
#   nothing reads it and a page that started writing it would not be judged.
#
# WHY THE LISTING IS HANDED IN RATHER THAN FETCHED, and it is the same trade the
# parity check writes at its own second mode. The tracker is a network call. A
# failed call and a document full of wrong claims arrive here as the same missing
# entries, so a check that refuses on that manufactures a refusal out of a
# timeout, and on a merge route that is a red gate somebody learns to re-run
# rather than to read. The caller fetches, in a step of its own, so a failed call
# fails as a failed call:
# .github/workflows/tracker-claim.yaml. The price is stated rather than hidden -
# a claim that goes stale on a Tuesday is unanswered until the next run.
#
# Two modes:
#   check <states-file> [root]  read tracked markdown under root and judge every
#                               present-tense claim against the listing
#   selftest                    fail unless each leg fires on its own fixture,
#                               fires alone, names the claim that moved and no
#                               other, and stays quiet on the clean pair
set -uo pipefail

FIXTURES=".github/lint/fixtures/tracker-claim"

# The claims one file carries, one per line, as "<line>\t<number>\t<state>".
#
# Fenced blocks are dropped before the scan. A document pastes commands and their
# output, and a listing pasted inside a fence is evidence of what a command said
# rather than a sentence the page is asserting, so reading one as a claim would
# refuse a page for quoting accurately.
claims_in() {
  local file="$1"
  awk '
    BEGIN { fenced = 0 }
    /^[[:space:]]*```/ { fenced = !fenced; next }
    fenced { next }
    {
      line = $0
      while (match(line, /#[0-9]+ is (still )?(open|closed)/)) {
        hit = substr(line, RSTART, RLENGTH)
        number = hit
        sub(/^#/, "", number)
        sub(/ .*$/, "", number)
        state = hit
        sub(/^.* /, "", state)
        print NR "\t" number "\t" toupper(state)
        line = substr(line, RSTART + RLENGTH)
      }
    }
  ' "$file"
}

# The state the listing gives one number, or the empty string when it names none.
# MERGED is read as CLOSED: a merged pull request is closed, and a document
# saying so is right rather than wrong about a distinction it does not draw.
state_of() {
  local number="$1" states_file="$2" state
  state=$(awk -F'\t' -v n="$number" '$1 == n { print $2; exit }' "$states_file")
  [ "$state" = "MERGED" ] && state=CLOSED
  printf '%s' "$state"
}

# Judges a set of claims and returns the number of legs that fired. claims is
# "<origin>\t<line>\t<number>\t<state>" per line, where origin is whatever the
# caller wants named in the message.
judge() {
  local claims="$1" states_file="$2" label="$3"
  local disagreed='' unknown='' bad=0 read_count=0
  local origin line number claimed actual

  if [ -z "$claims" ]; then
    echo "::error::${label}: no present-tense claim about an issue's state was found at all. A page reworded past the pattern and a tree that makes no such claim are the same silence from here, so this refuses rather than reporting the second."
    return 1
  fi

  while IFS=$'\t' read -r origin line number claimed; do
    [ -n "$number" ] || continue
    read_count=$((read_count + 1))
    actual=$(state_of "$number" "$states_file")
    if [ -z "$actual" ]; then
      unknown="${unknown}  ${origin}:${line}: #${number}, which the listing does not carry"$'\n'
    elif [ "$actual" != "$claimed" ]; then
      disagreed="${disagreed}  ${origin}:${line}: #${number} is written as ${claimed} and the tracker says ${actual}"$'\n'
    fi
  done <<< "$claims"

  if [ -n "$disagreed" ]; then
    echo "::error::${label}: a document says an issue is open or closed and the tracker disagrees. The sentence resting on it reads exactly as it did on the day it was correct, which is the whole failure: a row goes on reading as covered after what it says about its replacement has stopped being true."
    printf '%s' "$disagreed"
    bad=$((bad + 1))
  fi
  if [ -n "$unknown" ]; then
    echo "::error::${label}: a document names an issue the listing does not carry. A number that never existed and a listing that did not reach far enough are the same bytes here, so this refuses rather than passing over the one it cannot tell from the other."
    printf '%s' "$unknown"
    bad=$((bad + 1))
  fi

  if [ "$bad" -eq 0 ]; then
    echo "ok    ${label}: ${read_count} present-tense claim(s) read, all agreeing with the listing"
  fi
  return $bad
}

cmd_check() {
  local states_file="${1:-}"
  local root="${2:-.}"
  local claims='' file rel

  if [ -z "$states_file" ]; then
    echo "::error::check needs the tracker's listing in a file, one \"<number><tab><state>\" per line. This mode makes no network call of its own." >&2
    return 2
  fi
  if [ ! -f "$states_file" ]; then
    echo "::error::${states_file} is missing. That file is the listing this check judges against, and an absent listing and a tracker holding nothing are the same bytes here."
    return 1
  fi

  while IFS= read -r rel; do
    file="${root%/}/${rel}"
    [ -f "$file" ] || continue
    while IFS= read -r hit; do
      [ -n "$hit" ] && claims="${claims}${rel}"$'\t'"${hit}"$'\n'
    done < <(claims_in "$file")
  done < <(git -C "$root" ls-files '*.md')

  judge "$(printf '%s' "$claims")" "$states_file" "tree"
}

cmd_selftest() {
  # On every run rather than once at review time. Every claim in the tree agrees
  # today, so the check step cannot fire against it, and a pattern that had
  # stopped matching would go green forever while the pages it holds drifted
  # underneath it.
  #
  # Each case is "<directory>@<what it must name, or empty for the clean pair>".
  local cases=(
    'clean@'
    'claim-has-moved.trip@#107'
    'number-is-not-in-the-listing.trip@#4242'
    'page-carries-no-claim.trip@'
  )
  local entry name expected dir claims got fired errors picked fail=0

  for entry in "${cases[@]}"; do
    IFS='@' read -r name expected <<< "$entry"
    dir="${FIXTURES}/${name}"

    if [ ! -f "${dir}/page.md" ] || [ ! -f "${dir}/states.txt" ]; then
      echo "::error::${name}: missing a fixture. Every case owns ${dir}/page.md and ${dir}/states.txt."
      fail=1
      continue
    fi

    claims=$(claims_in "${dir}/page.md" | sed "s|^|page.md\t|")
    got=$(judge "$claims" "${dir}/states.txt" "$name" 2>&1)
    fired=$?

    case "$name" in
      clean)
        if [ "$fired" -ne 0 ]; then
          echo "::error::clean: the agreeing pair is refused. The check would red a pull request that had done nothing wrong."
          printf '%s\n' "$got" | sed 's/^/  /'
          fail=1
        elif [ "$(printf '%s\n' "$claims" | grep -c .)" -lt 3 ]; then
          echo "::error::clean: the fixture carries fewer than three claims, so the pair agrees for the wrong reason. This is the leg that says an open claim, a closed claim and a merged one are all being read."
          fail=1
        else
          echo "bites clean: $(printf '%s\n' "$claims" | grep -c .) claim(s) read, nothing fires"
        fi
        ;;
      page-carries-no-claim.trip)
        # The scan leg. It fires on an empty set rather than naming a claim, so
        # it is checked by what it says rather than by which number it picked.
        errors=$(printf '%s\n' "$got" | grep -c '^::error::')
        if [ "$fired" -eq 0 ]; then
          echo "::error::${name}: nothing fired on a page carrying no claim. A pattern that had stopped matching would report exactly this."
          fail=1
        elif [ "$errors" -ne 1 ]; then
          echo "::error::${name}: ${errors} legs fired and exactly one should."
          printf '%s\n' "$got" | sed 's/^/  /'
          fail=1
        else
          echo "bites ${name}: an empty scan is refused rather than reported as agreement"
        fi
        ;;
      *)
        # One leg fires, it names the claim that moved, and it names no other.
        errors=$(printf '%s\n' "$got" | grep -c '^::error::')
        picked=$(printf '%s\n' "$got" | sed -n 's/^.*: \(#[0-9]*\)[,. ].*$/\1/p' | sort -u)
        if [ "$fired" -eq 0 ]; then
          echo "::error::${name}: nothing fired. The fixture carries the drift this leg exists for."
          fail=1
        elif [ "$errors" -ne 1 ]; then
          echo "::error::${name}: ${errors} legs fired and exactly one should. A fixture tripping two legs proves neither."
          printf '%s\n' "$got" | sed 's/^/  /'
          fail=1
        elif [ "$picked" != "$expected" ]; then
          echo "::error::${name}: expected the claim that moved to be named alone."
          echo "  expected: ${expected}"
          echo "  got:      ${picked}"
          fail=1
        else
          echo "bites ${name}: ${expected} is named and no other claim is"
        fi
        ;;
    esac
  done
  return $fail
}

case "${1:-}" in
  check)    cmd_check "${2:-}" "${3:-.}" ;;
  selftest) cmd_selftest ;;
  *)        echo "usage: $0 check <states-file> [root] | $0 selftest" >&2; exit 2 ;;
esac
