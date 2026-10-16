# Security rule and code disagree on password hashing

**Status:** accepted
**Severity:** low
**Origin:** docs/plans/identity/2026-10-16-password-reset/plan.md · Gate 2 tests (tester)
**Date:** 2026-10-16

## Problem
The security skill said passwords must be hashed with ASP.NET Core Identity's `PasswordHasher`,
never custom hashing. The code hashes and checks passwords with BCrypt, and the password reset and
change plan (T7, T8) follows the code. Agents and reviewers reading the skill got a rule the code
does not follow, so every password change could be flagged as a violation, or a new feature could
mix two hashing schemes. BCrypt is also called directly from more than one class, so changing the
scheme later means finding every call.

## Decision
Keep BCrypt (2026-10-16). The security skill now requires BCrypt with work factor 12 or more,
through one password hashing interface, and passwords of 64 characters maximum. The code fix is
its own slice after password reset is merged (password reset was already being implemented).

## Evidence
- `.claude/skills/security/SKILL.md` section 5 — old rule: "Passwords hashed with ASP.NET Core Identity's `PasswordHasher`, never custom hashing"
- `src/RestaurantManagement.Modules.Identity/Services/RegistrationService.cs:49` — `BCrypt.Net.BCrypt.HashPassword`
- `src/RestaurantManagement.Modules.Identity/Services/PasswordVerifier.cs:12` — `BCrypt.Net.BCrypt.Verify`

## Acceptance criteria
- [x] The security skill names BCrypt (decision: keep BCrypt, 2026-10-16)
- [ ] All hashing and verification go through one interface; no service calls BCrypt directly,
      including the calls added by password reset
- [ ] Work factor 12 and a 64-character password maximum are set and covered by tests

## Related
- docs/plans/identity/2026-10-16-password-reset/plan.md (added more hashing calls)