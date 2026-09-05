#!/usr/bin/env bash
# A table column that points at work still to come, held to the tracker.
#
# Some tables on this board carry a column whose cells name the issue that will
# do a thing. `Produced by` on docs/attempt-outcomes.md says which issue writes
# a trail entry with that outcome. A reader uses that column to find out
# whether the thing they are waiting for has arrived. Sent to an issue that has
# closed, they read it as arrived and either stop or build it a second time.
#
# `Owned by` on docs/refusal-response.md was the second registered column until
# 2026-09-05. The post that serves every case landed, the page redefined the
# cells as the issue that DECIDED each row, and the first rule issues to
# complete turned three correct cells red. That column is the threat model's
# shape below now, headed `Decided in`, and it is out of the register for the
# reason given there.
#
# The class has been repaired by hand five times, each found by somebody reading
# a page against the tracker rather than by anything in this tree:
#
#   git log --format='%h %ad %s' --date=short origin/master -S'Owned by' -- docs/
#
# WHY A REGISTER RATHER THAN EVERY TABLE, which is the part to read before
# widening it. docs/threat-model.md carries a column of the same shape whose
# closed numbers mean the OPPOSITE: a mitigation that landed. The bytes are
# identical and the direction is reversed, and no reading of this tree tells the
# two apart, so the columns that point forward are named in a register and
# nothing else is judged. The price is stated rather than hidden: a column added
# tomorrow with no line in the register is invisible here, exactly as a paste
# outside pasted-line-reference.sh's subject is invisible to that check.
#
# WHAT A GREEN RUN DOES NOT SAY. It does not say every pointer is right. A cell
# naming an issue that is open and is the wrong one passes, which is the shape
# that left the setup page's number in the outcome table for three days after
# the act it named was split in two. That is a judgement about meaning rather
# than a state anything here could read.
#
# WHY THE LISTING IS HANDED IN RATHER THAN FETCHED. The same trade
# tracker-claim.sh writes at its own check mode: the tracker is a network call,
# and a failed call and a table full of stale pointers arrive here as the same
# missing entries, so a check that refused on it would manufacture a refusal out
# of a timeout. Whoever calls this fetches, in a step of its own, so a failed
# call fails as a failed call.
#
# Two modes:
#   check <states-file> <register> [root]  judge every registered column
#   selftest                               fail unless each leg fires on its own
#                                          fixture, fires alone, and stays quiet
#                                          on the clean one
set -uo pipefail

FIXTURES=".github/lint/fixtures/issue-pointer"

