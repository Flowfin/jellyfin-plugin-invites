#!/usr/bin/env bash
# The security policy is held to the threat model's undefended list.
#
# Two documents say what this plugin does not defend against, and they say it in
# the same words on purpose. The threat model is where each item is placed
# against the attack it belongs to; the security policy is where somebody
# deciding whether to run this plugin reads it. Kept by hand they drift in one
# direction: an item is softened or dropped in the policy, which is the document
# a reader trusts, and the threat model goes on carrying the honest wording where
# fewer people look.
#
# That direction is the one this refuses. Every item under "What is not defended"
# in the threat model has to appear, word for word, under the same heading in the
# policy.
#
# What it does not refuse, stated so a green run is not read as more than it is:
#
#   A paragraph the policy carries that the threat model does not. The two
#   sections open differently, the policy with two paragraphs of framing and the
#   threat model with one, so a symmetric rule would refuse the policy for having
#   its own voice. An item added to the policy alone is therefore invisible here
#   and the review is where it is caught.
#
#   Whether the list is the right list. Whether an item belongs, and whether it
#   is honestly worded, are judgements no reading of these two files makes.
#
# Line wrapping is normalised before the comparison. Word for word is about the
# words, and a reflow of one file is not drift; a synonym is.
#
# The first paragraph of each section is its opening rather than an item. That is
# positional and it is the one assumption here that a later edit could break, so
# the fixtures fix it: an item added ahead of the opening would be dropped
# silently and the clean fixture is what says otherwise.
#
# A second leg reads the same pair of documents for a different claim they both
# make about each other. The threat model carries the bound on what a leaked
# link costs as a block quote and says of it that the policy repeats it word for
# word; the policy carries the same quote and says the same thing back. Two
# sentences claiming to be one sentence, and until this leg nothing compared
# them, so an edit to either page left both claims standing and only a reader
# who opened both files could tell.
#
# It refuses in both directions, which the leg above deliberately does not. The
# asymmetry there is about section framing - the policy opens its section in its
# own voice and a symmetric rule would refuse it for that. A block quote has no
# framing: one that is only in the model is a sentence the policy has dropped,
# and one that is only in the policy is a quotation of something the model does
# not say, and both are the claim being false.
#
# What it does not refuse. Whether the bound is the right bound, and whether
# either page's prose around it still supports it, are judgements no reading of
# these two files makes. A block quote opening with a GitHub alert marker is
# dropped rather than compared, because those are formatting on a page rather
# than a sentence either document is quoting from the other; neither file
# carries one today and the rule would otherwise refuse the first one somebody
# adds to a single page.
#
# A third leg reads neither document. It reads the tracked path list and refuses
# a path other than SECURITY.md whose basename an analysis reads as the security
# policy. The check that scores this repository matches the basename and stops
# at the first path it finds, so a fixture named after the real file is the
# document an outside reader is scored on, and the two legs above stay green
# while it happens - they read the path they are given and never ask which path
# somebody else would have read.
#
# What it does not refuse. Whether the policy it protects says anything worth
# reading; that is the two legs above and the review. And a basename outside the
# list it carries: the list is the one the analysis matches today, so a name
# added upstream tomorrow walks through until the list moves.
#
# Two modes:
#   check             read the two tracked documents and judge them
#   selftest          fail unless the rule fires on each tripping fixture, names
#                     exactly the item that moved, and stays quiet on the clean
#                     pair
set -uo pipefail

FIXTURES=".github/lint/fixtures/undefended-parity"

# The two subjects and the heading they share.
THREAT_MODEL='docs/threat-model.md'
POLICY='SECURITY.md'
HEADING='## What is not defended'

# The fixture policy is NOT named after the real one, and the reason is the
# third leg below. A supply-chain analysis looks for the policy by basename and
# reads the first path it matches, so a fixture called SECURITY.md six
# directories down is the file an outside reader is scored on. That is not a
# hypothetical: it scored this repository on 842 bytes of fixture prose for a
# month. The fixture mirrors the real document in everything except its name.
FIXTURE_POLICY='policy.md'

