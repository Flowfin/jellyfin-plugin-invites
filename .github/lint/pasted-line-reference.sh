#!/usr/bin/env bash
# A pasted `path:line:content` reference is read back and has to still agree.
#
# This repository's documents point at the source rather than describing it, and
# the cheapest way to point is to paste what `git grep -n` printed. That paste
# carries a line number, and a line number is the one field in it that nothing
# else re-derives. Insert a paragraph above the match and the command still finds
# what the sentence claims, prints it at a different line, and the paste on the
# page reads exactly as it did on the day it was correct.
#
# #282 is where that was measured: eleven such references had drifted across five
# documents, and every one of them was repaired by hand.
#
# `.github/lint/pasted-exit-status.sh` is the neighbouring check and it declines
# this class by name in its own header, because comparing pasted OUTPUT means
# normalising line numbers, ordering and wrapping. That reason holds for output
# in general and does not hold for this subset: a `path:line:content` reference
# is three fields, and the third is compared against exactly one line of exactly
# one file. There is nothing to normalise, no ordering to reconcile and no
# wrapping.
#
# What this refuses: a `path:line:content` reference in tracked text whose line,
# read at this commit, does not carry the pasted content.
#
# What it does NOT refuse, stated so a green run is not read as more than it is:
#
#   A reference with no line number. A sentence saying a file says something
#   carries no field this can compare, and whether it still holds is a judgement
#   about meaning that no reading of the tree makes.
#
#   A reference whose path is not in the tree at the revision it names. That is a
#   different defect with a different repair, and it is reported as not evaluated
#   and counted separately rather than passed silently.
#
#   A truncated paste, in the sense of being told apart from a drift. A reference
#   piped through `cut` or `head` carries fewer bytes than the line it came from,
#   so it is refused here like any other disagreement. Nothing here can tell the
#   two apart, and the repair for both is the same: paste the line as it is.
#
#   Anything outside tracked text. A reference in an issue body, a pull-request
#   body or a commit message is not a byte in this tree and nothing here reads it.
#
#   A difference that is only a trailing carriage return. One is stripped off
#   both sides before they are compared, on the document line and on the target
#   line alike. That is not tidiness: a clone with `core.autocrlf` set hands this
#   check a working tree whose source files end every line with one and whose
#   documents do not, so without the strip the check refuses fifty-five of this
#   tree's fifty-eight references on Windows and none of them on a Linux runner.
#   A check whose verdict depends on the reader's git configuration is worse than
#   none, and the byte it is now blind to is not the drift it exists for.
#
# A reference may name a revision, as `<rev>:<path>:<line>:<content>`, and then
# it is read at that revision rather than at this commit. That is the shape a
# document uses when it records what a line said on a day, and reading it against
# the working tree instead would refuse an honest paste for being honest.
#
# Two modes:
#   check [root]  read the tracked documents under root and judge every reference
#   selftest      fail unless the rule fires on the fixture whose line has moved,
#                 names it alone, stays quiet on the clean one, and declines the
#                 one whose revision is not in the tree rather than passing it
set -uo pipefail

FIXTURES=".github/lint/fixtures/pasted-line-reference"

RECORDS="${RECORDS:-.github/lint/pasted-line-reference-records.txt}"

fail=0
read_count=0
ok_count=0
declined_count=0
recorded_count=0
dangling_count=0
mismatches=''

# Every reference in one file, as five fields separated by a unit separator:
# line, revision, path, number, content.
#
# The separator is not a tab. A tab is whitespace to `read`, so an empty revision
# field is swallowed rather than read as empty, every field after it shifts one
# place left, and the path arrives in the revision variable. That is exactly what
# the first run of this check did: it declined all thirty-seven references in the
# tree with a reason naming a path as a revision, which reads as a scan that ran
# and found nothing wrong.
#
# The first `:<digits>:` in the line separates the path from the content, because
# the fields before it are a revision and a path and neither of those carries
# one. What is left of it is `<path>` or `<rev>:<path>`, split at its first
# colon; what is right of it is the pasted content, colons and all.
references_in() {
  awk '
    BEGIN { unit = sprintf("%c", 31) }
    {
      here = $0
      sub(/\r$/, "", here)
      sub(/^[ \t]+/, "", here)
      if (!match(here, /:[0-9]+:/)) { next }

      head = substr(here, 1, RSTART - 1)
      number = substr(here, RSTART + 1, RLENGTH - 2)
      content = substr(here, RSTART + RLENGTH)

      rev = ""
      path = head
      colon = index(head, ":")
      if (colon > 0) {
        rev = substr(head, 1, colon - 1)
        path = substr(head, colon + 1)
      }

      # A path, and not a sentence that happens to carry a number between two
      # colons. One extension, no spaces, no second colon.
      if (path !~ /^[A-Za-z0-9_.\/-]+\.[A-Za-z0-9]+$/) { next }
      if (rev != "" && rev !~ /^[A-Za-z0-9_.\/-]+$/) { next }

      print NR unit rev unit path unit number unit content
    }
  ' "$1"
}

