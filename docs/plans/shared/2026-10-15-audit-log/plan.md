# Plan: Audit Log

**Spec:** docs/specs/shared/2026-10-15-audit-log.md   **Date:** 2026-10-16   **Status:** In progress
**Risk:** High — new table and first DbContext with migrations in Shared, new Shared contract, new endpoint behind a permission

## Summary
4 tasks in 3 batches. Shared gets an append-only audit log (table, writer contract `IAuditLog`, read endpoint behind reports.view). The Organization module then writes an entry when settings or a location's status change.

Terms used below:
- DoD (Definition of Done): the checks a task must pass to count as finished.
- DTO (Data Transfer Object): a plain request or response shape, never a database entity.
- EF Core (Entity Framework Core): the database library each module uses.
- IP address (Internet Protocol address): the caller's network address, the "network address" in Rule 1.
- UTC (Coordinated Universal Time): all stored times and date filters use it.

Decisions (answered by the user):
- Outcome #1 (role change) **can't be checked in this plan**. The code has no "change member role" feature. T1 defines the `member role changed` action; no task builds role changes. The role change spec is in `backlog/features/2026-10-16-member-role-change.md`.
- Phase 0 task 10 (Invitations) must write a `role assigned` entry when an invitation gives a member a role. That task adds the action.
- Write failure: the entry is written in its own save, right after the change is saved. If that write fails, the change stays, the failure is logged as an error and the request succeeds. The outbox (a table that stores pending writes so they can be retried) will remove that gap; see `backlog/improvements/2026-10-16-audit-entries-through-outbox.md`. That work must be done before Phase 1 logs cancellations, discounts or cash closings.

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Audit log storage and writer | B1 | P1 | — | #4, Rules 1–3 |
| 2 | T2 | Read audit log endpoint | B2 | P1 | T1 | #2, #3, Rule 4 |
| 3 | T3 | Record settings changes | B3 | P2 | T1, T2 | Rule 2 |
| 4 | T4 | Record location status changes | B3 | P2 | T1, T2 | Rule 2 |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | Npgsql EF Core provider added to the Shared project (every module already uses it, same version) | New Shared contract `IAuditLog`; new table and migration; new DbContext in Shared; tenant data |
| T2 | — | — | New endpoint and the first to use a permission policy (reports.view); host registers Shared's controllers |
| T3 | — | — | Writes tenant data |
| T4 | — | — | Writes tenant data |

No public endpoint, no role or auth change, no raw SQL.

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] Spec Outcomes #2, #3 and #4 met (#1 can't be checked in this plan, see Decisions)
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog
- [ ] No service, contract or endpoint can update or delete an audit entry (Outcome #4)

## Checklist
- [x] T1 — Audit log storage and writer
- [x] T2 — Read audit log endpoint
- [x] T3 — Record settings changes
- [x] T4 — Record location status changes
