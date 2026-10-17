# Sessions end after 7 days unused or 30 days after login

**Status:** proposed
**Severity:** high
**Origin:** docs/security/identity-asvs-gap-report.md · Summary #2 and #14 (ASVS V7.3.1, V7.3.2, V7.1.1)
**Date:** 2026-10-16

## Problem
A session never has to end. Each refresh gives the new refresh token a fresh 30-day expiry, so a
device (or a stolen refresh cookie) that keeps refreshing stays signed in forever, unless reuse
is detected. Approved by the user on 2026-10-16:
- **Inactivity:** a session not refreshed for 7 days ends; the user logs in again.
- **Absolute:** every session ends 30 days after its login, even on a device used every day.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Services/RefreshTokenService.cs:145` — every new token, including a rotation, expires `now + RefreshTokenExpireDays`
- `src/RestaurantManagement.Web/appsettings.json:40` — `RefreshTokenExpireDays: 30`
- `docs/security/identity-matrix.md:110` — F1 says "expiry renewed" on refresh

## Acceptance criteria
- [ ] A refresh token not used for 7 days is refused with the usual 401, and the user must log in
- [ ] A session refreshed regularly is refused 30 days after its login; refreshing never moves that end date
- [ ] Choosing an organization keeps the session's original login time (it does not restart the 30 days)
- [ ] Both limits come from configuration; the app refuses to start when either is not positive or inactivity exceeds the absolute limit
- [ ] Identity matrix: header lists both limits with the reason (risk decision for ASVS V7.1.1), F1 no longer says "expiry renewed", new rows with tests, shipped values guarded like T17
- [ ] Cleanup (T9) still keeps revoked tokens long enough for reuse detection

## Related
- backlog/improvements/session-redesign.md (access tokens still work up to 15 min after a session ends)
