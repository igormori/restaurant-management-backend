# Forgot-password response time reveals which emails have accounts

**Status:** proposed
**Severity:** critical
**Origin:** docs/plans/identity/2026-10-16-password-reset/review.md · Watch out #2
**Date:** 2026-10-16

**Launch blocker:** yes. Must be fixed before launch.

## Problem
Forgot-password returns the same body for every email, but the time it takes differs. A known, eligible email waits for the database write and the email send; an unknown email returns at once. Anyone can measure the difference and learn which emails have accounts. Fix: send the email through the outbox (Phase 0 — Task 11) so the request does the same work for every email.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Services/PasswordResetService.cs:54-78` — the known-email path saves the code and awaits `SendPasswordResetEmailAsync`; unknown, inactive, unverified and locked emails return before that
- `docs/plans/identity/2026-10-16-password-reset/review.md` — Watch out #2 raised the timing difference

## Acceptance criteria
- [ ] Forgot-password does not wait for the email to send: the email is written to the outbox and delivered from there
- [ ] The response time for a known email is not distinguishable from that of an unknown email
- [ ] A failed email delivery is retried later and not lost
- [ ] Done before launch

## Related
backlog/bugs/2026-10-16-verification-lockout-reveals-account.md
backlog/improvements/2026-10-16-audit-entries-through-outbox.md
