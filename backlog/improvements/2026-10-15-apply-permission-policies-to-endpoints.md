# Apply permission policies to existing endpoints

**Found during:** docs/plans/identity/2026-10-15-tighten-user-roles-and-permissions/plan.md
**Date:** 2026-10-15

## Description
The roles-and-permissions plan adds one access policy per permission but does not move existing endpoints onto them. Menu writes still check for Owner or Admin (`MenuController` attributes and `MenuService.CheckUserPermission`), location writes and organization edits check Owner or Admin in `LocationService` and `OrganizationService`. They should use menu.manage, locations.manage and settings.manage instead, in both the controller and the service check.

## Why it matters
Manager has menu.manage on paper but still cannot manage menus, because the old role checks refuse anyone who is not Owner or Admin. Until endpoints use permissions, the role-to-permission map only affects new endpoints.
