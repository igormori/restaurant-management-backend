# Plan: Owner role must never be missing after organization creation
Spec: docs/specs/organization/consistency-issue.md

## Approach
Keep the organization transaction open across the `IUserRoleAssigner.AssignRoleAsync` call and
commit only after the Owner role is confirmed. If assignment throws, roll back, so nothing was
ever committed. Each module still writes through its own DbContext and its own transaction.

## Changes
| File | New/Modified | What changes |
|---|---|---|
| src/RestaurantManagement.Modules.Organization/Services/OrganizationService.cs | Modified | Save, assign role, then commit; rollback and log on failure |
| tests/RestaurantManagement.Modules.Organization.Tests/RestaurantManagement.Modules.Organization.Tests.csproj | New | Copy of Identity test csproj, referencing Organization |
| tests/RestaurantManagement.Modules.Organization.Tests/OrganizationTestDatabase.cs | New | SQLite in-memory OrganizationDbContext factory |
| tests/RestaurantManagement.Modules.Organization.Tests/TestLocalizer.cs | New | Copy of Identity test localizer |
| tests/RestaurantManagement.Modules.Organization.Tests/OrganizationServiceCreateTests.cs | New | Tests below |
| RestaurantManagement.sln | Modified | Register the new test project |

## API
No change. Failure still surfaces as 500 through ExceptionHandlingMiddleware.

## Data
No change. No migration.

## Authorization
No change: `[Authorize]` on create; ownership scoping still comes from the Owner UserRole written by Identity.

## Steps
1. Add `ILogger<OrganizationService>` as a constructor dependency -> verify: `dotnet build RestaurantManagement.sln`
2. Move `SaveChangesAsync` inside the transaction and drop the early `CommitAsync` -> verify: build
3. Call `AssignRoleAsync`; on exception rollback, log error, rethrow -> verify: build
4. Commit after assignment; if commit throws, log the dangling UserRole and rethrow -> verify: build
5. Wrap the rollback in its own try/catch that logs failures -> verify: build
6. Create the test project and add it to the solution -> verify: `dotnet build RestaurantManagement.sln`
7. Add the test database and localizer helpers -> verify: build
8. Write the tests below -> verify: `dotnet test`
9. Format -> verify: `dotnet format`

## Tests
| Acceptance criterion | Test name and assertion |
|---|---|
| Not left orphaned when assignment fails | `CreateOrganizationAsync_WhenRoleAssignmentFails_PersistsNoOrganizationOrSettings`: both tables empty |
| Rolled back org absent from list | `GetOrganizationsAsync_AfterFailedRoleAssignment_ReturnsEmptyList` |
| Trial limit not consumed | `CreateOrganizationAsync_AfterFailedRoleAssignment_SecondAttemptSucceeds` |
| Compensation failure is observable | `CreateOrganizationAsync_WhenRollbackFails_LogsError`: close connection in stub, assert Error logged |
| Success only once Owner role held | `CreateOrganizationAsync_WhenRoleAssignmentFails_ThrowsAndReturnsNoResponse` |
| Happy path unchanged | `CreateOrganizationAsync_WhenRoleAssignmentSucceeds_PersistsAllAndReturnsResponse`: assigner got `Roles.Owner` |

## Decisions
- Chose deferred commit with rollback over retry, outbox, or post-commit delete; confirm before implementing.
- Organization is never transiently visible: it is uncommitted until the Owner role is confirmed.
- If the commit fails after assignment, a UserRole for a non-existent organization remains; logged, invisible to list and trial checks.
- Errors reported via `ILogger.LogError` (reaches Sentry through `UseSentry`) rather than a direct `SentrySdk` call, so tests can assert it.
- Tests use SQLite in-memory plus NSubstitute, mirroring the Identity test project.

## Follow-ups
- Organizations already created without an Owner role need a one-off audit and repair.
- Dangling UserRole rows pointing at non-existent organizations have no cleanup path.
- `TEST_SENTRY` name check is production code inside `CreateOrganizationAsync`.
- `CreateOrganizationAsync` throws raw `Exception` with database inner messages instead of a localized message.
