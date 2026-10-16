# Menu: decide whether menus and categories are deactivated instead of deleted

**Found during:** docs/specs/shared/2026-10-15-restrict-cascade-deletes.md
**Date:** 2026-10-15

## Description
Deleting a menu hard-deletes it, and the database also deletes its menu categories and location links (cascade). Menu categories are hard-deleted on their own too. This works as built today: categories and location links belong to one menu and are useless without it, and nothing else references menus yet.

## Why it matters
The standing rule "nothing in use is deleted" will apply once Dishes and Orders reference menus or categories. Hard-deleting would then lose history or fail on foreign keys. Decide before that work starts.

## Notes
Options:
- Deactivate instead of delete (Menu and LocationMenu already have IsActive; MenuCategory does not).
- Refuse the delete while the menu or category is in use, and keep hard delete for unused ones.

Switching the cascade to Restrict without changing MenuService would break menu deletion.
