# Organization selection accepts a missing organization ID without a field error

**Status:** proposed
**Severity:** medium
**Origin:** docs/plans/shared/2026-10-15-localize-client-messages/plan.md · Phase 3 — tests (Gate 2, option A: deferred)
**Date:** 2026-10-15

## Problem
The organization ID on organization selection is a required field, but a request without it is read as the empty ID, which the required rule accepts. No field error is ever returned and the request reaches the sign-in service. Sending `null` or text that is not an ID fails while reading the request instead, with the platform's English message, whatever language the client asked for. The menu ID on menu category creation has the same problem. Because of this, the translated "required" message can never reach the client on these fields.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Models/SelectOrganizationRequest.cs:7` — `[Required]` on a non-nullable `Guid`, which is never null
- `src/RestaurantManagement.Modules.Menu/Models/CreateMenuCategoryRequest.cs:7` — same pattern on `MenuId`
- `docs/plans/shared/2026-10-15-localize-client-messages/scenarios.md` — Outcome #6 for organization selection proven only by scenario 21 (attribute uses a catalog key)

## Acceptance criteria
- [ ] Organization selection without an organization ID returns 400 with the translated "required" message in en and pt
- [ ] Menu category creation without a menu ID returns 400 with the translated "required" message in en and pt
- [ ] A body with `null` or text that is not an ID for these fields returns a translated message, not the platform's English text

## Related
None
