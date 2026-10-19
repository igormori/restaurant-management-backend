# Refresh token cleanup worker lets a non-shutdown cancellation stop the app

**Status:** proposed
**Severity:** medium
**Origin:** docs/plans/identity/2026-10-17-forgot-password-same-response-time/review.md · second review, Watch out 5
**Date:** 2026-10-19

## Problem
`RefreshTokenCleanupWorker` catches every error except `OperationCanceledException`. A cancellation
that is not a shutdown (for example a database or driver timeout surfacing as `TaskCanceledException`)
therefore escapes the worker, and by default .NET stops the whole host. `PasswordResetEmailWorker`
was fixed to filter on `!stoppingToken.IsCancellationRequested` instead.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Services/RefreshTokenCleanupWorker.cs:43` — `catch (Exception ex) when (ex is not OperationCanceledException)`
- `src/RestaurantManagement.Modules.Identity/Services/PasswordResetEmailWorker.cs` — the fixed filter, with tests in `PasswordResetEmailWorkerCancellationTests.cs`

## Acceptance criteria
- [ ] A `TaskCanceledException` thrown by the cleanup service while the app is not stopping is logged and the worker keeps running
- [ ] A cancellation while the app is stopping still ends the worker without an error log
- [ ] A test covers both cases

## Related
None
