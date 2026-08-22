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
# refused.
#
# One cell of a row is judged as well, and exactly one. The Default column
# states a fact the type also states, so a row disagreeing with the initialiser
# beside it is a red mark rather than an argument. Everything else a row says is
# prose: whether the sentence about what breaks is true is what the review is
# for, and a check pretending to decide it would turn a red mark into an
# argument.
#
# What the Default leg cannot see is stated where a reader meets it rather than
# only here. A setting whose declared type has no unambiguous language default
# and no initialiser is reported as not judged, by name, so a green run cannot
# be read as every default having been compared.
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

# Normalises a default so the two sides can be compared. The type writes a C#
# expression and the row writes what an operator reads, and the two spellings of
# an absent string are the case that forces this: string.Empty and Empty are one
# fact written twice.
#
# The table is deliberately tiny. A wide one would start deciding that "7 days"
# and "7" are the same value, and the moment a check does that the Default cell
# stops being a value and becomes prose again. So the rule this leg imposes is
# that the Default cell carries the value and nothing else, and a unit or a
# qualification belongs in Bounds.
normalise_default() {
  printf '%s' "$1" \
    | tr '[:upper:]' '[:lower:]' \
    | sed -e 's/`//g' \
          -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' \
          -e 's/\.$//' \
          -e 's/^string\.empty$/empty/' \
          -e 's/^""$/empty/'
}

# The default each setting has according to the type, one "<name><tab><value>"
# per line. A setting whose default this leg will not guess at is emitted with
# "?" and is reported rather than compared.
#
# Three sources, in order. An initialiser on the property is the value. With no
# initialiser, a bool is false and an integer is zero, which the language
# decides and no reader disputes. With no initialiser and any other declared
# type the value is null, and how a row should write that - unset, none, empty -
# is a writing decision this check has no business taking.
defaults_in_the_type() {
  local file="$1" out rc=0
  out=$(grep -hP '^\s*public\s+(?!static\b)[^(){}=]+\s+[A-Za-z_][A-Za-z0-9_]*\s*\{\s*get;\s*(set|init);' \
        -- "$file" 2>/dev/null) || rc=$?
  if [ "$rc" -ge 2 ]; then
    return 2
  fi
  local line name declared initialiser value
  printf '%s\n' "$out" | sed '/^$/d' | while IFS= read -r line; do
    name=$(printf '%s' "$line" | sed -E 's/^.*[[:space:]]([A-Za-z_][A-Za-z0-9_]*)[[:space:]]*\{.*$/\1/')
    declared=$(printf '%s' "$line" \
      | sed -E 's/^[[:space:]]*public[[:space:]]+//' \
      | sed -E 's/[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*\{.*$//' \
      | sed -E 's/^[[:space:]]*//; s/[[:space:]]*$//')
    if printf '%s' "$line" | grep -q '}[[:space:]]*='; then
      initialiser=$(printf '%s' "$line" | sed -E 's/^.*\}[[:space:]]*=[[:space:]]*//; s/[[:space:]]*;[[:space:]]*$//')
      value=$(normalise_default "$initialiser")
    else
      case "$declared" in
        bool)                value=false ;;
        int|long|short|byte) value=0 ;;
        *)                   value='?' ;;
      esac
    fi
    printf '%s\t%s\n' "$name" "$value"
  done
  return 0
}

