# No entity is location-owned yet

**Found during:** docs/plans/shared/2026-10-15-tenant-query-filters-and-location-access/plan.md
**Date:** 2026-10-15

## Description
The location query filter applies to entities derived from `LocationEntity`, but none do. `LocationMenu` (required `LocationId`) and `MenuCategory` (optional `LocationId`) derive from `TenantEntity`, so selecting a location with X-Location-Id does not narrow them.

## Why it matters
Staff working in one location still see every location's menu links and categories. Each entity with a location should be reviewed and moved to `LocationEntity` where the data truly belongs to one location, keeping in mind that menu responses list all linked locations.
