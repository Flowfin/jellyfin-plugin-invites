#!/usr/bin/env bash
#
# A timeout is not a kill, decided in #376.
#
# Stryker scores a mutant that timed out as killed, and docs/mutation-testing.md
# argues that reading: a mutant that hangs the suite is one a test noticed. What
# the same rule also does is turn a run that was too slow into a run that
# passed, because nothing in the report separates a mutant that hung the suite
# from one whose test host was starved of a core.
#
# That is measured rather than supposed, and the measurement is why this file
# exists. Two runs of one configuration over one tree, minutes apart on one
# machine: one reported 206 killed, 85 timeouts, no survivors and a score of
# 100.00, which meets the break threshold; the other reported 287 killed, no
# timeouts and the four survivors #376 argues are equivalent, and a score of
# 98.63, which does not. Both readings are on that issue and on
# docs/mutation-testing.md.
#
# So the score alone is two verdicts, and the greener of them is the one a
# busy machine produces. This is the second half of the verdict: a run
# carrying a timeout has measured less than its score says, in whichever
# direction the score moved, and it is refused rather than counted.
#
# What that costs is the same cost the break threshold of 100 already pays,
# pointed at a different class. A mutant that genuinely hangs the suite - this
# scope has had one, in Codes/InvitationCode.cs - now reds this gate and has to
# be judged rather than counted as a kill. Judging it means killing it faster,
# arguing its class out on docs/mutation-testing.md, or raising the tool's own
# timeout so the run can tell the two apart. All three leave a record, which
# scoring it as a kill does not.
#
# What it does not do, stated so a green run is not read as more than it is.
# It does not make the run reproducible. A machine slow enough to time out
# every mutant still reports a different set from a machine that times out
# none, and this refuses both rather than reconciling them. Pinning a
# concurrency in stryker-config.json is the other repair #376 names, it fixes
# the gate to one machine shape, and it is not taken here: a runner and this
# machine do not have the same number of cores, so a number chosen for one is
# a guess about the other.
#
# It reads a report and judges nothing about the plugin. Which mutants a run
# tested is stryker-config.json's decision, and a report over the wrong scope
# passes this exactly as one over the right scope does.
#
# Two modes:
#   read <report.json>  refuse a run whose report carries a timeout
#   selftest            fail unless the rule fires on each tripping fixture and
#                       not on the clean one
set -uo pipefail

FIXTURES="$(dirname "$0")/fixtures/mutation-verdict"

# The report is one line of minified JSON, so it is broken up before it is read.
# Each file key in the report and each mutant object starts a line of its own,
# and nothing else is parsed. The two markers cannot be produced by the source
# text a report carries beside the mutants: every quote in that text is escaped,
# so a C# file containing either shape appears as \" and does not match.
normalise() {
  sed -e 's/"\([^"]*\.cs\)":{"language"/\n@file \1\n/g' \
      -e 's/{"id":"/\n@mutant {"id":"/g' -- "$1"
}

# Prints one line per file that carries a timeout, as "count path", followed by
# a line "@seen <mutants> <timeouts>" holding what the reader found at all.
survey() {
  normalise "$1" | awk '
    /^@file /   { path = substr($0, 7); next }
    /^@mutant / {
      if ($0 !~ /"status":"/) next
      seen++
      if ($0 ~ /"status":"Timeout"/) { timeouts++; per[path]++ }
      next
    }
    END {
      for (p in per) printf "%d %s\n", per[p], p
      printf "@seen %d %d\n", seen + 0, timeouts + 0
    }
  '
}

cmd_read() {
  local report="${1:-}"
  if [ -z "$report" ]; then
    echo "usage: $0 read <report.json>" >&2
    return 2
  fi
  if [ ! -f "$report" ]; then
    # Fails closed. A run that wrote no report is a run with no verdict, and
    # the alternative reading - no report, no timeouts, green - is the failure
    # this whole file is about in its cheapest form.
    #
    # This branch does not isolate and is not meant to. Taking it out leaves the
    # case refused by the leg below, on a reader that found no mutant, with the
    # reading tool's own error on standard error. What it buys is a sentence
    # naming what happened rather than one about the reader.
    echo "::error::${report} does not exist, so this run left no verdict to read."
    return 1
  fi

  local output seen timeouts
  output=$(survey "$report")
  seen=$(printf '%s\n' "$output" | awk '/^@seen /{print $2}')
  timeouts=$(printf '%s\n' "$output" | awk '/^@seen /{print $3}')

  if [ "${seen:-0}" -eq 0 ]; then
    # The near-miss this leg exists against. A report whose shape moved, or a
    # path that resolved to the wrong file, yields no mutant and therefore no
    # timeout, and without this the reader reports the same silence as a run
    # that timed nothing out.
    echo "::error::${report} yielded no mutant this reader recognises, so a pass here would be a reader that stopped matching rather than a run with nothing wrong."
    return 1
  fi

  if [ "${timeouts:-0}" -gt 0 ]; then
    echo "::error::${timeouts} of ${seen} mutants timed out. A timeout is scored as a kill, so this run's score is higher than what it measured. Judge each one: kill it faster, argue its class out on docs/mutation-testing.md, or raise the tool's timeout. Decided in #376."
    # The path is printed as the report spells it, which is JSON, so a Windows
    # separator arrives doubled. Rewriting it here would be a second spelling of
    # a path nobody resolves.
    printf '%s\n' "$output" | grep -v '^@seen ' | sort -rn
    return 1
  fi

  echo "ok    no mutant timed out (#376): ${seen} mutants read from ${report}"
  return 0
}

cmd_selftest() {
  # A rule nobody has watched fire proves nothing, and this one cannot fire
  # against the tree: the report it reads is written by a run rather than
  # tracked. This is where it is made to fire, on a report holding the shape
  # the measurement above found, and made to stop firing when that shape is
  # taken out.
  local fail=0 name out rc
  for name in timed-out no-mutants; do
    rc=0
    out=$(cmd_read "${FIXTURES}/${name}.trip.json") || rc=$?
    if [ "$rc" -ne 1 ]; then
      echo "::error::selftest: ${FIXTURES}/${name}.trip.json was not refused, so the rule cannot bite and proves nothing."
      fail=1
      continue
    fi
    echo "bites ${name} (#376): ${FIXTURES}/${name}.trip.json is refused"
  done

  rc=0
  out=$(cmd_read "${FIXTURES}/clean.json") || rc=$?
  if [ "$rc" -ne 0 ]; then
    echo "::error::selftest: ${FIXTURES}/clean.json is refused, so removing the timeout does not make the rule pass."
    printf '%s\n' "$out"
    fail=1
  else
    echo "passes clean (#376): ${FIXTURES}/clean.json is read and not refused"
  fi

  rc=0
  out=$(cmd_read "${FIXTURES}/no-such-report.json") || rc=$?
  if [ "$rc" -ne 1 ]; then
    echo "::error::selftest: a report that does not exist was not refused, so a run that wrote none would read as green."
    fail=1
  else
    echo "bites absent (#376): a report that does not exist is refused"
  fi

  return $fail
}

case "${1:-}" in
  read)     shift; cmd_read "$@" ;;
  selftest) cmd_selftest ;;
  *)        echo "usage: $0 read <report.json> | $0 selftest" >&2; exit 2 ;;
esac
