#!/usr/bin/env bash
# A red run on the default branch is reported to somebody, rather than left on a
# page nobody opens.
#
# A run that is not a required pull-request check is not mailed to anybody the
# way a red pull request is. The weekly mutation gate failed on every run it
# made and nothing said so anywhere in the tree or on the tracker, which is #376:
# the gate was working, and its only reader was somebody who happened to open
# the actions page. The target gate this repository is measured against holds
# the same class with a default-branch watchdog, and this is that watchdog's
# deciding half.
#
# What this decides, over a listing handed to it: for every workflow, whether
# its latest completed run on the default branch concluded in a way somebody has
# to be told about. The listing carries one line per workflow, and the LATEST
# run is the subject on purpose. A workflow that failed last week and passed
# since is not red, and a report built from every run in a window would go on
# naming it until the window closed.
#
# The conclusion test is a denylist rather than an allowlist. `failure`,
# `timed_out` and `startup_failure` all reach a reader, and so does any
# conclusion the runner has not been seen to produce yet, because a new class of
# not-green arriving in silence is exactly the failure this file exists for.
# What stays quiet is written out by name in QUIET below, with the reason beside
# each one.
#
# What it refuses rather than reporting as clean, because an empty answer and a
# board on which nothing is red are the same bytes from here:
#
#   A listing that is missing or empty. A failed call arrives as one, and a
#   sweep that read nothing must not close the open alert.
#
#   A line with no conclusion, or too few fields to read. The caller writes the
#   listing and the shape is fixed, so a line outside it is the caller having
#   broken rather than a run that is fine.
#
# WHY THE LISTING IS HANDED IN RATHER THAN FETCHED, which is the trade every
# scheduled check on this board makes. The run list is a network call, and a
# failed call and a default branch on which nothing is red arrive here as the
# same empty file. So the caller fetches, in a step of its own, so that a failed
# call fails as a failed call with the call in front of it, and this mode makes
# no network call at all: .github/workflows/failure-alert.yaml.
#
# What it does NOT do, stated so a green run is not read as more than it is. It
# judges a conclusion and never a cause: which step failed and what the log
# said are read by the caller off the run itself, after this has said which
# runs to ask. And it reads the latest COMPLETED run, so a workflow whose newest
# run is still in flight is judged on the one before it until that lands.
#
# Two modes:
#   read <listing> [red-file]  judge the listing; write the red lines to
#                              red-file when one is given. Exits 0 when nothing
#                              is red, 1 when something is, 2 when the listing
#                              cannot be read at all.
#   selftest                   fail unless each leg fires on its own fixture,
#                              names the workflow it is about and no other,
#                              refuses what it must refuse, and stays quiet on
#                              the clean listing for the right reason.
set -uo pipefail

FIXTURES=".github/lint/fixtures/failure-alert"

# The listing, one line per workflow, seven tab-separated fields:
#   path  name  conclusion  created_at  head_sha  url  run_id
# A workflow with no completed run on the default branch carries the conclusion
# NO_RUN and empty fields after it; a pull-request-only workflow never runs
# there and is not red for it.
FIELDS=7
NO_RUN="no-completed-run"

# The conclusions that reach no reader, each with its reason.
#   success           the run is green.
#   skipped           the run decided it had nothing to do, which is a verdict.
#   cancelled         somebody or a concurrency rule stopped it on purpose, and
#                     the run that superseded it is the one that answers.
#   no-completed-run  the workflow has never run on the default branch, which is
#                     what a pull-request-only workflow looks like from here.
QUIET="success skipped cancelled ${NO_RUN}"

