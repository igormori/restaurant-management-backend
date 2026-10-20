# Plan: Access decisions use all of a user's roles

**Spec:** docs/specs/shared/2026-10-19-access-decisions-use-all-user-roles.md   **Date:** 2026-10-19   **Status:** In progress
**Risk:** High — changes a Shared contract (the role lookup) and every menu, location and organization authorization decision

## Summary
5 tasks in 5 batches. A pure decision helper in Shared and an Identity lookup that returns every role assignment (role plus one location, or all locations) come first; Menu and Organization then decide with all assignments, keeping today's allowed roles per action, and the old single-role lookup is removed last. Decided by the user: a Manager never manages menus (Owner or Admin only for writes), so Outcome 1 is tested with Admin instead of Manager; Manager menu rights stay in `backlog/improvements/2026-10-15-apply-permission-policies-to-endpoints.md`. DTO (Data Transfer Object: a plain class passed between modules); DoD (Definition of Done: the checks a task must pass to count as finished).

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Role assignment DTO and access decision helper | B1 | P1 | — | #4, #5, #7, #8, #9, #10, #11 |
| 2 | T2 | Role lookup returns every assignment in the organization | B2 | P1 | T1 | #4, #5 |
| 3 | T3 | Menu and menu category checks use all assignments | B3 | P1 | T1, T2 | #1, #2, #3, #4, #5, #6, #9, #10, #12 |
| 4 | T4 | Location and organization checks use all assignments | B4 | P1 | T1, T2 | #3, #4, #5, #6, #7, #8, #11, #12 |
| 5 | T5 | Remove the single-role lookup | B5 | P2 | T3, T4 | #5 |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | New authorization decision type in Shared |
| T2 | — | — | Shared contract change (`IUserRoleLookup` gains a method); tenancy of role rows |
| T3 | — | — | Authorization change on menu and menu category endpoints |
| T4 | — | — | Authorization change on location and organization endpoints |
| T5 | — | — | Shared contract change (`GetRoleAsync` removed) |

No public endpoint, no raw SQL (Structured Query Language), no migration, no token or controller attribute change.

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog
- [ ] Every multi-role case is tested with the roles stored in each order (Outcome 5)
- [ ] Existing single-role allow and refuse tests keep their meaning (Outcome 6)
- [ ] No check counts a role held in another organization (Outcome 4)
- [ ] A Manager, alone or with other non-Admin roles, is still refused menu writes
- [ ] `python3 .claude/scripts/check-security-matrices.py` passes (Shared, Identity, Menu, Organization matrices)
- [ ] `backlog/tech-debt/2026-10-15-user-role-lookup-single-role.md` stays deleted, uncommitted (the user commits it)

## Checklist
- [x] T1 — Role assignment DTO and access decision helper
- [x] T2 — Role lookup returns every assignment in the organization
- [ ] T3 — Menu and menu category checks use all assignments
- [ ] T4 — Location and organization checks use all assignments
- [ ] T5 — Remove the single-role lookup
