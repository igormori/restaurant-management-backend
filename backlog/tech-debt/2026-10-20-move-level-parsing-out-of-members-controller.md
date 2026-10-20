# Move level parsing out of MembersController

**Status:** proposed
**Severity:** low
**Origin:** docs/plans/shared/2026-10-20-invite-member-and-edit-access-grid/review.md · Watch out #3
**Date:** 2026-10-20

## Problem
`MembersController.ParseLevels` is validation logic inside a controller, and it copies `InvitationGrids.ParseLevels`. Controllers should only bind and call; the parsing belongs in the service, reusing one implementation.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Controllers/MembersController.cs:36` — private `ParseLevels`
- `src/RestaurantManagement.Modules.Identity/Services/InvitationGrids.cs` — `ParseLevels`, the same parsing

## Acceptance criteria
- [ ] The controller passes the raw level names to the service and contains no parsing
- [ ] One `ParseLevels` implementation is used by both invitations and member access edits
- [ ] The existing member access tests still pass, including the 400 for an unknown module or level

## Related
backlog/tech-debt/2026-10-20-member-access-service-use-grid-builder.md
