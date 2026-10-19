# Review: Forgot password takes the same time for every email

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 1024 passed, 0 failed (Shared 159, Organization 130, Menu 63, Identity 290, Web 382). The first of four full runs had 1 failure in a test from the earlier branch, see Watch out 2.

Third review, after the second fix round. Every finding from the second review is fixed or recorded:
- Watch out 1 (claim from pass start): fixed in `3e37ac0`. `ClaimedUntil` now counts from `DateTime.UtcNow` and the row is read again after the claim. New row P26, scenarios 32 and 33.
- Watch out 2 (concurrency test that did not overlap): fixed in `c0105f1`. A `Barrier` makes the first two sends wait for each other, and both passes must handle requests.
- Watch out 3 (interval of 0 or below): fixed in `d461f49`. The app refuses to start and names the setting. New row D23.
- Watch out 4 (purge scan): fixed in `dd2aea8`, with a new `failed_at` index (third migration, index only).
- Watch out 5 (cancellation filter): fixed in `3ea7226`, now `when (!stoppingToken.IsCancellationRequested)`. New row D24. The same bug in `RefreshTokenCleanupWorker` is recorded in `backlog/bugs/2026-10-19-refresh-token-cleanup-worker-stops-host-on-timeout.md`. Its evidence is real (`RefreshTokenCleanupWorker.cs:43`).
- Watch out 6 (unused `IEmailService`): recorded in `backlog/tech-debt/2026-10-19-unused-email-service-in-password-reset-service.md`. Its evidence is real.
- Watch out 8 (`///` comments): now one-line "why" comments.
- Watch out 9 (Checklist): the plan's Checklist now has a "Fix round" line with every fix commit.

Also checked:
- `python3 .claude/scripts/check-security-matrices.py` passes. Rows P26, D23 and D24 each name tests that exist and check the row's Expected result. D18 now lists the `failed_at` index.
- Since the baseline `e08d72d`, existing tests changed in only four places, all approved by the user: scenario 24 (`dd1e68e`), the D18 column list (`945ecb2`), the P25 concurrency test (`c0105f1`) and the D18 index list (`dd2aea8`). Everything else under `tests/` is new.
- No package added, so no vulnerable-package scan was needed. No secrets in the diff. Every new file is in an allowed folder and is named in a batch file's fix-round note.
- Commits follow Conventional Commits and put scenario numbers in the body. Commits up to `e2d0a18` belong to the earlier branch. `aa5d341` and `57706ee` were made at the user's request.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | src/RestaurantManagement.Modules.Identity/Services/PasswordResetEmailProcessor.cs:58, :150, :162 | The claim now uses the real clock, but `HandleAsync` still uses the pass-start `utcNow`. It sets the code's `CreatedAt` and `ExpiresAt`, the resend wait, the lock check and the retry time. The default SMTP (Simple Mail Transfer Protocol) timeout is about 100 s, so with a slow provider a 100-row pass can run for hours. A row handled late in the pass then gets a code that has already expired, or expires much sooner than 20 minutes after the email arrives. The user gets an email with a code that does not work, which goes against Outcome 4. | Pass `DateTime.UtcNow` (or an injected `TimeProvider`) into `HandleAsync` for each row, and keep `utcNow` only for choosing which rows are due. Add a backlog item if it is not fixed now. |
| 2 | tests/RestaurantManagement.Web.Tests/Identity/TokenRolesEndpointTests.cs:35 | `CreateMenu_TokenOfOwnerInOneAndWaiterInAnotherOrganizationBeforeChoosing_Returns403` failed once in four full `dotnet test` runs. It passed in the other three full runs, in three Web-only runs and on its own. The test comes from the earlier branch (`0b7e36e`) and is not changed here. One possible cause on this branch: the concurrency test's `Barrier.SignalAndWait` blocks two thread-pool threads for up to 10 s while other test classes run in parallel. | Watch the CI (continuous integration) run. If it fails again, write a backlog item with the failure message. |
| 3 | docs/plans/identity/2026-10-17-forgot-password-same-response-time/scenarios.md:3, "Fix round" | The Coverage line says scenarios 26 to 33 were added in fix rounds, but 34 to 37 were added too. The "Fix round 2" paragraph leaves out scenarios 34 to 37 and the D18 index-list change. | Change the line to "26 to 37" and list 34 to 37 and the D18 index change in the paragraph. |
| 4 | tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetEmailWorkerCancellationTests.cs:43-55 | Scenario 37 says a shutdown cancellation is "not logged as an error", but the test only checks that the cancellation propagates. It never checks the log. | Change the scenario 37 text to match the test, or, with the user's approval, also assert that `logger.Levels` has no `Error`. |
| 5 | CLAUDE.md:118 | The root `CLAUDE.md` still lists `/fixbug`, which `aa5d341` removed. The user is leaving this line on purpose. | The user removes `/fixbug` from the line. |

