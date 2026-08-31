#!/usr/bin/env bash
# The manifest entry is generated from build.yaml and from the archive that was
# published, never written by hand.
#
# A Jellyfin server installs a plugin by reading a manifest: an entry per plugin,
# a version per server line, and on each version a source address and a checksum.
# The checksum is the field that decides what gets installed. A manifest whose
# checksum belongs to some other build is not a manifest with a typo in it; it is
# an install route that either refuses every download or hands a server bytes
# nobody reviewed.
#
# So the checksum here is read off the file, in the run that published it, and
# every other field is read out of build.yaml. Nothing in this script accepts a
# checksum, a version or a name as an argument, because a field that can be
# passed in is a field that can be passed in wrong.
#
# What it takes from outside is the three things build.yaml cannot know: which
# file was published, the address it was published at, and when. Each of those is
# refused unless it has the shape it is supposed to have, which is the whole of
# what a text check can do about them.
#
# Three modes:
#   generate <archive> <source-url> <timestamp> [<build.yaml>]
#                     write the manifest to stdout, or refuse and say why
#   field <key> [<build.yaml>]
#                     write one value to stdout, judging nothing. This is the
#                     reader on its own, so that a second caller reads the file
#                     the same way this one does instead of writing a reader of
#                     its own, and so that the two can be compared by hand while
#                     there are still two. It is #394's half of this file.
#   selftest          fail unless every refusal fires on its own fixture, fires
#                     alone, and stays quiet on the clean one, and unless the
#                     checksum moves when the archive does
#
# WHAT THIS DOES NOT DO, said here rather than left to be discovered.
#
# It does not publish anything. Where the file it writes is served from is not
# this repository's: the hosting half of decision 11 in #11 puts the catalogue on
# the hub rather than here, and #119 records that reading. What this script
# produces is the entry that repository takes, and the route it travels is not
# built in this tree.
#
# It does not read the metadata file the packaging tool writes beside the
# archive. That file is a release asset and nothing in this repository reads it,
# so its contents are not a shape this tree has measured, and a generator resting
# on them would be resting on one nobody here has seen. Everything below comes
# from build.yaml, which is tracked and reviewed, and from the archive, which is
# bytes.
#
# It does not compare against a published manifest. That is #119's fourth clause,
# it fetches from the hub, and a network call is a different subject with a
# different failure: a timeout and a drifted manifest reach a checker as the same
# empty answer.
#
# One refusal below has no fixture and it is named rather than left to be
# counted: checksum-unreadable fires when md5sum answers something that is not
# thirty-two hexadecimal characters, and nothing portable makes md5sum do that on
# a file this selftest can create. It is a guard against a failed read becoming
# an empty field, it has never been seen to fire, and a green selftest says
# nothing about it.
#
# Fields are read at column zero only, which is where build.yaml carries all of
# them. A key nested under another is invisible here, and so is one written with
# leading whitespace. That is a spelling bound of the same kind every check in
# this directory has, and the fixtures are what keep the reader reaching the
# spellings it claims.
#
# The reader below is this script's own, and .github/workflows/publish.yaml
# carries a second one inline that reads the same file for the release gate. Two
# readers of one file drift; holding them together is #394.
set -uo pipefail

FIXTURES=".github/lint/fixtures/manifest"

# The keys an entry is made of. The first list is read as ordinary scalars and
# the second as block scalars, and both are required: a manifest missing one of
# them is an entry a catalogue renders with a hole in it, which is the failure
# this generator exists to make impossible rather than unlikely.
SCALAR_KEYS=(name guid version targetAbi owner category overview)
BLOCK_KEYS=(description changelog)

