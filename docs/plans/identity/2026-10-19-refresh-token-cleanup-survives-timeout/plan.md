# Plan: Daily expired-session cleanup keeps running after a timeout

**Spec:** docs/specs/identity/2026-10-19-refresh-token-cleanup-survives-timeout.md   **Date:** 2026-10-19   **Status:** In progress
**Risk:** Low — one background job's error handling changes; no data, schema, contract or endpoint change

## Summary
1 task in 1 batch. The cleanup worker's error filter changes from "any error except a cancellation" to "any error while the application is not shutting down", the same fix already made in `PasswordResetEmailWorker`, proven by two unit tests and a new Identity matrix row.

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Cleanup worker tells a shutdown apart from a timeout | B1 | P1 | — | #1, #2, #3 |

## Flags
None

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog

## Checklist
- [x] T1 — Cleanup worker tells a shutdown apart from a timeout