## Check before merging
- **`PasswordResetEmailProcessor` claim, mark and send flow:** it creates every reset code and sends every reset email outside a request. A claim now lasts 5 minutes from the moment it is taken, and the row is read again, so a slow pass cannot hand the same request to a second instance. Code expiry still counts from the start of the pass (Watch out 1). A crash after the code is stored drops the request (at-most-once, documented in the Identity `CLAUDE.md`).
- **Worker on by default, and startup now fails on a bad interval (`Program.cs:255-266`, `appsettings.json`):** every instance runs a pass every 5 seconds. An `IntervalSeconds` of 0 or below now stops the deploy at startup. Check `PasswordResetEmails:*` and the email provider settings in each environment.
- **Three migrations on `password_reset_requests` (`20261017072357`, `20261019135643`, `20261019162448`):** new table, a nullable column and an index. There is no data yet. The table stores email addresses typed by anonymous callers, and failed rows are purged after 7 days.

## Pull request description
**Forgot password takes the same time for every email (Identity)**

"Forgot password" no longer does any account work during the request. Whatever the email, it adds one row to a new Identity table of pending reset requests and returns the same acknowledgement. A background worker processes that table every 5 seconds (configurable). Each pass handles up to 100 requests, oldest first. For each one, the worker claims the row, checks eligibility (active, verified, not reset-locked, outside the 60 s wait), creates the 6-digit code (only its fingerprint is stored), sends the email and deletes the request. A failed send is retried after 1, 2, 4 and 8 minutes. After the 5th failure the request is marked failed and purged after 7 days. If the app crashes after a code is stored, the request is dropped rather than sent again, because a second code would break the first.

**What changed**
- Identity: new entity `PasswordResetRequest`, table `password_reset_requests`, and migrations `AddPasswordResetRequests`, `AddPasswordResetRequestCodeIssuedAt` and `AddPasswordResetRequestFailedAtIndex`. `PasswordResetService.RequestResetCodeAsync` now only adds a queue row. New `IPasswordResetEmailProcessor` / `PasswordResetEmailProcessor`. It claims each row with a conditional update that lasts 5 minutes from the claim, then reads the row again, so several instances can run it safely. New `PasswordResetEmailWorker` (hosted service): only a real shutdown stops it, and a timeout is logged as a failed pass. Identity `CLAUDE.md` note added.
- Web: processor registered. The worker is registered when `PasswordResetEmails:Enabled` is true, using `PasswordResetEmails:IntervalSeconds` (defaults: true and 5, new `appsettings.json` entries). The app refuses to start when the interval is 0 or below.
- Security matrix: rows P1–P5 rewritten. P15–P26 and D18–D24 added, all with tests.
- Tests: 37 scenarios, including a PostgreSQL test where two processors really overlap and an endpoint test that runs the real worker loop. Existing reset tests now store codes directly. Four changes to existing tests after Gate 2 were approved by the user: scenario 24 (`dd1e68e`), the D18 column list (`945ecb2`), the P25 concurrency test (`c0105f1`) and the D18 index list (`dd2aea8`).
- Backlog: two new `proposed` items, the refresh-token worker cancellation filter and the unused `IEmailService` parameter.
- Also on this branch, at the user's request: the bug-fixer agent and `/fixbug` command were removed (`aa5d341`). The solved forgot-password timing backlog item was replaced by "resend verification reveals unverified accounts" (`57706ee`).

**Spec Outcomes delivered**
1. The response does not wait for email delivery.
2. Known and unknown emails cannot be told apart by time. This is proved by identical database commands, not by measuring time.
3. A failed delivery is retried, not lost.
4. An eligible email gets exactly one working code.
5. Unknown, inactive and unverified emails get no email.

**Risk: High.** A new table with three migrations (no existing data), a new background worker, and a behaviour change on the public `POST /api/auth/forgot-password` endpoint. No role, auth or Shared contract change, no new package, no raw SQL.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
