#!/usr/bin/env bash
# The parity ledger is held to both of the directories it answers for.
#
# docs/workflow-parity.md is the ledger #12 is about: every workflow on the
# target gate is adopted or declined with a reason, and every workflow this
# repository carries is kept with a reason or removed. An unexplained gap is a
# defect and an explained one is a decision, which only holds while the ledger
# knows what the two directories contain.
#
# It has stopped knowing four times. Each was found by somebody running a hand
# check written into the page, each landed green, and the fourth is why this
# exists rather than a fifth note. The cost is not tidiness: #24 builds the
# required-check list by reading the rows this document marks adopted or kept, so
# a workflow with no row is a check that quietly never reaches that list.
#
# Two subjects, and they are reached differently, which is why they are two modes
# rather than one run.
#
# check reads this repository's own directory out of the tree and judges it
# against "What this repository has". Three legs:
#
#   A file in .github/workflows/ with no row under that heading. That is the
#   direction that has drifted three of the four times.
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
# check-target reads a listing handed to it as a file and judges it against
# "What the target gate has". That listing lives on another repository, so this
# mode cannot fetch it and does not try: whoever calls it fetches it, and a
# failed fetch is that caller's failure rather than a verdict about the ledger.
# Two legs:
#
#   A name in the listing with no row under that heading. This is the drift that
#   put perf-baseline.yml on the target gate with nothing here answering for it,
#   found by hand months later.
#
#   A row under that heading naming a workflow the listing does not carry. The
#   first table is a ledger of somebody else's directory, so every row owes a
#   file there and there is no answer cell that says otherwise, which is why this
#   direction has two legs where the other has three.
#
# WHY THE SECOND MODE IS NOT ON A ROUTE A MERGE TAKES, and this is the part to
# read before moving it onto one. The listing is a network call. A failed call
# and a wrong ledger reach this script as the same empty listing, so a check that
# refuses on it manufactures a refusal out of a timeout, and on a merge route
# that is a gate somebody learns to re-run rather than to read. The scheduled job
# in .github/workflows/workflow-parity-target.yaml is where it is called from, it
# fetches in a step of its own so a failed call fails as a failed call, and the
# price of that placement is that a target workflow added on a Tuesday is
# unanswered until the next run rather than at the merge that followed it.
#
# What neither mode refuses, stated so a green run is not read as more than it
# is:
#
#   Whether the answer in a row is the right answer. Whether a workflow should be
#   kept, and whether the reason beside it is true, are judgements no reading of
#   these subjects makes. The review is where a wrong one is caught.
#
#   The other table, in either mode. A name may appear in both, and each mode
#   reads exactly the one that answers for its own subject.
#
#   Which direction lost a leg, for the first one. "A name in the listing with no
#   row" is ONE comparison serving both modes, so neutralising it silences both
#   fixtures at once and no fixture here can isolate it per direction. That was
#   run rather than reasoned about, and it is the bound on what the selftest
#   proves: the second leg is written per direction and each of those is proven
#   alone.
#
# Three modes:
#   check [root]                read the tracked ledger and the workflow directory
#   check-target <file> [root]  read the tracked ledger and a listing in <file>
#   selftest                    fail unless each leg fires on its own fixture,
#                               fires alone, names the file that moved and no
#                               other, and stays quiet on the clean pair
set -uo pipefail

FIXTURES=".github/lint/fixtures/workflow-parity"

LEDGER='docs/workflow-parity.md'
WORKFLOWS='.github/workflows'
HEADING_TREE='## What this repository has'
HEADING_TARGET='## What the target gate has'

# The heading that answers for one subject.
heading_for() {
  case "$1" in
    tree)   printf '%s' "$HEADING_TREE" ;;
    target) printf '%s' "$HEADING_TARGET" ;;
  esac
}

