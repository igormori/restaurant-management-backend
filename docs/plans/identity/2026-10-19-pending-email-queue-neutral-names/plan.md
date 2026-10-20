# Plan: Pending-email queue gets neutral names

**Spec:** docs/specs/identity/2026-10-19-pending-email-queue-neutral-names.md   **Date:** 2026-10-19   **Status:** In progress
**Risk:** High — renames a database table and its indexes (schema change, empty database) and renames the operators' startup settings

## Summary
2 tasks in 2 batches. Rename only, no behaviour change: first the queue's stored data and the limits it shares, then the sending job and its settings in the Web host.

Naming used throughout (the old name on the left, the new one on the right):

| What | Old name | New name |
|---|---|---|
| Queue entity / purpose enum | `PasswordResetRequest` / `PasswordResetRequestPurpose` | `PendingEmail` / `PendingEmailPurpose` |
| DbSet (Entity Framework table property) / table | `PasswordResetRequests` / `password_reset_requests` | `PendingEmails` / `pending_emails` |
| Queue-wide limits (attempts, batch size, failed-row retention) | on `PasswordResetEmailProcessor` | on a neutral queue-level holder |
| Sending job | `PasswordResetEmailWorker` | `PendingEmailWorker` |
| Settings | `PasswordResetEmails:Enabled`, `PasswordResetEmails:IntervalSeconds` | `PendingEmails:Enabled`, `PendingEmails:IntervalSeconds` |

Kept (truly about password reset, spec Rule 2): `PasswordResetEmailProcessor`, `PasswordResetCode`, `PasswordResetService`, the reset email template, the `PasswordReset` purpose value, and their tests.

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Rename the queue's stored data and shared limits | B1 | P1 | — | #1, #2, #3, #4 |
| 2 | T2 | Rename the sending job and its settings | B2 | P1 | T1 | #1, #2, #3, #4 |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | Schema change: table and index rename through a generated EF Core (Entity Framework Core) migration, no data copied |
| T2 | — | — | Operator settings renamed: the old `PasswordResetEmails:*` keys are no longer read |

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog
- [ ] Searching `src/`, `tests/`, `docs/security/` and module `CLAUDE.md` files for the old names in the table above finds nothing, except earlier migration files (generated history, never edited)
- [ ] `python3 .claude/scripts/check-security-matrices.py` passes

## Checklist
- [x] T1 — Rename the queue's stored data and shared limits
- [ ] T2 — Rename the sending job and its settings
