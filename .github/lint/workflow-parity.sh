#!/usr/bin/env bash
# The parity ledger is held to this repository's own workflow directory.
#
# docs/workflow-parity.md is the ledger #12 is about: every workflow on the
# target gate is adopted or declined with a reason, and every workflow this
# repository carries is kept with a reason or removed. An unexplained gap is a
# defect and an explained one is a decision, which only holds while the ledger
# knows what the directory contains.
#
# It has stopped knowing four times. Each was found by somebody running the hand
# check written into the page, each landed green, and the fourth is why this
# exists rather than a fifth note. The cost is not tidiness: #24 builds the
# required-check list by reading the rows this document marks adopted or kept, so
# a workflow with no row is a check that quietly never reaches that list.
#
# What this refuses, all three against the same two subjects:
#
#   A file in .github/workflows/ with no row under "What this repository has".
#   That is the direction that has drifted three of the four times.
#
#   A row under that heading whose answer is not "removed" naming a file that is
#   not in the directory. A row describing a workflow nobody can open is a reason
#   with nothing behind it.
#
#   A row whose answer IS "removed" naming a file that is in the directory. This
#   is the near-miss the third leg exists for. The mistake is to read every row
#   as owing a file, which turns the three honest "removed" rows red, and the
#   mistake in the other direction is a workflow reinstated with its row still
#   saying it went. The two are one condition apart in the comparison.
#
# What it does NOT refuse, stated so a green run is not read as more than it is:
#
#   The other direction of the ledger. "What the target gate has" is a listing on
#   another repository, and this reads the tree, so a workflow added there
#   arrives with no row and nothing here says so. That direction has drifted
#   once, it is the half a command over this tree cannot reach, and every run
#   prints that it was not evaluated and what asking would cost. Buying it means
#   a network call on a route a merge takes, where a failed call manufactures a
#   refusal as readily as a wrong ledger does, and that trade is not taken here.
#
#   Whether the answer in a row is the right answer. Whether a workflow should be
#   kept, and whether the reason beside it is true, are judgements no reading of
#   these two subjects makes. The review is where a wrong one is caught.
#
#   The first table. A name may appear in both tables, and this reads only the
#   second, because that is the one that answers for this tree.
#
# Two modes:
#   check [root]   read the tracked ledger and the workflow directory under root
#   selftest       fail unless each leg fires on its own fixture, fires alone,
#                  names the file that moved and no other, and stays quiet on the
#                  clean pair
set -uo pipefail

FIXTURES=".github/lint/fixtures/workflow-parity"

LEDGER='docs/workflow-parity.md'
WORKFLOWS='.github/workflows'
HEADING='## What this repository has'

