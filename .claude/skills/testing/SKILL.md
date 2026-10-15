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
- Realistic restaurant values (grams, dish names, prices in BRL), never `foo`/`bar`/`123`
- No logic in tests: no loops or conditionals deciding what to assert
- Cover the failure cases first, then the happy path
- Bug fixes start with a failing test that reproduces the bug
- Tests are written before the implementation and must fail first
