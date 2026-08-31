#!/usr/bin/env bash
# The document a server fetches is read back against the releases that exist.
#
# A server installs this plugin by fetching a manifest, keeping the entries whose
# targetAbi it can take, and offering the highest version among them. So what an
# operator can install is decided by that document and never by what this
# repository built: a release that was published and never reached the manifest
# is a release nobody can install, and the publishing run that shipped it is
# green.
#
# THAT IS WHY THIS DOES NOT LIVE INSIDE THE PUBLISH. A check in the publishing
# run cannot catch a publishing run that half failed, because the step that
# failed is the step that would have had to report it. This one trusts neither
# end: it is handed the document a server would fetch and the releases that
# exist, and it compares them.
#
# WHAT IT COMPARES. This repository claims one server line, the one build.yaml
# names in targetAbi, which is what .github/workflows/publish.yaml says about
# itself in its own header and what .github/lint/manifest.sh generates an entry
# for. A release is a tag ending -stable; an -rcN tag is a dry run that publishes
# nothing and is not a release this compares against.
#
# IT TAKES ITS INPUTS AS FILES AND REACHES NO NETWORK. The fetch and the release
# listing are the workflow's, so every answer this script can give is one the
# selftest can hand it directly from a fixture. A reader nobody has watched
# saying no is a reader that might say yes to everything, and this one is green
# on the day it lands and stays green for as long as nothing goes wrong - which
# is exactly the state the failure it is written against arises in.
#
# IT READS build.yaml THROUGH .github/lint/manifest.sh field RATHER THAN WITH A
# READER OF ITS OWN. Two readers of one file drift, which is #394's subject on
# this board, and the entry this compares against is the one that generator
# writes, so the identity and the line it looks up are the identity and the line
# that would be published.
#
# WHAT IT DOES NOT DO, said here rather than left to be discovered.
#
#   It does not fetch. A timeout and a drifted manifest reach a checker as the
#   same empty answer, so the fetch is the workflow's step and its failure says
#   which of the two happened.
#
#   It does not hash an archive. Whether the bytes behind an entry are the bytes
#   the entry promises is the generator's rule, proven in
#   .github/lint/manifest.sh selftest.
#
#   It does not install anything. Whether a package a manifest offers loads on a
#   server is #123's manual check and is a person at a machine.
#
#   It compares the NEWEST version of the line and not every version an entry
#   carries. An older entry whose release was taken down is a divergence this
#   says nothing about, because it is not what a server installs.
#
# Two modes:
#   check <manifest-json> <releases-file> [<build.yaml>]
#                 judge the document against the releases and refuse a
#                 disagreement. The releases file holds one tag per line, in any
#                 order, which is the shape gh release list --json tagName
#                 --jq '.[].tagName' produces.
#   selftest      fail unless every refusal fires on its own fixture and alone,
#                 and unless both clean fixtures pass
set -uo pipefail

FIXTURES=".github/lint/fixtures/manifest-freshness"
READER=".github/lint/manifest.sh"

fail=0

refuse() {
  local id="$1" detail="$2"
  echo "::error::${id}: ${detail}" >&2
  fail=1
}

# The higher of two dotted versions, compared position by position over four
# positions so that 0.10.0.0 is above 0.9.0.0. sort -V is not used: it is a GNU
# extension and this has to give the same answer wherever it runs.
higher() {
  local a="$1" b="$2" i x y
  local -a as bs
  IFS=. read -r -a as <<< "$a"
  IFS=. read -r -a bs <<< "$b"
  for i in 0 1 2 3; do
    x="${as[i]:-0}"; y="${bs[i]:-0}"
    if [ "$x" -gt "$y" ]; then printf '%s' "$a"; return; fi
    if [ "$x" -lt "$y" ]; then printf '%s' "$b"; return; fi
  done
  printf '%s' "$a"
}

