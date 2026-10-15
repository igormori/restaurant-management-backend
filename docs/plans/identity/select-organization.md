# Plan: Select organization
Spec: docs/specs/identity/select-organization.md

## Approach
Too big for one slice (about 25 files), so it ships as three: S1 Shared contract plus Organization.IsActive, S2 select and refresh, S3 login choices and inactive rejection. User stores SelectedOrganizationId; select and refresh issue the JWT for it with only that organization's roles. Organization data reaches Identity only through IOrganizationLookup.

## New files
| Module | Folder | File | Purpose |
|---|---|---|---|
| Shared | Services/Organization | OrganizationSummaryDto.cs | S1: Id, Name, IsActive |
| Organization | Migrations | AddOrganizationIsActive | S1: generated; column defaults to true |
| Organization.Tests | (root) | OrganizationLookupTests.cs | S1: names and active flag for requested IDs |
| Identity | Models | SelectOrganizationRequest.cs | S2: OrganizationId |
| Identity | Migrations | AddUserSelectedOrganizationId | S2: generated; nullable column, no backfill |
| Identity.Tests | (root) | SessionServiceSelectOrganizationTests.cs | S2, S3: select, refresh, roles, login choices |
| Web.Tests | (root) | SelectOrganizationEndpointTests.cs | S2: unauthenticated request returns 401 |
| Identity | Models | OrganizationChoice.cs | S3: Id, Name |

## Modified files
| Module | Folder | File | What changes |
|---|---|---|---|
| Shared | Services/Organization | IOrganizationLookup.cs | S1: add GetOrganizationsAsync |
| Organization | Entities | Organization.cs | S1: IsActive |
| Organization | Data | OrganizationDbContext.cs | S1: IsActive HasDefaultValue(true) |
| Organization | Services | OrganizationLookup.cs | S1: implement GetOrganizationsAsync |
| Organization.Tests | (root) | RestaurantManagement.Modules.Organization.Tests.csproj | S1: add Shouldly |
| Identity | Entities | User.cs | S2: SelectedOrganizationId |
| Identity | Services | ISessionService.cs | S2: SelectOrganizationAsync |
| Identity | Services | SessionService.cs | S2: select; refresh keeps organization; S3: lookup, choices |
| Identity | Controllers | AuthController.cs | S2: authorized select-organization action |
| Identity | Models | AuthResponse.cs | S3: Organizations list |
| Identity.Tests | (root) | SessionService{Login,Refresh,Verification,OrganizationClaim}Tests.cs, AuthUniformResponseTests.cs | S3: pass IOrganizationLookup substitute |

## Signatures
```csharp
public class OrganizationSummaryDto { Guid Id; string Name; bool IsActive }  // Shared, { get; set; }
Task<List<OrganizationSummaryDto>> GetOrganizationsAsync(IReadOnlyCollection<Guid> organizationIds);  // IOrganizationLookup
public bool IsActive { get; set; } = true;  // Organization/Entities/Organization.cs
public Guid? SelectedOrganizationId { get; set; }  // Identity/Entities/User.cs
public class SelectOrganizationRequest { [Required] Guid OrganizationId }  // { get; set; }
public class OrganizationChoice { Guid Id; string Name }  // { get; set; }
public List<OrganizationChoice>? Organizations { get; set; }  // AuthResponse
Task<AuthResponse> SelectOrganizationAsync(Guid userId, SelectOrganizationRequest request);  // ISessionService
// SessionService: S3 constructor; private GenerateJwt gains the selected organization
public SessionService(IdentityDbContext db, IOptions<SecurityOptions> securityOptions, IOptions<JwtOptions> jwtOptions,
    IStringLocalizer<SharedResource> localizer, IHub hub, IOrganizationLookup organizationLookup)
private string GenerateJwt(User user, Guid? selectedOrganizationId)
// AuthController
[HttpPost("select-organization")] [Authorize]
public async Task<ActionResult<AuthResponse>> SelectOrganization([FromBody] SelectOrganizationRequest request)
```

## Patterns followed
- Contract plus DTO: IOrganizationLookup.GetLocationAsync and LocationSummaryDto.
- User ID from NameIdentifier, else Unauthorized(): OrganizationController.cs.
- Token gates, rotation, BusinessException codes: SessionService.RefreshTokenAsync.
- SQLite tests with substitutes: SessionServiceOrganizationClaimTests.cs; endpoint tests: AuthApiFactory.cs.

## Assumptions (unconfirmed)
- Non-member, non-existent and inactive organizations: 403 `UserNotMemberOfOrganization`, identical body.
- Select and switch revoke the previous refresh token by overwriting RefreshTokenHash.
- Single-organization users may call select; it reissues that organization's token.
- Users with no organizations: login unchanged, no claim, empty list.

## Flags
- Slice split: S1 needs its own spec and ships first.
- Organization has no active concept; S1 adds IsActive, nothing sets it false yet.
- Migrations: AddOrganizationIsActive, AddUserSelectedOrganizationId. No migrationBuilder.Sql backfill.
- Empty OrganizationId: 400 with existing `OrganizationRequired` key.
- Login clears SelectedOrganizationId; refresh rejects 401 if membership was removed; no active recheck.
- Login choices exclude inactive organizations.
- Five existing test files change only their SessionService constructor call.
- Legacy cross-module DbContext use: none found.

## Follow-ups
- SessionService uses key `InvlideOrExpiredToken`; resx has `InvalidOrExpiredToken`, so raw key is returned.
- Multi-organization login token carries roles from all organizations.

## Scenarios
Slice and spec acceptance criterion (AC) in brackets. Organizations: Cantina da Nonna (Cantina), Trattoria do Sol (Trattoria). Maria is the user under test.

