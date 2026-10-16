# Verification lockout reveals that an account exists

**Status:** proposed
**Severity:** medium
**Origin:** docs/plans/identity/2026-10-16-password-reset/plan.md · planning
**Date:** 2026-10-16

## Problem
Verify and resend-verification answer an unknown email with a generic 401 or acknowledgement, but a
registered email whose verification is locked gets a distinct 423 with the unlock time. Anyone can
tell that the email has an account, which the module's uniform-response rule tries to prevent.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Services/VerificationService.cs:47` — 423 for a locked, existing user on verify
- `src/RestaurantManagement.Modules.Identity/Services/VerificationService.cs:90` — 423 for a locked, existing user on resend, while unknown emails get the acknowledgement
- `src/RestaurantManagement.Modules.Identity/Services/VerificationService.cs:169` — the attempt that trips the lock also returns 423
- `docs/security/identity-matrix.md` row V11 — found again in the Identity security review (2026-10-16), marked 🐞 until fixed

## Acceptance criteria
- [ ] Verify returns the same status and body for an unknown email and a locked account
- [ ] Resend returns the same acknowledgement for an unknown email and a locked account
- [ ] Matrix row V11 is ✅ and names the tests that prove both

## Related
None
