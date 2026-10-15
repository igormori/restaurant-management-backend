# Rename AuditFieldsPostgresFixture to a generic PostgreSQL fixture

**Found during:** docs/plans/organization/restrict-cascade-deletes/plan.md
**Date:** 2026-10-15

## Description
`tests/RestaurantManagement.Web.Tests/AuditFieldsMigrationTests.cs` defines `AuditFieldsPostgresFixture`. It creates a fresh database per test on one shared Testcontainers PostgreSQL container. Nothing in it is specific to audit fields. The restrict-cascade-deletes tests reuse it unchanged, so the name no longer matches how it is used. It also lives inside a test file rather than in its own file.

## Why it matters
More migration and constraint tests will reuse this fixture. A feature-specific name and location make it harder to find, and may lead to copy-pasted duplicates. Moving it to its own file with a generic name (for example `PostgresDatabaseFixture`) keeps Web.Tests consistent.
