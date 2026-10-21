# Switch MemberAccessService to the shared grid builder

**Status:** proposed
**Severity:** low
**Origin:** docs/plans/shared/2026-10-20-invite-member-and-edit-access-grid/review.md · Watch out #2
**Date:** 2026-10-20

## Problem
`MemberAccessService.SetAccessAsync` repeats the role merge (higher level per cell wins, changed levels on top) that `MemberGridBuilder` already does. The builder is injected but never used, so the rule lives in two places. The existing unit tests T37–T42 build the service with a substitute builder, so they must first move to the real builder.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Services/MemberAccessService.cs:26,45` — `_gridBuilder` is injected and unused; `SetAccessAsync` does its own merge
- `src/RestaurantManagement.Modules.Identity/Services/MemberGridBuilder.cs` — the shared merge used by invitations
- `tests/RestaurantManagement.Modules.Identity.Tests/MemberAccessTestSupport.cs` — `CreateService` passes a substitute builder

## Acceptance criteria
- [ ] `SetAccessAsync` builds the final grid through `IMemberGridBuilder`; no second merge remains
- [ ] Tests T37–T42 use the real builder and pass
- [ ] The unused field is gone or used

## Related
backlog/tech-debt/2026-10-20-move-level-parsing-out-of-members-controller.md
