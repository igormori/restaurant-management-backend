# Plan: Base entity and audit interceptor, part 1 (core)
Spec: docs/specs/shared/base-entity-and-audit-interceptor.md
Covers acceptance criteria 1, 2, 3, 4, 7 and the login organization claim rule. Part 2 covers 5, 6, 8, 9.

## Approach
Shared gets the base classes, ICurrentUser, an AsyncLocal tenant scope and one SaveChanges interceptor that stamps and enforces fields on BaseEntity entries.
Web registers the claims-based user, clock and scope, and attaches the interceptor to all three DbContexts; no entity derives from the base classes yet, so runtime behaviour is unchanged.
Login adds the organization claim for single-organization users.

## New files
| Module | Folder | File | Purpose |
|---|---|---|---|
| Shared | Data/ | BaseEntity.cs, TenantEntity.cs, LocationEntity.cs | Audit, tenant and location base classes |
| Shared | Data/ | ITenantScope.cs, TenantScope.cs | Explicit organization scope, AsyncLocal-backed |
| Shared | Data/ | AuditSaveChangesInterceptor.cs | Stamps audit fields, enforces tenant rules |
| Shared | Auth/ | ICurrentUser.cs, AppClaimTypes.cs | Current user contract, organization claim constant |
| Web | Auth/ | ClaimsCurrentUser.cs | ICurrentUser read from HttpContext claims |
| Shared.Tests | (root) | AuditTestDbContext.cs | Test-only SQLite context with base-class entities |
| Shared.Tests | (root) | AuditSaveChangesInterceptorTests.cs | Stamping, rejections, nothing written, fixed clock |
| Shared.Tests | (root) | TenantScopeTests.cs | Begin, dispose, nesting, flow across awaits |
| Identity.Tests | (root) | SessionServiceOrganizationClaimTests.cs | Org claim only when user has one org |
| Web.Tests | (root) | ClaimsCurrentUserTests.cs | Claim parsing; missing or malformed claims |
| Web.Tests | (root) | AuditInterceptorWiringTests.cs | Each module DbContext resolves with the interceptor |

## Modified files
| Module | Folder | File | What changes |
|---|---|---|---|
| Shared | (root) | RestaurantManagement.Shared.csproj | Add Microsoft.EntityFrameworkCore 9.0.2 |
| Shared | Localization/ | SharedResource.resx, SharedResource.pt.resx | Tenant mismatch and missing organization messages |
| Identity | Services/ | SessionService.cs | GenerateJwt adds org claim for single-org users |
| Web | (root) | Program.cs | Register ICurrentUser, TimeProvider, scope, interceptor on DbContexts |
| Shared.Tests | (root) | RestaurantManagement.Shared.Tests.csproj | Add Shouldly 4.3.0, EF Core Sqlite 9.0.2 |
| Identity.Tests | (root) | RestaurantManagement.Modules.Identity.Tests.csproj | Add Shouldly 4.3.0 |

## Signatures
```csharp
// Shared/Data (all properties { get; set; })
public abstract class BaseEntity // Guid Id, DateTime CreatedAt, DateTime UpdatedAt, Guid? CreatedByUserId, Guid? UpdatedByUserId
public abstract class TenantEntity : BaseEntity // Guid OrganizationId
public abstract class LocationEntity : TenantEntity // Guid LocationId
public interface ITenantScope { Guid? OrganizationId { get; } IDisposable Begin(Guid organizationId); }
public sealed class TenantScope : ITenantScope // Dispose restores the previous value
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
    public AuditSaveChangesInterceptor(ICurrentUser currentUser, ITenantScope tenantScope, TimeProvider clock, IStringLocalizer<SharedResource> localizer)
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
// Shared/Auth
public interface ICurrentUser { Guid? UserId { get; } Guid? OrganizationId { get; } }
public static class AppClaimTypes { public const string OrganizationId = "org_id"; }
// Web/Auth
public sealed class ClaimsCurrentUser : ICurrentUser // ctor(IHttpContextAccessor httpContextAccessor)
```

## Patterns followed
- Shared contract, implemented elsewhere, registered in Program.cs: IUserRoleLookup, UserRoleLookup.cs.
- Web helpers in feature folders: Web/RateLimiting, Web/Hosting.
- Localized BusinessException with status code: OrganizationService.cs.
- SQLite in-memory test context: MenuTestDatabase.cs.

## Scenarios
Fixed values: Org A `a1a1a1a1-...-0001`, Org B `b2b2b2b2-...-0002`, Org C `c3c3c3c3-...-0003`; Maria and Carlos are user ids; creation time 2026-03-14 18:30 UTC, update time 2026-03-15 11:45 UTC. AC = spec acceptance criterion (1 reject other org, 2 reject OrganizationId change, 3 reject no org/scope, 4 scope overrides user org, 5 created audit fields, 6 updated audit fields, 7 fixed clock UTC; the spec numbers them in this order). Tests use a test-only `TestDish : TenantEntity` and `TestOrganization : BaseEntity`.

