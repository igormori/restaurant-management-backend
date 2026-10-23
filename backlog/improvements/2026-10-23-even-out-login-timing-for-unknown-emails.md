# Even out login timing for unknown emails

**Status:** proposed
**Severity:** low
**Origin:** docs/plans/identity/2026-10-23-login-lockout-per-ip-address/review.md · Watch out #1
**Date:** 2026-10-23

## Problem
A login with an unknown email does the dummy password hash and no database writes. A wrong password
for a real account now also reads and writes login failures, checks the locks and may queue an
email: about 5 to 8 database round trips. The difference in response time can tell an attacker
which emails have an account. The rate limits slow this down but do not remove it.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Services/Sessions/SessionService.cs:86-89` — an unknown email only verifies the dummy hash, and `RecordFailedAttemptAsync` runs only when `user != null`
- `docs/security/identity-matrix.md:103` — row L6 promises that the timing matches a real account

## Acceptance criteria
- [ ] An unknown email does database work comparable to a wrong password for a real account (same number of round trips)
- [ ] A test or measurement shows the two response times differ by no more than the normal variation
- [ ] Every refusal still returns the same 401

## Related
None
