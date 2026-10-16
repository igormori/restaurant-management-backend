# Test summary script hides the full-suite results

**Status:** proposed
**Severity:** low
**Origin:** docs/plans/shared/2026-10-15-audit-log/plan.md · Phase 0 — Task 8 (tester step)
**Date:** 2026-10-16

## Problem
`.claude/scripts/test-summary.sh all` prints every passed test and then cuts the output at 80 lines.
The suite has about 730 tests, so the output stops partway through the first project: the failures
and the "Total tests" lines never appear. Cutting the pipe early may also stop `dotnet test` before
all projects have run. Agents cannot confirm that the full suite is green through the script, which the
tester and implementer instructions require.

## Evidence
- `.claude/scripts/test-summary.sh:14` — `MAX_LINES=80`
- `.claude/scripts/test-summary.sh:46` — `head -n "$MAX_LINES"` applied to all output, including passed tests

## Acceptance criteria
- [ ] `test-summary.sh all` always shows every failed test with its first message line and the totals for every test project
- [ ] Passed tests are left out of the output (or counted only) so the output stays short

## Related
None