# Each target is read once, whole, and kept. A document points at one file many
# times over, and reading a line out of it per reference would re-read the file
# per reference. Keeping the lines also separates a line that is empty from a
# line that is not there, which reading one line at a time cannot do.
declare -A TARGET_READ=()
declare -A TARGET_REFUSED=()
declare -A TARGET_LINE=()

# Reads the file a reference names into TARGET_LINE, or records in
# TARGET_REFUSED why it could not be read.
load_target() {
  local root="$1" rev="$2" path="$3" key="$4"
  local -a lines=()
  local i

  [ -n "${TARGET_READ[$key]:-}" ] && return 0
  TARGET_READ[$key]=1

  if [ -z "$rev" ]; then
    if [ ! -f "${root%/}/${path}" ]; then
      TARGET_REFUSED[$key]="${path} is not a file in this tree"
      return 0
    fi
    mapfile -t lines < "${root%/}/${path}"
  else
    if ! git -C "$root" rev-parse --verify --quiet "${rev}^{commit}" >/dev/null 2>&1; then
      TARGET_REFUSED[$key]="${rev} is not a revision in this clone"
      return 0
    fi
    if ! git -C "$root" cat-file -e "${rev}:${path}" 2>/dev/null; then
      TARGET_REFUSED[$key]="${path} is not a file at ${rev}"
      return 0
    fi
    mapfile -t lines < <(git -C "$root" show "${rev}:${path}")
  fi

  for i in "${!lines[@]}"; do
    TARGET_LINE["${key}:$((i + 1))"]="${lines[$i]%$'\r'}"
  done
  return 0
}

# A document that quotes a line as it read on a day, in a record of something
# that happened, is not drifting when the line moves afterwards. The register
# holds those quotations, one per line, as the document and the reference text
# separated by a tab.
#
# It is a debt carrying what retires it rather than a dispensation, and it fails
# closed in both directions: an entry whose reference has started agreeing again
# is refused as caught up, and an entry naming a reference no document carries is
# refused as dangling. So the register cannot quietly outlive the repair, and it
# cannot waive a reference that has begun to drift for a second reason.
declare -A RECORDED=()
declare -A RECORDED_MET=()

load_records() {
  local file="$1" entry document reference

  RECORDED=()
  RECORDED_MET=()
  [ -f "$file" ] || return 0

  while IFS= read -r entry; do
    entry="${entry%$'\r'}"
    case "$entry" in ''|'#'*) continue ;; esac
    case "$entry" in
      *$'\t'*) ;;
      *)
        echo "::error::${file}: an entry carries no tab, so it names no reference: ${entry}"
        fail=1
        continue
        ;;
    esac
    document="${entry%%$'\t'*}"
    reference="${entry#*$'\t'}"
    RECORDED["${document}"$'\t'"${reference}"]=1
  done < "$file"
}

