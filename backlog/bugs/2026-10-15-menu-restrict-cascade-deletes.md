# Menu: restrict cascade deletes

**Found during:** docs/specs/shared/2026-10-15-restrict-cascade-deletes.md
**Date:** 2026-10-15

## Description
In the Menu module, menus -> location_menus and menus -> menu_categories cascade at the database level. MenuService hard-deletes menus and relies on this to clean up children.

## Why it matters
Deleting a menu silently removes its categories and location links, the same data-loss risk fixed for organizations.

## Notes
Needs its own design: either switch menu deletion to deactivation (Menu already has IsActive) or keep the cascade as owned data. Changing to Restrict without changing MenuService would break menu deletion. Menu categories are also hard-deleted today.
