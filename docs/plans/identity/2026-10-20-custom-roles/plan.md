# Plan: Custom roles per organization

**Spec:** docs/specs/identity/2026-10-20-custom-roles.md   **Date:** 2026-10-20   **Status:** In progress
**Risk:** High — new signed-in endpoints with a new authorization rule (who may manage roles, capped at the caller's own levels), a schema change to the role name unique index (no data, pre-launch) and new Shared audit actions

## Summary
5 tasks in 3 batches. Shared gets the new audit actions and messages first. Identity then makes role names unique ignoring letter case in the database and adds one rule class for "who may manage roles and up to which level". Last come the role endpoints: list, read and create, then edit levels and rename. Each change is audited.

About 22 files, which is more than the 10-file split guide in the root `CLAUDE.md`. If the architect prefers two pull requests, split after B2 (foundations) and B3 (endpoints). B3 depends on B1 and B2, not the other way round.

Terms: DoD (Definition of Done: the checks a task must pass to count as finished) · DTO (Data Transfer Object: a plain request or response class) · EF Core (Entity Framework Core, the database mapper) · SQL (Structured Query Language) · grid (per member: location x module -> None, Read or Manage) · cap (the caller's level in a module at every non-closed location; an Admin may not set a role's default above it) · non-closed location (status is not Closed, as in `IOrganizationLookup.GetNonClosedLocationIdsAsync`).

Endpoints (all signed in, all in `src/RestaurantManagement.Modules.Identity/Controllers/RolesController.cs`):

| Method and route | Purpose | Task |
|---|---|---|
| `GET api/roles` | The token organization's roles with their default levels | T4 |
| `GET api/roles/{roleId}` | One role; another organization's role returns 404 | T4 |
| `POST api/roles` | Create a role (name and levels) | T4 |
| `PUT api/roles/{roleId}/levels` | Replace a role's default levels | T5 |
| `PUT api/roles/{roleId}/name` | Rename a role | T5 |

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Audit actions and messages for roles | B1 | P1 | — | #2, #10 |
| 2 | T2 | Role names unique ignoring letter case | B2 | P1 | — | #2 |
| 3 | T3 | Who may manage roles, and the cap | B2 | P1 | — | #5, #6, #7 |
| 4 | T4 | List, read and create roles | B3 | P1 | T1, T2, T3 | #1, #2, #5, #7, #8, #9, #10 |
| 5 | T5 | Edit a role's levels and rename it | B3 | P1 | T1, T2, T3, T4 | #2, #3, #4, #6, #7, #8, #9, #10 |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | Shared: new audited actions (`role.created`, `role.levels_changed`, `role.renamed`) |
| T2 | — | — | Migration replaces the unique index on `organization_roles` with one on a stored lowercased name (raw SQL `lower(name)` computed column, as for user email); no data, pre-launch |
| T3 | — | — | Authorization: new rule (Owner, or Members = Manage at every non-closed location; cap per module) |
| T4 | — | — | New signed-in endpoints; tenant data reads and writes |
| T5 | — | — | New signed-in endpoints; tenant data writes |

No new integration, no new library, no backfill. No public endpoint, and no change to the token or to existing roles' rules.

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog
- [ ] `python3 .claude/scripts/check-security-matrices.py` passes (identity and shared matrices)
- [ ] Spec Backlog Impact kept: the deletion of `backlog/features/2026-10-15-custom-roles.md` stays uncommitted for the user

## Checklist
- [x] T1 — Audit actions and messages for roles
- [x] T2 — Role names unique ignoring letter case
- [x] T3 — Who may manage roles, and the cap
- [x] T4 — List, read and create roles
- [ ] T5 — Edit a role's levels and rename it
