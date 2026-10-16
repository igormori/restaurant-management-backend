# "Menu not found" error has no translated message

**Found during:** review of branch fix/menu-category-accepts-foreign-menu
**Date:** 2026-10-15

## Description
`MenuService` and `MenuCategoryService` throw 404 with `_localizer["MenuNotFound"]`, but `SharedResource.resx` and `SharedResource.pt.resx` have no `MenuNotFound` entry. The localizer falls back to the key, so the user sees the raw text `MenuNotFound` in English and Portuguese.

## Why it matters
Every "menu not found" answer shows a code instead of a readable message. Add `MenuNotFound` to both resource files, and check the Menu module for other keys with no entry.
