#!/usr/bin/env bash
# Greppable invariants for this plugin.
#
# The value here is not the tool. It is that an invariant nobody can grep for is
# an invariant that comes back: it gets written into a document, agreed to, and
# then reintroduced by somebody who never read the document. Each rule below is
# the machine-readable half of a rule some issue in this plan decides in prose,
# and each names that issue.
#
# Several of these cannot bite against the tree yet, because the code they are
# about does not exist. That is deliberate. A rule landed before the code is a
# rule the first version of that code has to pass, and the fixtures are what
# prove the rule works in the meantime.
#
# Two modes:
#   check <path>...   fail if any rule matches inside those paths
#   selftest          fail unless every rule matches its tripping fixture and
#                     matches nothing in its clean one
#
# Fields are separated by @ because every pattern contains an alternation.
set -uo pipefail

FIXTURES=".github/lint/fixtures"

# id @ issue that decided it @ what may never appear @ lines this rule exempts
RULES=(
  'weak-random@#49@\bnew\s+Random\s*\(|\bRandom\.Shared\b|\bSystem\.Random\b@'
  'secret-compared-with-equality@#29@(?i)\b\w*(secret|token|hash)\w*\s*[=!]=|(?i)\b\w*(secret|token|hash)\w*\.Equals\s*\(@'
  'secret-in-a-log-call@#32@(?i)\bLog(Information|Warning|Error|Debug|Trace|Critical)?\s*\([^)]*\b(invitationcode|password|secret|hashsecret)\b@'
  'policy-written-outside-the-template@#69@\.Policy\s*=[^=]|UpdateUserPolicy\s*\(|UpdatePolicy\s*\(@^[^:]*AccountTemplate[^:]*:'
  'link-built-from-a-request-header@#50@(?i)\brequest\.headers\s*\[|(?i)\brequest\.host\b|X-Forwarded-(Host|Proto)@'
)

# What each rule is about, printed when it fires, so the failure explains itself
# rather than only pointing at a line.
explain() {
  case "$1" in
    weak-random)
      echo "An invitation code from a non-cryptographic source is guessable. Use RandomNumberGenerator." ;;
    secret-compared-with-equality)
      echo "Comparing a stored secret with == leaks its prefix through timing. Use CryptographicOperations.FixedTimeEquals." ;;
    secret-in-a-log-call)
      echo "An invitation code, a password or the hash secret in a log line is that secret written to disk in clear." ;;
    policy-written-outside-the-template)
      echo "A user policy written anywhere but the routine that applies an account template is a grant nobody reviewed." ;;
    link-built-from-a-request-header)
      echo "A link built from what the request says the host is, is a link an attacker chooses." ;;
  esac
}

fail=0

# Prints the matches a rule has in the given files, after removing the lines the
# rule exempts. Returns 2 and prints nothing when the scanner itself broke, so a
# broken scan is never read as a clean tree.
DROP_FIXTURES=0

matches() {
  local pattern="$1" exempt="$2"; shift 2
  local out rc=0
  out=$(grep -rnHP --include='*.cs' --include='*.csproj' --include='*.html' \
        --exclude-dir=bin --exclude-dir=obj -- "$pattern" "$@" 2>/dev/null) || rc=$?
  if [ "$rc" -ge 2 ]; then
    return 2
  fi
  if [ -n "$exempt" ] && [ -n "$out" ]; then
    out=$(printf '%s\n' "$out" | grep -v -- "$exempt" || true)
  fi
  # The fixtures hold the violations on purpose, so check mode scans the whole
  # tree and drops them here. Scanning a named list of source directories
  # instead would let a directory added later escape every rule silently, which
  # is the failure this lint exists to be immune to. Selftest leaves them in,
  # because they are what it is looking at.
  if [ "$DROP_FIXTURES" = "1" ] && [ -n "$out" ]; then
    out=$(printf '%s\n' "$out" | grep -v "^\./${FIXTURES}/\|^${FIXTURES}/" || true)
  fi
  printf '%s' "$out" | sed '/^$/d'
  return 0
}

cmd_check() {
  local rule id issue pattern exempt out rc
  DROP_FIXTURES=1
  for rule in "${RULES[@]}"; do
    IFS='@' read -r id issue pattern exempt <<< "$rule"
    rc=0
    out=$(matches "$pattern" "$exempt" "$@") || rc=$?
    if [ "$rc" -ge 2 ]; then
      echo "::error::${id}: the scanner failed. Failing closed rather than reading a broken scan as a clean tree."
      fail=1
      continue
    fi
    if [ -n "$out" ]; then
      echo "::error::${id} (decided in ${issue}): $(explain "$id")"
      printf '%s\n' "$out"
      fail=1
    else
      echo "ok    ${id} (${issue})"
    fi
  done
  return $fail
}

cmd_selftest() {
  # A rule that has never been seen to fire proves nothing, and most of these
  # cannot fire against the tree yet because the code is not written. This is
  # where each one is made to fire, and made to stop firing when the violation
  # is taken out.
  local rule id issue pattern exempt trip clean trip_out clean_out rc
  for rule in "${RULES[@]}"; do
    IFS='@' read -r id issue pattern exempt <<< "$rule"
    trip="${FIXTURES}/${id}.trip.cs"
    clean="${FIXTURES}/${id}.clean.cs"
    if [ ! -f "$trip" ] || [ ! -f "$clean" ]; then
      echo "::error::${id}: missing a fixture. Every rule owns ${id}.trip.cs and ${id}.clean.cs."
      fail=1
      continue
    fi
    rc=0
    trip_out=$(matches "$pattern" "$exempt" "$trip") || rc=$?
    if [ "$rc" -ge 2 ]; then
      echo "::error::${id}: the scanner failed on its own tripping fixture."
      fail=1
      continue
    fi
    if [ -z "$trip_out" ]; then
      echo "::error::${id}: ${trip} does not trip the rule, so the rule cannot bite and proves nothing."
      fail=1
      continue
    fi
    rc=0
    clean_out=$(matches "$pattern" "$exempt" "$clean") || rc=$?
    if [ "$rc" -ge 2 ]; then
      echo "::error::${id}: the scanner failed on its own clean fixture."
      fail=1
      continue
    fi
    if [ -n "$clean_out" ]; then
      echo "::error::${id}: ${clean} still matches, so removing the violation does not make the rule pass."
      printf '%s\n' "$clean_out"
      fail=1
      continue
    fi
    echo "bites ${id} (${issue}): ${trip} trips it, ${clean} does not"
  done
  return $fail
}

case "${1:-}" in
  check)    shift; cmd_check "$@" ;;
  selftest) cmd_selftest ;;
  *)        echo "usage: $0 check <path>... | $0 selftest" >&2; exit 2 ;;
esac
