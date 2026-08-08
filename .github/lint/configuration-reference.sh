#!/usr/bin/env bash
# The configuration reference is held to the configuration type.
#
# A reference document for settings drifts in one direction. Somebody adds a
# property, ships it, and the row explaining what it does and what breaks when
# it is wrong is written later or never. The operator who hits that setting then
# reads a file that does not mention it, which is worse than no file, because a
# reference with a gap reads as a reference without one.
#
# This holds the two together in both directions. A property in the
# configuration type with no row is refused, and a row naming no property is
# refused. It does not judge what a row says: whether the sentence about what
# breaks is true is what the review is for, and a check pretending to decide it
# would turn a red mark into an argument.
#
# The rows are checked rather than generated on purpose. Four of the six columns
# are prose somebody has to write, and a generated file is one nobody reviews the
# prose of.
#
# Two modes:
#   check             find the configuration type and the reference in the
#                     tracked tree and judge them against each other
#   selftest          fail unless each direction fires on its own fixture, fires
#                     alone, and stays quiet on the clean pair
#
# The rule matches spellings rather than meanings, which is the same bound the
# invariant lint carries and it is worth stating in the same words. A property
# written across several lines, or produced by a source generator, is invisible
# here. The shapes the pattern does reach are the ones in the fixtures, and the
# selftest is what keeps it reaching them.
set -uo pipefail

FIXTURES=".github/lint/fixtures/configuration-reference"

# Where the two subjects live. Named by pattern rather than by full path so the
# check survives the rename off the template in #2, which moves the directories
# above these two files and neither of the file names themselves.
TYPE_GLOB='*/Configuration/PluginConfiguration.cs'
REFERENCE='docs/configuration.md'

fail=0

# The settings the type declares: a public instance property with a setter.
#
# A property with no setter is not a setting, because nothing can configure it,
# and the server deserialises the configuration file into the setters. `static`
# is excluded for the same reason: a static property is not part of the
# serialised configuration and would owe a row nobody could set.
#
# Returns 2 and prints nothing if the scan itself broke, so a broken scan is
# never read as a type with no settings.
settings_in_the_type() {
  local file="$1" out rc=0
  out=$(grep -hP '^\s*public\s+(?!static\b)[^(){}=]+\s+[A-Za-z_][A-Za-z0-9_]*\s*\{\s*get;\s*(set|init);' \
        -- "$file" 2>/dev/null) || rc=$?
  if [ "$rc" -ge 2 ]; then
    return 2
  fi
  printf '%s' "$out" \
    | sed -E 's/^.*[[:space:]]([A-Za-z_][A-Za-z0-9_]*)[[:space:]]*\{.*$/\1/' \
    | sed '/^$/d' | sort -u
  return 0
}

# The settings the reference has a row for: a table row whose first cell is a
# name in backticks. The header row and the separator row carry no backticks, so
# neither is mistaken for a setting, and prose mentioning a setting in backticks
# is not a row because it does not begin with a pipe.
settings_in_the_reference() {
  local file="$1" out rc=0
  out=$(grep -hP '^\|\s*`[A-Za-z_][A-Za-z0-9_]*`\s*\|' -- "$file" 2>/dev/null) || rc=$?
  if [ "$rc" -ge 2 ]; then
    return 2
  fi
  printf '%s' "$out" \
    | sed -E 's/^\|[[:space:]]*`([A-Za-z_][A-Za-z0-9_]*)`.*$/\1/' \
    | sed '/^$/d' | sort -u
  return 0
}

# Judges one pair and prints what it found. Sets fail on a difference in either
# direction. Prints the two directions separately, because they are different
# mistakes with different repairs: one is a setting somebody shipped without
# explaining it, the other is a row left behind by a setting that was removed or
# renamed.
judge() {
  local type_file="$1" ref_file="$2" label="$3"
  local in_type in_ref missing extra rc=0

  in_type=$(settings_in_the_type "$type_file") || rc=$?
  if [ "$rc" -ge 2 ]; then
    echo "::error::${label}: the scan of ${type_file} failed. Failing closed rather than reading it as a type with no settings."
    fail=1
    return 1
  fi
  rc=0
  in_ref=$(settings_in_the_reference "$ref_file") || rc=$?
  if [ "$rc" -ge 2 ]; then
    echo "::error::${label}: the scan of ${ref_file} failed. Failing closed rather than reading it as a reference with no rows."
    fail=1
    return 1
  fi

  missing=$(comm -23 <(printf '%s\n' "$in_type" | sed '/^$/d') \
                     <(printf '%s\n' "$in_ref" | sed '/^$/d'))
  extra=$(comm -13 <(printf '%s\n' "$in_type" | sed '/^$/d') \
                   <(printf '%s\n' "$in_ref" | sed '/^$/d'))

  local found=0
  if [ -n "$missing" ]; then
    echo "::error::${label}: a setting has no row in ${ref_file}. An operator who meets it reads a reference that does not mention it, which reads as a setting that does not exist."
    printf '  %s\n' $missing
    found=1
    fail=1
  fi
  if [ -n "$extra" ]; then
    echo "::error::${label}: a row in ${ref_file} names no setting in ${type_file}. A row for a setting nobody can set sends an operator looking for a field that is not there."
    printf '  %s\n' $extra
    found=1
    fail=1
  fi
  if [ "$found" = "0" ]; then
    echo "ok    ${label}: every setting has a row and every row has a setting"
  fi
  return 0
}

