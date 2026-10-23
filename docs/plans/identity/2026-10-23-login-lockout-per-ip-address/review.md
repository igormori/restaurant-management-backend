# Review: Login lockout per IP address

**Verdict:** Ready to merge
**Build:** pass (0 errors, 5 old warnings in Web.Tests)   **Tests:** full suite runs in CI (Continuous Integration) — merge only when it is green

Also checked (fourth review, after the fix commit `dae79f8`):
- Previous Fix first #1 is fixed:
  - `PendingEmailStore.cs` is deleted, and its code is merged into `PendingEmailQueue.cs`.
  - `PendingEmailQueue` is now a normal (not static) class with constructor `(IdentityDbContext db, PendingEmailPurpose purpose)`.
  - `MaxAttempts`, `BatchSize` and `FailedRetention` stay public, so the tests that read them still compile.
  - `Services/Registration/` holds 10 files again, which is the limit.
  - No `src/`, test or doc file names `PendingEmailStore` any more. The B2 Delivered note mentions it only as history.
- The moved code is the same as before, line for line. Only `PendingEmailQueue.BatchSize`, `MaxAttempts` and `FailedRetention` became `BatchSize`, `MaxAttempts` and `FailedRetention` inside the class. The class comment now says "three email processors".
- Previous Watch out #1 is fixed: the three processors no longer have an empty line after the class's opening brace.
- The B2 Delivered note records the fix round.
- Security matrix check (`check-security-matrices.py`): passes. `dae79f8` changes no behaviour, so no row needed a change.
- Test files since the baseline `abaa7eb`: only the two edits the user approved changed:
  - B1's raw SQL user INSERTs.
  - `PendingEmailsTableTests.cs:74`, which adds `send_started_at` to the column list.
  
  `dae79f8` changes no test. No test was edited, deleted, skipped or weakened.
- No package added or updated, so `dotnet list package --vulnerable` was not needed.
- Commits: Conventional Commits, every summary is at most 72 characters, scenario numbers are in the body, nothing was amended.
- No secrets or personal data in the diff.
- The uncommitted backlog deletion belongs to the spec writer and is not a finding.

## Check before merging
- **Shared queue code in `PasswordResetEmailProcessor` and `VerificationEmailProcessor`.** These security flows were already live, and the refactor changed them. Only the processors' existing unit tests protect the "send at most once" and retry behaviour. They were not run locally after `dae79f8`, so CI is their first run.
- **`LoginLockout.LockAccountWhenLimitReachedAsync` (account lock and its email).**
  - One conditional update decides which request locks the account.
  - Everything after that update commits in one transaction.
  - These PostgreSQL tests were not run locally, so CI is their first run: `AccountLockedEmailEndpointTests` (L34) and the D32 and D33 schema tests.
- **Four Identity migrations in one branch.**
  - The first creates `login_failures` and drops `users.failed_attempts`.
  - The other three add `users.account_locked_email_at`, the `failed_at` index and `pending_emails.send_started_at`.
  - There is no data before launch. The chain must still apply in order on a clean database, as the Web.Tests fixtures do.

## Pull request description
**Login lockout per IP address (Identity)**

Before this change, 5 wrong passwords locked an account for everyone for 15 minutes. Anyone who knew an Owner's email could keep the Owner out all day. Login now has two locks:
- **Per address:** 5 wrong passwords for one account from one IP (Internet Protocol) address lock only that address for 15 minutes. Other addresses can still sign in.
- **Whole account:** 20 wrong passwords within 60 minutes lock the account for 15 minutes, from any mix of addresses.
  - One address adds at most 5, so this lock needs at least 4 addresses.
  - The owner gets one email: "someone tried to sign in; if it was not you, reset your password".
  - At most one such email goes out per account per 24 hours. It holds no code, no link and no address.

Wrong passwords during a lock are not counted. Every refusal returns the same 401. A password reset clears both locks. The client address follows the same trusted-proxy rules as rate limiting.

**Changes per module**
- **Identity:**
  - New `login_failures` table, indexed by user and time, and by time alone.
  - The `users.failed_attempts` column is dropped.
  - New columns: `users.account_locked_email_at` and `pending_emails.send_started_at`. Four migrations in total.
  - New `LoginLockout`, used by `SessionService`. The whole-account lock and its email are saved in one transaction. `PasswordResetService` now deletes the user's failures.
  - New `AccountLockedEmailProcessor` and its English template, run by `PendingEmailWorker`. Its crash guard uses its own `SendStartedAt` mark.
  - `PendingEmailQueue` (`Services/Registration/`) now holds the claim, retry, delete and purge code. The three email processors share it. Behaviour is unchanged.
  - Updated docs:
    - the Identity security matrix (L4–L37, D29–D33)
    - the ASVS (Application Security Verification Standard) gap report: #7 and V6.1.1 are now Covered
    - `Modules.Identity/CLAUDE.md`
- **Shared:** `SecurityOptions` gains `AccountMaxFailedLoginAttempts` (20) and `AccountFailedLoginWindowMinutes` (60).
- **Web:** `appsettings.json` gets the two new values. `Program.cs` registers the new processor.

**Spec Outcomes delivered:**
- #1 One address cannot lock others out.
- #2 Spread-out attacks are stopped.
- #3 No lock is revealed.
- #4 The lock email is sent once.
- #5 Attempts during a lock are not counted.
- #6 Address rules are shared with rate limiting.
- #7 The shipped limits match the documentation.

**Risk and flags (plan.md: High)**
- A new table, a dropped column and two new columns.
- Lockout behaviour changes on the public `POST /api/auth/login` endpoint.
- The Shared options class changes, so the full suite runs.
- A new email goes to account owners.
- The existing password-reset and verification email processors were refactored to share the queue code.

**Notes**
- The user approved two edits to existing tests. No assertion was weakened:
  - Four Web.Tests files changed only their raw SQL user INSERTs, to drop the removed column.
  - `PendingEmailsTableTests` now lists the new `send_started_at` column.
- The backlog item `backlog/improvements/2026-10-16-login-lockout-per-ip-address.md` is fully solved and removed.
- New backlog item: `backlog/improvements/2026-10-23-even-out-login-timing-for-unknown-emails.md`. A wrong password for a real account now does more database work than one for an unknown email.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