# The cells of one column of one file, one per line, as "<line>\t<cell>".
#
# Fenced blocks are dropped before the scan, for the reason tracker-claim.sh
# gives at its own parser: a table pasted inside a fence is evidence of what a
# page said rather than a table this page is asserting.
#
# Exits 1 where no header row in the file carries the heading, which is the
# register naming a column that is not there.
cells_in() {
  local file="$1" heading="$2"
  awk -v want="$heading" '
    function trim(s) { gsub(/^[[:space:]]+|[[:space:]]+$/, "", s); return s }
    BEGIN { fenced = 0; col = 0; inbody = 0; found = 0 }
    /^[[:space:]]*```/ { fenced = !fenced; next }
    fenced { next }
    {
      if ($0 !~ /^[[:space:]]*\|/) { col = 0; inbody = 0; next }
      n = split($0, cell, "|")
      # A row reads "| a | b |", so the split leaves an empty first and last
      # field and the k-th column is cell[k + 1].
      if (inbody && col > 0) {
        if (col + 1 <= n) { print NR "\t" trim(cell[col + 1]) }
        next
      }
      if (col > 0) {
        # The line after a header is the separator; anything else ends the table
        # before it began, so the heading found above was not a header at all.
        if (trim(cell[2]) ~ /^:?-+:?$/) { inbody = 1; next }
        col = 0; inbody = 0
      }
      for (i = 2; i < n; i++) {
        if (trim(cell[i]) == want) { col = i - 1; found = 1; break }
      }
    }
    END { exit(found ? 0 : 1) }
  ' "$file"
}

# The state the listing gives one number, or the empty string where it names
# none. MERGED is read as CLOSED, as it is next door: a merged pull request is
# closed, and a cell pointing at one is pointing at work that landed.
state_of() {
  local number="$1" states_file="$2" state
  state=$(awk -F'\t' -v n="$number" '$1 == n { print toupper($2); exit }' "$states_file")
  [ "$state" = "MERGED" ] && state=CLOSED
  printf '%s' "$state"
}

# The register, as "<path><tab><column heading>" per line. Comments and blank
# lines are dropped so the file can say why a column is in it.
register_lines() {
  sed -e 's/[[:space:]]*#.*$//' -e '/^[[:space:]]*$/d' "$1"
}

cmd_check() {
  local states_file="${1:-}" register="${2:-}" root="${3:-.}"
  local bad=0 read_count=0 registered=0
  local absent='' no_column='' closed='' unknown='' headless=''
  local path heading file line cell number state cells

  if [ -z "$states_file" ] || [ -z "$register" ]; then
    echo "::error::check needs the tracker's listing and the register, in that order. This mode makes no network call of its own." >&2
    return 2
  fi
  for needed in "$states_file" "$register"; do
    if [ ! -f "$needed" ]; then
      echo "::error::${needed} is missing. An absent listing and a tracker holding nothing are the same bytes here, and so are an absent register and a board with no such column."
      return 1
    fi
  done

  while IFS=$'\t' read -r path heading; do
    [ -n "$path" ] || continue
    if [ -z "$heading" ]; then
      headless="${headless}  ${path}"$'\n'
      continue
    fi
    registered=$((registered + 1))
    file="${root%/}/${path}"
    if ! git -C "$root" ls-files --error-unmatch -- "$path" >/dev/null 2>&1; then
      absent="${absent}  ${path}, registered for the column ${heading}"$'\n'
      continue
    fi
    if ! cells=$(cells_in "$file" "$heading"); then
      no_column="${no_column}  ${path} carries no table column headed ${heading}"$'\n'
      continue
    fi
    while IFS=$'\t' read -r line cell; do
      [ -n "$line" ] || continue
      for number in $(printf '%s' "$cell" | grep -oE '#[0-9]+' | tr -d '#'); do
        read_count=$((read_count + 1))
        state=$(state_of "$number" "$states_file")
        if [ -z "$state" ]; then
          unknown="${unknown}  ${path}:${line}: #${number}, which the listing does not carry"$'\n'
        elif [ "$state" = "CLOSED" ]; then
          closed="${closed}  ${path}:${line}: ${heading} names #${number} and it is closed"$'\n'
        fi
      done
    done <<< "$cells"
  done < <(register_lines "$register")

  if [ -n "$headless" ]; then
    echo "::error::register-line-names-no-column: a register line carries a path and no column heading. A path on its own names no subject, and reading it as every column would judge the tables this check deliberately does not reach."
    printf '%s' "$headless"
    bad=1
  fi
  if [ -n "$absent" ]; then
    echo "::error::register-names-a-file-that-is-not-there: a register line names a path this tree does not track. A register that has drifted refuses rather than falling silent, because a line naming nothing and a column with no stale pointer are the same green run."
    printf '%s' "$absent"
    bad=1
  fi
  if [ -n "$no_column" ]; then
    echo "::error::register-names-a-column-that-is-not-there: a register line names a column heading no table in that file carries. A renamed heading would otherwise take its column out of the population silently."
    printf '%s' "$no_column"
    bad=1
  fi
  if [ -n "$closed" ]; then
    echo "::error::cell-points-at-closed-work: a registered column sends a reader to an issue that is closed. The cell reads exactly as it did on the day it was correct, and somebody following it reads the work as arrived."
    printf '%s' "$closed"
    bad=1
  fi
  if [ -n "$unknown" ]; then
    echo "::error::cell-names-an-issue-the-listing-does-not-carry: a number that never existed and a listing that did not reach far enough are the same bytes here, so this refuses rather than passing over the one it cannot tell from the other."
    printf '%s' "$unknown"
    bad=1
  fi
  if [ "$registered" -eq 0 ] || [ "$read_count" -eq 0 ]; then
    echo "::error::nothing-was-read: the register named ${registered} column(s) and ${read_count} issue reference(s) were read out of them. A register reworded past the parser and a board with no such column are the same silence from here, so this refuses rather than reporting the second."
    bad=1
  fi

  if [ "$bad" -eq 0 ]; then
    echo "ok    ${read_count} issue reference(s) read under ${registered} registered column(s), none closed"
  fi
  return $bad
}

# Every leg, against its own fixture. On every run rather than once at review
# time: every registered cell on this board names an open issue today, so the
# check step cannot fire against the tree, and a parser that had stopped finding
# a column would report the same green forever while the columns it holds
# drifted underneath it.
cmd_selftest() {
  local bad=0 name dir output

  for name in clean cell-points-at-closed-work register-file-absent register-column-absent number-not-in-the-listing register-line-names-no-column; do
    dir="${FIXTURES}/${name}"
    if [ ! -d "$dir" ]; then
      echo "::error::selftest: ${dir} is missing, so the leg it proves has nothing behind it."
      bad=1
      continue
    fi
  done
  [ "$bad" -eq 0 ] || return 1

  output=$(cmd_check "${FIXTURES}/clean/states.txt" "${FIXTURES}/clean/columns.txt" "${FIXTURES}/clean" 2>&1)
  if [ $? -ne 0 ]; then
    echo "::error::selftest: the clean fixture is refused, so the check would redden honest work."
    printf '%s\n' "$output"
    bad=1
  else
    echo "quiet clean fixture passes"
  fi

  for name in cell-points-at-closed-work register-file-absent register-column-absent number-not-in-the-listing register-line-names-no-column; do
    dir="${FIXTURES}/${name}"
    output=$(cmd_check "${dir}/states.txt" "${dir}/columns.txt" "$dir" 2>&1)
    if [ $? -eq 0 ]; then
      echo "::error::selftest: ${name} is accepted. The leg it exists for is not biting and a green run below means nothing."
      bad=1
      continue
    fi
    # The leg that fired has to be the one the fixture is named for, and it has
    # to be the only one: a fixture that trips two legs proves neither, because
    # either could be the one that has stopped working.
    local fired
    fired=$(printf '%s\n' "$output" | grep -oE '::error::[a-z-]+' | sed 's/::error:://' | grep -v '^nothing-was-read$' | sort -u)
    if [ "$fired" != "${name%.trip}" ]; then
      # register-file-absent and register-column-absent name their legs with the
      # register-names- prefix the message carries.
      case "$name" in
        register-file-absent) [ "$fired" = "register-names-a-file-that-is-not-there" ] && { echo "bites ${name}"; continue; } ;;
        register-column-absent) [ "$fired" = "register-names-a-column-that-is-not-there" ] && { echo "bites ${name}"; continue; } ;;
        number-not-in-the-listing) [ "$fired" = "cell-names-an-issue-the-listing-does-not-carry" ] && { echo "bites ${name}"; continue; } ;;
      esac
      echo "::error::selftest: ${name} fired [${fired}] rather than its own leg alone. A fixture that trips two legs proves neither."
      bad=1
      continue
    fi
    echo "bites ${name}"
  done

  return $bad
}

case "${1:-}" in
  check) shift; cmd_check "$@" ;;
  selftest) cmd_selftest ;;
  *)
    echo "usage: $0 check <states-file> <register> [root]" >&2
    echo "       $0 selftest" >&2
    exit 2
    ;;
esac
