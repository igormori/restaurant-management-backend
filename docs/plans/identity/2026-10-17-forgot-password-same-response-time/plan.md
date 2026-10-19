# Plan: Forgot password takes the same time for every email

**Spec:** docs/specs/identity/2026-10-17-forgot-password-same-response-time.md   **Date:** 2026-10-17   **Status:** Done
**Risk:** High — new table and migration, a new background worker, and a behaviour change on a public (anonymous) endpoint

## Summary
4 tasks in 3 batches. "Forgot password" stops doing account work during the request: it only adds one row (the email and the time) to a new Identity table of pending reset requests, whatever the email. A background worker later reads each pending row, checks eligibility, creates the code, sends the email, and retries a failed send.

Terms used below:
- Queue: the new `password_reset_requests` table of pending requests, owned by the Identity module (not a Shared outbox).
- Processor: the Identity service that handles one pass over the due rows of the queue.
- Worker: the background service that runs the processor every few seconds.

Because the request never reads the users table, a known and an unknown email run exactly the
same database commands. Tests prove that by comparing those commands, not by measuring time,
which would make the tests unreliable.

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Pending reset request table | B1 | P1 | — | #1, #3 |
| 2 | T2 | Forgot password only queues the request | B2 | P1 | T1 | #1, #2 |
| 3 | T3 | Processor sends reset emails and retries failures | B2 | P1 | T1, T2 | #3, #4, #5 |
| 4 | T4 | Background worker runs the processor | B3 | P1 | T3 | #3, #4 |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | Schema change: new table storing raw email addresses until they are processed |
| T2 | — | — | Behaviour change on public endpoint `POST /api/auth/forgot-password` (anonymous). No role or auth change, no raw SQL |
| T3 | — | — | Creates reset codes and sends emails outside a request. Rows are claimed with a conditional update, no raw SQL |
| T4 | New background worker (hosted service) | — | — |

## Overall Definition of Done
- [x] All tasks done, tests passing
- [x] All spec Outcomes met
- [x] Full test suite green after the last batch
- [x] No out-of-scope changes; findings in the backlog
- [x] `docs/security/identity-matrix.md` rows P1–P5 changed, new P and D rows added, all ✅; `python3 .claude/scripts/check-security-matrices.py` passes
- [x] `backlog/bugs/2026-10-16-forgot-password-response-time-reveals-account.md` deleted in `57706ee` (committed at the user's request)

## Checklist
- [x] T1 — Pending reset request table (`966f9c1`)
- [x] T2 — Forgot password only queues the request (`8ee57b5`)
- [x] T3 — Processor sends reset emails and retries failures (`830523b`)
- [x] T4 — Background worker runs the processor (`19758cf`)
- [x] Fix round — `5b3293e`, `9283fc3`, `945ecb2` (first review); `3e37ac0`, `c0105f1`, `d461f49`, `3ea7226` (second review); `dd2aea8` (failed_at index); `4a323e7` (third review: code time per row)
