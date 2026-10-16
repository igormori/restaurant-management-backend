# Plan: Tenant Query Filters and Location Access Check

**Spec:** docs/specs/shared/2026-10-15-tenant-query-filters-and-location-access.md   **Date:** 2026-10-15   **Status:** Draft
**Risk:** High — changes how every module reads tenant data, breaks seven endpoint contracts (organization ID removed from URL and body), adds a 403 path to every authenticated request, and extends a Shared contract

## Summary
8 tasks in 6 batches. Shared gains a tenant context (organization from the sign-in token, location from the checked X-Location-Id header) and EF Core (Entity Framework Core, the database library) global query filters that each module's DbContext applies; endpoints stop accepting an organization ID from the client; a middleware checks the selected location against the user's role assignments.

Terms used below:
- JWT (JSON Web Token): the sign-in token; the organization comes only from its `org_id` claim
- DoD (Definition of Done): the checks a task must pass to count as finished
- Query filter: a condition EF Core adds to every query of an entity type unless the code explicitly turns it off (`IgnoreQueryFilters`)
- API (Application Programming Interface) contract: the routes and request/response shapes the frontend relies on

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Shared tenant context and query filter builder | B1 | P1 | — | #1, #2 |
| 2 | T2 | Apply filters in Menu | B2 | P1 | T1 | #1, #2, #6 |
| 3 | T3 | Menu endpoints take the organization from the token | B2 | P1 | T1, T2 | #1 (Rule 3) |
| 4 | T4 | Apply filters in Organization, with the listed bypasses | B3 | P1 | T1 | #1, #2 |
| 5 | T5 | Organization endpoints take the organization from the token | B3 | P1 | T1, T4 | #1 (Rule 3) |
| 6 | T6 | Location assignment lookup in Identity | B4 | P1 | — | #3, #4 |
| 7 | T7 | X-Location-Id check middleware | B5 | P1 | T1, T3, T4, T6 | #3, #4, #5 |
| 8 | T8 | Filter-bypass rule in CLAUDE.md | B6 | P2 | T2, T4 | Rule 7 (supports #1) |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | New Shared contracts (`ITenantContext`, selected-location holder); filters every tenant read |
| T2 | — | — | Tenant read scoping in Menu; other organization's menu changes from 403 to 404 |
| T3 | — | — | Breaking API contract change on four Menu endpoints; role checks move to the token organization |
| T4 | — | — | Tenant read scoping in Organization; two deliberate filter bypasses (`IgnoreQueryFilters`) |
| T5 | — | — | Breaking API contract change on three Organization endpoints; role checks move to the token organization |
| T6 | — | — | Shared contract change (`IUserRoleLookup` gains a method) |
| T7 | — | — | Auth change: new request header and new 403 on every authenticated request |
| T8 | — | — | Rule governing filter bypasses |

No public endpoint, no role change, no raw SQL, no migration (query filters do not change the schema). The frontend must switch to the new routes from T3 and T5 in the same release.

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog
- [ ] `IgnoreQueryFilters` appears only in the places listed in CLAUDE.md
- [ ] No endpoint reads an organization ID from the URL or request body (organization selection in Identity excepted)

## Checklist
- [ ] T1 — Shared tenant context and query filter builder
- [ ] T2 — Apply filters in Menu
- [ ] T3 — Menu endpoints take the organization from the token
- [ ] T4 — Apply filters in Organization, with the listed bypasses
- [ ] T5 — Organization endpoints take the organization from the token
- [ ] T6 — Location assignment lookup in Identity
- [ ] T7 — X-Location-Id check middleware
- [ ] T8 — Filter-bypass rule in CLAUDE.md
