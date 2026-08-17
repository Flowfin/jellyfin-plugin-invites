#!/usr/bin/env bash
# A pasted command with its pasted exit status is re-run and has to still agree.
#
# This repository's documents carry evidence rather than assertions: a claim
# comes with the command that produced it and, where the point is an absence,
# with the status that command exited. The claim then ages. The tree moves, the
# command starts answering something else, and the sentence resting on it reads
# exactly as it did on the day it was correct. Nothing notices, because a stale
# paste is well-formed prose and every check in this tree judges shapes that must
# never appear rather than claims that have stopped holding.
#
# Five of these were live when this check was written, in five documents, each
# pasting exit=1 against a command that now exits 0. The five landed green and
# stayed green through every run since. #257 is where that was found and argued.
#
# What this refuses: a pasted `exit=<n>` whose command, re-run at this commit,
# exits something else.
#
# What it does NOT refuse, stated so a green run is not read as more than it is:
#
#   A prose sentence claiming an absence. "There is no controller in this tree"
#   carries no command, so there is nothing here to re-run, and whether such a
#   sentence still holds is a judgement about meaning that no reading of the tree
#   makes. Both of the two claims #257 opens with were that shape. A rule
#   refusing the phrase would refuse the honest uses of it, which those two were
#   on the day they were written, so none is proposed and this check is
#   deliberately narrower than the class the issue names.
#
#   A pasted command with pasted OUTPUT rather than a pasted status. Comparing
#   output means normalising line numbers, ordering and wrapping, and a mismatch
#   there is as often a reflow as a drift. That population is larger than this
#   one and is not covered.
#
#   A command this check declines to run. Only read-only commands from the
#   allowlist below are executed; anything else is reported as not evaluated and
#   counted separately, so a document cannot buy a green mark by pasting a
#   command nothing here will touch.
#
# Two modes:
#   check [root]  read the tracked documents under root and judge every paste
#   selftest      fail unless the rule fires on the fixture whose status moved,
#                 names it alone, stays quiet on the clean one, and declines the
#                 one it may not run rather than passing it
set -uo pipefail

FIXTURES=".github/lint/fixtures/pasted-exit-status"

# Which commands may be run. This check executes text out of a document, so the
# allowlist is the whole of what makes that safe: read-only git plumbing and the
# text filters a paste pipes it through. A first word outside this is declined
# rather than run, and the decline is printed.
ALLOWED_FIRST='git grep sed awk wc head tail sort uniq tr cut'
ALLOWED_GIT='grep ls-files ls-tree log show rev-parse diff'

# Characters that end evaluation wherever they appear outside quotes: a second
# command, a redirection, a substitution, a background job. None of them belongs
# in the evidence a document pastes, and each is a way to make this check do
# something other than read.
REFUSED_OUTSIDE_QUOTES=';&<>`'

fail=0
read_count=0
ok_count=0
declined_count=0
mismatches=''

# The command with quoted spans blanked out, so a pipe or a semicolon inside a
# pattern is not read as shell syntax. The lint rules in this tree are full of
# alternations, so this is not a corner case: without it every interesting paste
# would be declined and the check would be green by never looking.
mask_quotes() {
  awk '{
    out = ""; q = ""
    n = length($0)
    for (i = 1; i <= n; i++) {
      c = substr($0, i, 1)
      if (q == "") {
        if (c == "'"'"'" || c == "\"") { q = c; out = out c; continue }
        out = out c
      } else {
        if (c == q) { q = "" ; out = out c; continue }
        out = out "_"
      }
    }
    print out
  }' <<< "$1"
}