# The rows of one section, one per line, as "<file><tab><answer>". A heading ends
# the section, so a document that never opens it prints nothing and the caller
# refuses rather than reading that as a section with no rows.
#
# The header row and the separator are dropped by the pattern rather than by
# counting lines: neither carries the backticked first cell every real row has.
rows_in_the_section() {
  local file="$1" heading="$2"
  awk -v heading="$heading" '
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
# name per line; which is tree or target and decides both the heading that is
# read and how many legs there are.
judge() {
  local ledger_file="$1" listing="$2" label="$3" which="$4"
  local heading rows named missing_row missing_file back_from_the_dead bad=0
  local where

  heading=$(heading_for "$which")
  case "$which" in
    tree)   where="${WORKFLOWS}/" ;;
    target) where="the target gate's workflow directory" ;;
  esac

  rows=$(rows_in_the_section "$ledger_file" "$heading")

  if [ -z "$rows" ]; then
    echo "::error::${label}: ${ledger_file} has no rows under '${heading}'. Refusing rather than passing on an empty table, which is what a renamed heading or a reordered document looks like from here."
    return 1
  fi
  if [ -z "$listing" ]; then
    echo "::error::${label}: ${where} reads as empty. Refusing rather than reporting a comparison that looked at nothing on one side."
    return 1
  fi

  named=$(printf '%s\n' "$rows" | cut -f1 | sort -u)

  missing_row=$(comm -23 <(printf '%s\n' "$listing" | sort -u) <(printf '%s\n' "$named"))

  if [ "$which" = tree ]; then
    missing_file=$(printf '%s\n' "$rows" \
      | awk -F'\t' '$2 !~ /^removed/ { print $1 }' \
      | sort -u \
      | comm -23 - <(printf '%s\n' "$listing" | sort -u))

    back_from_the_dead=$(printf '%s\n' "$rows" \
      | awk -F'\t' '$2 ~ /^removed/ { print $1 }' \
      | sort -u \
      | comm -12 - <(printf '%s\n' "$listing" | sort -u))
  else
    # Every row of the first table owes a file on the target gate, whatever its
    # answer says: declined and deferred are this repository's decisions about a
    # workflow that is there, not claims that it is gone.
    missing_file=$(printf '%s\n' "$named" \
      | comm -23 - <(printf '%s\n' "$listing" | sort -u))
    back_from_the_dead=''
  fi

  if [ -n "$missing_row" ]; then
    if [ "$which" = tree ]; then
      echo "::error::${label}: a workflow in ${WORKFLOWS}/ has no row under '${heading}' in ${ledger_file}. #24 builds the required-check list out of the rows marked adopted or kept, so a workflow with no row is a check that quietly never reaches that list."
    else
      echo "::error::${label}: a workflow on the target gate has no row under '${heading}' in ${ledger_file}. This ledger's whole claim is that a workflow is absent here on purpose rather than by having been forgotten, and a name nobody has answered for is the second of those wearing the clothes of the first."
    fi
    printf '%s\n' "$missing_row" | sed 's/^/  /'
    bad=$((bad + 1))
  fi
  if [ -n "$missing_file" ]; then
    if [ "$which" = tree ]; then
      echo "::error::${label}: a row under '${heading}' in ${ledger_file} names a workflow that is not in ${WORKFLOWS}/. A row describing a file nobody can open is a reason with nothing behind it. Where the workflow went, the answer cell is what says so."
    else
      echo "::error::${label}: a row under '${heading}' in ${ledger_file} names a workflow the target gate does not have. A decision about a file that is not there is a reason with nothing behind it, and this table carries no answer cell that says a workflow went, so the repair is to take the row out rather than to reword it."
    fi
    printf '%s\n' "$missing_file" | sed 's/^/  /'
    bad=$((bad + 1))
  fi
  if [ -n "$back_from_the_dead" ]; then
    echo "::error::${label}: a row under '${heading}' in ${ledger_file} says a workflow was removed and the file is in ${WORKFLOWS}/. A reinstated workflow with its row still saying it went is the ledger describing the opposite of the tree."
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

  judge "$ledger_file" "$listing" "tree" tree
  verdict=$?

  # Printed on every run rather than left to be inferred from a green mark. A
  # reader who takes this run for the whole of the ledger's condition in #12 is
  # making exactly the mistake the ledger exists against.
  echo "note  the target gate's own table is NOT evaluated on this route. It is a listing on another repository, this mode reads the tree, and asking here would put a network call on a route a merge takes. check-target is the mode that decides it and .github/workflows/workflow-parity-target.yaml is what calls it, on a schedule and on demand."

  [ "$verdict" -eq 0 ] && return 0
  return 1
}

