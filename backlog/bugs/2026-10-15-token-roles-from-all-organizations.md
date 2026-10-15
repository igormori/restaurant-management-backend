# Access token carries roles from every organization when none is selected

**Found during:** docs/plans/identity/2026-10-15-tighten-user-roles-and-permissions/plan.md
**Date:** 2026-10-15

## Description
`AccessTokenFactory.Create` adds role claims from all of the user's organizations when no organization is selected. A user who is Owner of one restaurant and Waiter in another gets an "Owner" claim that is not tied to any organization, and passes `[Authorize(Roles = "Owner,Admin")]` controller filters for either restaurant.

## Why it matters
Role claims should only describe the organization the user is acting in. Services re-check the role per organization today, and the new permission policies refuse tokens without an organization claim, but any future check that trusts the role claim alone would leak access across restaurants.