# Read one value out of a YAML file at column zero. A quoted scalar ends at its
# closing quote; an unquoted one ends at a comment or at the end of the line; a
# block scalar introduced by | or > is kept or folded according to which of the
# two it is. Trailing whitespace and a CR from a CRLF checkout are removed before
# any of those rules is applied, because otherwise the closing quote is no longer
# the last character on the line and stays inside the value.
read_value() {
  local key="$1" file="$2"
  awk -v key="$key" -v quote="'" '
    function flush_pending(   i) {
      for (i = 0; i < pending; i++) { lines[++count] = "" }
      pending = 0
    }
    BEGIN { found = 0; block = 0; indent = -1; count = 0; pending = 0; done = 0 }
    found == 0 && index($0, key ":") == 1 {
      rest = substr($0, length(key) + 2)
      sub(/\r$/, "", rest)
      sub(/^[[:space:]]+/, "", rest)
      sub(/[[:space:]]+$/, "", rest)
      found = 1
      if (rest ~ /^[|>][-+]?[0-9]*$/) {
        block = (substr(rest, 1, 1) == "|") ? 1 : 2
        next
      }
      if (substr(rest, 1, 1) == "\"") {
        rest = substr(rest, 2)
        end = index(rest, "\"")
        if (end > 0) { rest = substr(rest, 1, end - 1) }
      } else if (substr(rest, 1, 1) == quote) {
        rest = substr(rest, 2)
        end = index(rest, quote)
        if (end > 0) { rest = substr(rest, 1, end - 1) }
      } else {
        sub(/[[:space:]]*#.*$/, "", rest)
        sub(/[[:space:]]+$/, "", rest)
      }
      printf "%s", rest
      done = 1
      exit
    }
    block > 0 {
      line = $0
      sub(/\r$/, "", line)
      if (line ~ /^[[:space:]]*$/) { pending++; next }
      # A line that starts at column zero is the next key, so the block ended.
      if (line !~ /^[[:space:]]/) { exit }
      if (indent < 0) { match(line, /^[[:space:]]+/); indent = RLENGTH }
      flush_pending()
      lines[++count] = substr(line, indent + 1)
      next
    }
    END {
      if (done) { exit }
      if (block == 0) { exit }
      out = ""
      for (i = 1; i <= count; i++) {
        if (i == 1) { out = lines[i]; continue }
        if (block == 1) { out = out "\n" lines[i]; continue }
        # Folded. A blank line is a newline and every other join is a space,
        # which turns a wrapped paragraph back into one line and keeps two
        # paragraphs apart.
        if (lines[i] == "" || lines[i - 1] == "") { out = out "\n" lines[i] }
        else { out = out " " lines[i] }
      }
      # A block that ended on a blank line carries a trailing newline that says
      # nothing, and a JSON string ending in one reads as a formatting slip in
      # every renderer that shows it.
      sub(/\n+$/, "", out)
      printf "%s", out
    }
  ' "$file"
}

# A JSON string, escaped. Done in the shell rather than in a second awk pass
# because the shell already holds the value, and handing it through another
# parser to escape it is one more place for it to change on the way.
#
# The five characters handled below are the ones a description and a changelog
# actually contain. Any other control character is refused rather than escaped,
# by the caller: one in build.yaml is a defect in build.yaml, and a generator
# that quietly encoded it would carry it into a catalogue entry.
json_string() {
  local s="$1"
  s=${s//\\/\\\\}
  s=${s//\"/\\\"}
  s=${s//$'\n'/\\n}
  s=${s//$'\r'/\\r}
  s=${s//$'\t'/\\t}
  printf '"%s"' "$s"
}

fail=0

refuse() {
  local id="$1" detail="$2"
  echo "::error::${id}: ${detail}" >&2
  fail=1
}

# Judges the inputs and writes the manifest. Every refusal is reached before a
# byte of the entry is written, so a refused run leaves a message and no
# half-written file for a later step to pick up.
cmd_generate() {
  local archive="${1:-}" source_url="${2:-}" timestamp="${3:-}" file="${4:-build.yaml}"

  fail=0

  if [ -z "$archive" ] || [ -z "$source_url" ] || [ -z "$timestamp" ]; then
    echo "usage: $0 generate <archive> <source-url> <timestamp> [<build.yaml>]" >&2
    return 2
  fi

  if [ ! -f "$file" ]; then
    refuse metadata-missing "there is no ${file} to read the entry out of. Every field except the checksum, the address and the time comes from it."
    return 1
  fi

  # The archive first, because it is the input every other field is published
  # alongside. A run against a file that is not there would take the checksum of
  # nothing, and md5sum's own failure is a line on stderr that a pipeline turns
  # into an empty field without complaining.
  if [ ! -f "$archive" ]; then
    refuse archive-missing "the archive '${archive}' does not exist, so there is nothing to take a checksum of. The entry describes a file that was published; if the packaging step produced none, that is the failure rather than this."
    return 1
  fi

  local checksum
  checksum="$(md5sum -- "$archive" | awk '{ print $1 }')"
  if [[ ! "$checksum" =~ ^[0-9a-f]{32}$ ]]; then
    refuse checksum-unreadable "reading a checksum of '${archive}' produced '${checksum}', which is not thirty-two hexadecimal characters. Refusing rather than writing whatever that was into the field a server installs on."
    return 1
  fi

  # An address a server fetches over a route it cannot authenticate is an install
  # route somebody on the path chooses the bytes for. The checksum bounds what
  # that costs and does not remove it, because the entry travels the same way.
  if [[ ! "$source_url" =~ ^https://[^[:space:]]+$ ]]; then
    refuse source-url-not-https "the source address '${source_url}' is not an absolute https address. A server downloads the archive from it."
  fi

  # One spelling of an instant, so two entries generated by two runs sort against
  # each other. A local time with no offset is the spelling that reads correctly
  # and orders wrongly.
  if [[ ! "$timestamp" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$ ]]; then
    refuse timestamp-not-utc "the timestamp '${timestamp}' is not YYYY-MM-DDTHH:MM:SSZ. Produce one with: date -u +%Y-%m-%dT%H:%M:%SZ"
  fi

  local key value
  declare -A values=()
  for key in "${SCALAR_KEYS[@]}" "${BLOCK_KEYS[@]}"; do
    value="$(read_value "$key" "$file")"
    if [ -z "$value" ]; then
      refuse field-absent "${file} carries no '${key}' at column zero, or carries it empty. An entry built from this would be missing a field a reader looks for."
    fi
    if printf '%s' "$value" | LC_ALL=C grep -qP '[\x00-\x08\x0b\x0c\x0e-\x1f]' 2>/dev/null; then
      refuse control-character-in-a-field "'${key}' in ${file} holds a control character other than a tab, a carriage return or a newline. That is a defect in the file rather than something to encode into a catalogue entry."
    fi
    values["$key"]="$value"
  done

  # imageUrl is the entry's one optional field. About half the entries in the
  # published catalogue carry one, so its absence is a tile without a picture
  # rather than an entry with a hole in it.
  local image_url
  image_url="$(read_value imageUrl "$file")"

  if [[ ! "${values[version]:-}" =~ ^[0-9]+\.[0-9]+\.[0-9]+(\.[0-9]+)?$ ]]; then
    refuse version-not-numeric "build.yaml version '${values[version]:-}' is not three or four numeric parts. The server compares it against what is installed."
  fi

  if [[ ! "${values[targetAbi]:-}" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    refuse abi-not-four-parts "build.yaml targetAbi '${values[targetAbi]:-}' is not four numeric parts. Jellyfin reads this field to decide whether to offer the plugin at all, and a value it cannot read is a plugin nobody is offered."
  fi

  if [[ ! "${values[guid]:-}" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$ ]]; then
    refuse guid-not-a-uuid "build.yaml guid '${values[guid]:-}' is not a UUID. It is this plugin's permanent identity and a catalogue keys its entries on it."
  fi

  if [ "$fail" -ne 0 ]; then
    return 1
  fi

  # One entry, one version. This repository publishes one server line, which is
  # what build.yaml's framework and targetAbi name and what publish.yaml refuses
  # to guess about. An entry carrying two versions belongs to a repository that
  # ships two, and this is not one.
  printf '[\n  {\n'
  printf '    "guid": %s,\n' "$(json_string "${values[guid]}")"
  printf '    "name": %s,\n' "$(json_string "${values[name]}")"
  printf '    "description": %s,\n' "$(json_string "${values[description]}")"
  printf '    "overview": %s,\n' "$(json_string "${values[overview]}")"
  printf '    "owner": %s,\n' "$(json_string "${values[owner]}")"
  printf '    "category": %s,\n' "$(json_string "${values[category]}")"
  if [ -n "$image_url" ]; then
    printf '    "imageUrl": %s,\n' "$(json_string "$image_url")"
  fi
  printf '    "versions": [\n      {\n'
  printf '        "version": %s,\n' "$(json_string "${values[version]}")"
  printf '        "changelog": %s,\n' "$(json_string "${values[changelog]}")"
  printf '        "targetAbi": %s,\n' "$(json_string "${values[targetAbi]}")"
  printf '        "sourceUrl": %s,\n' "$(json_string "$source_url")"
  printf '        "checksum": %s,\n' "$(json_string "$checksum")"
  printf '        "timestamp": %s\n' "$(json_string "$timestamp")"
  printf '      }\n    ]\n  }\n]\n'

  return 0
}

# Every case names the refusal it is supposed to reach. A case that fires
# something else, or fires nothing, proves that the generator refuses rather
# than proving what it refuses.
#
#   id @ build.yaml fixture @ source url @ timestamp @ archive
SELFTEST_CASES=(
  'field-absent@field-absent.trip.build.yaml@https://example.invalid/a.zip@2026-08-31T00:00:00Z@clean.zip'
  'version-not-numeric@version-not-numeric.trip.build.yaml@https://example.invalid/a.zip@2026-08-31T00:00:00Z@clean.zip'
  'guid-not-a-uuid@guid-not-a-uuid.trip.build.yaml@https://example.invalid/a.zip@2026-08-31T00:00:00Z@clean.zip'
  'abi-not-four-parts@abi-not-four-parts.trip.build.yaml@https://example.invalid/a.zip@2026-08-31T00:00:00Z@clean.zip'
  'source-url-not-https@clean.build.yaml@http://example.invalid/a.zip@2026-08-31T00:00:00Z@clean.zip'
  'timestamp-not-utc@clean.build.yaml@https://example.invalid/a.zip@2026-08-31 00:00:00@clean.zip'
  'archive-missing@clean.build.yaml@https://example.invalid/a.zip@2026-08-31T00:00:00Z@absent.zip'
  'metadata-missing@absent.build.yaml@https://example.invalid/a.zip@2026-08-31T00:00:00Z@clean.zip'
)

# The fields a catalogue entry carries. Held here so that a field dropped from
# the writer above turns the selftest red rather than producing a shorter entry
# nobody compared against anything.
ENTRY_FIELDS=(guid name description overview owner category imageUrl versions version changelog targetAbi sourceUrl checksum timestamp)

cmd_selftest() {
  local overall=0 case_line id metadata url stamp archive out rc others

  if [ ! -d "$FIXTURES" ]; then
    echo "::error::${FIXTURES} does not exist, so this selftest read nothing. Failing rather than reporting a check that looked at nothing." >&2
    return 1
  fi

  for case_line in "${SELFTEST_CASES[@]}"; do
    IFS='@' read -r id metadata url stamp archive <<< "$case_line"

    out="$(cmd_generate "${FIXTURES}/${archive}" "$url" "$stamp" "${FIXTURES}/${metadata}" 2>&1 >/dev/null)"
    rc=$?

    if [ "$rc" -eq 0 ]; then
      echo "::error::${id}: the generator accepted its own tripping fixture. Do not read the rest of this run as a manifest that was judged." >&2
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

    echo "bites ${id}: ${metadata} with that address, time and archive is refused by ${id} and by nothing else"
  done

  # The clean pair. A run of refusals that never sees an accepted input is a
  # generator that could be refusing everything.
  out="$(cmd_generate "${FIXTURES}/clean.zip" "https://example.invalid/a.zip" "2026-08-31T00:00:00Z" "${FIXTURES}/clean.build.yaml" 2>&1)"
  rc=$?
  if [ "$rc" -ne 0 ]; then
    echo "::error::the clean fixture is refused (exit ${rc}). The generator would refuse an honest release." >&2
    printf '%s\n' "$out" >&2
    overall=1
  else
    local missing="" field
    for field in "${ENTRY_FIELDS[@]}"; do
      printf '%s' "$out" | grep -q "\"${field}\":" || missing="${missing} ${field}"
    done
    if [ -n "$missing" ]; then
      echo "::error::the clean fixture generated an entry with no${missing}. A catalogue reads those fields." >&2
      overall=1
    else
      echo "ok    the clean fixture generates an entry carrying every field a catalogue entry has"
    fi
  fi

  # The checksum comes from the archive. This is the property the whole generator
  # exists for, and a run that never changed the bytes could not tell a checksum
  # read off the file from a constant somebody typed.
  local first second copy
  first="$(cmd_generate "${FIXTURES}/clean.zip" "https://example.invalid/a.zip" "2026-08-31T00:00:00Z" "${FIXTURES}/clean.build.yaml" 2>/dev/null | sed -n 's/.*"checksum": "\([0-9a-f]*\)".*/\1/p')"
  copy="$(mktemp -t manifest-selftest-XXXXXX)"
  cat -- "${FIXTURES}/clean.zip" > "$copy"
  printf 'x' >> "$copy"
  second="$(cmd_generate "$copy" "https://example.invalid/a.zip" "2026-08-31T00:00:00Z" "${FIXTURES}/clean.build.yaml" 2>/dev/null | sed -n 's/.*"checksum": "\([0-9a-f]*\)".*/\1/p')"
  rm -f -- "$copy"

  if [ -z "$first" ] || [ -z "$second" ]; then
    echo "::error::the checksum leg read no checksum out of one of its two runs, so it compared nothing." >&2
    overall=1
  elif [ "$first" = "$second" ]; then
    echo "::error::one byte was added to the archive and the checksum did not move. The field does not come from the file." >&2
    overall=1
  else
    echo "bites checksum-follows-the-archive: ${first} for the fixture, ${second} with one byte added to it"
  fi

  if [ "$overall" -eq 0 ]; then
    echo "ok    every refusal fires on its own fixture and alone, the clean pair generates, and the checksum follows the archive"
  fi
  return $overall
}

# The reader on its own. It judges nothing, because the caller that wants one
# value out of build.yaml is not always the caller that wants an entry, and a
# reader that refused would make the two callers disagree about what a readable
# file is. An absent key is empty output and exit 1, which is how the caller
# tells it from a key that is there and empty.
cmd_field() {
  local key="${1:-}" file="${2:-build.yaml}" value
  if [ -z "$key" ]; then
    echo "usage: $0 field <key> [<build.yaml>]" >&2
    return 2
  fi
  if [ ! -f "$file" ]; then
    echo "::error::metadata-missing: there is no ${file} to read '${key}' out of." >&2
    return 1
  fi
  value="$(read_value "$key" "$file")"
  printf '%s\n' "$value"
  [ -n "$value" ]
}

case "${1:-}" in
  generate) shift; cmd_generate "$@" ;;
  field)    shift; cmd_field "$@" ;;
  selftest) cmd_selftest ;;
  *)        echo "usage: $0 generate <archive> <source-url> <timestamp> [<build.yaml>] | $0 field <key> [<build.yaml>] | $0 selftest" >&2; exit 2 ;;
esac
