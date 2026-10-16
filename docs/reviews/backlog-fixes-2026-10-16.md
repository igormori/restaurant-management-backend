# Review: backlog-fixes (refresh token purge, SQLite pin, workflow rule changes)

**Verdict:** Fix first — the new cleanup worker runs in the Web integration test host and deletes rows in the developer's local Development database instead of the Testcontainers database.
**Build:** pass (0 warnings, 0 errors)   **Tests:** Identity 193 passed, 0 failed; Web 291 passed, 0 failed (run to check the worker in the test host)

No spec or plan folder (small change), so the Completeness check was skipped. `dotnet list package --vulnerable --include-transitive`: no vulnerable packages in any project.

## Fix first
| # | File:line | Problem | Change |
|---|---|---|---|
| 1 | src/RestaurantManagement.Web/Program.cs:244, tests/RestaurantManagement.Web.Tests/AuthApiFactory.cs:36 | `AddHostedService<RefreshTokenCleanupWorker>()` also runs in every `WebApplicationFactory<Program>` (4 factories). WebApplicationFactory uses the Development environment, so the worker connects to `restaurant_db-development` on localhost:5434 and runs a real `DELETE` there. The Web test log shows "Refresh token cleanup deleted 0 expired tokens" on each host start. The testing skill says integration tests use Testcontainers PostgreSQL only. On a machine or CI runner without that database, every host start logs an error. | In the test factories' `ConfigureServices`, remove the worker (`services.RemoveAll<IHostedService>()`, or remove the `ImplementationType == typeof(RefreshTokenCleanupWorker)` descriptor). Put it in one shared place so all 4 factories get it. |

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | src/RestaurantManagement.Modules.Identity/Services/RefreshTokenCleanupWorker.cs:43 | `when (ex is not OperationCanceledException)` lets *any* cancellation exception escape, including ones that have nothing to do with shutdown. With the default `BackgroundServiceExceptionBehavior.StopHost`, an escaped exception stops the whole API. | Use `catch (Exception ex) when (!stoppingToken.IsCancellationRequested)`. |
| 2 | tests/RestaurantManagement.Modules.Identity.Tests/RefreshTokenCleanupServiceTests.cs:52-110 | No test covers the exact boundary. A token with `ExpiresAt == utcNow - 30 days` must be kept, because the code uses `<`. Changing it to `<=` would still pass every test. | Add `PurgeExpiredAsync_TokenExpiredExactlyAtCutoff_KeepsIt`. Also add a test for revoked tokens expired inside the retention window (kept). |
| 3 | src/RestaurantManagement.Modules.Identity/Services/RefreshTokenCleanupWorker.cs:24-49 | The worker has no tests. Nothing proves that a failed run is logged and does not stop the host. | Add a unit test with a substituted `IRefreshTokenCleanupService` that throws once and is called again on the next run, or log a backlog item. |
| 4 | src/RestaurantManagement.Modules.Identity/Services/RefreshTokenCleanupService.cs:17-18 | The comment covers only rotation. `RefreshTokenService.RevokeAsync` (logout) does not check expiry: today, sending a revoked, expired token to logout still revokes the whole family. Once the purge runs, that reuse signal is lost for tokens expired more than 30 days. This is a small loss of detection, not a way for an attacker to get in. | Accept it on purpose and say so in the comment. Alternatively, make logout ignore expired tokens too, so both paths behave the same (separate item). |
| 5 | src/RestaurantManagement.Modules.Identity/Data/IdentityDbContext.cs:78-89 | `refresh_tokens` has no index on `ExpiresAt`, so the daily delete scans the whole table. | Fine at current size. Log a backlog item to add the index (needs a migration, so it is out of scope here). |
| 6 | src/RestaurantManagement.Modules.Identity/Services/RefreshTokenCleanupWorker.cs:7-10 | A `///` summary on a non-Shared class. The clean-code skill allows `///` only on public interfaces in Shared. | Make it a one-line `//` "why" comment (several instances are safe because the delete is idempotent). |
| 7 | commit e0c4f35 | The summary "purge expired and revoked refresh tokens" (the backlog title) is wrong. Revoked tokens that have not expired are deliberately kept. The deleted backlog item asked for per-family cleanup; this change cleans up per row (safe, see below). | Optional: reword before pushing (`purge refresh tokens 30 days after expiry`). |
| 8 | tests/*/\*.Tests.csproj (4 files) | The pin produces a mixed SQLitePCLRaw set: `lib.e_sqlite3` 2.1.14 next to `bundle`/`core`/`provider` 2.1.10. It works (tests pass) because the security fix is in the native library. The comment names 2.1.10 and EF Core Sqlite 9.0.2, which matches the csproj files. | Remove the 4 pins when EF Core Sqlite is upgraded to a version that brings a patched SQLite. |

## Check before merging
- **Refresh token purge (e0c4f35) is authentication data.** I traced `RefreshTokenService.RotateAsync`. It returns null when `ExpiresAt <= now`, before any reuse check. The purge deletes only rows expired more than 30 days ago, so it cannot delete a token that is still usable or still needed for rotation reuse detection. Deleting older rows of a family that is still active is also safe: `ReplacedByTokenId` has no foreign key, and family revocation uses `FamilyId`. Sign-in behaviour does not change, so I do not count this as an "auth change" in the high-risk sense. It still touches auth tables, and the small-batch gate says "Roles, auth … not changed". Decide whether this item should have gone through `/spec`.
- **The worker in the test host (Fix first 1)**: every Web test run sends a `DELETE` to your local dev database.
- **Workflow rule changes (2874662, 97d7c94, 6c727fa)**: these change how high risk is defined for packages. "Pinning an existing transitive package" is now low risk and allowed in `/small`. Check that this matches what you want before other agents rely on it. 2874662 says "commit the deletion" and 97d7c94 reverses that. The final text is consistent.

## Pull request description
**Backlog fixes: refresh token cleanup, patched SQLite in tests, workflow rules**

Summary: a daily job now deletes refresh tokens 30 days after they expire, so the `refresh_tokens` table stops growing and device and IP (Internet Protocol) address data is not kept forever. The test projects now use a patched native SQLite. The agent workflow rules now say how solved backlog items are committed, and when a package change counts as high risk.

Identity
- `RefreshTokenCleanupService` deletes refresh tokens whose expiry is more than 30 days in the past. Revoked tokens that have not expired are kept, so replaying one still triggers reuse detection.
- `RefreshTokenCleanupWorker` (a background service) runs the cleanup at startup and then once a day. A failed run is logged and retried on the next run. It is safe with several instances running.
- Deletes the solved backlog item "resend email failure log drops the exception" (fixed by 3f42769).

Web
- Registers the cleanup service and the worker.

Tests and dependencies
- 5 new tests in `RefreshTokenCleanupServiceTests`.
- Pins `SQLitePCLRaw.lib.e_sqlite3` 2.1.14 in the 4 test projects (GHSA-2m69-gcr7-jv3q). Test-only; nothing shipped changes.

Docs and agent rules
- `CLAUDE.md`, security and backlog skills, the small-batch and small-feature agents, and the `small-match` command.

Risk and flags
- No migration, no new endpoint, no new Shared contract, and no package new to the dependency graph.
- Touches authentication data (refresh token rows). Reviewed against rotation and reuse detection: no usable or needed token is deleted.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
