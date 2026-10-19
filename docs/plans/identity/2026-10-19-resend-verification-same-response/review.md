# Review: Resend verification answers every email the same way

**Verdict:** Fix first — the code is sound, but two Overall DoD (Definition of Done: the checks that must pass to count as finished) boxes are still unticked and one new backlog item points at lines that no longer exist
**Build:** pass (0 warnings, 0 errors)   **Tests:** 1039 passed, 0 failed (Shared 159, Organization 130, Menu 63, Identity 301, Web 386)

Matrix check: `python3 .claude/scripts/check-security-matrices.py` passes ("Security matrices in step with the tests").
Tests since baseline `016875f`: only the two one-line `IEmailService` argument removals the user approved (`TestVerificationService.cs`, `RetireVerificationCodesMigrationTests.cs`). Nothing deleted, skipped or weakened.
No packages added, so the vulnerable-package check was not needed. No secrets in the diff.

## Fix first
| # | File:line | Problem | Change |
|---|---|---|---|
| 1 | `docs/plans/identity/2026-10-19-resend-verification-same-response/plan.md:43`, `:45` | Two Overall DoD items are unticked, but both are met: the matrix check passes, and the Identity `CLAUDE.md` now describes the queued resend flow (commit `07c2eaf`) | Tick both boxes. Plan Status already says Done |
| 2 | `backlog/tech-debt/2026-10-19-unused-resend-verification-messages.md:14-15` | The evidence points at `VerificationService.cs:139` and `:142`, but this branch removed that code, so the lines no longer exist. A third use is missing: `tests/RestaurantManagement.Modules.Identity.Tests/AuthControllerTests.cs:47` still returns `"VerificationCodeResent"` from a mock, so the criterion "no use in `tests/`" means editing a test | Replace both lines with the `.resx` key lines, add the `AuthControllerTests.cs:47` mock string as evidence, and say in the criteria that the mock string changes too |

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `src/RestaurantManagement.Modules.Identity/Services/VerificationService.cs:19` | `_logger` is assigned and never used. Delivered note T3 says why: the test helper always passes a logger | Remove it in a later cleanup, together with the test helper argument (needs the user's approval for the test edit) |
| 2 | `src/RestaurantManagement.Modules.Identity/Migrations/20261019185342_AddPasswordResetRequestPurpose.cs:18` | EF Core (Entity Framework Core) generated `defaultValue: 0`, which matches no enum value. A row written without a purpose (raw SQL, for example) is never processed, and neither purge removes it, because both filter by purpose | Fine for now because the table is empty. Think about a check constraint `purpose IN (1, 2)` when the queue is renamed (backlog item `2026-10-19-pending-email-queue-has-password-reset-names.md`) |
| 3 | `src/RestaurantManagement.Modules.Identity/Services/VerificationEmailProcessor.cs:14` | Most of the class (due query, claim, purge, retry schedule, delete) is a copy of `PasswordResetEmailProcessor`, so a fix to one can be missed in the other | When the queue is renamed, move the shared queue mechanics into one class and keep only eligibility and the email in each processor |
| 4 | `src/RestaurantManagement.Modules.Identity/Services/VerificationEmailProcessor.cs:90`, `:141`, `:164` | `HandleAsync` is about 35 lines (the limit is 30). `StoreCodeAsync` and `RegisterFailureAsync` take 5 parameters (the limit is 4). The reset processor has the same shape | Pull the eligibility check out of `HandleAsync`, and pass the request instead of separate ids. Style only |

## Check before merging
1. `VerificationService.ResendVerificationCodeAsync`: this public endpoint now writes one database row per request for any email. Only the existing rate limit (RS5, the matrix row for 429 Too Many Requests) caps how fast the queue can grow. This is the same exposure "forgot password" already accepts.
2. `PasswordResetEmailProcessor` purpose filters (due query, claim, purge): if any filter were missing, a reset pass would take verification rows, or purge them. Matrix row P28 covers both, and P1 to P27 still pass.
3. `PasswordResetEmailWorker.RunOnceAsync`: both processors now run in one pass, each in its own try/catch. The verification email is now sent only by the worker, so the setting `PasswordResetEmails:Enabled=false` now also stops verification resends.

## Pull request description
**Resend verification answers every email the same way**

`POST /api/auth/resend-verification` used to give an unverified account a different message
("code resent"), a slower answer, and a 500 error if the email failed to send. Comparing those
told anyone which emails belong to unverified accounts. Now the request only adds a row to the
pending-email queue (the queue "forgot password" already uses) and always returns the same 200
acknowledgement. A background processor later decides who gets a code, sends it, and retries
if sending fails.

**Identity**
- Queue rows (`password_reset_requests`) now record their purpose: password reset or verification. The migration adds a required `purpose` column. The table has no data, so there is no backfill
- The password reset processor takes, claims and purges only password reset rows
- New `VerificationEmailProcessor`: checks the account when it handles the row (unverified, not locked, outside the 60 s wait), cancels older codes, stores only the code's fingerprint, sends the email, and retries after 1, 2, 4 and 8 minutes. A row is marked failed after the 5th failure. Logs hold the user ID only
- `PasswordResetEmailWorker` runs both processors in each pass, and a failure in one does not stop the other
- `VerificationService` no longer takes an email service. Resend never reads accounts and never sends
- Identity `CLAUDE.md` and `docs/security/identity-matrix.md` are updated: RS1 to RS6 rewritten, RS7 to RS11, P28 and D25 to D27 added, V7 and V11 updated

**Web**: the new processor is registered in `Program.cs` (one line).

**Spec Outcomes delivered:** 1 same answer for every email · 2 the answer never waits for the email · 3 the same database commands for every email · 4 a failed send is retried · 5 an eligible account gets exactly one code, and it verifies · 6 no email for other accounts · 7 matrix rows rewritten, each naming a passing test.

**Risk:** High (from `plan.md`). It changes the schema of the pending-email queue and the behaviour of a public endpoint. There is no role, auth or tenancy change and no raw SQL (Structured Query Language).

**Backlog:** solves `backlog/bugs/2026-10-17-resend-verification-reveals-unverified-account.md` (the deletion is left for you to commit). Adds `backlog/tech-debt/2026-10-19-pending-email-queue-has-password-reset-names.md` and `backlog/tech-debt/2026-10-19-unused-resend-verification-messages.md`.

**Test edits after Gate 2 (approved by the user):** one line removed from each of `TestVerificationService.cs` and `RetireVerificationCodesMigrationTests.cs`. Both passed an `IEmailService` argument that the service constructor no longer takes.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
