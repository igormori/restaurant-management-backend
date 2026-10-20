# Menu module

Inferred from code; no spec exists yet for this module.

Deleting a Menu is a hard delete (_menuDb.Menus.Remove), not a status flag like
Organization's Location. The menus->location_menus and menus->menu_categories foreign
keys cascade at the database level, so related rows are cleaned up by Postgres, not by
MenuService.
MenuService and MenuCategoryService enforce authorization themselves through MenuAccessChecker,
which asks IAccessGuard about the member's access grid (module Menus) on every read and write; the
controllers carry a plain [Authorize]. Read at a location for its menus and categories; Manage to
write. An action across locations (a menu linked to several or none, a category without a location,
the full lists) needs the level at every location involved, or at every non-closed location when
none is involved. 404 comes before 403.
Out-of-scope findings and accepted gaps are tracked in backlog/; don't fix them as drive-by changes.
