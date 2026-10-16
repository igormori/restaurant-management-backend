# Plan: Organization and Location Fields

**Spec:** docs/specs/organization/2026-10-15-organization-and-location-fields.md   **Date:** 2026-10-15   **Status:** Approved
**Risk:** Medium — three schema-only migrations (new required columns and a unique index) on Organization tables; no real data, no backfill

## Summary
4 tasks in 3 batches, all in the Organization module.
Each data task adds its fields to the entity, the request and response DTOs (Data Transfer Objects:
the request and response shapes of the API), validation, and its own schema-only migration.
The business-day rule is a pure calculator with no database access, unit-tested per country.

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Organization currency | B1 | P1 | — | #1 |
| 2 | T2 | Settings: service charge, discount limit, cutoff | B1 | P1 | T1 | #3, #6 |
| 3 | T3 | Location time zone, type and code | B2 | P1 | — | #2, #3, #4, #5 |
| 4 | T4 | Business day calculator | B3 | P2 | — | #7 |

- T1 and T2 share a batch: both change the same organization request, response and service files.
- T4 takes the time zone and cutoff as inputs, so it needs neither T2 nor T3 in place.
- Confirmed by the user at Gate 1 (2026-10-15): currency is set per organization at creation and
  can never be changed; the edit endpoint does not accept it.
- Endpoints touched: `POST api/organizations/create`, `PUT api/organizations/edit`,
  `GET api/organizations`, `POST api/locations`, `PUT api/locations/{id}`, `GET api/locations`.

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T1 | — | — | Changes the create-organization contract; migration adding a required column |
| T2 | — | — | Changes the edit-organization contract (Owner/Admin only, unchanged); migration on the settings table |
| T3 | — | — | Changes the location create and edit contracts; migration with a unique index per organization |
| T4 | — | — | — |

- No public endpoint, no role or auth change, no raw SQL, no new Shared contract, no new NuGet package.
- IANA (Internet Assigned Numbers Authority) time zone names are checked with the built-in .NET
  `TimeZoneInfo`, not a new library.
- Role checks stay as they are today (by role name); moving them to permission policies is already
  in backlog/improvements/2026-10-15-apply-permission-policies-to-endpoints.md.

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog
- [ ] No country default for currency or time zone anywhere in code, migrations or test seed helpers
- [ ] Every changed endpoint has a cross-tenant test and rejection tests for Manager, Cashier, Waiter and Kitchen
- [ ] Migrations generated following `.claude/skills/ef-migration/SKILL.md`, never hand-edited

## Checklist
- [ ] T1 — Organization currency
- [ ] T2 — Settings: service charge, discount limit, cutoff
- [ ] T3 — Location time zone, type and code
- [ ] T4 — Business day calculator
