# Custom roles per organization

**Found during:** docs/plans/identity/2026-10-15-tighten-user-roles-and-permissions/plan.md
**Date:** 2026-10-15

## Problem
Roles and their permissions are fixed by the product (Owner, Admin, Manager, Cashier, Waiter,
Kitchen). An Owner cannot create a role with a custom set of permissions, for example a waiter
who can also manage the menu, without giving that person a second full role.

## Fix
- Let an Owner (or Admin) create, edit and delete roles for their own organization and choose
  which permissions each role grants.
- The permission list stays fixed by the product; only the role-to-permission mapping becomes
  configurable per organization.
- Built-in roles keep working; decide whether they can be edited.

## Notes
- Moves the role-to-permission map from code into the database, so it is a high-risk change
  (new tables, auth change).
- Depends on 2026-10-15-tighten-user-roles-and-permissions being shipped.
