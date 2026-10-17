# ASVS small fixes and missing tests for Identity

**Status:** proposed
**Severity:** low
**Origin:** docs/security/identity-asvs-gap-report.md · Summary #15, #17–#19, #21–#28
**Date:** 2026-10-16

## Problem
Small Identity gaps against OWASP ASVS 5.0 Level 2. Each is one small change, suited to one
small batch (one commit per line below).

**Small fixes (code changes):**
- #18: no JWT algorithm allowlist; only `alg: none` is known to be refused
- #17: refresh cookie is named `refresh_token`, without the `__Secure-` prefix
- #19: email addresses are written to the logs when a verification email fails to send
- #15: logging in again on a browser leaves that browser's previous refresh token valid until it expires

**Missing tests (code already does it; needs a matrix row and a test):**
- #21: register cannot set protected fields (verified, active, roles)
- #22: codes and refresh tokens come from a secure random generator (refresh tokens 64 bytes)
- #23: passwords are checked exactly as typed: spaces and casing kept
- #24: an access token used before its `nbf` (not before) time is refused
- #25: `jku`, `x5u` and `jwk` headers in an access token are ignored
- #26: every Identity endpoint takes secrets in the body or cookie, never the URL
- #27: a freshly migrated database has no users (no default accounts)
- #28: refresh fingerprints use SHA-256 and access tokens HS256

## Evidence
- `src/RestaurantManagement.Web/Program.cs:78-88` — `TokenValidationParameters` without `ValidAlgorithms`
- `src/RestaurantManagement.Modules.Identity/Controllers/RefreshTokenCookie.cs:10` — `Name = "refresh_token"`
- `src/RestaurantManagement.Modules.Identity/Services/RegistrationService.cs:100`, `VerificationService.cs:138` — `{Email}` in the error log
- `src/RestaurantManagement.Modules.Identity/Controllers/AuthController.cs:42-46` — login never reads the existing cookie
- `src/RestaurantManagement.Modules.Identity/Services/RefreshTokenService.cs:143` — 64 random bytes, untested

## Acceptance criteria
- [ ] Access tokens signed with any algorithm other than HS256 are refused
- [ ] The refresh cookie is named `__Secure-refresh_token`; the old name is no longer read or set
- [ ] A failed verification email logs the user ID, never the email address
- [ ] Logging in with an existing refresh cookie revokes that cookie's token
- [ ] Each "missing test" line has an Identity matrix row and a passing test
- [ ] The gap report summary rows #15, #17–#19 and #21–#28 can be marked Covered

## Related
- backlog/improvements/session-redesign.md
