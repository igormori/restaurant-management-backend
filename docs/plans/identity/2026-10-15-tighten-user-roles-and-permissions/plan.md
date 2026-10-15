# Plan: Tighten user roles and add permissions in code

**Spec:** docs/specs/identity/2026-10-15-tighten-user-roles-and-permissions.md   **Date:** 2026-10-15   **Status:** Done
**Risk:** High — schema change on `user_roles`, new default authorization for every endpoint, new Shared contract, Staff role removed

## Summary
4 tasks in 4 batches. First the fixed role and permission lists in Shared, then the database rules on role assignments, then the access policies (permission policies plus a default "must be signed in" rule), and last the removal of the old Staff role. Existing menu, location and organization endpoints keep their current role checks; moving them to permissions comes later (backlog/improvements/2026-10-15-apply-permission-policies-to-endpoints.md).

Terms used below:
- Policy: a named access rule that ASP.NET Core (the web framework) checks before running an endpoint.
- Fallback policy: the rule applied to every endpoint that declares no rule of its own.
- `NULLS NOT DISTINCT`: a PostgreSQL index option that treats two empty (NULL) values as equal, so two "all locations" rows count as duplicates.
- JWT (JSON Web Token): the signed access token; it carries the user's role claims and selected organization.
- DoD (Definition of Done): the checks a task must pass to count as finished (in each batch file).
- Testcontainers: a library that starts a real PostgreSQL in Docker for tests.

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | New roles, permission list and role-to-permission map | B1 | P1 | — | #1 |
| 2 | T2 | Role assignment needs an organization; no duplicates | B2 | P1 | — | #4, #5 |
| 3 | T3 | Permission policies and private-by-default endpoints | B3 | P1 | T1 | #2, #3 |
| 4 | T4 | Remove the Staff role | B4 | P2 | T1 | #1 |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | New Shared contract: `Roles` (changed), `Permissions`, `RolePermissions` |
| T2 | — | — | Schema change on `user_roles` (NOT NULL `organization_id`, unique index). Table is empty everywhere (spec decision); other local databases must be emptied first |
| T3 | — | — | Auth change: fallback policy and one policy per permission. Public endpoints declared explicitly: register, login, refresh, logout, verify, resend-verification |
| T4 | — | — | Role change: Staff removed; all six roles may read their organization's menus (architect decision, 2026-10-15) |

No raw SQL in production code. No new library or test project.

## Overall Definition of Done
- [x] All tasks done, tests passing
- [x] All spec Outcomes met
- [x] Full test suite green after the last batch
- [x] No out-of-scope changes; findings in the backlog

## Checklist
- [x] T1 — New roles, permission list and role-to-permission map
- [x] T2 — Role assignment needs an organization; no duplicates
- [x] T3 — Permission policies and private-by-default endpoints
- [x] T4 — Remove the Staff role
