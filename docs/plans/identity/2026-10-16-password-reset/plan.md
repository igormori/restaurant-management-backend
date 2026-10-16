# Plan: Forgot and change password

**Spec:** docs/specs/identity/2026-10-16-password-reset.md   **Date:** 2026-10-16   **Status:** In progress
**Risk:** High — new table and migration, two new public endpoints, password and session changes, Shared contract changes

## Summary
9 tasks in 7 batches. Shared foundations first (reset email, rate limit policies), then Identity storage and helpers, then the
forgot, reset and change use cases as services, and last the three endpoints with end-to-end tests on PostgreSQL.
Reset codes reuse the verification code hasher and limits but live in their own table; every success revokes all refresh
tokens of the user through the existing `IRefreshTokenService.RevokeAllForUserAsync`.
Decided: audit entries go to each organization the user holds a role in; a user with no organization gets none.

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Password reset email | B1 | P1 | — | #2, #10 |
| 2 | T2 | Rate limit policies for the three endpoints | B2 | P1 | — | #10 |
| 3 | T3 | Reset code storage and migration | B3 | P1 | — | #3, #4, #6 |
| 4 | T4 | Reusable registration password rules | B4 | P1 | — | #7 |
| 5 | T5 | Password audit recorder | B4 | P1 | — | #11 |
| 6 | T6 | Request a reset code | B5 | P1 | T1, T3 | #1, #5, #6, #12 |
| 7 | T7 | Reset the password with a code | B5 | P1 | T3, T4, T5, T6 | #2, #3, #4, #7, #9, #11 |
| 8 | T8 | Change a known password | B6 | P1 | T4, T5, T7 | #7, #8, #9, #11 |
| 9 | T9 | Forgot, reset and change endpoints | B7 | P1 | T2, T6, T7, T8 | #1, #9, #10, #11 |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | Shared contract change: new method on `IEmailService`; email body no longer logged in development |
| T2 | — | — | Shared contract change: three new rate limit policies and options |
| T3 | — | — | Migration: new `password_reset_codes` table and two `users` columns (no data, no backfill) |
| T5 | — | — | Writes audit entries under an explicit organization scope (`ITenantScope.Begin`), one per organization the user holds a role in (decided by the user) |
| T7 | — | — | Auth change: password replaced and all refresh tokens revoked |
| T8 | — | — | Auth change: password replaced and all refresh tokens revoked |
| T9 | — | — | Two new public (anonymous) endpoints; one authenticated endpoint |

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog

## Checklist
- [x] T1 — Password reset email
- [x] T2 — Rate limit policies for the three endpoints
- [x] T3 — Reset code storage and migration
- [x] T4 — Reusable registration password rules
- [x] T5 — Password audit recorder
- [x] T6 — Request a reset code
- [x] T7 — Reset the password with a code
- [ ] T8 — Change a known password
- [ ] T9 — Forgot, reset and change endpoints
