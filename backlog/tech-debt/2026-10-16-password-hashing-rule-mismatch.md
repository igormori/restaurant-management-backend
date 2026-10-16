# Security rule and code disagree on password hashing

**Status:** proposed
**Severity:** low
**Origin:** docs/plans/identity/2026-10-16-password-reset/plan.md · Gate 2 tests (tester)
**Date:** 2026-10-16

## Problem
The security skill says passwords must be hashed with ASP.NET Core Identity's `PasswordHasher`,
never custom hashing. The code hashes and checks passwords with BCrypt, and the password reset and
change plan (T7, T8) follows the code. Agents and reviewers reading the skill get a rule the code
does not follow, so every password change can be flagged as a violation, or a new feature could
mix two hashing schemes.

## Evidence
- `.claude/skills/security/SKILL.md` section 5 — "Passwords hashed with ASP.NET Core Identity's `PasswordHasher`, never custom hashing"
- `src/RestaurantManagement.Modules.Identity/Services/RegistrationService.cs:49` — `BCrypt.Net.BCrypt.HashPassword`
- `src/RestaurantManagement.Modules.Identity/Services/PasswordVerifier.cs:12` — `BCrypt.Net.BCrypt.Verify`

## Acceptance criteria
- [ ] The security skill and the code name the same password hashing approach (either the skill
      accepts BCrypt, or the code moves to `PasswordHasher` in its own slice)

## Related
None
