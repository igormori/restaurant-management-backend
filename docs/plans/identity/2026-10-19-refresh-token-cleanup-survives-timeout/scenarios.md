# Scenarios: Daily expired-session cleanup keeps running after a timeout

**Plan:** docs/plans/identity/2026-10-19-refresh-token-cleanup-survives-timeout/plan.md
**Coverage:** 2 scenarios — 0 happy · 0 edge · 1 failure · 0 security · 1 guard

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Failure | A database timeout during the cleanup, with no shutdown requested, is logged as an error and the cleanup job keeps running | `ExecuteAsync_TimeoutCancellationWhileNotStopping_LogsErrorAndWorkerKeepsRunning` | `tests/RestaurantManagement.Modules.Identity.Tests/RefreshTokenCleanupWorkerCancellationTests.cs` |
| 2 | T1 | Guard | When the application shuts down during a cleanup run, the job ends and no error is logged (already true today, must stay true) | `ExecuteAsync_ShutdownRequestedDuringRun_WorkerEndsWithoutErrorLog` | `tests/RestaurantManagement.Modules.Identity.Tests/RefreshTokenCleanupWorkerCancellationTests.cs` |

Both scenarios are recorded in Identity matrix row D28 (section 11).

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 A timeout does not stop the application | 1 |
| #2 Shutdown still ends the job cleanly | 2 |
| #3 Both cases proven and recorded in the matrix | 1, 2 (row D28) |
