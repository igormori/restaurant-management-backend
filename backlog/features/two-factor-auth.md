# Two-factor authentication

**Status:** proposed
**Severity:** high
**Origin:** docs/security/identity-asvs-gap-report.md · Summary #1 (ASVS V6.3.3, V6.4.4)
**Date:** 2026-10-16

## Problem
A stolen password is enough to log in, including for Owner and Admin accounts, who control
money, staff and settings. ASVS 5.0 Level 2 requires multi-factor authentication, or a written
rationale with other safeguards. Decided by the user on 2026-10-16: build it after the MVP.

Recommended scope (to confirm in the spec):
- Authenticator app (TOTP, time-based one-time password) with one-time backup codes.
- Required for Owner and Admin, optional for Manager, not required for Cashier, Waiter and
  Kitchen (shared tablets, no permissions today). The reason for not requiring it is written in
  the Identity matrix.
- Losing the phone: recovery with a backup code; no recovery path that skips the second factor
  (password reset must not turn it off).

## Evidence
- `docs/security/identity-matrix.md` — no second factor anywhere; login (section 4) issues tokens on password alone
- `docs/security/identity-asvs-gap-report.md` — Summary #1, the only High gap with no code or rationale

## Acceptance criteria
- [ ] Owner and Admin cannot get tokens without a second factor once it is required for them
- [ ] Other roles can turn it on; once on, it is always asked
- [ ] Backup codes are single use and stored only as hashes
- [ ] Password reset does not remove or skip the second factor
- [ ] Until it ships, the rationale for not having MFA is written in the Identity matrix (ASVS V6.3.3)

## Related
- backlog/features/audit-log.md
