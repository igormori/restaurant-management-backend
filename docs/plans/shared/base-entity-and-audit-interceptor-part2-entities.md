# Plan: Base entity and audit interceptor, part 2 (entities)
Spec: docs/specs/shared/base-entity-and-audit-interceptor.md
Depends on: docs/plans/shared/base-entity-and-audit-interceptor-part1-core.md
Covers acceptance criteria 5, 6, 8, 9 and the organizations/user_roles audit-only rule. Part 1 covers 1, 2, 3, 4, 7.

## Approach
Entities move onto the part 1 base classes and stop declaring Id and audit fields themselves.
Each module gets one generated AddAuditFields migration. OrganizationService creates the organization, settings and Owner role inside a tenant scope for the new organization.

## New files
| Module | Folder | File | Purpose |
|---|---|---|---|
| Menu | Migrations/ | AddAuditFields | Audit columns; location_menus.organization_id with backfill |
| Organization | Migrations/ | AddAuditFields | Audit columns on organizations, settings, locations |
| Identity | Migrations/ | AddAuditFields | Audit columns on user_roles |
| Menu.Tests | (root) | MenuAuditTests.cs | Menu create and update audit fields |
| Web.Tests | (root) | OrganizationCreationScopeTests.cs | Settings and Owner role get new org ID |
| Web.Tests | (root) | AuditFieldsMigrationTests.cs | Existing rows keep data after migrations (Postgres) |

## Modified files
| Module | Folder | File | What changes |
|---|---|---|---|
| Menu | Entities/ | Menu.cs, MenuCategory.cs, LocationMenu.cs | Derive TenantEntity; drop duplicated Id and audit fields |
| Organization | Entities/ | OrganizationSettings.cs, Location.cs | Derive TenantEntity; drop duplicated fields |
| Organization | Entities/ | Organization.cs | Derive BaseEntity; drop duplicated fields |
| Identity | Entities/ | UserRole.cs | Derive BaseEntity; keep nullable OrganizationId |
| Organization | Services/ | OrganizationService.cs | Inject ITenantScope; create saves and role assignment inside scope |
| Organization.Tests | (root) | OrganizationServiceCreateTests.cs, OrganizationServiceEditTests.cs | Pass ITenantScope to constructor |
| Menu.Tests | (root) | RestaurantManagement.Modules.Menu.Tests.csproj | Add Shouldly 4.3.0 |
| Organization.Tests | (root) | RestaurantManagement.Modules.Organization.Tests.csproj | Add Shouldly 4.3.0 |
| Web.Tests | (root) | RestaurantManagement.Web.Tests.csproj | Add Testcontainers.PostgreSql 4.15.0 |

## Signatures
```csharp
// Menu/Entities
public class Menu : TenantEntity
public class MenuCategory : TenantEntity // keeps Guid? LocationId
public class LocationMenu : TenantEntity
// Organization/Entities
public class Organization : BaseEntity
public class OrganizationSettings : TenantEntity
public class Location : TenantEntity
// Identity/Entities
public class UserRole : BaseEntity // keeps Guid? OrganizationId, Guid? LocationId
// Organization/Services/OrganizationService.cs
public OrganizationService(OrganizationDbContext orgDb, IUserRoleLookup userRoleLookup, IUserRoleAssigner userRoleAssigner, ITenantScope tenantScope, IStringLocalizer<SharedResource> localizer, ILogger<OrganizationService> logger)
```

## Patterns followed
- Migrations generated with the CLAUDE.md command, one per DbContext: Identity UniqueUserEmail.
- Postgres migration test via Testcontainers: PendingMigrationTests.cs.
- SQLite in-memory test context: MenuTestDatabase.cs.

## Flags
- Depends on part 1 and the select-organization task; do not start before both are merged (otherwise multi-org users cannot save tenant rows).
- A1: user_roles get audit fields only; OrganizationId untouched, not enforced.
- A4: UserRole derives BaseEntity, keeps nullable OrganizationId. A5: no OrganizationId-change check on user_roles or organizations.
- A6: location_menus.organization_id backfilled from menus.organization_id via menu_id.
- Backfills (decided): hand-written migrationBuilder.Sql allowed in the generated migrations for organization_id (from menus via menu_id) and updated_at (from created_at) only.
- "One migration" becomes three, one per module DbContext.
- MenuAuditTests attach the interceptor themselves; Web wiring is proven only by part 1 AuditInterceptorWiringTests.
- OrganizationService gains a constructor parameter (approved): existing Organization.Tests constructor calls get `new TenantScope()` added; no other test changes.
- Web.Tests gains Testcontainers.PostgreSql.
- users and user_verification_codes stay off BaseEntity; spec does not list them.
- Legacy: seven services set timestamps by hand; the interceptor overwrites them. Not refactored.

## Follow-ups
- OrganizationService still throws a hardcoded TEST_SENTRY exception in production code.
