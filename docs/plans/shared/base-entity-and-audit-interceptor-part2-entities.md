# Plan: Base entity and audit interceptor, part 2 (entities)
Spec: docs/specs/shared/base-entity-and-audit-interceptor.md
Depends on: docs/plans/shared/base-entity-and-audit-interceptor-part1-core.md
Covers acceptance criteria 5, 6, 8, 9 and the organizations/user_roles audit-only rule. Part 1 covers 1, 2, 3, 4, 7.

## Risk
High: three migrations on populated tables, two with migrationBuilder.Sql backfills.

## Approach
Entities derive from the part 1 base classes and drop their own Id and audit properties.
Each module gets one generated AddAuditFields migration. OrganizationService wraps organization creation and Owner assignment in a tenant scope for the new organization.

## New files
| Module | Folder | File | Purpose |
|---|---|---|---|
| Menu | Migrations/ | AddAuditFields | Audit columns; location_menus.organization_id; backfills; snapshot updated |
| Organization | Migrations/ | AddAuditFields | Audit user columns on organizations, settings, locations |
| Identity | Migrations/ | AddAuditFields | Audit user columns on user_roles |
| Menu.Tests | (root) | MenuAuditTests.cs | Menu create and update audit fields (SQLite, interceptor attached) |
| Organization.Tests | (root) | OrganizationCreationScopeTests.cs | Settings get new org ID; Owner assigned inside scope |
| Web.Tests | (root) | AuditFieldsMigrationTests.cs | Existing rows keep data after migrations (Postgres) |

