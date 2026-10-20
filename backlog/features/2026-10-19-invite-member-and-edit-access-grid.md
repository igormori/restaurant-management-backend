# Invite a member by email and edit their access grid

**Status:** proposed
**Severity:** medium
**Origin:** docs/specs/shared/2026-10-19-per-location-access-grid.md · Slice 2 of 3
**Date:** 2026-10-19

## Problem
Nobody can invite a member by email or set and edit a member's access grid. Delegation rules are decided but not enforced: an Admin with Members = Manage may add other Admins and set their grids, cannot change their own, can grant at most their own level per location, and can never edit the Owner.

## Evidence
- `docs/specs/shared/2026-10-19-per-location-access-grid.md` — Delegation table

## Acceptance criteria
- [ ] An authorized member invites someone by email and chooses presets and grid
- [ ] A member's grid can be edited; each change is audited
- [ ] Delegation rules above are enforced, including no self-edit and no Owner edit

## Related
backlog/features/2026-10-16-member-role-change.md, backlog/features/2026-10-19-filter-lists-by-location-access.md
