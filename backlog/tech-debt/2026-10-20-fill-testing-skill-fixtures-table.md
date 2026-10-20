# Fill in the testing skill's fixtures and example files tables

**Status:** proposed
**Severity:** low
**Origin:** docs/plans/shared/2026-10-19-per-location-access-grid/plan.md · step 3 (tests)
**Date:** 2026-10-20

## Problem
Section 4 of the testing skill ("Existing fixtures and patterns") and its "Example files to copy"
table still hold the template placeholders (`<class/helper>`, `<path>`). The tester and the
implementer are told to use those fixtures and not to search for them, but the table names none,
so each agent has to search the test projects to find the seeding helpers, the signed-in client
and the database fixtures.

## Evidence
- `.claude/skills/testing/SKILL.md` section 4 — every row of both tables is a placeholder
- Real helpers it should name: `tests/RestaurantManagement.Web.Tests/TenantApiFactory.cs` (app,
  PostgreSQL database, seeding, signed-in client), `PostgresDatabaseFixture.cs`,
  `tests/RestaurantManagement.Modules.*.Tests/*TestDatabase.cs` (SQLite) and `TestTenant.cs`

## Acceptance criteria
- [ ] Every row of the fixtures table and of the example files table names a real class or file
- [ ] No `<...>` placeholder is left in `.claude/skills/testing/SKILL.md`

## Related
None
