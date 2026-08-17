#!/usr/bin/env bash
#
# The coverage floor, per area, decided in #108.
#
# One number over the whole assembly is a number people game by testing the easy
# parts, so there is a number per area and each one is set from what the area is
# worth rather than from what it currently measures. The numbers and the reason
# for each are in docs/coverage-floors.md, which is the file to argue with. This
# script is what refuses.
#
# Two modes:
#   check <root>   run the suite once per area and fail any area below its floor
#   selftest       fail unless the gate refuses an area whose filter matches
#                  nothing and an area whose floor is not met
#
# Fields are separated by @ because a filter contains dots and brackets.
set -uo pipefail

ASSEMBLY="Jellyfin.Plugin.Invites"
SOLUTION="Jellyfin.Plugin.Invites.sln"
COVERAGE_JSON="Jellyfin.Plugin.Invites.Tests/coverage.json"

# id @ namespace under the assembly @ floor as a line percentage @ directory
#
# An area with no directory of its own is written with the path its one file
# sits at. The order is the order the run reports them in and nothing depends
# on it.
AREAS=(
  'redemption@Redemption@95@Jellyfin.Plugin.Invites/Redemption'
  'codes@Codes@95@Jellyfin.Plugin.Invites/Codes'
  'invitations@Invitations@90@Jellyfin.Plugin.Invites/Invitations'
  'template@Accounts@90@Jellyfin.Plugin.Invites/Accounts'
  'store@Storage@80@Jellyfin.Plugin.Invites/Storage'
  'startup@Startup@85@Jellyfin.Plugin.Invites/Startup'
  'clock@Time@90@Jellyfin.Plugin.Invites/Time'
  'controllers@Controllers@70@Jellyfin.Plugin.Invites/Controllers'
  'setup@Setup@90@Jellyfin.Plugin.Invites/Setup'
)

# What is measured and given no floor, with the reason in
# docs/coverage-floors.md rather than here. Named so that a reader can see the
# exclusion was decided rather than achieved by the area quietly not being in
# the list above.
EXCLUDED=(
  'configuration@Jellyfin.Plugin.Invites/Configuration@The settings class and the page it is edited on'
)

fail=0

# One coverage run over one area. Prints the line percentage on standard output
# and returns non-zero where the run itself failed.
measure() {
  local namespace="$1"
  local output
  output=$(dotnet test "$SOLUTION" --nologo --configuration Release \
    -p:CollectCoverage=true \
    -p:CoverletOutputFormat=json \
    -p:Include="[${ASSEMBLY}]${ASSEMBLY}.${namespace}.*" 2>&1)
  local rc=$?
  if [ "$rc" -ne 0 ]; then
    printf '%s\n' "$output" >&2
    return 1
  fi
  # The module row rather than the total row: the total is an average over
  # modules and there is one module, but the module row is the one that names
  # what was measured.
  printf '%s\n' "$output" \
    | awk -F'|' -v m="$ASSEMBLY" '$2 ~ m {gsub(/[ %]/, "", $3); print $3; exit}'
}

# Whether the run actually reached any file in the area. Coverlet answers 100%
# for a filter that matches nothing, so a typed namespace that matches no
# document reports a perfect score for an area nobody measured. This is the
# near-miss this gate exists against: the mistake is one character in a
# namespace, and without this check it is invisible and green.
reached() {
  local directory="$1"
  [ -f "$COVERAGE_JSON" ] && grep -qF "$(basename "$directory")" "$COVERAGE_JSON"
}

# Whether the area has any source in it yet. An area with no code is reported as
# empty rather than as a floor that was met, because a floor over nothing is not
# a measurement and reading it as one is how an area ships uncovered.
has_source() {
  local directory="$1"
  [ -d "$directory" ] && [ -n "$(find "$directory" -maxdepth 1 -name '*.cs' -print -quit)" ]
}

judge_area() {
  local id="$1" namespace="$2" floor="$3" directory="$4"

  if ! has_source "$directory"; then
    echo "empty ${id} (floor ${floor}%): no source in ${directory} yet, so nothing was measured"
    return 0
  fi

  rm -f "$COVERAGE_JSON"

  local measured
  measured=$(measure "$namespace")
  if [ -z "$measured" ]; then
    echo "::error::${id}: the coverage run produced no figure for ${namespace}. Failing rather than reporting a floor that was never compared against."
    return 1
  fi

  if ! reached "$directory"; then
    echo "::error::${id}: the filter [${ASSEMBLY}]${ASSEMBLY}.${namespace}.* reached no file under ${directory}, and an unmatched filter reports 100%. Check the namespace."
    return 1
  fi

  # Integer comparison on the whole-percent part. A floor is a whole number and
  # an area at 89.9 has not met a floor of 90.
  local whole="${measured%%.*}"
  if [ "$whole" -lt "$floor" ]; then
    echo "::error::${id}: ${measured}% of lines under ${directory}, below the floor of ${floor}%. docs/coverage-floors.md says what this number is for."
    return 1
  fi

  echo "ok    ${id} (${measured}%, floor ${floor}%)"
  return 0
}

cmd_check() {
  local root="${1:-}"
  if [ -z "$root" ]; then
    echo "::error::coverage-floors: no root. Failing rather than reporting a check that read nothing." >&2
    return 1
  fi
  cd "$root" || return 1

  local entry id namespace floor directory
  for entry in "${AREAS[@]}"; do
    IFS='@' read -r id namespace floor directory <<< "$entry"
    judge_area "$id" "$namespace" "$floor" "$directory" || fail=1
  done

  for entry in "${EXCLUDED[@]}"; do
    IFS='@' read -r id directory _ <<< "$entry"
    echo "none  ${id}: no floor, by decision. See docs/coverage-floors.md."
  done

  return $fail
}

# The two ways this gate can be wrong while looking right, each proven rather
# than asserted. Both are run against the real suite, because a fixture for a
# coverage number would be a fixture of this script's arithmetic and not of the
# thing it reads.
cmd_selftest() {
  local root="${1:-.}"
  cd "$root" || return 1

  local rc

  # A namespace nothing is in. Coverlet answers 100%, so an area passing here
  # would be an area nobody measured reporting a perfect score.
  judge_area "probe-unmatched" "ThisNamespaceIsNotInTheAssembly" 10 "Jellyfin.Plugin.Invites/Redemption" >/dev/null 2>&1
  rc=$?
  if [ "$rc" -eq 0 ]; then
    echo "::error::selftest: an area whose filter matches nothing was reported as meeting its floor. The unmatched-filter check is not biting."
    return 1
  fi
  echo "bites unmatched filter: a namespace no file is in is refused rather than scored"

  # A floor nothing meets.
  judge_area "probe-floor" "Storage" 100 "Jellyfin.Plugin.Invites/Storage" >/dev/null 2>&1
  rc=$?
  if [ "$rc" -eq 0 ]; then
    echo "::error::selftest: an area was reported as meeting a floor of 100%. The comparison is not biting."
    return 1
  fi
  echo "bites floor: an area below its floor is refused"

  return 0
}

case "${1:-}" in
  check) shift; cmd_check "$@" ;;
  selftest) shift; cmd_selftest "$@" ;;
  *)
    echo "usage: $0 check <root> | $0 selftest [root]" >&2
    exit 2
    ;;
esac
