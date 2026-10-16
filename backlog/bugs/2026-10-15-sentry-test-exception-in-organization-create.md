# Creating an organization named "TEST_SENTRY" throws on purpose

**Found during:** docs/plans/shared/2026-10-15-tenant-query-filters-and-location-access/plan.md
**Date:** 2026-10-15

## Description
`OrganizationService.CreateOrganizationAsync` throws an intentional exception when the requested name is `TEST_SENTRY` (any case). It was added to test Sentry error tracking and is still in the production code path.

## Why it matters
Any user can trigger a 500 error and a Sentry event at will, and a real restaurant with that name cannot be created. Test hooks belong in tests or a development-only endpoint.
