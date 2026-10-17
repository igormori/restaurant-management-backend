# Login lockout targets the attacker's IP address, not the account

**Status:** proposed
**Severity:** high
**Origin:** docs/security/identity-asvs-gap-report.md · Summary #7 (ASVS V6.1.1, V6.3.1)
**Date:** 2026-10-16

## Problem
After 5 wrong passwords the whole account is locked for 15 minutes, for everyone. Anyone who
knows an Owner's email can keep them out all day by sending 5 wrong passwords every 15 minutes.
Approved by the user on 2026-10-16 (no CAPTCHA for now):
1. **Per IP address:** 5 wrong passwords for an account from one IP address lock that IP address
   out of that account for 15 minutes. The real user on another IP address can still log in.
2. **Whole account, higher limit:** about 20 wrong passwords for an account within an hour, from
   any mix of IP addresses, lock the whole account for 15 minutes (stops attacks spread over many
   addresses).
3. **Email the user when their account gets locked:** "Someone tried to sign in to your account.
   If it wasn't you, reset your password." Password reset already clears the lock (P10).

## Evidence
- `src/RestaurantManagement.Modules.Identity/Services/SessionService.cs:175-180` — one counter per user; reaching it locks the account for everyone
- `src/RestaurantManagement.Web/appsettings.json:46-47` — `MaxFailedLoginAttempts: 5`, `LockoutDurationMinutes: 15`
- `docs/security/identity-matrix.md` — L7–L11 describe the account-wide lock

## Acceptance criteria
- [ ] 5 wrong passwords from one IP address refuse further logins to that account from that address for 15 min; a correct password from another address logs in
- [ ] The whole-account limit (number and window from configuration) locks the account for every address
- [ ] Every refusal keeps the same 401 `InvalidEmailOrPassword` as today (L5): no hint which lock applies
- [ ] The user gets one email when the whole account is locked; no email when only an IP address is locked; the email holds no code or link that unlocks without a password reset
- [ ] IP address comes from the same trusted-proxy rules as rate limiting (L17)
- [ ] Identity matrix: L7–L11, L18 and T16 updated; new rows with tests

## Related
- backlog/features/audit-log.md (lockouts should be recorded)