### AuditSaveChangesInterceptorTests (Shared.Tests)
| # | Scenario | Given | When | Then |
|---|---|---|---|---|
| 1 | New tenant row with another org's id (AC1) | User Maria in Org A; new dish "Pizza Margherita" R$ 54,90 with OrganizationId = Org B | SaveChangesAsync | BusinessException 403 "TenantMismatch"; 0 dishes stored |
| 2 | Same, synchronous save (AC1) | Same as 1 | SaveChanges | BusinessException 403; 0 dishes stored |
| 3 | Rejection writes nothing in a batch (AC1) | User in Org A; new organization "Trattoria Bella Vita" plus dish with OrganizationId = Org B in one save | SaveChangesAsync | Throws; 0 organizations and 0 dishes stored |
| 4 | Scope org differs from the entity's own org (A2) | User in Org A, scope Org C; new dish carrying OrganizationId = Org A | SaveChangesAsync | BusinessException 403 "TenantMismatch"; 0 dishes stored |
| 5 | OrganizationId changed on existing row (AC2) | Stored dish "Risoto de Cogumelos" in Org A; user in Org A sets OrganizationId = Org B | SaveChangesAsync | BusinessException 403 "TenantMismatch"; stored row still Org A |
| 6 | No current org and no scope (AC3) | User Maria with no org, no scope; new dish | SaveChangesAsync | BusinessException 403 "OrganizationRequired"; 0 dishes stored |
| 7 | Scope overrides the user's org (AC4) | User in Org A, scope Org C; new dish with OrganizationId unset | SaveChangesAsync | Dish OrganizationId = Org C |
| 8 | Scope open, user has no org (AC4, org creation) | User Maria with no org, scope Org C; new dish | SaveChangesAsync | Saved; stored OrganizationId = Org C |
| 9 | New dish by a user in Org A (AC5) | User Maria in Org A, clock 2026-03-14 18:30 UTC | SaveChangesAsync | Stored OrganizationId = Org A, CreatedByUserId = Maria, CreatedAt = 18:30 UTC |
| 10 | UpdatedAt equals CreatedAt on create (A8) | Same as 9 | SaveChangesAsync | UpdatedAt = CreatedAt; UpdatedByUserId = Maria |
| 11 | Empty Id is filled | New dish with Guid.Empty id (Id has ValueGeneratedNever in the test context) | SaveChangesAsync | Id is not Guid.Empty |
| 12 | Preset Id is kept | New dish with Id `33333333-cccc-...-000c` | SaveChangesAsync | Id still `33333333-cccc-...-000c` |
| 13 | Entity already carries the current org | User in Org A; new dish with OrganizationId = Org A | SaveChangesAsync | Saved; stored OrganizationId = Org A |
| 14 | Caller cannot set audit users | User Maria; new dish with CreatedByUserId and UpdatedByUserId = Carlos | SaveChangesAsync | Both = Maria |
| 15 | Synchronous save stamps too (AC5) | User Maria in Org A, clock 18:30 UTC; new dish | SaveChanges | OrganizationId = Org A, CreatedByUserId = Maria, CreatedAt = 18:30 UTC |
| 16 | Non-tenant row needs no org (spec: organizations get audit only) | User Maria, no org, no scope; new TestOrganization "Cantina da Nonna" | SaveChangesAsync | Saved; CreatedByUserId = Maria, CreatedAt = 18:30 UTC |
| 17 | Non-tenant row with no signed-in user (A7) | No user, no org; new TestOrganization | SaveChangesAsync | Saved; CreatedByUserId and UpdatedByUserId null |
| 18 | Modified dish gets update audit (AC6) | Stored dish created 03-14 by Maria; Carlos in Org A at 03-15 11:45 UTC changes price to R$ 72,50 | SaveChangesAsync | UpdatedAt = 03-15 11:45 UTC; UpdatedByUserId = Carlos |
| 19 | Created fields unchanged on update (AC6) | Same as 18 | SaveChangesAsync | CreatedAt = 03-14 18:30 UTC; CreatedByUserId = Maria |
| 20 | Modified non-tenant row, no org | Stored TestOrganization created by Maria; Carlos with no org renames it | SaveChangesAsync | UpdatedAt = 03-15 11:45 UTC; UpdatedByUserId = Carlos; CreatedByUserId = Maria |
| 21 | Fixed clock on create (AC7) | Clock fixed at 2026-03-14 18:30 UTC; new dish | SaveChangesAsync | CreatedAt and UpdatedAt = 18:30, Kind = Utc |
| 22 | Fixed clock on update (AC7) | Clock fixed at 2026-03-15 11:45 UTC; stored dish modified | SaveChangesAsync | UpdatedAt = 11:45, Kind = Utc |