# Judges one listing. Prints the red lines to stdout, one per red workflow, in
# the listing's own shape, and a summary to stderr. Returns 0 clean, 1 red,
# 2 unreadable.
judge() {
  local listing="$1" label="$2"
  local line path fields read_count red_count red

  if [ ! -f "$listing" ]; then
    echo "::error::${label}: the listing ${listing} is missing. A failed call and a default branch on which nothing is red are the same bytes here, so this refuses rather than reporting the second." >&2
    return 2
  fi
  # This refusal does not isolate, and that is recorded rather than smoothed
  # over: with it taken out, an empty listing is still refused one step down,
  # by the pass below finding no workflow to read, and the selftest stays
  # green. What this buys is the sentence, which names an empty file rather
  # than a listing that read as nothing. Neutralising both is what reds the
  # empty-listing leg.
  if [ ! -s "$listing" ]; then
    echo "::error::${label}: the listing is empty. A board with no workflows and a call that answered nothing are the same bytes here, so this refuses rather than closing an alert on the strength of nothing." >&2
    return 2
  fi

  # One pass, in awk, rather than a subprocess per field: the listing is a few
  # dozen lines and the runner does not care, but the selftest runs seven of
  # them, and a check nobody runs locally because it is slow is a check nobody
  # runs locally. Every line is judged before any verdict is given, so a line
  # outside the shape refuses the whole listing rather than the lines after it.
  local out rc
  out=$(awk -F'\t' -v fields="$FIELDS" -v quiet=" ${QUIET} " '
    NF == 0 { next }
    bad { next }
    NF != fields { bad = 3; printf "SHAPE\t%d\t%s\n", NF, substr($0, 1, 200); next }
    $3 == "" { bad = 4; printf "BLANK\t%s\n", $1; next }
    { read++ }
    index(quiet, " " $3 " ") == 0 { red++; print "RED\t" $0 }
    END {
      if (bad) exit bad
      if (read == 0) { print "NONE"; exit 5 }
      printf "SUMMARY\t%d\t%d\n", read, red
      exit (red > 0 ? 1 : 0)
    }
  ' "$listing")
  rc=$?

  case "$rc" in
    3)
      fields=$(printf '%s\n' "$out" | awk -F'\t' '$1 == "SHAPE" { print $2 }')
      line=$(printf '%s\n' "$out" | awk -F'\t' '$1 == "SHAPE" { print substr($0, index($0, $3)) }')
      echo "::error::${label}: a line of the listing carries ${fields} field(s) and the shape is ${FIELDS}. The caller writes this listing, so a line outside its shape is the caller having broken rather than a run that is fine, and nothing in it has been judged." >&2
      printf '  %s\n' "$line" >&2
      return 2
      ;;
    4)
      path=$(printf '%s\n' "$out" | awk -F'\t' '$1 == "BLANK" { print $2 }')
      echo "::error::${label}: ${path} carries no conclusion. A run still in flight has none, and the caller asks for completed runs only, so an empty cell here is the listing being wrong rather than the run being fine, and nothing in it has been judged." >&2
      return 2
      ;;
    5)
      echo "::error::${label}: the listing carried no workflow at all. Refusing rather than reading an empty set as a default branch on which nothing is red." >&2
      return 2
      ;;
  esac

  read_count=$(printf '%s\n' "$out" | awk -F'\t' '$1 == "SUMMARY" { print $2 }')
  red_count=$(printf '%s\n' "$out" | awk -F'\t' '$1 == "SUMMARY" { print $3 }')
  red=$(printf '%s\n' "$out" | awk -F'\t' '$1 == "RED" { print substr($0, 5) }')

  if [ "$rc" -eq 0 ]; then
    echo "ok    ${label}: ${read_count} workflow(s) read, and the latest completed run of every one of them on the default branch is quiet" >&2
    return 0
  fi

  echo "::warning::${label}: ${red_count} of ${read_count} workflow(s) concluded non-success on their latest completed run on the default branch, and somebody has to be told." >&2
  printf '%s\n' "$red" | awk -F'\t' '{ printf "  %s\t%s\t%s\n", $1, $3, $6 }' >&2
  printf '%s\n' "$red"
  return 1
}

