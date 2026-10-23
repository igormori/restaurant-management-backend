# Plan: Login lockout per IP address

**Spec:** docs/specs/identity/2026-10-23-login-lockout-per-ip-address.md   **Date:** 2026-10-23   **Status:** In progress
**Risk:** High — new `login_failures` table and a dropped `users.failed_attempts` column (schema), and changed lockout rules on the public login endpoint (security)

## Summary
3 tasks in 2 batches. Each counted wrong password is stored as a row (account, IP (Internet Protocol) address, time); the per-address lock (5) and the whole-account lock (20 within 1 hour, at most 5 counted per address, kept on `users.locked_until`) are decided from those rows, and the whole-account lock queues one email (at most one per account per 24 hours) through the existing pending-email queue and worker.

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Per-address and whole-account login lock | B1 | P1 | — | #1, #2, #3, #5, #6, #7 |
| 2 | T2 | Account-locked email | B2 | P2 | T1 | #4 |
| 3 | T3 | Identity security notes describe the new limits | B2 | P2 | T1, T2 | #7 (Rule 9) |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | Behaviour change on public endpoint `POST /api/auth/login` (auth lockout); migration creates a table and drops a column; Shared `SecurityOptions` gains two settings (full suite runs) |
| T2 | — | — | New email to account owners: no code, no link, nothing visible to the requester |
| T3 | — | — | — |

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog

## Checklist
- [x] T1 — Per-address and whole-account login lock
- [x] T2 — Account-locked email
- [ ] T3 — Identity security notes describe the new limits