cmd_check() {
  local type_file count
  type_file=$(git ls-files -- "$TYPE_GLOB")
  count=$(printf '%s\n' "$type_file" | grep -c . || true)
  if [ -z "$type_file" ]; then
    echo "::error::No tracked file matches ${TYPE_GLOB}. The check cannot find the configuration type, so it refuses rather than passing on an empty scan."
    return 1
  fi
  if [ "$count" != "1" ]; then
    echo "::error::${count} tracked files match ${TYPE_GLOB}. Which one the server deserialises into is not a thing this check should guess."
    printf '  %s\n' $type_file
    return 1
  fi
  if [ ! -f "$REFERENCE" ]; then
    echo "::error::${REFERENCE} is missing. The reference is the thing this check is about; without it every setting is undocumented and the check would pass."
    return 1
  fi
  judge "$type_file" "$REFERENCE" "tree"
  return $fail
}

cmd_selftest() {
  # Neither direction of this check can fire against the tree today, because the
  # configuration type carries no settings. Without this the job below would go
  # green for as long as that stays true, and would keep going green after a
  # change that stopped the pattern matching a property at all. Each direction is
  # made to fire here, made to fire alone, and made to stay quiet once the
  # mismatch is taken out.
  local dir name expected got
  local cases=(
    'clean@'
    'setting-without-a-row.trip@MaximumUseCount'
    'row-without-a-setting.trip@PublicBaseAddress'
  )
  local entry
  for entry in "${cases[@]}"; do
    IFS='@' read -r name expected <<< "$entry"
    dir="${FIXTURES}/${name}"
    if [ ! -f "${dir}/PluginConfiguration.cs" ] || [ ! -f "${dir}/configuration.md" ]; then
      echo "::error::${name}: missing a fixture. Every case owns ${dir}/PluginConfiguration.cs and ${dir}/configuration.md."
      fail=1
      continue
    fi

    local in_type in_ref missing extra
    in_type=$(settings_in_the_type "${dir}/PluginConfiguration.cs") || {
      echo "::error::${name}: the scanner failed on its own fixture type."
      fail=1
      continue
    }
    in_ref=$(settings_in_the_reference "${dir}/configuration.md") || {
      echo "::error::${name}: the scanner failed on its own fixture reference."
      fail=1
      continue
    }
    missing=$(comm -23 <(printf '%s\n' "$in_type" | sed '/^$/d') \
                       <(printf '%s\n' "$in_ref" | sed '/^$/d') | tr '\n' ' ')
    extra=$(comm -13 <(printf '%s\n' "$in_type" | sed '/^$/d') \
                     <(printf '%s\n' "$in_ref" | sed '/^$/d') | tr '\n' ' ')
    missing=${missing% }
    extra=${extra% }

    case "$name" in
      clean)
        if [ -n "$missing" ] || [ -n "$extra" ]; then
          echo "::error::clean: the matching pair is refused (missing='${missing}' extra='${extra}'). The check would red a pull request that had done nothing wrong."
          fail=1
        elif [ -z "$in_type" ]; then
          echo "::error::clean: the fixture type reads as having no settings, so the pair matches for the wrong reason and proves nothing."
          fail=1
        else
          echo "bites clean: $(printf '%s' "$in_type" | tr '\n' ' ')- matched on both sides"
        fi
        ;;
      setting-without-a-row.trip)
        if [ "$missing" != "$expected" ]; then
          echo "::error::${name}: expected the missing row to be '${expected}', got '${missing}'."
          fail=1
        elif [ -n "$extra" ]; then
          echo "::error::${name}: also fired the other direction ('${extra}'), so a failure here does not say which mistake was made."
          fail=1
        else
          echo "bites ${name}: ${expected} is in the type and has no row"
        fi
        ;;
      row-without-a-setting.trip)
        if [ "$extra" != "$expected" ]; then
          echo "::error::${name}: expected the orphan row to be '${expected}', got '${extra}'."
          fail=1
        elif [ -n "$missing" ]; then
          echo "::error::${name}: also fired the other direction ('${missing}'), so a failure here does not say which mistake was made."
          fail=1
        else
          echo "bites ${name}: ${expected} has a row and is in no type"
        fi
        ;;
    esac
  done
  return $fail
}

case "${1:-}" in
  check)    cmd_check ;;
  selftest) cmd_selftest ;;
  *)        echo "usage: $0 check | $0 selftest" >&2; exit 2 ;;
esac
