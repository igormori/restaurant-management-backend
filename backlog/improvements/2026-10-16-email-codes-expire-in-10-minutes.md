# Emailed codes expire after 10 minutes

**Status:** proposed
**Severity:** medium
**Origin:** docs/security/identity-asvs-gap-report.md · Summary #8 (ASVS V6.5.5)
**Date:** 2026-10-16

## Problem
Email verification and password reset codes work for 15 minutes. ASVS 5.0 Level 2 allows at most
10 minutes for codes sent by email. Approved by the user on 2026-10-16: shorten to 10 minutes.
Only the code lifetime changes: the 15-minute lockout after 5 wrong codes and the 60-second
resend wait stay as they are.

## Evidence
- `src/RestaurantManagement.Web/appsettings.json:48` — `VerificationCodeExpiryMinutes: 15`, used by verification and reset codes
- `src/RestaurantManagement.Shared/Options/SecurityOptions.cs:7` — default of 15
- `tests/RestaurantManagement.Web.Tests/Identity/ShippedLimitsTests.cs:46` — guards the shipped 15 (matrix T16)
- `docs/security/identity-matrix.md:34` — limits header says codes expire after 15 min

## Acceptance criteria
- [ ] Verification and reset codes are refused 10 minutes after they are sent, and the emails say 10 minutes
- [ ] The default and the shipped setting are both 10
- [ ] Matrix header and T16 say 10 minutes, and the T16 test proves it
- [ ] Lockout (15 min) and resend wait (60 s) are unchanged

## Related
None
