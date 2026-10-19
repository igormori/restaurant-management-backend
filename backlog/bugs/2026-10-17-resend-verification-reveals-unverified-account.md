# Resend verification reveals which emails have an unverified account

**Status:** proposed
**Severity:** critical
**Origin:** docs/plans/identity/2026-10-17-forgot-password-same-response-time/plan.md · Planning
**Date:** 2026-10-17

## Problem
"Resend verification" answers an unverified account differently from every other email. It returns a different message, takes longer because it waits to send the email, and returns a 500 error when sending fails. An unknown email, a verified account, a locked account and one within the 60 s wait all get the same quick acknowledgement. Anyone can therefore learn which emails belong to unverified accounts. This is the same kind of leak the forgot-password fix closes.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Services/VerificationService.cs:142` — an unverified account gets `VerificationCodeResent`; other cases get `VerificationResendAcknowledged` (lines 84, 87, 91, 101)
- `src/RestaurantManagement.Modules.Identity/Services/VerificationService.cs:130-140` — the request waits for the email and throws `BusinessException(500)` when it fails
- `docs/security/identity-matrix.md` rows RS1, RS2, RS6 — the different answers are recorded as expected behaviour

## Acceptance criteria
- [ ] Resend verification returns the same status and body for every email, whatever the account state or the email outcome
- [ ] The request does the same work for every email and never waits for the email to be sent (for example through the pending email queue built for forgot password)
- [ ] A failed verification email is retried later and not lost
- [ ] Matrix rows RS1–RS6 updated with their tests

## Related
backlog/improvements/2026-10-16-audit-entries-through-outbox.md