judge_file() {
  local root="$1" relative="$2"
  local absolute="${root%/}/${relative}"
  local line rev path number pasted key where entry

  while IFS=$'\037' read -r line rev path number pasted; do
    [ -n "$line" ] || continue
    read_count=$((read_count + 1))
    where="${rev:+${rev}:}${path}:${number}"
    key="${rev}|${path}"
    entry="${relative}"$'\t'"${where}:${pasted}"

    load_target "$root" "$rev" "$path" "$key"
    if [ -n "${TARGET_REFUSED[$key]:-}" ]; then
      declined_count=$((declined_count + 1))
      echo "note  ${relative}:${line}: not evaluated, ${TARGET_REFUSED[$key]}. The reference to ${where} stands on nothing this check read."
      continue
    fi

    if [ "${TARGET_LINE["${key}:${number}"]-}" = "$pasted" ]; then
      if [ -n "${RECORDED[$entry]:-}" ]; then
        RECORDED_MET[$entry]=1
        mismatches="${mismatches}${relative}:${line}"$'\n'
        echo "::error::${relative}:${line}: the register records this reference as a dated quotation and it agrees with the tree again."
        echo "  ${where}"
        echo "  An entry that has caught up is refused rather than kept, because a register nobody prunes stops being a list of debts and becomes a list of references nothing judges. Take the entry out."
        fail=1
        continue
      fi
      ok_count=$((ok_count + 1))
      continue
    fi

    if [ -n "${RECORDED[$entry]:-}" ]; then
      RECORDED_MET[$entry]=1
      recorded_count=$((recorded_count + 1))
      echo "note  ${relative}:${line}: recorded as a dated quotation rather than a claim about this commit, so ${where} is not judged here."
      continue
    fi

    mismatches="${mismatches}${relative}:${line}"$'\n'
    echo "::error::${relative}:${line}: the reference names ${where} and that line carries something else at this commit."
    echo "  pasted: ${pasted}"
    if [ -z "${TARGET_LINE["${key}:${number}"]+set}" ]; then
      echo "  there:  <no line ${number} in that file>"
    else
      echo "  there:  ${TARGET_LINE["${key}:${number}"]}"
    fi
    echo "  A line number is the one field in a paste that nothing re-derives, so the sentence resting on it reads as it did on the day it was correct. Re-run the command, paste what it prints, and say what had moved rather than correcting the number quietly."
    fail=1
  done < <(references_in "$absolute")
}

cmd_check() {
  local root="${1:-.}"

  if ! git -C "$root" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "::error::${root} is not a git work tree. The subjects are the tracked documents and a reference naming a revision is resolved with git, so this refuses rather than reporting a scan that could read neither."
    return 1
  fi

  local subjects
  subjects=$(git -C "$root" ls-files -- '*.md' ':!.github/lint/fixtures/**')
  if [ -z "$subjects" ]; then
    echo "::error::no tracked documents under ${root}. Refusing rather than passing on an empty subject list, which is what a moved directory looks like from here."
    return 1
  fi

  load_records "${root%/}/${RECORDS}"

  local relative
  while IFS= read -r relative; do
    [ -n "$relative" ] || continue
    judge_file "$root" "$relative"
  done <<< "$subjects"

  refuse_dangling_records

  if [ "$read_count" -eq 0 ]; then
    echo "::error::no pasted line reference was found in any tracked document. This tree had fifty-eight when the check was written, so nothing being found means the recognised shape has moved and this check is green by looking at nothing."
    return 1
  fi

  echo "ok    ${read_count} pasted line reference(s) read, ${ok_count} agree, ${recorded_count} recorded as dated quotations, ${declined_count} not evaluated"
  return $fail
}

# An entry naming a reference no document carries. It is refused rather than
# ignored, because an entry that resolves to nothing waives nothing and reads as
# though it still covers the thing it was written for.
refuse_dangling_records() {
  local entry document reference
  dangling_count=0
  for entry in "${!RECORDED[@]}"; do
    [ -n "${RECORDED_MET[$entry]:-}" ] && continue
    dangling_count=$((dangling_count + 1))
    document="${entry%%$'\t'*}"
    reference="${entry#*$'\t'}"
    echo "::error::${RECORDS}: no document carries the reference this entry names, so it waives nothing."
    echo "  ${document}"
    echo "  ${reference}"
    echo "  Either the reference was repaired and the entry is owed removal, or the entry was written with a byte the document does not carry. It is refused rather than ignored, because an entry that resolves to nothing still reads as cover."
    fail=1
  done
}

# Runs one fixture and prints "<read> <ok> <declined> <recorded> <mismatched lines>".
outcome_of() {
  fail=0; read_count=0; ok_count=0; declined_count=0; recorded_count=0; mismatches=''
  judge_file "." "$1" >/dev/null 2>&1
  printf '%s %s %s %s %s' "$read_count" "$ok_count" "$declined_count" "$recorded_count" \
    "$(printf '%s' "$mismatches" | sed '/^$/d' | tr '\n' ',' | sed 's/,$//')"
}