# The rows of the section that answers for this tree, one per line, as
# "<file><tab><answer>". A heading ends the section, so a document that never
# opens it prints nothing and the caller refuses rather than reading that as a
# section with no rows.
#
# The header row and the separator are dropped by the pattern rather than by
# counting lines: neither carries the backticked first cell every real row has.
rows_in_the_section() {
  local file="$1"
  awk -v heading="$HEADING" '
    BEGIN { inside = 0 }
    /^## / { if (inside) { exit } ; if ($0 == heading) { inside = 1 } ; next }
    inside && /^\| `/ {
      line = $0
      sub(/^\| `/, "", line)
      name = line
      sub(/`.*$/, "", name)
      rest = line
      sub(/^[^`]*` \| /, "", rest)
      answer = rest
      sub(/ \|.*$/, "", answer)
      gsub(/^[ \t]+|[ \t]+$/, "", answer)
      print name "\t" answer
    }
  ' "$file"
}

# Judges one pair and returns the number of legs that fired, so a caller can ask
# how many rather than only whether. ledger_file is a path; listing is one file
# name per line.
judge() {
  local ledger_file="$1" listing="$2" label="$3"
  local rows named missing_row missing_file back_from_the_dead bad=0

  rows=$(rows_in_the_section "$ledger_file")

  if [ -z "$rows" ]; then
    echo "::error::${label}: ${ledger_file} has no rows under '${HEADING}'. Refusing rather than passing on an empty table, which is what a renamed heading or a reordered document looks like from here."
    return 1
  fi
  if [ -z "$listing" ]; then
    echo "::error::${label}: the workflow directory reads as empty. Refusing rather than reporting a comparison that looked at nothing on one side."
    return 1
  fi

  named=$(printf '%s\n' "$rows" | cut -f1 | sort -u)

  missing_row=$(comm -23 <(printf '%s\n' "$listing" | sort -u) <(printf '%s\n' "$named"))

  missing_file=$(printf '%s\n' "$rows" \
    | awk -F'\t' '$2 !~ /^removed/ { print $1 }' \
    | sort -u \
    | comm -23 - <(printf '%s\n' "$listing" | sort -u))

  back_from_the_dead=$(printf '%s\n' "$rows" \
    | awk -F'\t' '$2 ~ /^removed/ { print $1 }' \
    | sort -u \
    | comm -12 - <(printf '%s\n' "$listing" | sort -u))

  if [ -n "$missing_row" ]; then
    echo "::error::${label}: a workflow in ${WORKFLOWS}/ has no row under '${HEADING}' in ${ledger_file}. #24 builds the required-check list out of the rows marked adopted or kept, so a workflow with no row is a check that quietly never reaches that list."
    printf '%s\n' "$missing_row" | sed 's/^/  /'
    bad=$((bad + 1))
  fi
  if [ -n "$missing_file" ]; then
    echo "::error::${label}: a row under '${HEADING}' in ${ledger_file} names a workflow that is not in ${WORKFLOWS}/. A row describing a file nobody can open is a reason with nothing behind it. Where the workflow went, the answer cell is what says so."
    printf '%s\n' "$missing_file" | sed 's/^/  /'
    bad=$((bad + 1))
  fi
  if [ -n "$back_from_the_dead" ]; then
    echo "::error::${label}: a row under '${HEADING}' in ${ledger_file} says a workflow was removed and the file is in ${WORKFLOWS}/. A reinstated workflow with its row still saying it went is the ledger describing the opposite of the tree."
    printf '%s\n' "$back_from_the_dead" | sed 's/^/  /'
    bad=$((bad + 1))
  fi

  if [ "$bad" -eq 0 ]; then
    echo "ok    ${label}: $(printf '%s\n' "$listing" | grep -c .) workflow file(s) and $(printf '%s\n' "$rows" | grep -c .) row(s), each accounted for in both directions"
  fi
  return $bad
}

# The directory as a listing, one name per line. Written with a glob rather than
# find -printf so the same line runs wherever bash does.
listing_of() {
  local dir="$1" path
  for path in "$dir"/*.yml "$dir"/*.yaml; do
    [ -f "$path" ] && basename "$path"
  done | sort -u
}

cmd_check() {
  local root="${1:-.}"
  local ledger_file="${root%/}/${LEDGER}"
  local dir="${root%/}/${WORKFLOWS}"
  local listing verdict

  if [ ! -f "$ledger_file" ]; then
    echo "::error::${ledger_file} is missing. The ledger is what this check reads, so it refuses rather than reporting a scan that read nothing."
    return 1
  fi
  if [ ! -d "$dir" ]; then
    echo "::error::${dir} is missing. The directory is the other subject, so it refuses rather than reporting a scan that read one side."
    return 1
  fi

  listing=$(listing_of "$dir")

  judge "$ledger_file" "$listing" "tree"
  verdict=$?

  # Printed on every run rather than left to be inferred from a green mark. A
  # reader who takes this check for the whole of the ledger's condition in #12 is
  # making exactly the mistake the ledger exists against.
  echo "note  the target gate's own table is NOT evaluated here. It is a listing on another repository and this reads the tree. Asking costs a network call on a route a merge takes, and the command is the one written into ${LEDGER} beside the direction this does decide."

  [ "$verdict" -eq 0 ] && return 0
  return 1
}

cmd_selftest() {
  # On every run rather than once at review time. The tree agrees today, so the
  # step below cannot fire against it, and a rule that had stopped finding the
  # section at all would go green forever while the page it holds drifted
  # underneath it.
  #
  # The fixture supplies the directory as a listing rather than as real workflow
  # files, so nothing that audits .github/workflows/ has to be taught to skip a
  # directory of deliberately wrong ones. What that leaves unfixtured is the one
  # glob in listing_of that turns a directory into that listing, and this comment
  # is the whole disclosure of it.
  local cases=(
    'clean@'
    'file-without-a-row.trip@fuzz.yaml'
    'row-without-a-file.trip@headless.yaml'
    'removed-row-names-a-file.trip@sync-labels.yaml'
  )
  local entry name expected dir listing got fired errors picked fail=0

  for entry in "${cases[@]}"; do
    IFS='@' read -r name expected <<< "$entry"
    dir="${FIXTURES}/${name}"

    if [ ! -f "${dir}/workflow-parity.md" ] || [ ! -f "${dir}/workflows.txt" ]; then
      echo "::error::${name}: missing a fixture. Every case owns ${dir}/workflow-parity.md and ${dir}/workflows.txt."
      fail=1
      continue
    fi

    listing=$(grep -v '^[[:space:]]*$' "${dir}/workflows.txt" | sort -u)

    got=$(judge "${dir}/workflow-parity.md" "$listing" "$name" 2>&1)
    fired=$?

    case "$name" in
      clean)
        if [ "$fired" -ne 0 ]; then
          echo "::error::clean: the agreeing pair is refused. The check would red a pull request that had done nothing wrong."
          printf '%s\n' "$got" | sed 's/^/  /'
          fail=1
        elif [ "$(printf '%s\n' "$listing" | grep -c .)" -lt 3 ]; then
          echo "::error::clean: the fixture lists fewer than three workflows, so the pair agrees for the wrong reason and proves nothing. This is the leg that says a removed row and a kept row are both being read."
          fail=1
        else
          echo "bites clean: $(printf '%s\n' "$listing" | grep -c .) workflow(s) read against $(rows_in_the_section "${dir}/workflow-parity.md" | grep -c .) row(s), nothing fires"
        fi
        ;;
      *)
        # One leg fires, it names the file that moved, and it names no other. A
        # leg that reds for a second file is a leg that will red on somebody
        # else's change for the wrong reason.
        errors=$(printf '%s\n' "$got" | grep -c '^::error::')
        picked=$(printf '%s\n' "$got" | sed -n 's/^  \([A-Za-z0-9._-]*\.\(yml\|yaml\)\)$/\1/p' | sort -u)
        if [ "$fired" -eq 0 ]; then
          echo "::error::${name}: nothing fired. The fixture carries the drift this leg exists for."
          fail=1
        elif [ "$errors" -ne 1 ]; then
          echo "::error::${name}: ${errors} legs fired and exactly one should. A fixture tripping two legs proves neither."
          printf '%s\n' "$got" | sed 's/^/  /'
          fail=1
        elif [ "$picked" != "$expected" ]; then
          echo "::error::${name}: expected the file that moved to be named alone."
          echo "  expected: ${expected}"
          echo "  got:      ${picked}"
          fail=1
        else
          echo "bites ${name}: ${expected} is named and no other file is"
        fi
        ;;
    esac
  done
  return $fail
}

case "${1:-}" in
  check)    cmd_check "${2:-.}" ;;
  selftest) cmd_selftest ;;
  *)        echo "usage: $0 check [root] | $0 selftest" >&2; exit 2 ;;
esac
