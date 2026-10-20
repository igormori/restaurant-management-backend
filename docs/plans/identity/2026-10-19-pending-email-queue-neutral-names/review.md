# Review: Pending-email queue gets neutral names

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 1041 passed, 0 failed (Shared 159, Organization 130, Menu 63, Identity 303, Web 386)

Security matrix check (`python3 .claude/scripts/check-security-matrices.py`): passes. Since the baseline commit `aed3670`, the only test change is the planned removal of the TEMPORARY `PasswordResetEmails__Enabled` line in `TestHostSettings.cs`, which Gate 2 approved. Searching `src/`, `tests/`, `docs/security/`, `backlog/` and `.github/` for the old names finds nothing outside earlier migrations. They still appear in older plan documents, which are history. No package was added, so the vulnerable-package check was not needed.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `src/RestaurantManagement.Modules.Identity/Migrations/20261020001004_RenamePasswordResetRequestsToPendingEmails.cs:15` | The migration drops `password_reset_requests` and creates `pending_emails`. It does not rename the table. Step 5 of the T1 guide asked for a rename. The ef-migration skill says a drop-and-recreate must not stand in for an intended rename, but it also says never to hand-edit a generated migration. Spec Rule 3 allows it because the database holds no data, and the Delivered note records the choice. Any row queued in a local or staging database is lost, and `Down()` (line 51) loses rows the same way | Accept for now (not launched yet). Mention it in the pull request so anyone with a local database knows queued emails are dropped |
| 2 | `src/RestaurantManagement.Modules.Identity/Services/VerificationEmailProcessor.cs:16` | The verification processor keeps private copies of `MaxAttempts` and `BatchSize` from `PendingEmailQueue`. `PasswordResetEmailProcessor` and its own `FailedRetention` use read `PendingEmailQueue.*` directly, so the two processors are written differently | In a later change, delete the two private constants and read `PendingEmailQueue.MaxAttempts` and `PendingEmailQueue.BatchSize` directly |
| 3 | `tests/RestaurantManagement.Web.Tests/Identity/PendingEmailsTableTests.cs:9` | The class comment still says "queued forgot-password requests", but the table now holds every pending email. Test files are read-only after Gate 2, so the implementer was right not to change it | At the next approved test change, update the comment to "pending emails of any purpose" |
| 4 | `src/RestaurantManagement.Modules.Identity/Entities/PendingEmail.cs:5` | "only the processor decides": there are now two processors, one for each purpose | Say "only the processor for its purpose decides" next time the file changes |

## Check before merging
1. **Renamed operator settings** (`src/RestaurantManagement.Web/Program.cs:255`). The app no longer reads `PasswordResetEmails:Enabled` or `PasswordResetEmails:IntervalSeconds`. Missing keys fall back to the defaults (on, 5 seconds), with no warning. If any environment switched the worker off or changed its interval with the old keys (for example `PasswordResetEmails__Enabled=false`), that setting is now silently ignored. Check your deployment settings and secrets, and rename them to `PendingEmails__*`.
2. **Table dropped and recreated** (see Watch out 1). This is harmless only while no environment holds real queued emails. Confirm that is true before you apply the migration anywhere shared.
3. **Uncommitted backlog deletion.** `backlog/tech-debt/2026-10-19-pending-email-queue-has-password-reset-names.md` is deleted but not committed. This feature meets all three of its acceptance criteria. Commit the deletion with this branch.

## Pull request description
**Pending-email queue gets neutral names**

The queue of pending emails now sends both password reset and verification emails, but everything about it was still named after password reset. This pull request renames the queue, its sending job and its settings to neutral "pending email" names. Behaviour does not change: the same emails are sent, at the same times, with the same retries and the same answers to users.

**Identity**
- Entity `PasswordResetRequest` is now `PendingEmail`, and enum `PasswordResetRequestPurpose` is now `PendingEmailPurpose` (values unchanged).
- DbSet `PasswordResetRequests` is now `PendingEmails`. Table `password_reset_requests` is now `pending_emails`, with key `pk_pending_emails` and indexes `ix_pending_emails_next_attempt_at` and `ix_pending_emails_failed_at`.
- The queue-wide limits (5 attempts, 100 per pass, failed rows kept 7 days) moved from `PasswordResetEmailProcessor` to a new `PendingEmailQueue` class, which both processors read.
- `PasswordResetEmailWorker` is now `PendingEmailWorker`.
- Names that really are about password reset keep their names: `PasswordResetEmailProcessor`, `PasswordResetCode`, `PasswordResetService`.
- New migration `RenamePasswordResetRequestsToPendingEmails`. EF Core (Entity Framework Core) generated it as a drop and recreate, which is allowed because the database holds no real data (spec Rule 3). Queued rows in any existing database are lost.
- Security matrix rows D18 to D26 now name the renamed tests and use "pending email" wording.

**Web**
- The settings `PasswordResetEmails:Enabled` and `PasswordResetEmails:IntervalSeconds` are now `PendingEmails:Enabled` and `PendingEmails:IntervalSeconds`, in `Program.cs` and `appsettings.json`. Defaults and the startup refusal for an interval of 0 or below are unchanged; the error message now names the new key.

**Spec Outcomes delivered**
1. The queue, sending job and settings have neutral names: a search finds none of the old names outside earlier migrations.
2. Password reset and resend verification emails still go out and retry as before: the existing tests pass with only names changed.
3. The security rules list matches the new names: the matrix check passes.
4. Build and the full test suite pass: 1041 tests.

**Risk and flags (from plan.md):** High risk.
- T1 changes the schema: the table and its indexes are renamed on an empty database, with no data copied.
- T2 renames the operator settings: deployments must set `PendingEmails:*`. The old `PasswordResetEmails:*` keys are ignored without a warning.

**Backlog:** solves and removes `backlog/tech-debt/2026-10-19-pending-email-queue-has-password-reset-names.md`.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