cmd_selftest() {
  # On every run rather than once at review time. Every reference in this tree
  # agrees today, so the step that scans the tree cannot fire, and a rule that
  # had stopped recognising the shape would go green forever while the documents
  # drifted underneath it. That is the failure that makes a documentation check
  # worse than none: the page is trusted because a green mark stands behind it.
  local selftest_fail=0 got

  local clean="${FIXTURES}/clean.md"
  local moved="${FIXTURES}/line-has-moved.trip.md"
  local absent="${FIXTURES}/revision-is-not-in-the-tree.md"
  local recorded="${FIXTURES}/recorded-quotation.md"
  local caught="${FIXTURES}/record-has-caught-up.trip.md"
  local register="${FIXTURES}/records.txt"
  local target="${FIXTURES}/target.txt"
  local f
  for f in "$clean" "$moved" "$absent" "$recorded" "$caught" "$register" "$target"; do
    if [ ! -f "$f" ]; then
      echo "::error::missing ${f}. Every case owns its own document, and all of them point at a target inside the fixture directory so that an edit to the plugin cannot move a fixture's line underneath it."
      return 1
    fi
  done

  RECORDS="$register"
  load_records "$register"

  got=$(outcome_of "$clean")
  case "$got" in
    '2 2 0 0 ')
      echo "quiet clean.md: two references read, both agree, nothing fires"
      ;;
    *)
      echo "::error::clean.md: expected [2 2 0 0] and no mismatch, got [${got}]. Either the check reds a document that had done nothing wrong, or it is agreeing by reading fewer references than the fixture holds."
      selftest_fail=1
      ;;
  esac

  got=$(outcome_of "$moved")
  case "$got" in
    "2 1 0 0 ${moved}:11")
      echo "bites line-has-moved.trip.md: the one reference whose line moved is named and the correct one beside it is not"
      ;;
    *)
      echo "::error::line-has-moved.trip.md: expected [2 1 0 0] with ${moved}:11 the only mismatch, got [${got}]. The fixture pastes a real line of the target under the number of its neighbour, which is what inserting one line above a match does to every reference below it."
      selftest_fail=1
      ;;
  esac

  got=$(outcome_of "$absent")
  case "$got" in
    '1 0 1 0 ')
      echo "holds revision-is-not-in-the-tree.md: the reference is declined and counted, not resolved and not passed"
      ;;
    *)
      echo "::error::revision-is-not-in-the-tree.md: expected [1 0 1 0] and no mismatch, got [${got}]. A reference this cannot resolve has to be reported as not evaluated; passing it silently would let a paste buy a green mark by naming something nothing here can read."
      selftest_fail=1
      ;;
  esac

  got=$(outcome_of "$recorded")
  case "$got" in
    '1 0 0 1 ')
      echo "holds recorded-quotation.md: the disagreement the register names is counted as a dated quotation and refuses nothing"
      ;;
    *)
      echo "::error::recorded-quotation.md: expected [1 0 0 1] and no mismatch, got [${got}]. A document quoting a line as it read on a day is not drifting when the line moves afterwards, and the register is how it says so."
      selftest_fail=1
      ;;
  esac

  got=$(outcome_of "$caught")
  case "$got" in
    "1 0 0 0 ${caught}:6")
      echo "bites record-has-caught-up.trip.md: the register entry whose reference agrees again is refused rather than kept"
      ;;
    *)
      echo "::error::record-has-caught-up.trip.md: expected [1 0 0 0] with ${caught}:6 the only mismatch, got [${got}]. An entry that has caught up waives nothing, and a register nobody prunes stops being a list of debts."
      selftest_fail=1
      ;;
  esac

  # The other direction of the same register. The two documents above are read
  # again here rather than reusing the readings, because those were taken inside
  # a command substitution and which entries they met died with that subshell.
  # What is left unmet afterwards is the entry written for nothing.
  judge_file "." "$recorded" >/dev/null 2>&1
  judge_file "." "$caught" >/dev/null 2>&1
  fail=0
  dangling_count=0
  refuse_dangling_records >/dev/null
  got="$dangling_count"
  if [ "$got" = 1 ] && [ "$fail" = 1 ]; then
    echo "bites records.txt: the entry naming a reference no document carries is refused as dangling"
  else
    echo "::error::records.txt: expected exactly one dangling entry refused, got ${got} with fail=${fail}. A register that fails closed in one direction only is a register that outlives its repair."
    selftest_fail=1
  fi

  return $selftest_fail
}

case "${1:-}" in
  check)    cmd_check "${2:-.}" ;;
  selftest) cmd_selftest ;;
  *)        echo "usage: $0 check [root] | $0 selftest" >&2; exit 2 ;;
esac
