---
name: testing
description: Testing conventions for the RestaurantManagement backend. Use when planning, writing or reviewing tests, deciding which test project a test belongs in, or defining a task's Definition of Done.
---

# Testing conventions

## 1. Tools
- xUnit, Shouldly for assertions, NSubstitute for mocks
- New tests use Shouldly, never FluentAssertions. Existing FluentAssertions tests stay as they
  are; don't migrate them unless asked

## 2. Where tests go
Test projects mirror `src/`: `tests/<ProjectName>.Tests`

| Test type | Project | Database |
|---|---|---|
| Module unit tests | `tests/RestaurantManagement.Modules.<Module>.Tests` | SQLite in-memory |
| Shared project tests | `tests/RestaurantManagement.Shared.Tests` | — |
| Integration tests (endpoints, real database behaviour, migrations) | `tests/RestaurantManagement.Web.Tests` | Testcontainers PostgreSQL, WebApplicationFactory |

- Behaviour that only PostgreSQL enforces (constraints, delete rules, migrations on data) is
  tested in Web.Tests, never on SQLite
- Never create a new test project without flagging it first

## 3. Writing tests
- Names: `Method_Scenario_ExpectedResult`,
  e.g. `DetectWaste_PurchasesExceedUsageBy25Percent_ReturnsVarianceAlert`
- Every test has explicit `// Arrange`, `// Act`, `// Assert` comments; one behaviour per test
- Realistic restaurant values (grams, dish names, prices in BRL, USD or CAD), never
  `foo`/`bar`/`123`
- No logic in tests: no loops or conditionals deciding what to assert
- Cover the failure cases first, then the happy path
- Bug fixes start with a failing test that reproduces the bug
- Tests are written before the implementation and must fail first

## 4. Existing fixtures and patterns
Use these; don't search the codebase for them. If one is missing for your case, follow the
closest example file below.

| Need | Use | Where |
|---|---|---|
| Module unit test database (SQLite in-memory) | <class/helper> | <path> |
| Integration test app and database (WebApplicationFactory + Testcontainers PostgreSQL) | <fixture class>, collection `<name>` | <path> |
| Signed-in client with organization, location and role | <helper method> | <path> |
| Second organization for cross-tenant tests | <helper method> | <path> |
| Seed an organization, location or user | <builder/helper> | <path> |
| Fake current user / tenant in unit tests | <helper or NSubstitute setup> | <path> |
| Clock / time zone in tests | <helper> | <path> |

### Example files to copy
| Test type | Example |
|---|---|
| Service unit test | <path> |
| Endpoint integration test (incl. 403 and cross-tenant 404) | <path> |
| PostgreSQL-only behaviour (constraint, migration) | <path> |