## Modified files
| Module | Folder | File | What changes |
|---|---|---|---|
| Menu | Entities/ | Menu.cs, MenuCategory.cs, LocationMenu.cs | Derive TenantEntity; drop Id and audit properties |
| Organization | Entities/ | OrganizationSettings.cs, Location.cs | Derive TenantEntity; drop Id and audit properties |
| Organization | Entities/ | Organization.cs | Derive BaseEntity; drop Id and audit properties |
| Identity | Entities/ | UserRole.cs | Derive BaseEntity; keep nullable OrganizationId |
| Organization | Services/ | OrganizationService.cs | Inject ITenantScope; saves and Owner assignment inside scope |
| Organization.Tests | (root) | OrganizationServiceCreateTests.cs, OrganizationServiceEditTests.cs | Pass new TenantScope() to constructor |
| Menu.Tests | (root) | MenuTestDatabase.cs | CreateContext overload accepting an interceptor |
| Organization.Tests | (root) | OrganizationTestDatabase.cs | CreateContext overload accepting an interceptor |
| Menu.Tests | (root) | RestaurantManagement.Modules.Menu.Tests.csproj | Add Shouldly 4.3.0 |
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
// Menu.Tests/MenuTestDatabase.cs, Organization.Tests/OrganizationTestDatabase.cs
public MenuDbContext CreateContext(IInterceptor interceptor)
public OrganizationDbContext CreateContext(IInterceptor interceptor)
```

## Patterns followed
- Migration per DbContext via ef-migration skill: Identity UniqueUserEmail.
- Postgres container in-test, migrate then assert: Identity.Tests PendingMigrationTests.cs.
- SQLite in-memory test context: MenuTestDatabase.cs, OrganizationTestDatabase.cs.
- Shared contract injected into module service: IUserRoleAssigner in OrganizationService.cs.

## Flags
- Backfill (decided): location_menus.organization_id from menus via menu_id.
- Backfill (decided): updated_at from created_at on menu_categories and location_menus.
- 20 files exceed the 10-file slice rule. Proposed split: Organization, Menu, Identity slices.
- A1/A5: user_roles and organizations get audit only; no OrganizationId checks.
- Behaviour change: writes whose route or body org differs from the token org now return 403.
- Users with no org claim cannot write tenant rows; select-organization is merged.
- Web.Tests gains Testcontainers.PostgreSql, already used by Identity.Tests.
- MenuAuditTests attach the interceptor themselves; Web wiring proven by part 1 tests.
- users and user_verification_codes stay off BaseEntity; spec does not list them.
- Legacy: services still set timestamps by hand; the interceptor overwrites them. Not refactored.

## Follow-ups
- OrganizationService throws a hardcoded TEST_SENTRY exception in production code.
- CreateMenuRequest accepts OrganizationId from the body, against the security skill.
- MenuService loads menus by Id alone before checking the caller's organization.

## Scenarios
Fixed values: Org A `a1a1a1a1-...-0001`, Org B `b2b2b2b2-...-0002`; Maria and Carlos are user ids; creation time 2026-03-14 18:30 UTC, update time 2026-03-15 11:45 UTC. Part 1 (interceptor, scope, claims) is assumed done.

### MenuAuditTests (Menu.Tests, SQLite, interceptor attached)
| # | Scenario | Given | When | Then |
|---|---|---|---|---|
| 1 | New menu by user in Org A | Maria in Org A; new menu "Cardápio de Almoço" with no OrganizationId | SaveChangesAsync | OrganizationId = Org A; CreatedByUserId = Maria; CreatedAt = UpdatedAt = 18:30 UTC |
| 2 | Update menu | Stored "Cardápio de Jantar" (Org A, by Maria 03-14); Carlos in Org A renames it at 03-15 11:45 | SaveChangesAsync | UpdatedAt = 03-15 11:45; UpdatedByUserId = Carlos; CreatedAt 03-14 18:30 and CreatedByUserId Maria unchanged |
| 3 | New menu for another organization | Maria in Org A; new menu with OrganizationId = Org B | SaveChangesAsync | BusinessException 403 "TenantMismatch"; 0 menus stored |
| 4 | Cross-tenant update | Stored menu in Org A; Carlos in Org B changes its name | SaveChangesAsync | 403 "TenantMismatch"; name still "Cardápio de Jantar", UpdatedByUserId still Maria |
| 5 | OrganizationId changed on stored menu | Stored menu in Org A; Maria in Org A sets OrganizationId = Org B | SaveChangesAsync | 403 "TenantMismatch"; menu still Org A |
| 6 | No current organization | Maria with no org, no scope; new menu | SaveChangesAsync | 403 "OrganizationRequired"; 0 menus stored |
| 7 | New category gets org and UpdatedAt | Maria in Org A; new category "Massas" under an Org A menu | SaveChangesAsync | OrganizationId = Org A; CreatedAt = UpdatedAt = 18:30 UTC; CreatedByUserId = Maria |
| 8 | Category without location | Maria in Org A; new category "Sobremesas", LocationId null | SaveChangesAsync | Saved; LocationId stays null |
| 9 | New location menu gets org and UpdatedAt | Maria in Org A; new location menu for Centro location (no OrganizationId) | SaveChangesAsync | OrganizationId = Org A; UpdatedAt = 18:30 UTC; CreatedByUserId = Maria |
| 10 | Update category | Stored category "Massas" sort 1 created 03-14; Carlos in Org A sets sort 2 at 03-15 11:45 | SaveChangesAsync | UpdatedAt = 03-15 11:45; UpdatedByUserId = Carlos; CreatedAt = 03-14 18:30 |

### OrganizationCreationScopeTests (Organization.Tests, SQLite, real TenantScope and interceptor, IUserRoleAssigner mocked)
| # | Scenario | Given | When | Then |
|---|---|---|---|---|
| 1 | Settings get new org ID | Maria has no organization; creates "Cantina da Nonna" | CreateOrganizationAsync | Stored settings OrganizationId = response.Id |
| 2 | Scope wins over user's org | Maria's current org is Org A; creates "Cantina da Nonna" | CreateOrganizationAsync | Settings OrganizationId = new org id, not Org A |
| 3 | Audit fields on creation | Maria has no org, clock 18:30 UTC | CreateOrganizationAsync | Organization and settings: CreatedByUserId = Maria, CreatedAt = 18:30 UTC |
| 4 | Owner role gets new org ID inside scope | Maria creates "Cantina da Nonna"; assigner records ITenantScope.OrganizationId when called | CreateOrganizationAsync | AssignRoleAsync(Maria, response.Id, "Owner") called once; scope org during the call = response.Id |
| 5 | Scope closed after success | Maria creates an organization | CreateOrganizationAsync returns | ITenantScope.OrganizationId = null |
| 6 | Role assignment fails | Assigner throws InvalidOperationException | CreateOrganizationAsync | InvalidOperationException propagates; scope closed (null); 0 organizations and 0 settings stored |

### AuditFieldsMigrationTests (Web.Tests, PostgreSQL via Testcontainers; migrate to the previous migration, seed rows with SQL, migrate to AddAuditFields)
Seed for 1-5: menus "Cardápio de Jantar" (Org A, created 03-14 18:30) and "Cardápio de Almoço" (Org B, created 03-16 09:15), one location_menu per menu, category "Massas" (Org A, sort 3).
| # | Scenario | Given | When | Then |
|---|---|---|---|---|
| 1 | Menus keep data | Seed above | Migrate to AddAuditFields | Dinner menu keeps name, description "Pratos da noite", Org A, CreatedAt, UpdatedAt; CreatedByUserId and UpdatedByUserId null |
| 2 | location_menus.organization_id backfilled | Dinner location menu (menu in Org A), lunch location menu (menu in Org B) | Migrate | Dinner = Org A; lunch = Org B (not an empty Guid) |
| 3 | location_menus.updated_at backfilled | Lunch location menu created 03-16 09:15 | Migrate | UpdatedAt = 03-16 09:15; CreatedAt, LocationId, MenuId, IsActive kept |
| 4 | menu_categories.updated_at backfilled | Category "Massas" created 03-14 18:30 | Migrate | UpdatedAt = 03-14 18:30; name, description "Massas frescas", sort 3, Org A kept |
| 5 | New columns end NOT NULL | Seed above | Migrate | location_menus.organization_id, location_menus.updated_at, menu_categories.updated_at have is_nullable = NO |
| 6 | Empty menu tables | No rows | Migrate | Succeeds; all three tables empty |
| 7 | Organization tables keep data | Organization "Cantina da Nonna", its TRIAL settings, location "Unidade Centro" | Migrate | All rows keep their data; CreatedByUserId and UpdatedByUserId null on each |
| 8 | user_roles keep data | User Maria with Owner role in Org A, LocationId null | Migrate | Role, UserId, OrganizationId = Org A, LocationId null, CreatedAt kept; audit user ids null |
| 9 | Menu migrations match model | Empty database | Apply all Menu migrations | HasPendingModelChanges = false |
| 10 | Organization migrations match model | Empty database | Apply all Organization migrations | HasPendingModelChanges = false |
| 11 | Identity migrations match model | Empty database | Apply all Identity migrations | HasPendingModelChanges = false |

Security coverage: no new endpoint or role. Cross-tenant write is MenuAuditTests 3-5; the organization-A-cannot-change-B rule is enforced by the interceptor (part 1).

## Scaffold notes
- Entities already derive from the base classes (needed to compile the tests), so MenuAuditTests pass today; they guard against regression and prove the entity mapping. All the migration and OrganizationService behaviour is still unimplemented.
- OrganizationService takes ITenantScope and stores it; it does not use it yet.
- The two existing OrganizationService test files had `new TenantScope()` added to constructor calls (compile only, listed in this plan).
- Until the three AddAuditFields migrations exist, existing Identity tests that run MigrateAsync fail with PendingModelChangesWarning (8 tests). They go green with the Identity migration.
- Gate 1 decision: the user accepted MenuAuditTests as passing regression guards.
- Orphan location_menus (menu missing): no test. `location_menus.menu_id` has a required foreign key to `menus` (`fk_location_menus_menus_menu_id`, ON DELETE CASCADE) in `20260917231406_InitialCreate` and the Menu snapshot, so an orphan cannot exist in real data and the organization_id backfill join always finds a menu.
