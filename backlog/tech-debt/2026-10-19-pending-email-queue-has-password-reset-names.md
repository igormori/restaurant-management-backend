# Pending-email queue still carries password-reset names

**Status:** proposed
**Severity:** low
**Origin:** docs/plans/identity/2026-10-19-resend-verification-same-response/plan.md · T1, T2
**Date:** 2026-10-19

## Problem
Once resend verification uses the same queue as forgot password, the queue holds both password
reset and verification rows, but its table, entity, worker and settings are still named after
password reset. Readers will assume the queue only sends reset emails.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Entities/PasswordResetRequest.cs:5` — queue entity named for password reset
- `src/RestaurantManagement.Modules.Identity/Data/IdentityDbContext.cs:80` — table `password_reset_requests`
- `src/RestaurantManagement.Modules.Identity/Services/PasswordResetEmailWorker.cs:8` — worker that will run both processors
- `src/RestaurantManagement.Web/Program.cs:255` — settings `PasswordResetEmails:Enabled` and `PasswordResetEmails:IntervalSeconds`

## Acceptance criteria
- [ ] Queue table, entity, worker and settings have neutral names (for example "pending emails")
- [ ] Matrix rows and tests that name the old classes are updated; `python3 .claude/scripts/check-security-matrices.py` passes
- [ ] Done before launch, while the table can be renamed without keeping data

## Related
None
