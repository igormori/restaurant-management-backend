# Menu returns 403 instead of not found for another organization's data

**Found during:** docs/plans/identity/2026-10-15-tighten-user-roles-and-permissions/plan.md
**Date:** 2026-10-15

## Description
`MenuService.CheckReadPermission` and `CheckUserPermission` throw a 403 (forbidden) when the user has no role in the organization that owns the menu. The project's tenancy rule is that another organization's data returns not found (404).

## Why it matters
A 403 confirms that the menu or organization exists, which tells an outsider which IDs are real. Returning 404 hides other restaurants' data completely.