cmd_check() {
  local manifest="${1:-}" releases="${2:-}" metadata="${3:-build.yaml}"
  local guid abi entry offered released newest_offered newest_released tag version f

  fail=0

  if [ -z "$manifest" ] || [ -z "$releases" ]; then
    echo "usage: $0 check <manifest-json> <releases-file> [<build.yaml>]" >&2
    return 2
  fi

  for f in "$manifest" "$releases" "$metadata"; do
    if [ ! -f "$f" ]; then
      refuse input-missing "there is no ${f} to read. This judges what a fetch and a listing actually returned, never what they were expected to return."
      return 1
    fi
  done

  if ! jq -e 'type == "array"' "$manifest" >/dev/null 2>&1; then
    refuse manifest-not-an-array "${manifest} does not parse as a JSON array. A manifest a server cannot read is a manifest nobody can install from, and it arrives here as a fetch that returned an error page rather than as a network failure."
    return 1
  fi

  guid="$(bash "$READER" field guid "$metadata" 2>/dev/null)"
  if [ -z "$guid" ]; then
    refuse metadata-declares-no-guid "${metadata} carries no guid at column zero, so there is no identity to look an entry up by. A catalogue keys its entries on it and the display name is prose that may change."
    return 1
  fi

  abi="$(bash "$READER" field targetAbi "$metadata" 2>/dev/null)"
  if [ -z "$abi" ]; then
    refuse metadata-declares-no-abi "${metadata} carries no targetAbi at column zero, so the server line this repository claims cannot be told from any other and there is nothing to compare an entry's versions against."
    return 1
  fi

  # The newest release, out of the tags that ended a publish. An -rcN tag runs
  # the same gate and creates nothing, so it is not a release, and comparing
  # against one would report drift for a build that was never offered.
  newest_released=""
  released=""
  while read -r tag; do
    tag="${tag%$'\r'}"
    [ -n "$tag" ] || continue
    case "$tag" in
      *-stable) version="${tag%-stable}" ;;
      *)
        echo "note: ${tag} does not end in -stable, so it published nothing and is not compared."
        continue
        ;;
    esac
    if ! printf '%s' "$version" | grep -qE '^[0-9]+\.[0-9]+\.[0-9]+(\.[0-9]+)?$'; then
      refuse release-version-unreadable "the tag ${tag} ends in -stable and its numeric part ${version} is not three or four numeric parts. That is the version a server compares against what is installed, and a value nothing can read is not one to guess at."
      continue
    fi
    released="${released}${version} "
    if [ -z "$newest_released" ]; then
      newest_released="$version"
    else
      newest_released="$(higher "$newest_released" "$version")"
    fi
  done < "$releases"

  entry="$(jq --arg guid "$guid" '[.[] | select((.guid // "") | ascii_downcase == ($guid | ascii_downcase))] | first' "$manifest" | tr -d '\r')"

  if [ "$entry" = "null" ] || [ -z "$entry" ]; then
    if [ -n "$newest_released" ]; then
      refuse entry-absent-for-a-release "the newest release is ${newest_released} and the manifest carries no entry for ${guid} at all. A server that added this address is offered nothing, which is what a publish that created the release and never reached the manifest leaves behind."
      return $fail
    fi
    echo "ok    no -stable release exists and the manifest carries no entry for ${guid}. Nothing is published and nothing is offered, so there is nothing here to disagree."
    return $fail
  fi

  offered="$(printf '%s' "$entry" | jq -r --arg abi "$abi" '[.versions[]? | select(.targetAbi == $abi) | .version] | .[]' | tr -d '\r' | tr '\n' ' ')"

  newest_offered=""
  for version in $offered; do
    if [ -z "$newest_offered" ]; then
      newest_offered="$version"
    else
      newest_offered="$(higher "$newest_offered" "$version")"
    fi
  done

  if [ -z "$newest_released" ]; then
    if [ -n "$newest_offered" ]; then
      refuse offered-with-nothing-released "the manifest offers ${offered}at targetAbi ${abi} and no -stable release exists. A server would be handed a download for something this repository never published."
      return $fail
    fi
    echo "ok    no -stable release exists and the entry for ${guid} offers nothing at targetAbi ${abi}. Nothing is published and nothing is offered."
    return $fail
  fi

  if [ -z "$newest_offered" ]; then
    refuse line-absent-for-a-release "the newest release is ${newest_released} and the entry for ${guid} carries no version at targetAbi ${abi}. The entry exists and the line this repository packages for is not in it, so a server on that line is offered nothing."
    return $fail
  fi

  if [ "$newest_offered" != "$newest_released" ]; then
    if [ "$(higher "$newest_offered" "$newest_released")" = "$newest_released" ]; then
      refuse manifest-behind-the-newest-release "the newest release is ${newest_released} and the newest the manifest offers at targetAbi ${abi} is ${newest_offered}. A server takes ${newest_offered} and has no way to learn that ${newest_released} exists."
    else
      refuse manifest-ahead-of-every-release "the manifest offers ${newest_offered} at targetAbi ${abi} and the newest release is ${newest_released}. A server takes ${newest_offered} and downloads it from an address no release of this repository published."
    fi
    return $fail
  fi

  echo "ok    the newest release is ${newest_released} and the manifest offers ${offered}at targetAbi ${abi}, newest ${newest_offered}. They agree."
  return $fail
}