# Whether this command may be run, and why not when it may not. Prints an empty
# line for yes and the reason for no.
why_not_evaluable() {
  local cmd="$1" masked segment first sub i
  masked=$(mask_quotes "$cmd")

  if [ "$masked" != "${masked%\$(*}" ]; then
    printf 'it contains a command substitution'
    return
  fi
  for (( i = 0; i < ${#REFUSED_OUTSIDE_QUOTES}; i++ )); do
    local ch="${REFUSED_OUTSIDE_QUOTES:i:1}"
    if [ "$masked" != "${masked//"$ch"/}" ]; then
      printf 'it contains %s outside quotes' "$ch"
      return
    fi
  done

  # Split on the pipes the masked form shows, then read the first word of each
  # segment out of the masked text. A segment's first word cannot be inside
  # quotes, so the masked copy is safe to read it from.
  local IFS='|'
  read -r -a segments <<< "$masked"
  unset IFS
  for segment in "${segments[@]}"; do
    first=$(printf '%s' "$segment" | awk '{print $1}')
    if [ -z "$first" ]; then
      printf 'it has an empty pipeline segment'
      return
    fi
    if ! printf ' %s ' "$ALLOWED_FIRST" | grep -qF " $first "; then
      printf '%s is not on the allowlist of read-only commands' "$first"
      return
    fi
    if [ "$first" = git ]; then
      sub=$(printf '%s' "$segment" | awk '{print $2}')
      if ! printf ' %s ' "$ALLOWED_GIT" | grep -qF " $sub "; then
        printf 'git %s is not on the allowlist of read-only subcommands' "${sub:-<none>}"
        return
      fi
    fi
  done
  printf ''
}

# Runs one command at root and prints the status it exited. pipefail is off
# inside, because a reader reproducing the paste in their own shell gets the last
# segment's status and this has to agree with them rather than with this script's
# own options.
status_of() {
  local root="$1" cmd="$2"
  (
    set +u
    set +o pipefail
    cd "$root" || exit 127
    eval "$cmd" >/dev/null 2>&1
  )
  printf '%s' "$?"
}

# The command a paste is about: the line above the status, with the prompt and
# the trailing echo taken off. Both spellings of the echo appear in this tree.
command_on_line() {
  local line="$1"
  line="${line#"${line%%[![:space:]]*}"}"
  line="${line#\$ }"
  line=$(printf '%s' "$line" | sed -E 's/[[:space:]]*;[[:space:]]*echo[[:space:]]+"?exit=\$\?"?[[:space:]]*$//')
  printf '%s' "$line"
}

# Every paste in one file, as "<line number> <expected status> <command>".
pastes_in() {
  awk '
    function trim(s) { gsub(/^[ \t]+|[ \t]+$/, "", s); return s }
    {
      here = trim($0)
      if (here ~ /^exit=[0-9]+$/ && previous != "") {
        split(here, parts, "=")
        print NR "\t" parts[2] "\t" previous
      }
      if (here != "" && here != "```") { previous = $0 } else { previous = "" }
    }
  ' "$1"
}

judge_file() {
  local root="$1" relative="$2"
  local absolute="${root%/}/${relative}"
  local line expected raw cmd reason actual

  while IFS=$'\t' read -r line expected raw; do
    [ -n "$line" ] || continue
    cmd=$(command_on_line "$raw")
    read_count=$((read_count + 1))

    reason=$(why_not_evaluable "$cmd")
    if [ -n "$reason" ]; then
      declined_count=$((declined_count + 1))
      echo "note  ${relative}:${line}: not evaluated, ${reason}. The pasted exit=${expected} stands on nothing this check read."
      continue
    fi

    actual=$(status_of "$root" "$cmd")
    if [ "$actual" = "$expected" ]; then
      ok_count=$((ok_count + 1))
    else
      mismatches="${mismatches}${relative}:${line}"$'\n'
      echo "::error::${relative}:${line}: the paste says exit=${expected} and the command exits ${actual} at this commit."
      echo "  ${cmd}"
      echo "  The sentence resting on it reads as it did on the day it was correct, which is why nothing else catches this. Correct the paste and the claim together, and say what was wrong rather than editing it quietly."
      fail=1
    fi
  done < <(pastes_in "$absolute")
}

cmd_check() {
  local root="${1:-.}"

  if ! git -C "$root" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "::error::${root} is not a git work tree. Every command this check re-runs is git plumbing, so it refuses rather than reporting a scan that could not run one."
    return 1
  fi

  local subjects
  subjects=$(git -C "$root" ls-files -- '*.md' ':!.github/lint/fixtures/**')
  if [ -z "$subjects" ]; then
    echo "::error::no tracked documents under ${root}. Refusing rather than passing on an empty subject list, which is what a moved directory looks like from here."
    return 1
  fi

  local relative
  while IFS= read -r relative; do
    [ -n "$relative" ] || continue
    judge_file "$root" "$relative"
  done <<< "$subjects"

  if [ "$read_count" -eq 0 ]; then
    echo "::error::no pasted exit status was found in any tracked document. This tree had eleven when the check was written, so nothing being found means the recognised shape has moved and this check is green by looking at nothing."
    return 1
  fi

  echo "ok    ${read_count} pasted exit status(es) read, ${ok_count} agree, ${declined_count} not evaluated"
  return $fail
}

# Runs one fixture and prints "<read> <ok> <declined> <mismatched lines>".
outcome_of() {
  fail=0; read_count=0; ok_count=0; declined_count=0; mismatches=''
  judge_file "." "$1" >/dev/null 2>&1
  printf '%s %s %s %s' "$read_count" "$ok_count" "$declined_count" \
    "$(printf '%s' "$mismatches" | sed '/^$/d' | tr '\n' ',' | sed 's/,$//')"
}

cmd_selftest() {
  # On every run rather than once at review time. Every paste in this tree agrees
  # today, so the step that scans the tree cannot fire, and a rule that had
  # stopped recognising the shape would go green forever while the documents
  # drifted underneath it. That is the failure that makes a documentation check
  # worse than none: the file is trusted because a green mark stands behind it.
  local selftest_fail=0 got

  local clean="${FIXTURES}/clean.md"
  local moved="${FIXTURES}/status-has-moved.trip.md"
  local opaque="${FIXTURES}/command-is-not-evaluable.md"
  local f
  for f in "$clean" "$moved" "$opaque"; do
    if [ ! -f "$f" ]; then
      echo "::error::missing ${f}. Every case owns its own document."
      return 1
    fi
  done

  got=$(outcome_of "$clean")
  case "$got" in
    '2 2 0 ')
      echo "quiet clean.md: two pastes read, both agree, nothing fires"
      ;;
    *)
      echo "::error::clean.md: expected [2 2 0] and no mismatch, got [${got}]. Either the check reds a document that had done nothing wrong, or it is agreeing by reading fewer pastes than the fixture holds."
      selftest_fail=1
      ;;
  esac

  got=$(outcome_of "$moved")
  case "$got" in
    "2 1 0 ${moved}:4")
      echo "bites status-has-moved.trip.md: the one paste whose status moved is named and the correct one beside it is not"
      ;;
    *)
      echo "::error::status-has-moved.trip.md: expected [2 1 0] with ${moved}:4 the only mismatch, got [${got}]. The fixture pastes exit=1 against a command that exits 0, which is the one-character mistake this check exists for."
      selftest_fail=1
      ;;
  esac

  got=$(outcome_of "$opaque")
  case "$got" in
    '1 0 1 ')
      echo "holds command-is-not-evaluable.md: the paste is declined and counted, not run and not passed"
      ;;
    *)
      echo "::error::command-is-not-evaluable.md: expected [1 0 1] and no mismatch, got [${got}]. A command outside the allowlist has to be reported as not evaluated; running it would make this check execute whatever a document says, and passing it silently would let a paste buy a green mark."
      selftest_fail=1
      ;;
  esac

  return $selftest_fail
}

case "${1:-}" in
  check)    cmd_check "${2:-.}" ;;
  selftest) cmd_selftest ;;
  *)        echo "usage: $0 check [root] | $0 selftest" >&2; exit 2 ;;
esac
