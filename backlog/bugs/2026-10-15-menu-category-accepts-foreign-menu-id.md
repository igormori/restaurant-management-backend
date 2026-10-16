# Menu category can point at another organization's menu

**Found during:** docs/plans/shared/2026-10-15-tenant-query-filters-and-location-access/scenarios.md
**Date:** 2026-10-15

## Description
`MenuCategoryService.CreateAsync` stores the `MenuId` from the request without checking that the menu exists in the signed-in organization. After the tenant filters, the new category is saved in the user's own organization, but its `MenuId` can still be the ID of another organization's menu (the database foreign key only checks that the menu exists).

## Why it matters
A category in Cantina da Nonna could reference Trattoria do Sol's menu, mixing two organizations' data through a cross-tenant reference. Creating a category should answer 404 when the menu is not found in the user's organization, with a cross-tenant test.
