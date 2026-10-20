# List members, their access and pending invitations

**Status:** proposed
**Severity:** medium
**Origin:** docs/plans/shared/2026-10-20-invite-member-and-edit-access-grid/plan.md · Planning
**Date:** 2026-10-20

## Problem
Slice 2 lets an authorized member edit a member's grid and cancel a pending invitation, but no
endpoint lists the organization's members, shows one member's current presets and grid, or lists
pending invitations. A screen cannot show what it is about to edit, or find the invitation to
cancel, without them.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Services/IMemberAccessService.cs` — `GetAccessAsync` reads a member's grid, but no endpoint exposes it
- `docs/specs/shared/2026-10-20-invite-member-and-edit-access-grid.md` — Outcomes cover invite, accept, edit and cancel only; nothing lists members or invitations

## Acceptance criteria
- [ ] An authorized member lists the organization's members and pending invitations, limited to the locations where they have Members >= Read
- [ ] An authorized member reads one member's presets and levels per location
- [ ] Members without Members >= Read are refused; another organization's members and invitations are never listed (not found)

## Related
backlog/features/2026-10-19-filter-lists-by-location-access.md
