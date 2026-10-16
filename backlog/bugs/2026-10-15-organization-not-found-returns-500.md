# Organization: missing user, organization or settings returns 500 instead of a not-found answer

**Found during:** docs/plans/shared/2026-10-15-localize-client-messages/plan.md
**Date:** 2026-10-15

## Description
`OrganizationService` throws `InvalidOperationException` (with a localized message) when the user, the organization or its settings are not found (create, edit, list). The exception handler treats it as an unexpected error, so the client gets a 500 with the generic message and the localized text is thrown away.

## Why it matters
A normal "not found" case looks like a server failure to the client, is reported to Sentry as a crash, and the translated message never reaches the user. It should be a 404 (or 401 for a missing user) with the localized message.