# The default each row states, one "<name><tab><value>" per line. The Default
# column is found by its header rather than by counting to the third cell, so a
# column inserted before it does not silently move what this leg reads. The
# settings table is the one whose header's first cell is Setting, so a reference
# carrying a second table is not mistaken for it.
defaults_in_the_reference() {
  local file="$1" header column
  header=$(grep -m1 -P '^\|\s*Setting\s*\|' -- "$file" 2>/dev/null) || true
  if [ -z "$header" ]; then
    return 3
  fi
  column=$(printf '%s' "$header" | awk -F'|' '{
    for (i = 2; i <= NF; i++) {
      cell = $i
      gsub(/^[ \t]+|[ \t]+$/, "", cell)
      if (cell == "Default") { print i; exit }
    }
  }')
  if [ -z "$column" ]; then
    return 4
  fi
  local name value
  grep -hP '^\|\s*`[A-Za-z_][A-Za-z0-9_]*`\s*\|' -- "$file" 2>/dev/null \
    | awk -F'|' -v c="$column" '{
        name = $2
        gsub(/`/, "", name)
        gsub(/^[ \t]+|[ \t]+$/, "", name)
        value = (c <= NF) ? $c : ""
        printf "%s\t%s\n", name, value
      }' \
    | while IFS=$'\t' read -r name value; do
        printf '%s\t%s\n' "$name" "$(normalise_default "$value")"
      done
  return 0
}

# Compares the Default cell of every row against the type, for the settings both
# sides name. A setting missing from one side is the business of the two legs
# above and is not judged twice here.
#
# Prints one record per finding, so the selftest can compare a set of names the
# way the two older legs do rather than reading a formatted message:
#
#   mismatch<tab><name><tab><what the row says><tab><what the type says>
#   skipped<tab><name>
#
# Returns 2 if either scan broke, so a broken scan is never read as a set of
# defaults that all agree.
default_findings() {
  local type_file="$1" ref_file="$2"
  local type_defaults ref_defaults rc=0

  type_defaults=$(defaults_in_the_type "$type_file") || rc=$?
  if [ "$rc" -ge 2 ]; then
    return 2
  fi
  rc=0
  ref_defaults=$(defaults_in_the_reference "$ref_file") || rc=$?
  if [ "$rc" != "0" ]; then
    return "$rc"
  fi

  local name expected stated
  while IFS=$'\t' read -r name expected; do
    [ -z "$name" ] && continue
    stated=$(printf '%s\n' "$ref_defaults" | awk -F'\t' -v n="$name" '$1 == n { print $2; exit }')
    if [ -z "$stated" ]; then
      continue
    fi
    if [ "$expected" = "?" ]; then
      printf 'skipped\t%s\n' "$name"
      continue
    fi
    if [ "$stated" != "$expected" ]; then
      printf 'mismatch\t%s\t%s\t%s\n' "$name" "$stated" "$expected"
    fi
  done <<< "$type_defaults"
  return 0
}

# Reads the findings above and says what they mean. Sets nothing and prints
# nothing else, so the message wording can change without moving what the
# selftest compares.
judge_defaults() {
  local type_file="$1" ref_file="$2" label="$3"
  local findings rc=0 disagreed=0 skipped="" kind name stated expected

  findings=$(default_findings "$type_file" "$ref_file") || rc=$?
  case "$rc" in
    0) ;;
    3)
      echo "::error::${label}: ${ref_file} has no settings table, so no Default cell was read. Failing closed rather than passing on a scan that found nothing to compare."
      return 1
      ;;
    4)
      echo "::error::${label}: the settings table in ${ref_file} has no Default column. The column this leg reads is found by its header, so a renamed header is a reference this check would silently stop holding."
      return 1
      ;;
    *)
      echo "::error::${label}: the default scan of ${type_file} or ${ref_file} failed. Failing closed rather than reading it as a set of defaults that all agree."
      return 1
      ;;
  esac

  while IFS=$'\t' read -r kind name stated expected; do
    case "$kind" in
      mismatch)
        echo "::error::${label}: the Default cell for ${name} in ${ref_file} says '${stated}' and the type says '${expected}'. An operator reading a default the code does not have is the drift this file exists to refuse."
        disagreed=1
        ;;
      skipped)
        skipped="${skipped} ${name}"
        ;;
    esac
  done <<< "$findings"

  if [ -n "$skipped" ]; then
    echo "note  ${label}: the default is not judged for:${skipped}. Each has no initialiser and a declared type with no unambiguous language default, so this leg compared nothing for it."
  fi
  if [ "$disagreed" = "0" ]; then
    echo "ok    ${label}: every Default cell this leg reads agrees with the type"
    return 0
  fi
  return 1
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

  judge_defaults "$type_file" "$ref_file" "$label" || fail=1
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
  # Neither direction of this check could fire against the tree when it was
  # written, because the configuration type carried no settings. Without this the
  # job below would go green for as long as that stays true, and would keep going
  # green after a change that stopped the pattern matching a property at all.
  # Each direction is made to fire here, made to fire alone, and made to stay
  # quiet once the mismatch is taken out.
  #
  # That reason has not expired for the Default leg either. One setting exists
  # today, so a leg that stopped reading the Default column would go green
  # against the tree, and the fixture pair below is what refuses that.
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

  # The Default leg. It has its own cases because it fires on a pair the two
  # legs above are quiet on: every setting has a row and every row has a
  # setting, and one of the rows tells an operator a value the type does not
  # have. The clean pair is here too, because a leg that fires on a matching
  # pair would red every change and be turned off within a week.
  local default_cases=(
    'clean@'
    'default-disagrees.trip@MaximumUseCount'
    'default-disagrees-on-an-uninitialised-setting.trip@AllowRemoteAccess'
  )
  local findings got_mismatches drc
  for entry in "${default_cases[@]}"; do
    IFS='@' read -r name expected <<< "$entry"
    dir="${FIXTURES}/${name}"
    if [ ! -f "${dir}/PluginConfiguration.cs" ] || [ ! -f "${dir}/configuration.md" ]; then
      echo "::error::${name}: missing a fixture. Every case owns ${dir}/PluginConfiguration.cs and ${dir}/configuration.md."
      fail=1
      continue
    fi

    drc=0
    findings=$(default_findings "${dir}/PluginConfiguration.cs" "${dir}/configuration.md") || drc=$?
    if [ "$drc" != "0" ]; then
      echo "::error::${name}: the default scan failed on its own fixture (exit ${drc})."
      fail=1
      continue
    fi
    got_mismatches=$(printf '%s\n' "$findings" | awk -F'\t' '$1 == "mismatch" { print $2 }' | sort -u | tr '\n' ' ')
    got_mismatches=${got_mismatches% }

    if [ "$got_mismatches" != "$expected" ]; then
      echo "::error::${name}: expected the Default leg to fire on '${expected}', got '${got_mismatches}'."
      fail=1
      continue
    fi

    # A case that fires must also fire alone, and a case that is quiet must have
    # compared something. Both are the way a leg stops meaning anything: one by
    # naming the wrong setting, the other by comparing nothing and reporting it
    # as agreement.
    if [ -z "$expected" ]; then
      local judged
      judged=$(defaults_in_the_type "${dir}/PluginConfiguration.cs" | awk -F'\t' '$2 != "?" { print $1 }' | wc -l)
      if [ "$judged" -lt 1 ]; then
        echo "::error::${name}: no setting in the fixture has a default this leg judges, so the pair is quiet for the wrong reason and proves nothing."
        fail=1
      else
        echo "bites ${name}: ${judged} default(s) compared and none disagrees"
      fi
    else
      echo "bites ${name}: ${expected} is the only Default cell that disagrees with the type"
    fi
  done

  return $fail
}

case "${1:-}" in
  check)    cmd_check ;;
  selftest) cmd_selftest ;;
  *)        echo "usage: $0 check | $0 selftest" >&2; exit 2 ;;
esac