cmd_read() {
  local listing="${1:-}" red_file="${2:-}"
  local red rc

  if [ -z "$listing" ]; then
    echo "::error::read needs the listing in a file, one workflow per line. This mode makes no network call of its own." >&2
    return 2
  fi

  red=$(judge "$listing" "default-branch")
  rc=$?
  if [ -n "$red_file" ] && [ "$rc" -ne 2 ]; then
    # Every line terminated, including the last. A caller reading this file
    # with `while read` drops an unterminated last line on the floor, and the
    # first run of this against the live listing did exactly that: one red
    # workflow, written without its newline, read by nobody.
    if [ -n "$red" ]; then
      printf '%s\n' "$red" > "$red_file"
    else
      : > "$red_file"
    fi
  fi
  return $rc
}

cmd_selftest() {
  # On every run rather than once at review time. The mainline is green most
  # weeks, so the read step cannot fire against it, and a denylist that had
  # quietly turned into an allowlist would go green forever while a red run sat
  # on the default branch unreported.
  #
  # Each case is "<fixture>@<exit expected>@<paths that must be named, comma
  # separated, or empty>".
  local cases=(
    'clean.tsv@0@'
    'failure-is-red.trip.tsv@1@.github/workflows/stryker-mutation.yaml'
    'startup-failure-is-red.trip.tsv@1@.github/workflows/scorecard.yml'
    'two-workflows-red.trip.tsv@1@.github/workflows/fuzz.yaml,.github/workflows/tracker-claim.yaml'
    'empty-listing.trip.tsv@2@'
    'conclusion-missing.trip.tsv@2@'
    'too-few-fields.trip.tsv@2@'
  )
  local entry name expected_rc expected_paths file red rc named fail=0

  for entry in "${cases[@]}"; do
    IFS='@' read -r name expected_rc expected_paths <<< "$entry"
    file="${FIXTURES}/${name}"

    if [ ! -f "$file" ]; then
      echo "::error::${name}: missing a fixture. Every case owns ${file}."
      fail=1
      continue
    fi

    red=$(judge "$file" "$name" 2>/dev/null)
    rc=$?
    named=$(printf '%s' "$red" | cut -f1 | sort | tr '\n' ',' | sed 's/,$//')

    if [ "$rc" -ne "$expected_rc" ]; then
      echo "::error::${name}: exited ${rc} and ${expected_rc} was expected. The fixture carries exactly the case this leg exists for."
      fail=1
      continue
    fi

    case "$expected_rc" in
      0)
        # The clean pair agrees for the right reason: every quiet conclusion is
        # in the fixture, so each one is being read rather than the listing
        # happening to hold only successes.
        local q missing=''
        for q in $QUIET; do
          grep -q $'\t'"${q}"$'\t' "$file" || missing="${missing} ${q}"
        done
        if [ -n "$missing" ]; then
          echo "::error::${name}: the clean listing carries no line concluding${missing}, so it agrees for the wrong reason. This is the leg that says each quiet conclusion is being read as quiet."
          fail=1
        elif [ "$(grep -c . "$file")" -lt 5 ]; then
          echo "::error::${name}: the clean listing carries fewer than five workflows and proves too little."
          fail=1
        else
          echo "bites ${name}: $(grep -c . "$file") workflow(s) read, every quiet conclusion among them, nothing fires"
        fi
        ;;
      1)
        if [ "$named" != "$(printf '%s' "$expected_paths" | tr ',' '\n' | sort | tr '\n' ',' | sed 's/,$//')" ]; then
          echo "::error::${name}: expected exactly these workflows to be named."
          echo "  expected: ${expected_paths}"
          echo "  got:      ${named}"
          fail=1
        else
          echo "bites ${name}: ${expected_paths} named and no other"
        fi
        ;;
      2)
        if [ -n "$red" ]; then
          echo "::error::${name}: an unreadable listing still produced red lines, so a caller reading them would act on a listing this refused."
          fail=1
        else
          echo "bites ${name}: refused rather than read"
        fi
        ;;
    esac
  done

  return $fail
}

case "${1:-}" in
  read)     cmd_read "${2:-}" "${3:-}" ;;
  selftest) cmd_selftest ;;
  *)        echo "usage: $0 read <listing> [red-file] | $0 selftest" >&2; exit 2 ;;
esac
