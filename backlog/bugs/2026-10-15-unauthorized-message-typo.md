# English "not signed in" message is misspelled

**Found during:** docs/plans/identity/2026-10-15-tighten-user-roles-and-permissions/scenarios.md
**Date:** 2026-10-15

## Description
The English text for `UnauthorizedMessage` in `src/RestaurantManagement.Shared/Localization/SharedResource.resx` reads "Unautorized user." (missing "h"). Every 401 response in English shows it, and after the private-by-default change that is every endpoint called without signing in. Scenario 28 (`UndeclaredEndpoint_NoAccessToken_ReturnsStandardUnauthorizedMessage`) asserts the current text, so fixing the typo must update that test in the same change.

## Why it matters
Users and client developers see the misspelled message on every 401.
