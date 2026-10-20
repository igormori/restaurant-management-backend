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
- Integration tests go in the module's folder under `tests/RestaurantManagement.Web.Tests/`
  (`Identity/`, `Web/`, `Menu/`, `Organization/`, `Shared/`; same namespace as the rest of
  Web.Tests), so each module's tests sit in two folders (root `CLAUDE.md`, "Security matrices")
- Every test of a module with a matrix is named in `docs/security/<module>-matrix.md`; a new one
  is added to its row in the same commit
- Never create a new test project without flagging it first

## 3. Writing tests
- Names: `Method_Scenario_ExpectedResult`,
  e.g. `DetectWaste_PurchasesExceedUsageBy25Percent_ReturnsVarianceAlert`
- Every test has explicit `// Arrange`, `// Act`, `// Assert` comments; one behaviour per test
- Realistic restaurant values (grams, dish names, prices in BRL, USD or CAD), never
  `foo`/`bar`/`123`
- A test named in a security matrix (`docs/security/*-matrix.md`) carries one
  `[Trait("Rule", "<row ID>")]` per row that names it, placed directly above the method
  (`dotnet test --filter Rule=L8` runs one rule). The matrix check fails if the tags and the rows
  disagree
- The assertions that prove the rule carry a message saying the rule in plain words, so a
  failure reads without opening the test: Shouldly `customMessage`
  (`user.LockedUntil.ShouldNotBeNull("the fifth wrong password locks the account for 15 minutes")`),
  FluentAssertions `because` (without the word "because"). Setup assertions need none
- No logic in tests: no loops or conditionals deciding what to assert
- Cover the failure cases first, then the happy path
- Bug fixes start with a failing test that reproduces the bug
- Tests are written before the implementation and must fail first

## 4. Existing fixtures and patterns
Use these; don't search the codebase for them. If one is missing for your case, follow the
closest example file below.

| Need | Use | Where |
|---|---|---|
| Module unit test database (SQLite in-memory) | `MenuTestDatabase`, `OrganizationTestDatabase`, `IdentityTestDatabase` (`CreateContext()` seeds with no organization; `CreateContext(tenant)` reads as one) | `tests/RestaurantManagement.Modules.<Module>.Tests/<Module>TestDatabase.cs` |
| Integration test app and database (WebApplicationFactory + Testcontainers PostgreSQL) | `TenantApiFactory.CreateAsync(postgres)` over `PostgresDatabaseFixture` as `IClassFixture<PostgresDatabaseFixture>` (one container per class, one empty migrated database per test; no collection) | `tests/RestaurantManagement.Web.Tests/TenantApiFactory.cs`, `PostgresDatabaseFixture.cs` |
| Signed-in client with organization, location and access grid | `factory.ClientFor(userId, organizationId, locationHeader)`; the token holds no role, so give access with `SeedUserAsync` (`OwnerOf` / `MemberOf`) and `GrantAsync` / `SetLevelAsync` | `tests/RestaurantManagement.Web.Tests/TenantApiFactory.cs` |
| Second organization for cross-tenant tests | Seed it with `SeedOrganizationAsync` and read its rows back with `InOrganizationAsync<TContext, T>`; see `CreateFactoryWithTwoOrganizationsAsync` | `tests/RestaurantManagement.Web.Tests/Menu/MenuTenantEndpointTests.cs` |
| Seed an organization, location or user | `SeedOrganizationAsync`, `SeedLocationAsync`, `SeedUserAsync`, `AddMembershipAsync`, `SeedMenuAsync`, `SeedCategoryAsync`, `SeedAuditEntriesAsync` | `tests/RestaurantManagement.Web.Tests/TenantApiFactory.cs` |
| Fake current user / tenant in unit tests | `TestTenant.Of(organizationId, locationId)` / `TestTenant.None`; `TestAccess.Guard(...)`, `OwnerGuard`, `Permissive()` for the access guard; `Substitute.For<ICurrentUser>()`; `TestMembers` and `MemberAccessTestSupport` in Identity | `tests/RestaurantManagement.Modules.{Menu,Organization}.Tests/TestTenant.cs`, `TestAccess.cs`; `tests/RestaurantManagement.Modules.Identity.Tests/TestMembers.cs`, `MemberAccessTestSupport.cs` |
| Clock / time zone in tests | `FixedTimeProvider(now)` passed as the `TimeProvider`; locations take an IANA time zone in `SeedLocationAsync` (e.g. `"America/Sao_Paulo"`) | `tests/RestaurantManagement.Shared.Tests/AuditTestDbContext.cs`, `tests/RestaurantManagement.Modules.Menu.Tests/MenuAuditTests.cs` |

### Example files to copy
| Test type | Example |
|---|---|
| Service unit test | `tests/RestaurantManagement.Modules.Menu.Tests/MenuGridAccessTests.cs` |
| Endpoint integration test (incl. 403 and cross-tenant 404) | `tests/RestaurantManagement.Web.Tests/Menu/MenuGridEndpointTests.cs` (403), `tests/RestaurantManagement.Web.Tests/Menu/MenuTenantEndpointTests.cs` (cross-tenant 404) |
| PostgreSQL-only behaviour (constraint, migration) | `tests/RestaurantManagement.Web.Tests/Menu/MenuDeleteCascadeTests.cs` (delete rule), `tests/RestaurantManagement.Web.Tests/Identity/DropUserSelectedOrganizationMigrationTests.cs` (migration) |