# The basenames an analysis reads as this repository's security policy, lower
# case. Taken from the set the OpenSSF Scorecard check matches; a name added
# there later is a name this list does not hold, which is what the leg's own
# output says rather than claiming coverage.
POLICY_BASENAMES='security.md security.markdown security.adoc security.rst'

fail=0

# The paragraphs of one section, one per line, with the wrapping taken out.
#
# A heading ends the section, so a document that never opens it prints nothing
# and the caller refuses rather than reading that as a section with no items.
paragraphs_in_the_section() {
  local file="$1"
  awk -v heading="$HEADING" '
    BEGIN { inside = 0; buf = "" }
    /^## / {
      if (inside) { if (buf != "") { print buf; buf = "" } ; inside = 0; next }
      if ($0 == heading) { inside = 1 }
      next
    }
    inside {
      line = $0
      gsub(/^[ \t]+|[ \t]+$/, "", line)
      if (line == "") { if (buf != "") { print buf; buf = "" } }
      else { buf = (buf == "" ? line : buf " " line) }
    }
    END { if (buf != "") print buf }
  ' "$file"
}

# The items, which is every paragraph after the opening one.
items_in_the_section() {
  paragraphs_in_the_section "$1" | tail -n +2
}

# The block quotes of a document, one per line, with the wrapping taken out.
#
# Consecutive quoted lines are one quotation and a line that is not quoted ends
# it, so two quotes separated by prose are two subjects rather than one. A quote
# opening with a GitHub alert marker is dropped, for the reason at the top.
quotations_in() {
  local file="$1"
  awk '
    BEGIN { buf = "" }
    /^[ 	]*>/ {
      line = $0
      sub(/^[ 	]*>[ 	]?/, "", line)
      gsub(/^[ 	]+|[ 	]+$/, "", line)
      if (line == "") { next }
      buf = (buf == "" ? line : buf " " line)
      next
    }
    { if (buf != "") { print buf; buf = "" } }
    END { if (buf != "") print buf }
  ' "$file" | grep -v '^\[!'
}

# Judges the quotations of one pair, in both directions. Prints the wording in
# full rather than a first line, because the repair is a copy and a reader needs
# the bytes to copy.
judge_quotations() {
  local model_file="$1" policy_file="$2" label="$3"
  local model_quotes policy_quotes dropped added

  model_quotes=$(quotations_in "$model_file")
  policy_quotes=$(quotations_in "$policy_file")

  if [ -z "$model_quotes" ] || [ -z "$policy_quotes" ]; then
    echo "::error::${label}: one of the two documents carries no block quote. The bound both of them say the other repeats word for word is what this leg reads, so an empty side is refused rather than passed as a pair that agrees."
    fail=1
    return 1
  fi

  dropped=$(comm -23 <(printf '%s
' "$model_quotes" | sort)                      <(printf '%s
' "$policy_quotes" | sort))
  added=$(comm -13 <(printf '%s
' "$model_quotes" | sort)                    <(printf '%s
' "$policy_quotes" | sort))

  if [ -n "$dropped" ]; then
    echo "::error::${label}: a block quote in ${model_file} is not in ${policy_file} word for word. Both pages say of it that the other repeats it, so an edit to one leaves two documents each claiming to quote the other and neither doing it."
    printf '%s
' "$dropped" | sed 's/^/  /'
    fail=1
  fi
  if [ -n "$added" ]; then
    echo "::error::${label}: a block quote in ${policy_file} is not in ${model_file} word for word. A quotation of something the model does not say reads as the model's wording to somebody who only opens the policy."
    printf '%s
' "$added" | sed 's/^/  /'
    fail=1
  fi
  if [ -n "$dropped" ] || [ -n "$added" ]; then
    return 1
  fi

  echo "ok    ${label}: $(printf '%s
' "$model_quotes" | grep -c .) block quote(s), each word for word in both documents"
  return 0
}

# Judges one pair. Prints the items the policy is missing, each on its own line,
# and sets fail. The wording is printed in full rather than as a first line,
# because the repair is a copy and a reader needs the bytes to copy.
judge() {
  local model_file="$1" policy_file="$2" label="$3"
  local model_items policy_paragraphs missing

  model_items=$(items_in_the_section "$model_file")
  policy_paragraphs=$(paragraphs_in_the_section "$policy_file")

  if [ -z "$model_items" ]; then
    echo "::error::${label}: ${model_file} has no items under '${HEADING}'. Refusing rather than passing on an empty list, which is what a renamed heading or a reordered document looks like from here."
    fail=1
    return 1
  fi
  if [ -z "$policy_paragraphs" ]; then
    echo "::error::${label}: ${policy_file} has no section headed '${HEADING}'. The section is the thing this check is about; without it every item is missing and a silent pass would say the opposite."
    fail=1
    return 1
  fi

  missing=$(comm -23 <(printf '%s\n' "$model_items" | sort) \
                     <(printf '%s\n' "$policy_paragraphs" | sort))

  if [ -n "$missing" ]; then
    echo "::error::${label}: an undefended item in ${model_file} is not in ${policy_file} word for word. The policy is the document somebody reads before trusting this plugin, so an item that is softened or dropped there is the honest wording kept where fewer people look."
    printf '%s\n' "$missing" | sed 's/^/  /'
    fail=1
    return 1
  fi

  echo "ok    ${label}: $(printf '%s\n' "$model_items" | grep -c .) undefended item(s), each word for word in ${policy_file}"
  return 0
}

# Every tracked path whose basename an analysis would read as this repository's
# security policy, except the policy itself. Reads the path list on stdin so the
# selftest can hand it a list it wrote rather than the tree it is running in.
paths_read_as_the_policy() {
  local line lowered base
  while IFS= read -r line; do
    [ -n "$line" ] || continue
    lowered=$(printf '%s' "$line" | tr '[:upper:]' '[:lower:]')
    base="${lowered##*/}"
    case " ${POLICY_BASENAMES} " in
      *" ${base} "*) [ "$line" = "$POLICY" ] || printf '%s\n' "$line" ;;
    esac
  done
}

# Judges one path list. Prints every path that shadows the policy and sets fail.
judge_policy_names() {
  local label="$1" shadows
  shadows=$(paths_read_as_the_policy)

  if [ -n "$shadows" ]; then
    echo "::error::${label}: a tracked path other than ${POLICY} carries a basename an analysis reads as the security policy. The check that scores this repository matches the basename and stops at the first path it finds, so the document an outside reader is scored on is whichever of these git lists first - not the policy."
    printf '%s\n' "$shadows" | sed 's/^/  /'
    fail=1
    return 1
  fi

  echo "ok    ${label}: no tracked path but ${POLICY} is read as the policy"
  return 0
}

cmd_check() {
  local root="${1:-.}"
  local model_file="${root%/}/${THREAT_MODEL}"
  local policy_file="${root%/}/${POLICY}"

  if [ ! -f "$model_file" ]; then
    echo "::error::${model_file} is missing. The undefended list is what this check reads, so it refuses rather than reporting a scan that read nothing."
    return 1
  fi
  if [ ! -f "$policy_file" ]; then
    echo "::error::${policy_file} is missing. The policy is the document the list has to reach, so it refuses rather than reporting a scan that read nothing."
    return 1
  fi

  judge "$model_file" "$policy_file" "tree"
  judge_quotations "$model_file" "$policy_file" "tree"
  # Process substitution rather than a pipe: a function on the right of a pipe
  # runs in a subshell, and the fail it sets there is lost. That was written as a
  # pipe first and the leg printed its refusal while the check exited 0.
  judge_policy_names "tree" < <(git -C "$root" ls-files)
  return $fail
}

cmd_selftest() {
  # On every run rather than once at review time. The two documents agree today,
  # so the step below cannot fire against the tree, and a rule that had stopped
  # finding the section at all would go green forever while the file it is meant
  # to hold drifted underneath it. Each case is made to fire, made to name the
  # exact item that moved, and made to stay quiet once the item is put back.
  local cases=(
    'clean@'
    'item-missing-from-the-policy.trip@An operator with administrator rights can mint whatever the ceilings allow.'
    'item-reworded-in-the-policy.trip@An operator with administrator rights can mint whatever the ceilings allow.'
  )
  local entry name expected dir model_items policy_paragraphs missing

  for entry in "${cases[@]}"; do
    IFS='@' read -r name expected <<< "$entry"
    dir="${FIXTURES}/${name}"

    if [ ! -f "${dir}/threat-model.md" ] || [ ! -f "${dir}/${FIXTURE_POLICY}" ]; then
      echo "::error::${name}: missing a fixture. Every case owns ${dir}/threat-model.md and ${dir}/${FIXTURE_POLICY}."
      fail=1
      continue
    fi

    model_items=$(items_in_the_section "${dir}/threat-model.md")
    policy_paragraphs=$(paragraphs_in_the_section "${dir}/${FIXTURE_POLICY}")
    missing=$(comm -23 <(printf '%s\n' "$model_items" | sort) \
                       <(printf '%s\n' "$policy_paragraphs" | sort))

    case "$name" in
      clean)
        if [ -n "$missing" ]; then
          echo "::error::clean: the matching pair is refused. The check would red a pull request that had done nothing wrong."
          printf '%s\n' "$missing" | sed 's/^/  /'
          fail=1
        elif [ "$(printf '%s\n' "$model_items" | grep -c .)" -lt 2 ]; then
          echo "::error::clean: the fixture reads as having fewer than two items, so the pair matches for the wrong reason and proves nothing. This is the leg that says the opening paragraph is being dropped and the items are not."
          fail=1
        else
          echo "bites clean: $(printf '%s\n' "$model_items" | grep -c .) items read, none missing"
        fi
        ;;
      *)
        if [ "$missing" != "$expected" ]; then
          echo "::error::${name}: expected exactly one missing item and this wording."
          echo "  expected: ${expected}"
          echo "  got:      ${missing}"
          fail=1
        else
          echo "bites ${name}: the item that moved is named and no other is"
        fi
        ;;
    esac
  done
  return $fail
}

# The quotation leg's own cases, in the same shape and for the same reason: the
# two documents agree today, so the leg cannot fire against the tree, and one
# that had stopped finding a block quote at all would go green forever.
#
# Three trip cases rather than two, because this leg refuses in both directions
# and a case that fires both proves neither on its own. A quotation dropped from
# the policy fires the first direction alone, one carried only by the policy
# fires the second alone, and a reworded one fires both, which is what a reword
# is: the same sentence missing from one side and a new one present on the other.
cmd_selftest_quotations() {
  local cases=(
    'quotation-clean@@'
    'quotation-missing-from-the-policy.trip@Two servers from one data directory both honour the same live invitations, because neither knows the other exists.@'
    'quotation-only-in-the-policy.trip@@A restored backup revives spent invitations and undoes the revocations made since it was taken.'
    'quotation-reworded-in-the-policy.trip@Two servers from one data directory both honour the same live invitations, because neither knows the other exists.@Two servers from one data directory both honour the same valid invitations, because neither knows the other exists.'
  )
  local entry name want_dropped want_added dir model_quotes policy_quotes dropped added

  for entry in "${cases[@]}"; do
    IFS='@' read -r name want_dropped want_added <<< "$entry"
    dir="${FIXTURES}/${name}"

    if [ ! -f "${dir}/threat-model.md" ] || [ ! -f "${dir}/${FIXTURE_POLICY}" ]; then
      echo "::error::${name}: missing a fixture. Every case owns ${dir}/threat-model.md and ${dir}/${FIXTURE_POLICY}."
      fail=1
      continue
    fi

    model_quotes=$(quotations_in "${dir}/threat-model.md")
    policy_quotes=$(quotations_in "${dir}/${FIXTURE_POLICY}")
    dropped=$(comm -23 <(printf '%s
' "$model_quotes" | sort)                        <(printf '%s
' "$policy_quotes" | sort))
    added=$(comm -13 <(printf '%s
' "$model_quotes" | sort)                      <(printf '%s
' "$policy_quotes" | sort))

    if [ "$dropped" != "$want_dropped" ] || [ "$added" != "$want_added" ]; then
      echo "::error::${name}: this case did not move exactly the quotation it is about."
      echo "  expected dropped: ${want_dropped}"
      echo "  got dropped:      ${dropped}"
      echo "  expected added:   ${want_added}"
      echo "  got added:        ${added}"
      fail=1
      continue
    fi

    if [ "$name" = "quotation-clean" ]; then
      if [ "$(printf '%s
' "$model_quotes" | grep -c .)" -lt 2 ]; then
        echo "::error::quotation-clean: the fixture reads as carrying fewer than two quotations, so the pair matches for the wrong reason and proves nothing. This is the leg that says two quotes separated by prose are read as two subjects."
        fail=1
      else
        echo "bites quotation-clean: $(printf '%s
' "$model_quotes" | grep -c .) quotations read on each side, none moved in either direction"
      fi
    else
      echo "bites ${name}: the quotation that moved is named, in that direction and no other"
    fi
  done
  return $fail
}

# The third leg's own cases. It reads a path list rather than a pair of
# documents, so its fixtures are lists written here: a tree that is clean, three
# that shadow the policy in the three ways this has actually been reached, and
# the near misses that have to stay quiet. The near misses are the half worth
# spending the effort on - a rule that named docs/security-policy.md would be
# refusing an ordinary document for a resemblance.
cmd_selftest_policy_names() {
  local clean shadowed near_misses got want

  clean='SECURITY.md
docs/threat-model.md
.github/lint/fixtures/undefended-parity/clean/policy.md'

  # One per way this is reached: a second policy at a path the analysis names
  # itself, a fixture named after the real file, and a spelling that differs
  # from the policy only in case and extension.
  shadowed='.github/SECURITY.md
.github/lint/fixtures/undefended-parity/clean/SECURITY.md
docs/Security.RST'

  near_misses='docs/security-policy.md
SECURITY.md.bak
docs/security.txt
Jellyfin.Plugin.Invites/Security.cs'

  got=$(printf '%s\n' "$clean" | paths_read_as_the_policy)
  if [ -n "$got" ]; then
    echo "::error::policy-names clean: a tree with only the real policy is refused. The leg would red a pull request that had done nothing wrong."
    printf '%s\n' "$got" | sed 's/^/  /'
    fail=1
  else
    echo "bites policy-names clean: the policy itself is not read as a shadow of itself"
  fi

  want="$shadowed"
  got=$(printf '%s\n' "$shadowed" | paths_read_as_the_policy)
  if [ "$got" != "$want" ]; then
    echo "::error::policy-names trip: expected every shadowing path to be named and only those."
    echo "  expected: $(printf '%s' "$want" | tr '\n' ' ')"
    echo "  got:      $(printf '%s' "$got" | tr '\n' ' ')"
    fail=1
  else
    echo "bites policy-names trip: $(printf '%s\n' "$got" | grep -c .) shadowing path(s) named"
  fi

  got=$(printf '%s\n' "$near_misses" | paths_read_as_the_policy)
  if [ -n "$got" ]; then
    echo "::error::policy-names near-miss: a path an analysis does not read as the policy is named. A rule that refuses an ordinary document for its resemblance to the policy is worse than none."
    printf '%s\n' "$got" | sed 's/^/  /'
    fail=1
  else
    echo "bites policy-names near-miss: $(printf '%s\n' "$near_misses" | grep -c .) neighbouring path(s), none named"
  fi

  return $fail
}

case "${1:-}" in
  check)    cmd_check "${2:-.}" ;;
  selftest) cmd_selftest; cmd_selftest_quotations; cmd_selftest_policy_names ;;
  *)        echo "usage: $0 check [root] | $0 selftest" >&2; exit 2 ;;
esac
