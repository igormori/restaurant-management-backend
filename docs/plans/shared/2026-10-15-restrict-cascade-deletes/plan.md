# Plan: Restrict Cascade Deletes

**Spec:** docs/specs/shared/2026-10-15-restrict-cascade-deletes.md   **Date:** 2026-10-15   **Status:** Done
**Risk:** High — changes foreign key delete rules on tables that already hold data (no rows touched, no backfill)

## Summary
2 tasks in 2 batches, one per module. The four foreign keys in Organization and Identity were reviewed: locations, organization settings and user roles change from Cascade to Restrict; verification codes keep Cascade. Each task sets the rules explicitly in its DbContext, adds one schema-only migration, and proves the rules on real PostgreSQL in Web.Tests.

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Restrict organization deletes | B1 | P1 | — | #1, #2, #4, #5 |
| 2 | T2 | Review Identity delete rules | B2 | P1 | — | #3, #4, #5 |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | Schema change on a populated table (migration, no backfill); raw SQL DELETE in tests only |
| T2 | — | — | Schema change on a populated table (migration, no backfill); raw SQL DELETE in tests only |

No endpoint, role, auth or tenant query changes, so no cross-tenant or role rejection tests apply.

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog
- [ ] Commit bodies list every link changed and every link kept as cascade, with the reason (Outcome 4)

## Checklist
- [x] T1 — Restrict organization deletes (47c679a)
- [x] T2 — Review Identity delete rules (e8cbcea)