### TenantScopeTests (Shared.Tests)
| # | Scenario | Given | When | Then |
|---|---|---|---|---|
| 23 | No scope | New TenantScope | Read OrganizationId | null |
| 24 | Begin | New TenantScope | Begin(Org C) | OrganizationId = Org C |
| 25 | Dispose restores | Scope begun with Org C | Dispose | OrganizationId = null |
| 26 | Nested scope | Outer Begin(Org A), inner Begin(Org C) | Inner disposed | OrganizationId = Org A |
| 27 | Flows across awaits | Scope begun with Org C | await Task.Delay(10); await Task.Yield() | OrganizationId = Org C |
| 28 | Does not leak between async flows | Begin(Org C) called inside Task.Run | Awaited, caller reads OrganizationId | null |

### SessionServiceOrganizationClaimTests (Identity.Tests; login org claim rule)
| # | Scenario | Given | When | Then |
|---|---|---|---|---|
| 29 | Multi-org user gets no claim | Maria has Owner in Org A and Staff in Org B | LoginAsync | JWT has no `org_id` claim |
| 30 | User with no roles | Maria has no roles | LoginAsync | No `org_id` claim |
| 31 | Roles without organization | Maria's only role has OrganizationId null | LoginAsync | No `org_id` claim |
| 32 | Single-org user | Maria has Owner in Org A | LoginAsync | `org_id` = Org A id |
| 33 | Two roles, same org | Maria has Owner in Org A and Staff in Org A at a location | LoginAsync | Exactly one `org_id` claim = Org A id |
| 34 | Refresh reissues the claim (A3) | Single-org Maria logged in | RefreshTokenAsync | New JWT `org_id` = Org A id |

### ClaimsCurrentUserTests (Web.Tests)
| # | Scenario | Given | When | Then |
|---|---|---|---|---|
| 35 | No HttpContext (UserId) | Accessor returns null HttpContext | Read UserId | null |
| 36 | No HttpContext (OrganizationId) | Accessor returns null HttpContext | Read OrganizationId | null |
| 37 | Malformed user claim | NameIdentifier = "maria-souza" | Read UserId | null |
| 38 | Malformed org claim | `org_id` = "cantina-da-nonna" | Read OrganizationId | null |
| 39 | Missing org claim | Only NameIdentifier set | Read OrganizationId | null |
| 40 | Valid user claim | NameIdentifier = Maria's guid | Read UserId | Maria's guid |
| 41 | Valid org claim | `org_id` = Org A guid | Read OrganizationId | Org A guid |

### AuditInterceptorWiringTests (Web.Tests)
| # | Scenario | Given | When | Then |
|---|---|---|---|---|
| 42 | Identity context wired | Web host built | Resolve IdentityDbContext | Options contain an AuditSaveChangesInterceptor |
| 43 | Menu context wired | Web host built | Resolve MenuDbContext | Same |
| 44 | Organization context wired | Web host built | Resolve OrganizationDbContext | Same |
| 45 | ICurrentUser registered | Web host built | Resolve ICurrentUser in a scope | ClaimsCurrentUser |
| 46 | Tenant scope is a singleton | Web host built | Resolve ITenantScope from two scopes | Same instance |

## Tasks
1. [x] TenantScope (AsyncLocal) — scenarios 23–28
2. AuditSaveChangesInterceptor — scenarios 1–22
3. ClaimsCurrentUser — scenarios 35–41
4. SessionService org claim — scenarios 29–34
5. Program.cs wiring (lifetimes, interceptor on all DbContexts) — scenarios 42–46

## Flags
- A2 (decided): explicit scope overrides the claim; only a differing entity org is rejected. Begin is only for organization creation; /review checks every call.
- A3: claim auto-set for exactly one org; GenerateJwt is shared, so refresh reissues it too.
- Multi-org users get no claim (decided): harmless in part 1; the select-organization task ships before part 2.
- A7: no signed-in user on non-tenant rows leaves CreatedBy/UpdatedBy null. A8: UpdatedAt equals CreatedAt on create.
- A9: `using (tenantScope.Begin(orgId)) { ... }`; nested Begin restores the outer scope on dispose.
- A10: interceptor attached in Web AddDbContext, not inside module DbContexts. Overrides the assumption's wording.
- Lifetimes: TenantScope singleton, ClaimsCurrentUser scoped, TimeProvider.System singleton, interceptor scoped and resolved per DbContext scope.
- LocationEntity has no users until task 6.
- Shared gains an EF Core package reference. Rejections throw BusinessException 403 before any write.
- No cross-module DbContext injection found. No new test project; Shared.Tests exists.

## Follow-ups
- RefreshTokenAsync matches request.Email without lowercasing; login lowercases.
