# Menu module

Inferred from code; no spec exists yet for this module.

Deleting a Menu is a hard delete (_menuDb.Menus.Remove), not a status flag like
Organization's Location. The menus->location_menus and menus->menu_categories foreign
keys cascade at the database level, so related rows are cleaned up by Postgres, not by
MenuService.
MenuService enforces authorization itself via IUserRoleLookup on every write and read
(CheckUserPermission), independent of whatever [Authorize(Roles = ...)] the controller
declares; treat the controller attribute as a first filter, not the source of truth.
Accepted gaps are tracked in docs/follow-ups.md; don't fix them as drive-by changes.
