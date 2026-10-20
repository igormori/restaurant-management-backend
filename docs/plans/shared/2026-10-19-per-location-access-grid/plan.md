# Plan: Per-location access grid

**Spec:** docs/specs/shared/2026-10-19-per-location-access-grid.md   **Date:** 2026-10-20   **Status:** In progress
**Risk:** High — replaces the role table and the token's role claims, adds and removes Shared contracts, and changes authorization in every module (Identity migrations; no data, pre-launch)

## Summary
10 tasks in 9 batches. Shared grid types and a pure decision come first; Identity then stores memberships (with an Owner flag), per-organization roles and member grids; a Shared guard reads the grid on every request; Web, Organization and Menu switch to it; the old role types are removed last. Decided by the user at Gate 1: actions not tied to one location (a menu linked to several locations or none, a category without a location, the full menu and category lists, the audit log) need the level at every location involved, or at every non-closed location of the organization when none is involved; T2 provides that list of locations.

About 50 files, far over the 10-file split guide in the root `CLAUDE.md`. It cannot ship in parts: the token loses its roles in B3, and the checks that read them are replaced only in B5–B8.

Terms: DoD (Definition of Done: the checks a task must pass to count as finished); DTO (Data Transfer Object: a plain class passed between modules); grid (per member: location x module -> None, Read or Manage); preset (a stored role whose default levels fill a grid).

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Access grid types, decision and contracts | B1 | P1 | — | #1, #2, #3, #5, #9c |
| 2 | T2 | Lookup lists an organization's non-closed locations | B2 | P1 | — | #3, #4, #12 |
| 3 | T3 | Memberships replace role rows; token carries no roles | B3 | P1 | — | #5, #11, #12 |
| 4 | T4 | Organization roles with default levels; Owner setup | B4 | P1 | T1, T3 | #9a |
| 5 | T5 | Member grids: storage, lookup, audited changes | B4 | P1 | T1, T3, T4 | #5, #7, #8, #9, #12, #13 |
| 6 | T6 | Access guard and location choice on the grid | B5 | P1 | T1, T2, T5 | #1, #2, #7, #10, #12 |
| 7 | T7 | Grid policies; audit log needs Reports | B6 | P2 | T6 | #5, #11, #12 |
| 8 | T8 | Locations, settings and organization creation use the grid | B7 | P1 | T4, T5, T6 | #5, #6, #7, #9a, #9b, #12 |
| 9 | T9 | Menus and categories use the grid | B8 | P1 | T5, T6 | #1, #2, #3, #4, #9c, #10, #12 |
| 10 | T10 | Remove the retired role types | B9 | P2 | T7, T8, T9 | #11 |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | New Shared contracts (grid lookup, organization access setup) |
| T2 | — | — | Shared contract change (`IOrganizationLookup` gains a method) |
| T3 | — | — | Token loses role claims; migration drops `user_roles`; raw SQL (Structured Query Language) check constraint carried over |
| T4 | — | — | Migration adds the roles table; tenant data |
| T5 | — | — | Migration adds the grid tables; tenant data writes; audit entries |
| T6 | — | — | New authorization guard; `X-Location-Id` rule changes |
| T7 | — | — | Authorization: policy model and audit log access |
| T8 | — | — | Authorization: locations, organization settings, organization creation |
| T9 | — | — | Authorization: menus and categories |
| T10 | — | — | Shared contracts removed |

No public endpoint, no new library or integration, no backfill (pre-launch, root `CLAUDE.md`).

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog
- [ ] Spec Backlog Impact kept: the deletion of `backlog/improvements/2026-10-15-apply-permission-policies-to-endpoints.md` stays uncommitted for the user

## Checklist
- [x] T1 — Access grid types, decision and contracts
- [x] T2 — Lookup lists an organization's non-closed locations
- [x] T3 — Memberships replace role rows; token carries no roles
- [x] T4 — Organization roles with default levels; Owner setup
- [x] T5 — Member grids: storage, lookup, audited changes
- [x] T6 — Access guard and location choice on the grid
- [ ] T7 — Grid policies; audit log needs Reports
- [ ] T8 — Locations, settings and organization creation use the grid
- [ ] T9 — Menus and categories use the grid
- [ ] T10 — Remove the retired role types
