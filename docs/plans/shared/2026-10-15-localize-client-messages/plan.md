# Plan: Localize All Client-Facing Messages

**Spec:** docs/specs/shared/2026-10-15-localize-client-messages.md   **Date:** 2026-10-15   **Status:** In progress
**Risk:** Medium — changes shared code used by every endpoint (message catalog, error handler); no data, schema or permission change

## Summary
9 tasks in 5 batches. Add every missing message to the shared en/pt catalog first, then point each module's hard-coded or default English messages at catalog keys, turn the locations invalid-user case into a localized 401, and finish with an automated check that no message is missing in either language.

Terms used below:
- Catalog: the shared message files `SharedResource.resx` (en) and `SharedResource.pt.resx` (pt)
- Endpoint test: a test in `tests/RestaurantManagement.Web.Tests` that calls the real API (WebApplicationFactory: an in-memory test host; Testcontainers: a throwaway PostgreSQL) with the `Accept-Language` header set to `en` or `pt`
- DoD: Definition of Done, the checks a task must pass to count as finished

## Tasks
| Order | ID | Task | Batch | Priority | Depends on | Spec outcome |
|---|---|---|---|---|---|---|
| 1 | T1 | Add missing messages to the catalog | B1 | P1 | — | #1–#8 |
| 2 | T2 | Translate the unexpected-error message | B1 | P2 | T1 | #7 |
| 3 | T3 | Translate menu and menu category "not found" | B2 | P1 | T1 | #1, #2 |
| 4 | T4 | Translate menu and menu category field errors | B2 | P2 | T1 | #4, #6 |
| 5 | T5 | Translate registration phone, login and organization-selection field errors | B3 | P2 | T1 | #3, #6 |
| 6 | T6 | Translate organization create/edit field errors | B4 | P2 | T1 | #5, #6, #9 |
| 7 | T7 | Translate location create/edit field errors | B4 | P2 | T1 | #6, #9 |
| 8 | T8 | Answer an invalid user identifier on locations with a localized 401 | B4 | P2 | T1 | #8 |
| 9 | T9 | Check that every message exists in en and pt | B5 | P2 | T2–T8 | #9 |

## Flags
| Task | New integration | New library | Security |
|---|---|---|---|
| T2 | — | — | Error handler shared by all endpoints; must keep hiding exception details outside Development |
| T8 | — | — | Auth failure response changes from 500 to 401 on `api/locations`; no permission or role change |

## Overall Definition of Done
- [ ] All tasks done, tests passing
- [ ] All spec Outcomes met
- [ ] Full test suite green after the last batch
- [ ] No out-of-scope changes; findings in the backlog

## Checklist
- [x] T1 — Add missing messages to the catalog
- [x] T2 — Translate the unexpected-error message
- [x] T3 — Translate menu and menu category "not found"
- [x] T4 — Translate menu and menu category field errors
- [x] T5 — Translate registration phone, login and organization-selection field errors
- [x] T6 — Translate organization create/edit field errors
- [x] T7 — Translate location create/edit field errors
- [x] T8 — Answer an invalid user identifier on locations with a localized 401
- [ ] T9 — Check that every message exists in en and pt