cmd_check_target() {
  local listing_file="${1:-}"
  local root="${2:-.}"
  local ledger_file="${root%/}/${LEDGER}"
  local listing verdict

  if [ -z "$listing_file" ]; then
    echo "::error::check-target needs the target gate's listing in a file, one workflow file name per line. This mode makes no network call of its own." >&2
    return 2
  fi
  if [ ! -f "$listing_file" ]; then
    echo "::error::${listing_file} is missing. That file is the listing this mode judges, and refusing is the only honest answer to being handed nothing: an absent listing and an empty target directory are the same bytes here."
    return 1
  fi
  if [ ! -f "$ledger_file" ]; then
    echo "::error::${ledger_file} is missing. The ledger is what this check reads, so it refuses rather than reporting a scan that read nothing."
    return 1
  fi

  listing=$(grep -v '^[[:space:]]*$' "$listing_file" | sort -u)

  judge "$ledger_file" "$listing" "target" target
  verdict=$?

  # The same disclosure as the other mode, pointed the other way, so neither run
  # can be read as having covered both directories.
  echo "note  this repository's own directory is NOT evaluated on this route. check is the mode that decides it, on every push and every pull request."

  [ "$verdict" -eq 0 ] && return 0
  return 1
}

cmd_selftest() {
  # On every run rather than once at review time. The tree agrees today, so the
  # check steps cannot fire against it, and a rule that had stopped finding a
  # section at all would go green forever while the page it holds drifted
  # underneath it.
  #
  # A fixture supplies its directory as a listing rather than as real workflow
  # files, so nothing that audits .github/workflows/ has to be taught to skip a
  # directory of deliberately wrong ones. What that leaves unfixtured is the one
  # glob in listing_of that turns a directory into that listing, and this comment
  # is the whole disclosure of it. The target mode is handed a listing by its
  # caller in the same shape, so nothing there is unfixtured for that reason.
  #
  # Each case is <directory>@<listing file>@<which>@<the one file it must name>.
  local cases=(
    'clean@workflows.txt@tree@'
    'file-without-a-row.trip@workflows.txt@tree@fuzz.yaml'
    'row-without-a-file.trip@workflows.txt@tree@headless.yaml'
    'removed-row-names-a-file.trip@workflows.txt@tree@sync-labels.yaml'
    'target-clean@target-workflows.txt@target@'
    'target-file-without-a-row.trip@target-workflows.txt@target@perf-baseline.yml'
    'target-row-without-a-file.trip@target-workflows.txt@target@wiki-lint.yml'
  )
  local entry name listing_name which expected dir listing got fired errors picked fail=0
  local heading

  for entry in "${cases[@]}"; do
    IFS='@' read -r name listing_name which expected <<< "$entry"
    dir="${FIXTURES}/${name}"
    heading=$(heading_for "$which")

    if [ ! -f "${dir}/workflow-parity.md" ] || [ ! -f "${dir}/${listing_name}" ]; then
      echo "::error::${name}: missing a fixture. Every case owns ${dir}/workflow-parity.md and ${dir}/${listing_name}."
      fail=1
      continue
    fi

    listing=$(grep -v '^[[:space:]]*$' "${dir}/${listing_name}" | sort -u)

    got=$(judge "${dir}/workflow-parity.md" "$listing" "$name" "$which" 2>&1)
    fired=$?

    case "$name" in
      clean|target-clean)
        if [ "$fired" -ne 0 ]; then
          echo "::error::${name}: the agreeing pair is refused. The check would red a pull request that had done nothing wrong."
          printf '%s\n' "$got" | sed 's/^/  /'
          fail=1
        elif [ "$(printf '%s\n' "$listing" | grep -c .)" -lt 3 ]; then
          echo "::error::${name}: the fixture lists fewer than three workflows, so the pair agrees for the wrong reason and proves nothing. This is the leg that says every answer in the table is being read rather than one of them."
          fail=1
        else
          echo "bites ${name}: $(printf '%s\n' "$listing" | grep -c .) workflow(s) read against $(rows_in_the_section "${dir}/workflow-parity.md" "$heading" | grep -c .) row(s), nothing fires"
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
  check)        cmd_check "${2:-.}" ;;
  check-target) cmd_check_target "${2:-}" "${3:-.}" ;;
  selftest)     cmd_selftest ;;
  *)            echo "usage: $0 check [root] | $0 check-target <listing-file> [root] | $0 selftest" >&2; exit 2 ;;
esac
