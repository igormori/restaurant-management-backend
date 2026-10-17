# Reject breached passwords

**Status:** proposed
**Severity:** high
**Origin:** docs/security/identity-asvs-gap-report.md · Summary #3, #4, #20 (ASVS V6.2.4, V6.2.11, V6.2.12, V6.1.2)
**Date:** 2026-10-16

## Problem
Password rules check complexity only; a common leaked password like `Summer2026!` is accepted.

## Fix
- On register, password change **and password reset**, check the password against Have I Been
  Pwned using the k-anonymity range API (only a hash prefix leaves the server). Its list also
  covers the top-3000 common passwords (ASVS V6.2.4).
- Decide behaviour if the service is unavailable (allow, and log).
- Also refuse a short list of product words (product name, role names such as "owner",
  "waiter", "restaurant"), kept in one documented place (ASVS V6.1.2, V6.2.11).

## Evidence
- `docs/security/identity-matrix.md` — R6–R9, P11, C4: only length and composition are checked

## Acceptance criteria
- [ ] A breached password is refused with 400 on register, reset and change
- [ ] Only the first 5 characters of the password's SHA-1 hash leave the server
- [ ] When the service is unavailable, the password is accepted and the failure is logged
- [ ] A password containing a listed product word is refused
- [ ] Identity matrix rows with tests

## Related
None
