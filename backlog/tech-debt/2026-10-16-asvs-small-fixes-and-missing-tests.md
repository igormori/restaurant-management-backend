# ASVS small fixes and missing tests for Identity

**Status:** proposed
**Severity:** low
**Origin:** docs/security/identity-asvs-gap-report.md · Summary #15, #17–#19, #21–#28
**Date:** 2026-10-16

## Problem
Small Identity gaps against OWASP ASVS 5.0 Level 2. Each is one small change, suited to one
small batch (one commit per line below). #17, #19 and #21–#23 are done.

**Small fixes (code changes):**
- #18: no JWT algorithm allowlist; only `alg: none` is known to be refused
- #15: logging in again on a browser leaves that browser's previous refresh token valid until it expires

**Missing tests (code already does it; needs a matrix row and a test):**
- #26: every Identity endpoint takes secrets in the body or cookie, never the URL
- #27: a freshly migrated database has no users (no default accounts)
- #28: refresh fingerprints use SHA-256 and access tokens HS256

## Evidence
- `src/RestaurantManagement.Web/Program.cs:78-88` — `TokenValidationParameters` without `ValidAlgorithms`
- `src/RestaurantManagement.Modules.Identity/Controllers/AuthController.cs:42-46` — login never reads the existing cookie

## Acceptance criteria
- [ ] Access tokens signed with any algorithm other than HS256 are refused
- [ ] Logging in with an existing refresh cookie revokes that cookie's token
- [ ] Each remaining "missing test" line (#24–#28) has an Identity matrix row and a passing test
- [ ] The gap report summary rows #15, #18 and #24–#28 can be marked Covered

## Related
- backlog/improvements/session-redesign.md
