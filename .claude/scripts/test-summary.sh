#!/usr/bin/env bash
# Runs tests and prints a short summary: build errors, each test's result, the first line of
# each failure's message, and the totals. Never prints stack traces or container logs.
#
# Usage:
#   .claude/scripts/test-summary.sh <test-project-path> [filter]
#   .claude/scripts/test-summary.sh all
# Examples:
#   .claude/scripts/test-summary.sh tests/RestaurantManagement.Web.Tests "FullyQualifiedName~RolePermissionsTests"
#   .claude/scripts/test-summary.sh all

set -uo pipefail

MAX_LINES=80
TARGET="${1:-}"
FILTER="${2:-}"

if [[ -z "$TARGET" ]]; then
  echo "Usage: $0 <test-project-path> [filter] | all" >&2
  exit 2
fi

BUILD_TARGET="RestaurantManagement.sln"
TEST_TARGET="$TARGET"
[[ "$TARGET" == "all" ]] && TEST_TARGET="$BUILD_TARGET"

# 1. Build quietly; show only errors
BUILD_OUT=$(dotnet build "$BUILD_TARGET" --verbosity quiet --nologo 2>&1)
if [[ $? -ne 0 ]]; then
  echo "BUILD FAILED"
  echo "$BUILD_OUT" | grep -E "error [A-Z]+[0-9]+" | sed -E 's|^.*/||' | sort -u | head -n 20
  exit 1
fi

# 2. Run tests with names, then keep only result lines and the first message line per failure
ARGS=(test "$TEST_TARGET" --no-build --nologo --logger "console;verbosity=normal")
[[ -n "$FILTER" ]] && ARGS+=(--filter "$FILTER")

dotnet "${ARGS[@]}" 2>&1 | awk '
  /^[[:space:]]+Passed [A-Za-z]/ { sub(/^[[:space:]]+/, ""); sub(/ \[[^]]*\]$/, ""); print "  " $0; next }
  /^[[:space:]]+Failed [A-Za-z]/ { sub(/^[[:space:]]+/, ""); sub(/ \[[^]]*\]$/, ""); print "  " $0; want_msg = 1; next }
  want_msg && /^[[:space:]]*Error Message:/ { next }
  want_msg && /[A-Za-z]/ && !/^[[:space:]]+at / && !/Stack Trace:/ {
    msg = $0; sub(/^[[:space:]]+/, "", msg); print "      -> " substr(msg, 1, 200); want_msg = 0; next }
  /(Total tests|Passed!|Failed!)/ { sub(/^[[:space:]]+/, ""); print $0 }
' | head -n "$MAX_LINES"

exit "${PIPESTATUS[0]}"