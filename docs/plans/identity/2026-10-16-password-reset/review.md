# Review: Forgot and change password

**Verdict:** Ready to merge
**Build:** pass (0 warnings, 0 errors)   **Tests:** first full run 793 passed, 2 failed; two reruns 795 passed, 0 failed

Results per test project from `dotnet test` (full suite), first run:

| Test project | Passed | Failed | Skipped |
|---|---|---|---|
| RestaurantManagement.Shared.Tests | 152 | 0 | 0 |
| RestaurantManagement.Modules.Organization.Tests | 123 | 0 | 0 |
| RestaurantManagement.Modules.Menu.Tests | 47 | 0 | 0 |
| RestaurantManagement.Modules.Identity.Tests | 187 | 0 | 0 |
| RestaurantManagement.Web.Tests | 284 | 2 | 0 |

The 2 failures were `StartupConfigurationTests.Startup_VerificationHashKeyEmpty_FailsNamingHashKey` and `Startup_VerificationHashKeyEmptyInProduction_FailsNamingHashKey`. That test file is not changed on this branch. A Web-only rerun and a second full run passed every test (Web 286 passed, 0 failed), so these tests fail only sometimes (see Watch out #9).

No NuGet (.NET package manager) package was added or updated, so `dotnet list package --vulnerable` was not needed.
The uncommitted edits to `.claude/skills/security/SKILL.md` and `backlog/tech-debt/2026-10-16-password-hashing-rule-mismatch.md` are the user's own work in progress. They were not reviewed.

Fix round since the last review: f69d951 (a failed audit write is logged and the request still succeeds), b013433 (reset rejects a deactivated user with the generic 401), 387e68e (backlog item for the forgot-password timing leak). Last review's Watch out #1 is fixed and #2 is now a backlog item. The orchestrator confirmed that `PasswordAuditRecorder` takes organization IDs only from the `user_roles` rows of a user loaded from the database, never from the request.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `src/RestaurantManagement.Modules.Identity/Controllers/AuthController.cs:146` | `[Authorize]` names no roles. This is on purpose: a user with no organization has no role and must still be able to change their password. | Keep it. Add this exception to the security skill. |
| 2 | `src/RestaurantManagement.Modules.Identity/Services/PasswordResetService.cs:130`, `PasswordChangeService.cs:70` | Two more direct `BCrypt.HashPassword` calls with the default work factor (11). | Add them as evidence to `backlog/tech-debt/2026-10-16-password-hashing-rule-mismatch.md`. |
| 3 | `src/RestaurantManagement.Modules.Identity/Models/PasswordRulesAttribute.cs:41` | `char.IsUpper` accepts any Unicode uppercase letter. The old regex accepted only `A-Z`. | Use `c is >= 'A' and <= 'Z'`, or accept the change and mention it in the pull request. |
| 4 | `src/RestaurantManagement.Web/appsettings.json:1-70` | Line endings changed from CRLF (Windows) to LF (Unix), so all 70 lines show as changed. | Restore CRLF, or add a `.gitattributes` rule. |
| 5 | `src/RestaurantManagement.Modules.Identity/Services/IPasswordResetService.cs:7,10`, `IPasswordAuditRecorder.cs:3`, `IPasswordChangeService.cs:7-10`, `Models/PasswordRulesAttribute.cs:7-10` | `///` comments on Identity types. The clean-code skill allows them only on Shared contracts, and task IDs such as "(task T6)" will go out of date. | Remove the `///` blocks, or at least the task IDs. |
| 6 | `src/RestaurantManagement.Shared/RestaurantManagement.Shared.csproj:21` | The `EmbeddedResource` line is needed, but T1's "Files" row does not list it. | Nothing to change now. Future plans should list csproj edits. |
| 7 | `src/RestaurantManagement.Modules.Identity/Services/PasswordChangeService.cs:27` | `ILogger<PasswordChangeService>? logger = null` exists only so old tests that pass four arguments still compile. Without a logger, audit failures go to `NullLogger` and nobody sees them. `PasswordResetService` takes its logger as a required parameter. Dependency injection gives the real logger, so production is fine. | Make the parameter required once those tests may be changed. Record a backlog item. |
| 8 | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordAuditFailureTests.cs`, `PasswordResetInactiveUserTests.cs` | The fix round added two test files and changed behaviour. Neither file is in any task's "Files" row or in `scenarios.md`. f69d951 and b013433 have no `Scenarios:` line. They are in an allowed folder and test the review fixes. | Accept as review fixes. They are recorded in the Process notes. |
| 9 | `tests/RestaurantManagement.Web.Tests/StartupConfigurationTests.cs:14` | In this review, two hash-key startup tests failed in one full run and passed on rerun. Earlier, the JwtKey test failed the same way. The class probably shares state with tests running in parallel. | Record a backlog item (tech-debt) for the flaky startup tests. |
| 10 | `src/RestaurantManagement.Modules.Identity/Services/PasswordResetService.cs:100` | The reset guard now checks `IsActive` but not `IsVerified`. Spec rule 12 requires both. A code can only be issued to a verified user, and nothing un-verifies a user, so this cannot be hit today. | Add `!user.IsVerified` next time this file changes, to match the forgot-password check on line 54. |

## Check before merging
- **Audit entry after the password is saved** (`PasswordResetService.cs:114-125`, `PasswordChangeService.cs:49-65`): the password update and session revocation share one transaction. If the audit write then fails, the error is logged and the user gets success. The password change stays, but it has no audit entry. `backlog/improvements/2026-10-16-audit-entries-through-outbox.md` tracks writing entries in the same save.
- **Two new anonymous endpoints** (`AuthController.cs:125-142`): forgot and reset password are protected only by IP (Internet Protocol address) rate limits. Deactivated users are now rejected. The timing leak is open in `backlog/bugs/2026-10-16-forgot-password-response-time-reveals-account.md` as a launch blocker. It is marked `high`, but the backlog skill rates security problems `critical`, so the user may want to raise it.
- **Audit entries under an explicit organization** (`PasswordAuditRecorder.cs:30-41`): this is the only place that writes tenant data for an anonymous caller. Organization IDs come only from the database `user_roles` rows of the loaded user.

## Pull request description

**Forgot and change password (Identity)**

Users who forget their password can now get a 6-digit code by email and set a new password with it. Signed-in users can change their password by giving the current one. Both actions end every login session of the user and are written to the audit log.

What changed:
- **Shared:** new `IEmailService.SendPasswordResetEmailAsync` with an HTML template. The development email log no longer prints email bodies. Three new rate limit policies (`auth-forgot-password` 3/600 s, `auth-reset-password` 10/600 s, `auth-change-password` 10/60 s) with options validation. New audit actions `password.reset` and `password.changed`.
- **Identity:** new `password_reset_codes` table (codes stored only as keyed hashes) and two `users` columns for the reset lockout. Migration `20261016180603_AddPasswordResetCodes`. `PasswordRulesAttribute` holds the registration password rules, now reused by reset and change. New `PasswordResetService`, `PasswordChangeService` and `PasswordAuditRecorder`. Endpoints `POST api/auth/forgot-password`, `POST api/auth/reset-password` (anonymous) and `POST api/auth/change-password` (signed in; clears the refresh cookie). Reset rejects a deactivated user with the generic 401. A failed audit write is logged and does not fail the request.
- **Web:** service registration and rate limit wiring.

Spec Outcomes delivered: all 12 (#1 to #12), covered by scenarios 1 to 40, plus two review-fix test files.

Risk: High. New table and migration (the database holds no real data, no backfill). Two new public endpoints. Password and session changes. Shared contract changes (`IEmailService`, rate limit options, audit actions). Audit entries are written under an explicit organization scope, one per organization the user holds a role in (decided by the user at Gate 1).

Out of scope: access tokens already issued stay valid until they expire (`backlog/improvements/session-redesign.md`). Breached-password checks (`backlog/improvements/breached-passwords.md`). Forgot-password timing leak, a launch blocker (`backlog/bugs/2026-10-16-forgot-password-response-time-reveals-account.md`). `backlog/features/password-reset.md` was removed because it is fully solved.

### Process notes
History is kept as it is: nothing was amended or rebased for this list.
1. Baseline commit 9b52a81: the `Scenarios: 1–40` line in its body was guessed, not checked against `scenarios.md`. It happens to match: `scenarios.md` has 40 scenarios.
2. B3: the implementer was denied a `git checkout` to undo a formatter change to a test file (`OrganizationServiceAuditTests.cs`). It then ran `git restore`, which does the same thing, instead of stopping. That file has no changes on this branch.
3. B4: the T4 commit (a556047) was amended locally to add its Delivered note and tick.
4. B5: T6 and T7 were committed together in 736ccf6 instead of one commit per task.
5. B7: 58afe99 set Status to Done but did not include its Definition of Done ticks. Follow-up commit f238ed5 added them.
6. d018f57 edits a test after Gate 2: `RetireVerificationCodesMigrationTests` now migrates to the current schema after the retire migration. The user explicitly approved this, because T3's new `users` columns broke a test that ran today's services on an old schema.
7. 888e2da (tester model in `.claude` and the fixtures table in the testing skill) and 060383d (backlog updates) are on this branch. They were committed at the user's request before implementation.
8. `StartupConfigurationTests` fails only sometimes. During B5/B6, `Startup_JwtKeyMissingOrUnder32Bytes_FailsNamingJwtKey(key: "")` failed once in a full run. In the final review, `Startup_VerificationHashKeyEmpty_FailsNamingHashKey` and `Startup_VerificationHashKeyEmptyInProduction_FailsNamingHashKey` failed once in a full run. Every rerun passed, and the file is not changed on this branch.
9. Review fix round (f69d951, b013433, 387e68e): these commits came after Status was set to Done and the Definition of Done was ticked. `plan.md`, the batch files and `scenarios.md` were not updated for them. The two new test files (`PasswordAuditFailureTests.cs`, `PasswordResetInactiveUserTests.cs`) were added after Gate 2 without being added to `scenarios.md`. f69d951 and b013433 have no `Scenarios:` line. `PasswordChangeService` gained an optional logger parameter so the existing tests that pass four arguments compile without being edited.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
