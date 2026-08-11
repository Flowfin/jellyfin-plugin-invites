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
  'secret-compared-by-sequence@#29@(?i)^(?=.*\b\w*(secret|token|hash)\w*\b).*\.SequenceEqual\s*\(@'
  'secret-in-a-log-call@#32@(?i)\bLog(Information|Warning|Error|Debug|Trace|Critical)?\s*\([^)]*\b(invitationcode|password|secret|hashsecret)\b@'
  'policy-written-outside-the-template@#69@\.Policy\s*=[^=]|UpdateUserPolicy\s*\(|UpdatePolicy\s*\(@^[^:]*AccountTemplate[^:]*:'
  'policy-field-written-outside-the-template@#69@\.Policy\s*\.\s*\w+\s*=[^=]@^[^:]*AccountTemplate[^:]*:'
  'link-built-from-a-request-header@#50@(?i)\brequest\.headers\s*\[|(?i)\brequest\.host\b|X-Forwarded-(Host|Proto)@'
  'link-built-from-the-request-url-helper@#50@\bGetSmartApiUrl\s*\(@'
  'clock-read-outside-the-seam@#41@\bDateTime\.(UtcNow|Now|Today)\b|\bDateTimeOffset\.(UtcNow|Now)\b|\bEnvironment\.TickCount(64)?\b|\bStopwatch\.GetTimestamp\s*\(|\bTimeProvider\.System\b@^[^:]*/SystemClock\.cs:'
  'code-canonicalised-outside-one-function@#49@(?i)\bcode\b[^;\n]*\.(Trim|ToUpper|ToLower)@^[^:]*InvitationCode\.cs:'
)

# What each rule is about, printed when it fires, so the failure explains itself
# rather than only pointing at a line.
explain() {
  case "$1" in
    weak-random)
      echo "An invitation code from a non-cryptographic source is guessable. Use RandomNumberGenerator." ;;
    secret-compared-with-equality)
      echo "Comparing a stored secret with == leaks its prefix through timing. Use CryptographicOperations.FixedTimeEquals." ;;
    secret-compared-by-sequence)
      echo "A keyed hash is bytes, and a sequence comparison of it stops at the first differing byte. Use CryptographicOperations.FixedTimeEquals." ;;
    secret-in-a-log-call)
      echo "An invitation code, a password or the hash secret in a log line is that secret written to disk in clear." ;;
    policy-written-outside-the-template)
      echo "A user policy written anywhere but the routine that applies an account template is a grant nobody reviewed." ;;
    policy-field-written-outside-the-template)
      echo "One field of a user policy set outside that routine is the same grant, made one field at a time. IsAdministrator is the one this plugin may never set." ;;
    link-built-from-a-request-header)
      echo "A link built from what the request says the host is, is a link an attacker chooses." ;;
    link-built-from-the-request-url-helper)
      echo "GetSmartApiUrl answers from the request, so a link built through it is still the caller's host. Take the base address from configuration." ;;
    clock-read-outside-the-seam)
      echo "A call site that reads the machine clock can only be tested by a test that sleeps. Take an IClock." ;;
    code-canonicalised-outside-one-function)
      echo "A code trimmed or cased anywhere but InvitationCode.Canonicalise is a second definition of which codes are equal. Call it instead." ;;
  esac
}

# The one file allowed to read the machine clock is the implementation of the
# seam, and the rule above exempts it by name rather than by directory. Naming
# the basename rather than the full path keeps the exemption alive across the
# rename off the template; naming SystemClock.cs rather than a pattern keeps a
# second file from joining it by being called something clock-shaped.
#
# Two rules carry #50 rather than one, and the split is what makes the second
# one proven. The selftest reads one tripping fixture per rule id, so a fourth
# alternative added to link-built-from-a-request-header would be checked by a
# fixture that already trips on the header spelling: the run would print bites
# whether the new alternative worked or not. A rule of its own gets a fixture
# pair of its own, and the pair is the only thing that says it fires.
#
# Two rules carry #29 for the same reason. The first refuses the string
# spellings, an identifier compared with == or handed to .Equals(. The second
# refuses the one the plugin actually writes: the stored keyed hash is bytes, so
# what a comparison of it reaches for is .SequenceEqual, which no part of the
# first rule's pattern reaches. It matches a line that names something
# secret-shaped anywhere on it and calls .SequenceEqual anywhere on it, so the
# secret is seen on either side of the call, and a sequence comparison of
# something that is not a secret is left alone.
#
# What neither of them refuses is written here rather than left to be
# discovered. A hash held in a variable called digest, mac, tag or expected is
# outside both vocabularies. A comparison written as string.Equals(stored,
# presented, StringComparison.Ordinal), or through StringComparer.Ordinal, puts
# the secret in an argument where the first rule's pattern cannot see it. And
# the first rule reds on a null check written as hash == null, which is correct
# code. All three are recorded on #29.
#
# Two rules carry #69, and the second one was found inside the first one's own
# tripping fixture. That fixture holds two lines, a policy field set and a whole
# policy handed to UpdatePolicy, and only the second is a spelling the first
# rule matches: .Policy followed by = is not .Policy followed by a field. So the
# rule printed bites off a fixture whose first line walked straight through it,
# which is what a fixture proving one alternative of a multi-alternative rule
# looks like from the outside. The second rule is about the single field, which
# is what somebody writes when they mean to change one thing and leave the rest,
# and the field it exists for is IsAdministrator.
#
# Its bounds, in the same spirit. It reads a write and not a read, so a
# comparison against a policy field is left alone, which is how the refusal to
# widen an existing account gets written. It sees one statement, so the same
# write through a local, var p = user.Policy followed by p.IsAdministrator on
# the next line, is invisible to it and to any text pattern over one line. And
# UpdatePolicyAsync is still unmatched by either rule, because the first one
# spells the call UpdatePolicy followed by an open bracket. All three are
# recorded on #69.
#
# The canonicalisation rule exempts InvitationCode.cs the same way, by basename
# rather than by directory, because that file is the one function the rule
# exists to keep alone. What the rule matches is a code being trimmed or cased
# on the same statement it is named on, which is the shape a redemption route
# reaches for when it wants to be forgiving about what somebody typed. Its bound
# is the bound every rule here has: it matches a spelling. The same normalisation
# done to a variable called something other than a code is not matched, and
# neither is one split across two statements. What it buys is that the obvious
# spelling of the mistake cannot land quietly, and the reason a second
# normalisation is a defect at all is in the remarks on Canonicalise.
#
# They also forbid different things. The first is a request field read by hand.
# The second is the server's own helper, which takes a request, a remote address
# or a hostname and returns a URL for it, and is what somebody reaching for a
# base address finds first while believing they asked the server. All three
# overloads answer from what the caller presented, so the helper is refused
# outright here rather than exempted somewhere: this plugin builds a link at
# mint time, when there is no request to derive one from.

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
