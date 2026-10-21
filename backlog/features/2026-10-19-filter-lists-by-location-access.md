# Filter lists by location access

**Status:** proposed
**Severity:** medium
**Origin:** docs/specs/shared/2026-10-19-per-location-access-grid.md · Slice 3 of 3
**Date:** 2026-10-19

## Problem
Every member sees every location in lists. Lists should show only the locations (and their data) where the member has at least Read for the module.

## Evidence
- `docs/specs/shared/2026-10-19-per-location-access-grid.md` — Current State: every member sees every location
- Merged from `backlog/tech-debt/2026-10-15-no-location-owned-entities.md` (2026-10-19): the location
  query filter applies only to entities derived from `LocationEntity`, and none do. `LocationMenu`
  (required `LocationId`) and `MenuCategory` (optional `LocationId`) derive from `TenantEntity`, so
  selecting a location with X-Location-Id does not narrow them. Menu responses list all linked
  locations, which must be kept in mind when moving them
- `MenuCategoryService.GetAllAsync` and `GetByMenuIdAsync` refuse an Admin assigned to one location
  instead of returning only that location's categories

## Acceptance criteria
- [ ] A location list returns only locations where the member has Locations >= Read
- [ ] Menu lists return only menus at locations where the member has Menus >= Read
- [ ] Category lists (all, and by menu) return only categories at locations where the member has
      Menus >= Read, plus organization-wide categories (no location)
- [ ] Selecting a location narrows menu-location links and categories to that location
- [ ] Owner still sees everything

## Related
docs/specs/shared/2026-10-20-invite-member-and-edit-access-grid.md
