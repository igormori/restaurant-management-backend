#!/usr/bin/env bash
# Runs tests and prints a short summary: build errors, each failed test (passed tests are left out), the first line of
# each failure's message, and the totals. Never prints stack traces or container logs.
#
# Usage:
#   .claude/scripts/test-summary.sh <test-project-path> [filter]
#   .claude/scripts/test-summary.sh all
#   .claude/scripts/test-summary.sh affected [base]     (default base: origin/development)
#
# "affected" runs only what the branch's changes (committed and uncommitted) can break:
#   - only docs or tooling changed          -> no tests
#   - Shared, Web or build files changed    -> full suite
#   - module code changed                   -> that module's test project + Web.Tests tests named after the module
#   - Shared.Tests or Web.Tests changed     -> that whole test project
# Examples:
#   .claude/scripts/test-summary.sh tests/RestaurantManagement.Web.Tests "FullyQualifiedName~RolePermissionsTests"
#   .claude/scripts/test-summary.sh affected
#   .claude/scripts/test-summary.sh all

set -uo pipefail

MAX_FAILURE_LINES=80
SOLUTION="RestaurantManagement.sln"
WEB_TESTS="tests/RestaurantManagement.Web.Tests"
SHARED_TESTS="tests/RestaurantManagement.Shared.Tests"
RUN_ALL_PATTERN='^src/RestaurantManagement\.(Shared|Web)/|\.(sln|props|targets)$|(^|/)global\.json$'
CODE_PATTERN='^(src|tests)/|\.(sln|props|targets)$|(^|/)global\.json$'
TOTALS_PATTERN="^(Total tests|Passed!|Failed!)|^(Passed|Failed|Skipped): "

TARGET="${1:-}"
if [[ -z "$TARGET" ]]; then
  echo "Usage: $0 <test-project-path> [filter] | all | affected [base]" >&2
  exit 2
fi

STATUS=0
NO_MATCH=0

# Build quietly; show only errors
build() {
  local out
  out=$(dotnet build "$SOLUTION" --verbosity quiet --nologo 2>&1)
  if [[ $? -ne 0 ]]; then
    echo "BUILD FAILED"
    echo "$out" | grep -E "error [A-Z]+[0-9]+" | sed -E 's|^.*/||' | sort -u | head -n 20
    exit 1
  fi
}

# Runs one test target: failures first (capped), then the totals. Sets NO_MATCH=1 when the filter matched no test.
run_tests() {
  local target="$1" filter="${2:-}" out code
  local args=(test "$target" --no-build --nologo --logger "console;verbosity=normal")
  [[ -n "$filter" ]] && args+=(--filter "$filter")
  echo "== $target ${filter:+($filter)}"
  out=$(mktemp)
  dotnet "${args[@]}" 2>&1 | awk '
    /No test matches the given testcase filter/ { print "NO_MATCH"; next }
    /^[[:space:]]+Passed [A-Za-z]/ { next }
    /^[[:space:]]+Failed [A-Za-z].* \[[0-9<]/ { sub(/^[[:space:]]+/, ""); sub(/ \[[^]]*\]$/, ""); print "  " $0; want_msg = 1; next }
    want_msg && /^[[:space:]]*Error Message:/ { next }
    want_msg && /[A-Za-z]/ && !/^[[:space:]]+at / && !/Stack Trace:/ {
      msg = $0; sub(/^[[:space:]]+/, "", msg); print "      -> " substr(msg, 1, 200); want_msg = 0; next }
    /(Total tests|Passed!|Failed!|^[[:space:]]+(Passed|Failed|Skipped): )/ { sub(/^[[:space:]]+/, ""); print $0 }
  ' > "$out"
  code=${PIPESTATUS[0]}
  NO_MATCH=0
  grep -q "^NO_MATCH" "$out" && NO_MATCH=1
  grep -vE "$TOTALS_PATTERN|^NO_MATCH" "$out" | head -n "$MAX_FAILURE_LINES"
  grep -E "$TOTALS_PATTERN" "$out"
  rm -f "$out"
  [[ $code -ne 0 ]] && STATUS=1
  return 0
}

# Runs only the tests the branch's changes can break. When unsure, runs more, never fewer.
run_affected() {
  local base="${1:-origin/development}" merge_base changed code modules m project
  merge_base=$(git merge-base "$base" HEAD 2>/dev/null) || { echo "Cannot find $base (run: git fetch origin)" >&2; exit 2; }
  changed=$( { git diff --name-only "$merge_base"; git ls-files --others --exclude-standard; } | sort -u )
  code=$(echo "$changed" | grep -E "$CODE_PATTERN" || true)

  if [[ -z "$code" ]]; then
    echo "Only docs or tooling changed against $base: no tests to run."
    exit 0
  fi

  build

  if echo "$code" | grep -qE "$RUN_ALL_PATTERN"; then
    echo "Shared, Web or build files changed: running the full suite."
    run_tests "$SOLUTION"
    exit "$STATUS"
  fi

  modules=$(echo "$code" | sed -nE 's#^(src|tests)/RestaurantManagement\.Modules\.([A-Za-z]+)(\.Tests)?/.*#\2#p' | sort -u)
  echo "Affected modules: ${modules:-none}"

  for m in $modules; do
    project="tests/RestaurantManagement.Modules.$m.Tests"
    [[ -d "$project" ]] && run_tests "$project"
  done

  echo "$code" | grep -q "^$SHARED_TESTS/" && run_tests "$SHARED_TESTS"

  if echo "$code" | grep -q "^$WEB_TESTS/"; then
    echo "Web.Tests changed: running all of Web.Tests."
    run_tests "$WEB_TESTS"
  else
    for m in $modules; do
      run_tests "$WEB_TESTS" "FullyQualifiedName~$m"
      if [[ $NO_MATCH -eq 1 ]]; then
        echo "No Web.Tests tests named after $m: running all of Web.Tests."
        run_tests "$WEB_TESTS"
        break
      fi
    done
  fi
  exit "$STATUS"
}

if [[ "$TARGET" == "affected" ]]; then
  run_affected "${2:-}"
fi

build
if [[ "$TARGET" == "all" ]]; then
  run_tests "$SOLUTION"
else
  run_tests "$TARGET" "${2:-}"
fi
exit "$STATUS"