# Every case names the refusal it is supposed to reach. A case that fires
# something else, or fires nothing, proves that the check refuses rather than
# proving what it refuses.
#
#   id @ fixture directory
SELFTEST_CASES=(
  'manifest-not-an-array@manifest-not-an-array.trip'
  'metadata-declares-no-guid@metadata-declares-no-guid.trip'
  'metadata-declares-no-abi@metadata-declares-no-abi.trip'
  'release-version-unreadable@release-version-unreadable.trip'
  'entry-absent-for-a-release@entry-absent-for-a-release.trip'
  'line-absent-for-a-release@line-absent-for-a-release.trip'
  'manifest-behind-the-newest-release@manifest-behind-the-newest-release.trip'
  'manifest-ahead-of-every-release@manifest-ahead-of-every-release.trip'
  'offered-with-nothing-released@offered-with-nothing-released.trip'
)

# The clean cases, and there are two because this repository is in the first of
# them today. A check that only ever saw the state it was written for would not
# say whether it can read the state it exists to judge.
SELFTEST_CLEAN=(
  'clean-nothing-published'
  'clean-agrees'
)

cmd_selftest() {
  local overall=0 case_line id dir out rc others

  if [ ! -d "$FIXTURES" ]; then
    echo "::error::${FIXTURES} does not exist, so this selftest read nothing. Failing rather than reporting a check that looked at nothing." >&2
    return 1
  fi

  for case_line in "${SELFTEST_CASES[@]}"; do
    IFS='@' read -r id dir <<< "$case_line"

    out="$(cmd_check "${FIXTURES}/${dir}/manifest.json" "${FIXTURES}/${dir}/releases.txt" "${FIXTURES}/${dir}/build.yaml" 2>&1 >/dev/null)"
    rc=$?

    if [ "$rc" -eq 0 ]; then
      echo "::error::${id}: the check accepted its own tripping fixture. Do not read the rest of this run as a manifest that was judged." >&2
      overall=1
      continue
    fi

    if ! printf '%s' "$out" | grep -q "::error::${id}:"; then
      echo "::error::${id}: the fixture was refused and not by this rule. What fired: $(printf '%s' "$out" | sed -n 's/^::error::\([a-z-]*\):.*/\1/p' | sort -u | tr '\n' ' ')" >&2
      overall=1
      continue
    fi

    # Fires alone. A fixture that trips two rules proves neither, because taking
    # the rule under test away leaves the run just as red.
    others="$(printf '%s' "$out" | sed -n 's/^::error::\([a-z-]*\):.*/\1/p' | sort -u | grep -v "^${id}$" | tr '\n' ' ')"
    if [ -n "$others" ]; then
      echo "::error::${id}: the fixture also trips ${others}, so it does not prove this rule." >&2
      overall=1
      continue
    fi

    echo "bites ${id}: ${dir} is refused by ${id} and by nothing else"
  done

  # The one refusal with no fixture directory of its own, because what it is
  # about is a file that is not there: a fixture holding it would be a directory
  # whose absence is the fixture.
  out="$(cmd_check "${FIXTURES}/clean-agrees/manifest.json" "${FIXTURES}/clean-agrees/absent-releases.txt" "${FIXTURES}/clean-agrees/build.yaml" 2>&1 >/dev/null)"
  rc=$?
  if [ "$rc" -eq 0 ] || ! printf '%s' "$out" | grep -q '::error::input-missing:'; then
    echo "::error::input-missing: a releases file that does not exist was not refused by input-missing. What fired: $(printf '%s' "$out" | sed -n 's/^::error::\([a-z-]*\):.*/\1/p' | sort -u | tr '\n' ' ')" >&2
    overall=1
  else
    echo "bites input-missing: a releases file that is not there is refused rather than read as no releases"
  fi

  for dir in "${SELFTEST_CLEAN[@]}"; do
    out="$(cmd_check "${FIXTURES}/${dir}/manifest.json" "${FIXTURES}/${dir}/releases.txt" "${FIXTURES}/${dir}/build.yaml" 2>&1)"
    rc=$?
    if [ "$rc" -ne 0 ]; then
      echo "::error::the clean fixture ${dir} is refused (exit ${rc}). The check would report drift where there is none." >&2
      printf '%s\n' "$out" >&2
      overall=1
    else
      echo "ok    ${dir}: ${out}"
    fi
  done

  if [ "$overall" -eq 0 ]; then
    echo "ok    every refusal fires on its own fixture and alone, and both clean fixtures pass"
  fi
  return $overall
}

case "${1:-}" in
  check)    shift; cmd_check "$@"; exit $? ;;
  selftest) cmd_selftest ;;
  *)        echo "usage: $0 check <manifest-json> <releases-file> [<build.yaml>] | $0 selftest" >&2; exit 2 ;;
esac