| # | Scenario | Given | When | Then |
|---|---|---|---|---|
| 1 | Empty organization ID [S2; AC empty ID] | Maria is Owner in Cantina, Staff in Trattoria | SelectOrganizationAsync with Guid.Empty | BusinessException 400, message `OrganizationRequired` |
| 2 | Non-member [S2; AC no role] | Maria is Owner in Cantina only; Trattoria exists and is active | Select Trattoria | BusinessException 403, `UserNotMemberOfOrganization` |
| 3 | Non-existent organization [S2; AC same rejection] | Maria is Owner in Cantina; lookup knows only Cantina | Select an unknown organization ID | BusinessException 403, `UserNotMemberOfOrganization` (identical to 2) |
| 4 | Role in an organization that no longer exists [S2; AC same rejection] | Maria has a Staff role in an ID the lookup does not return | Select that ID | BusinessException 403, `UserNotMemberOfOrganization` |
| 5 | Inactive organization [S2; AC inactive] | Maria is a member of both; Trattoria IsActive=false | Select Trattoria | BusinessException 403, `UserNotMemberOfOrganization` |
| 6 | Another user's organization [S2; AC other user's org] | Maria in Cantina; Paolo is Owner of Trattoria (exists, active) | Maria selects Trattoria | BusinessException 403, `UserNotMemberOfOrganization` |
| 7 | Unknown user ID [S2] | No user with the ID | Select Cantina | BusinessException 401 |
| 8 | Deactivated user [S2; AC deactivated] | Maria IsActive=false | Select Cantina | BusinessException 401 |
| 9 | Unverified user [S2] | Maria IsVerified=false | Select Cantina | BusinessException 401 |
| 10 | Locked user [S2] | Maria LockedUntil = now + 15 min | Select Cantina | BusinessException 401 |
| 11 | Select a member organization [S2; AC token claim] | Owner in Cantina, Staff in Trattoria | Select Trattoria | JWT organization claim = Trattoria ID |
| 12 | Roles limited to selected organization [S2; AC roles] | Owner in Cantina, Staff in Trattoria | Select Trattoria | Role claims = [Staff] |
| 13 | Several roles in selected organization [S2; AC roles] | Owner and Admin in Cantina, Staff in Trattoria | Select Cantina | Role claims = [Admin, Owner] |
| 14 | Single-organization user selects [S2; assumption] | Owner in Cantina only | Select Cantina | Claim = Cantina ID |
| 15 | New refresh token works [S2; AC refresh keeps org] | Maria selected Cantina | Refresh with the select response's refresh token | Succeeds, claim = Cantina ID |
| 16 | Select revokes previous refresh token [S2; assumption] | Maria logged in, then selected Cantina | Refresh with the login refresh token | BusinessException 401 |
| 17 | Switch organization [S2; AC second org] | Maria selected Cantina, then Trattoria | Refresh with the second select's refresh token | Select token claim = Trattoria; refreshed claim = Trattoria |
| 18 | Refresh keeps selection and roles [S2; AC refresh] | Maria selected Trattoria (Staff there, Owner in Cantina) | Refresh | Claim = Trattoria ID; role claims = [Staff] |
| 19 | Membership removed after selecting [S2; plan flag] | Maria selected Trattoria; her Trattoria role is deleted | Refresh | BusinessException 401 |
| 20 | Login choices [S3; AC login choices, names] | Owner in Cantina, Staff in Trattoria; both active | LoginAsync | No organization claim; Organizations = (Cantina, "Cantina da Nonna"), (Trattoria, "Trattoria do Sol") |
| 21 | Login excludes inactive organizations [S3; plan flag] | Same, Trattoria IsActive=false | LoginAsync | Organizations = [Cantina] |
| 22 | Login lists only own organizations [S3; AC only roles held] | Paolo owns a third organization (Pizzaria Vila Madalena); lookup knows all three | Maria logs in | Organizations = [Cantina, Trattoria] |
| 23 | Login with no organizations [S3; assumption] | Maria has no roles | LoginAsync | No organization claim; Organizations is an empty list (not null) |
| 24 | Login clears selection [S3; plan flag] | Maria selected Cantina | LoginAsync | No organization claim |
| 25 | Refresh after new login does not restore selection [S3; plan flag] | Maria selected Cantina, then logged in again | Refresh with the login refresh token | No organization claim |
| 26 | Lookup returns names and flags [S1] | Cantina active, Trattoria inactive, Pizzaria active | GetOrganizationsAsync([Cantina, Trattoria]) | (Cantina, "Cantina da Nonna", true), (Trattoria, "Trattoria do Sol", false) |
| 27 | Lookup ignores unrequested organizations [S1] | Same three organizations | GetOrganizationsAsync([Cantina]) | Only Cantina |
| 28 | Lookup omits unknown IDs [S1] | Same three | GetOrganizationsAsync([Pizzaria, unknown ID]) | Only Pizzaria |
| 29 | Lookup with empty list [S1] | Same three | GetOrganizationsAsync([]) | Empty list |
| 30 | New organization is active by default [S1] | Pizzaria saved without setting IsActive | GetOrganizationsAsync([Pizzaria]) | IsActive = true |
| 31 | Unauthenticated select [S2; AC unauthenticated] | No bearer token | POST /api/auth/select-organization | 401 |

Test files: tests/RestaurantManagement.Modules.Identity.Tests/SessionServiceSelectOrganizationTests.cs (1-25), tests/RestaurantManagement.Modules.Organization.Tests/OrganizationLookupTests.cs (26-30), tests/RestaurantManagement.Web.Tests/SelectOrganizationEndpointTests.cs (31).
