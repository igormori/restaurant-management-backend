# Review: Daily expired-session cleanup keeps running after a timeout

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 1041 passed, 0 failed

Also checked: `python3 .claude/scripts/check-security-matrices.py` passes. No test was changed after the
baseline commit `ccdabb6` (`git diff ccdabb6..HEAD -- tests/` is empty). No packages, migrations,
endpoints or Shared contracts were added.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | commits `ccdabb6`, `9f0c397` (body) | The commit bodies say `Scenarios: 1–3`, but `scenarios.md` has only 2 scenarios. "3" is the spec Outcome number, not a scenario. | When merging, use `Scenarios: 1–2` in the squash commit message. Do not rewrite commits that are already shared. |

## Check before merging
- `src/RestaurantManagement.Modules.Identity/Services/RefreshTokenCleanupWorker.cs:43`: the catch filter
  now catches every exception, including `OperationCanceledException`, unless shutdown has started. This
  is the whole fix. It uses the same filter as `PasswordResetEmailWorker.cs:47`. A cancellation during
  shutdown is not caught by `PurgeOnceAsync`, and `WaitForNextTickAsync` also stops on it, so the worker
  ends with no error log. Test 2 proves this.
- `tests/RestaurantManagement.Modules.Identity.Tests/RefreshTokenCleanupWorkerCancellationTests.cs:47`:
  test 1 polls for up to 5 seconds and then checks that the worker is still running. This is timing-based
  but safe: after the error, the worker waits a full day for its next run, so it cannot finish early by
  accident.
- The backlog item deletion `backlog/bugs/2026-10-19-refresh-token-cleanup-worker-stops-host-on-timeout.md`
  is not committed, as the rules require. Commit it yourself before you merge.

## Pull request description
**Fix: daily expired-session cleanup keeps running after a timeout**

The background job that removes expired login sessions (refresh tokens) used to let a database
timeout escape. By default .NET then stops the whole application. The job now logs a timeout as a
failed run and tries again at the next daily run. A real shutdown still ends the job quietly. The
password-reset email job already worked this way; the cleanup job had been missed.

**Identity**
- `RefreshTokenCleanupWorker`: the catch filter changed from "any error except a cancellation" to
  "any error while the application is not shutting down".
- New unit tests in `RefreshTokenCleanupWorkerCancellationTests`: a timeout is logged and the worker
  keeps running; a shutdown ends the worker with no error log.
- `docs/security/identity-matrix.md`: new row D28 names both tests.

**Spec Outcomes delivered**
1. A timeout does not stop the application: error logged, worker still running (test 1).
2. Shutdown still ends the job cleanly: no error log (test 2).
3. Both cases are recorded in Identity matrix row D28.

**Risk:** Low. One background job's error handling changed. No data, schema, contract or endpoint
changes. **Flags:** none.

**Backlog:** solves and removes `backlog/bugs/2026-10-19-refresh-token-cleanup-worker-stops-host-on-timeout.md`.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
