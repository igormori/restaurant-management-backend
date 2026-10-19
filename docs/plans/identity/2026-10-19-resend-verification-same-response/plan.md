# Plan: Resend verification answers every email the same way

**Spec:** docs/specs/identity/2026-10-19-resend-verification-same-response.md   **Date:** 2026-10-19   **Status:** In progress
**Risk:** High — schema change on the pending-email queue, and a security fix on a public endpoint (`POST /api/auth/resend-verification`)

## Summary
3 tasks in 2 batches. "Resend verification" follows the "forgot password" pattern: the request only
adds a row to the existing pending-email queue and always returns the same acknowledgement, while a
new verification email processor, run by the existing email worker, decides who gets a code, sends
it and retries failed sends.

Terms used in this plan and its batches:
- Queue: the `password_reset_requests` table, read by `PasswordResetEmailProcessor` and run on a
  timer by `PasswordResetEmailWorker`
- Processor: the class that reads due queue rows, checks who may get a code, stores the code,
  sends the email and schedules retries
- Eligible account: unverified, verification not locked, last code older than the 60-second wait
- DoD (Definition of Done): the checks a task must pass to count as finished
- Matrix: `docs/security/identity-matrix.md`, the Identity rules and the test that proves each one
- EF Core (Entity Framework Core): the database library that generates the migration

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Queue rows record their purpose (password reset or verification) | B1 | P1 | — | #4, #5 |
| 2 | T2 | Verification email processor: eligibility, code, send, retry | B1 | P1 | T1 | #4, #5, #6 |
| 3 | T3 | Resend only queues and always returns the same acknowledgement | B2 | P1 | T1, T2 | #1, #2, #3, #7 |

T3 runs last so that, at every commit, a queued verification row has a processor to send it.

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | Schema change: new column on `password_reset_requests` through an EF Core migration (the table holds no real data, so no backfill) |
| T2 | — | — | Verification codes are sent outside the request; logs hold the user ID only, never the email address or the code |
| T3 | — | — | Public endpoint behaviour change (`POST /api/auth/resend-verification`) that closes an account-state leak. No role, auth or tenancy change; no raw SQL (Structured Query Language) |

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog
- [ ] `python3 .claude/scripts/check-security-matrices.py` passes
- [ ] Forgot-password rows P1 to P27 stay ✅ with unchanged behaviour
- [ ] `src/RestaurantManagement.Modules.Identity/CLAUDE.md` describes the queued resend flow

## Checklist
- [x] T1 — Queue rows record their purpose
- [x] T2 — Verification email processor
- [ ] T3 — Resend only queues and always returns the same acknowledgement
