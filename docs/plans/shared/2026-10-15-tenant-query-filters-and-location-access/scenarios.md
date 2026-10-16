# Scenarios: Tenant Query Filters and Location Access Check

**Plan:** docs/plans/shared/2026-10-15-tenant-query-filters-and-location-access/plan.md
**Coverage:** 40 scenarios — 11 happy · 4 edge · 9 failure · 13 security · 3 guard

Names used below: **Cantina da Nonna** (Cantina) and **Trattoria do Sol** (Trattoria) are two organizations. Cantina has the locations **Batel** and **Mercado Municipal**; Trattoria has **Centro**. **Ana** is Owner of both Cantina and Trattoria and is signed in to Cantina, unless a scenario says otherwise. "Signed in to" means the organization written in the user's sign-in token (JWT, JSON Web Token).

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Failure | With no organization known, reading organization data returns nothing (never everything) | `Query_NoCurrentOrganization_ReturnsNoRows` | `tests/RestaurantManagement.Shared.Tests/Data/TenantQueryFilterTests.cs` |
| 2 | T1 | Happy | With dishes in two organizations, a Cantina user reading dishes gets only Cantina's | `Query_RowsInTwoOrganizations_ReturnsOnlyCurrentOrganizationRows` | `tests/RestaurantManagement.Shared.Tests/Data/TenantQueryFilterTests.cs` |
| 3 | T1 | Security | A Cantina user looking up a Trattoria dish by its ID finds nothing | `FindById_RowOfAnotherOrganization_ReturnsNull` | `tests/RestaurantManagement.Shared.Tests/Data/TenantQueryFilterTests.cs` |
| 4 | T1 | Edge | Two requests from different organizations, one after the other, each see only their own data (the filter is not frozen on the first organization) | `Query_TwoContextsOnTheSameModel_EachSeesOnlyItsOwnOrganization` | `tests/RestaurantManagement.Shared.Tests/Data/TenantQueryFilterTests.cs` |
| 5 | T1 | Happy | With Batel selected, location data (stock counts) shows only Batel's | `Query_LocationSelected_ReturnsOnlyThatLocationsRows` | `tests/RestaurantManagement.Shared.Tests/Data/TenantQueryFilterTests.cs` |
| 6 | T1 | Edge | With no location selected, location data shows every Cantina location and none of Trattoria's | `Query_NoLocationSelected_ReturnsEveryLocationOfTheOrganizationOnly` | `tests/RestaurantManagement.Shared.Tests/Data/TenantQueryFilterTests.cs` |
| 7 | T1 | Happy | While a new organization is being set up for the user, that organization wins over the one in the token | `OrganizationId_ScopeOpenedForAnotherOrganization_ReturnsTheScopedOrganization` | `tests/RestaurantManagement.Shared.Tests/Data/TenantContextTests.cs` |
| 8 | T1 | Happy | Otherwise the organization comes from the sign-in token | `OrganizationId_NoScopeOpen_ReturnsTheTokenOrganization` | `tests/RestaurantManagement.Shared.Tests/Data/TenantContextTests.cs` |
| 9 | T2 | Security | Ana, signed in to Cantina, editing, deleting, linking or unlinking a Trattoria menu gets "not found" and the menu is untouched (4 cases) | `MenuById_MenuOfAnotherOrganization_ReturnsNotFoundAndLeavesItUnchanged` | `tests/RestaurantManagement.Web.Tests/MenuTenantEndpointTests.cs` |
| 10 | T2 | Security | Ana, signed in to Cantina, reading, editing or deleting a Trattoria menu category gets "not found" and the category is untouched (3 cases) | `MenuCategoryById_CategoryOfAnotherOrganization_ReturnsNotFoundAndLeavesItUnchanged` | `tests/RestaurantManagement.Web.Tests/MenuTenantEndpointTests.cs` |
| 11 | T2 | Guard | Saving a menu with no signed-in organization still fails | `SaveChangesAsync_NewMenuWithoutCurrentOrganization_Throws403OrganizationRequired` (existing) | `tests/RestaurantManagement.Modules.Menu.Tests/MenuAuditTests.cs` |
| 12 | T3 | Failure | A user with no organization in the token is refused (403, "organization required") when creating or listing menus or categories (4 cases) | `MenuEndpoints_NoOrganizationInToken_ReturnsForbiddenOrganizationRequired` | `tests/RestaurantManagement.Web.Tests/MenuTenantEndpointTests.cs` |
| 13 | T3 | Security | Ana, signed in to Cantina, creates a menu whose request names Trattoria: it is stored in Cantina | `CreateMenu_BodyNamesAnotherOrganization_StoresMenuInTokenOrganization` | `tests/RestaurantManagement.Web.Tests/MenuTenantEndpointTests.cs` |
| 14 | T3 | Security | Ana, signed in to Cantina, creates a menu category whose request names Trattoria: it is stored in Cantina | `CreateMenuCategory_BodyNamesAnotherOrganization_StoresCategoryInTokenOrganization` | `tests/RestaurantManagement.Web.Tests/MenuTenantEndpointTests.cs` |
| 15 | T3 | Happy | With menus in Cantina and Trattoria, Ana listing menus sees only Cantina's | `GetMenus_MenusInTwoOrganizations_ReturnsOnlyTokenOrganizationsMenus` | `tests/RestaurantManagement.Web.Tests/MenuTenantEndpointTests.cs` |
| 16 | T3 | Happy | With categories in Cantina and Trattoria, Ana listing categories sees only Cantina's | `GetMenuCategories_CategoriesInTwoOrganizations_ReturnsOnlyTokenOrganizationsCategories` | `tests/RestaurantManagement.Web.Tests/MenuTenantEndpointTests.cs` |
| 17 | T3 | Failure | The old menu and category list addresses that carried the organization ID no longer exist (2 cases) | `OldOrganizationInUrlRoute_Called_ReturnsNotFound` | `tests/RestaurantManagement.Web.Tests/MenuTenantEndpointTests.cs` |
| 18 | T4 | Security | Ana, signed in to Cantina, editing or closing Trattoria's Centro location gets "not found" and Centro is untouched (2 cases) | `LocationById_LocationOfAnotherOrganization_ReturnsNotFoundAndLeavesItUnchanged` | `tests/RestaurantManagement.Web.Tests/OrganizationTenantEndpointTests.cs` |
| 19 | T4 | Security | The location lookup other modules use finds nothing for a Trattoria location when signed in to Cantina | `GetLocationAsync_LocationOfAnotherOrganization_ReturnsNull` | `tests/RestaurantManagement.Modules.Organization.Tests/OrganizationTenantFilterTests.cs` |
| 20 | T4 | Guard | A user who owns a trial organization, with no organization selected yet, still cannot create a second organization | `CreateOrganizationAsync_TrialOwnerWithNoOrganizationInToken_ThrowsBadRequest` | `tests/RestaurantManagement.Modules.Organization.Tests/OrganizationTenantFilterTests.cs` |
| 21 | T4 | Guard | Ana's organization list still shows the plan of both Cantina and Trattoria while she is signed in to Cantina | `GetOrganizationsAsync_SignedInToOneOfTwoOrganizations_ShowsPlanDetailsOfBoth` | `tests/RestaurantManagement.Modules.Organization.Tests/OrganizationTenantFilterTests.cs` |
| 22 | T5 | Happy | With locations in Cantina and Trattoria, Ana listing locations sees only Cantina's | `GetLocations_LocationsInTwoOrganizations_ReturnsOnlyTokenOrganizationsLocations` | `tests/RestaurantManagement.Web.Tests/OrganizationTenantEndpointTests.cs` |
| 23 | T5 | Security | Ana, signed in to Cantina, creates a location whose request names Trattoria: it is created in Cantina | `CreateLocation_BodyNamesAnotherOrganization_StoresLocationInTokenOrganization` | `tests/RestaurantManagement.Web.Tests/OrganizationTenantEndpointTests.cs` |
| 24 | T5 | Security | Ana, signed in to Cantina, edits "her organization": Cantina changes, Trattoria does not | `EditOrganization_SignedInToCantina_EditsCantinaAndLeavesTrattoriaUnchanged` | `tests/RestaurantManagement.Web.Tests/OrganizationTenantEndpointTests.cs` |
| 25 | T5 | Failure | The old location and organization-edit addresses that carried the organization ID no longer work (3 cases) | `OldOrganizationInUrlRoute_Called_IsNotRouted` | `tests/RestaurantManagement.Web.Tests/OrganizationTenantEndpointTests.cs` |
| 26 | T5 | Failure | A user with no organization in the token is refused (403, "organization required") when listing or creating locations or editing the organization (3 cases) | `OrganizationEndpoints_NoOrganizationInToken_ReturnsForbiddenOrganizationRequired` | `tests/RestaurantManagement.Web.Tests/OrganizationTenantEndpointTests.cs` |
| 27 | T5 | Security | A Manager, Cashier, Waiter or Kitchen user cannot create a location, and nothing is created (4 cases) | `CreateLocationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndCreatesNothing` | `tests/RestaurantManagement.Modules.Organization.Tests/LocationServiceTests.cs` |
| 28 | T5 | Security | A Manager, Cashier, Waiter or Kitchen user cannot edit the organization (4 cases) | `EditOrganizationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbidden` | `tests/RestaurantManagement.Modules.Organization.Tests/OrganizationServiceEditTests.cs` |
| 29 | T6 | Happy | A user with a role for all Cantina locations may work in any Cantina location | `HasLocationAccessAsync_RoleForAllLocations_ReturnsTrueForAnyLocation` | `tests/RestaurantManagement.Modules.Identity.Tests/UserRoleLookupLocationAccessTests.cs` |
| 30 | T6 | Security | Roles held only in Trattoria (for Batel's ID, or for all locations) never let the user work in a Cantina location | `HasLocationAccessAsync_RolesOnlyInAnotherOrganization_ReturnsFalse` | `tests/RestaurantManagement.Modules.Identity.Tests/UserRoleLookupLocationAccessTests.cs` |
| 31 | T6 | Edge | A user assigned to Batel and Centro may work in each of them but not in Mercado Municipal (3 cases) | `HasLocationAccessAsync_RolesForTwoLocations_AllowsOnlyThoseTwo` | `tests/RestaurantManagement.Modules.Identity.Tests/UserRoleLookupLocationAccessTests.cs` |
| 32 | T7 | Failure | A selected location that is not a valid ID is refused (400) and the request stops (2 cases) | `InvokeAsync_HeaderIsNotAValidId_ThrowsBadRequestAndStops` | `tests/RestaurantManagement.Shared.Tests/Middleware/LocationAccessMiddlewareTests.cs` |
| 33 | T7 | Failure | Selecting a location with no organization in the token is refused (403) | `InvokeAsync_NoOrganizationInToken_ThrowsForbiddenAndStops` | `tests/RestaurantManagement.Shared.Tests/Middleware/LocationAccessMiddlewareTests.cs` |
| 34 | T7 | Failure | Selecting a location the user may not work in is refused (403) and no location is applied: one that does not exist in the user's organization, or a Cantina location with no role assignment for it (2 cases) | `InvokeAsync_LocationUserMayNotWorkIn_ThrowsForbiddenAndStops` | `tests/RestaurantManagement.Shared.Tests/Middleware/LocationAccessMiddlewareTests.cs` |
| 35 | T7 | Happy | A request that selects no location goes through with no location limit | `InvokeAsync_NoHeader_ContinuesWithoutLocationLimit` | `tests/RestaurantManagement.Shared.Tests/Middleware/LocationAccessMiddlewareTests.cs` |
| 36 | T7 | Edge | A request without sign-in that sends a location is not checked and no location is applied | `InvokeAsync_AnonymousRequestWithHeader_IgnoresHeaderAndContinues` | `tests/RestaurantManagement.Shared.Tests/Middleware/LocationAccessMiddlewareTests.cs` |
| 37 | T7 | Happy | A user assigned to Batel who selects Batel goes through, and Batel becomes the request's location | `InvokeAsync_UserAssignedToTheLocation_StoresLocationAndContinues` | `tests/RestaurantManagement.Shared.Tests/Middleware/LocationAccessMiddlewareTests.cs` |
| 38 | T7 | Failure | Bia, a Waiter assigned only to Batel, selecting Mercado Municipal is refused (403) | `GetMenus_WaiterAssignedOnlyToBatelSelectsMercadoMunicipal_ReturnsForbidden` | `tests/RestaurantManagement.Web.Tests/LocationAccessEndpointTests.cs` |
| 39 | T7 | Happy | A Manager of all Cantina locations can select Batel or Mercado Municipal (2 cases) | `GetMenus_ManagerOfAllLocationsSelectsAnyCantinaLocation_ReturnsOk` | `tests/RestaurantManagement.Web.Tests/LocationAccessEndpointTests.cs` |
| 40 | T7 | Security | A Manager of all Cantina locations selecting Trattoria's Centro is refused (403) | `GetMenus_ManagerOfAllLocationsSelectsAnotherOrganizationsLocation_ReturnsForbidden` | `tests/RestaurantManagement.Web.Tests/LocationAccessEndpointTests.cs` |

T8 changes only `CLAUDE.md`, so it has no scenario (its DoD, the Definition of Done, says "no new tests"); it is checked at review with a search for `IgnoreQueryFilters` in `src/`.

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 Lists show only own organization | 2, 4, 15, 16, 22 (supported by Rule 3 scenarios 12, 13, 14, 23, 24, 26) |
| #2 Other organization's record is hidden (404) | 3, 9, 10, 18, 19 |
| #3 Unassigned location refused (403) | 31, 34, 38 |
| #4 "All locations" works | 29, 39 |
| #5 Foreign location refused (403) | 30, 34, 40 |
| #6 No anonymous saves | 11 |
| Rule 2 (selected location limits location data) | 5, 6, 37 |
| Rule 7 (filter turned off only in listed places) | Checked at review (T8): a search for `IgnoreQueryFilters` in `src/` must match the list in `CLAUDE.md`; the two listed places are protected by guards 20 and 21 |

## Existing tests updated
Forced by the plan's signature changes (new `ITenantContext` constructor parameter on `MenuDbContext`, `OrganizationDbContext`, `MenuService`, `MenuCategoryService`, `LocationService`, `OrganizationService`; organization removed from request bodies and URLs) and by the filters hiding rows from a context with no organization. No assertion was weakened.

| File | Change |
|---|---|
| `tests/RestaurantManagement.Modules.Menu.Tests/MenuTestDatabase.cs` | Contexts take a tenant; the no-argument version (no organization) is for seeding |
| `tests/RestaurantManagement.Modules.Menu.Tests/TestTenant.cs` (new helper) | Builds the tenant a context and a service see |
| `tests/RestaurantManagement.Modules.Menu.Tests/MenuAuditTests.cs` | Contexts get the matching tenant; the "Org B user updates Org A menu" test now sends the change as a detached entity, since the filter stops Org B from loading the menu |
| `tests/RestaurantManagement.Modules.Menu.Tests/MenuCategoryServiceTests.cs` | Tenant passed to context and service; request no longer sets `OrganizationId`; list test calls `GetAllAsync` |
| `tests/RestaurantManagement.Modules.Menu.Tests/MenuServiceTests.cs`, `MenuServiceRoleAccessTests.cs` | Tenant passed; list tests call `GetMenusAsync`; create request no longer sets `OrganizationId` |
| `tests/RestaurantManagement.Modules.Organization.Tests/OrganizationTestDatabase.cs`, `TestTenant.cs` (new helper) | Same as Menu |
| `tests/RestaurantManagement.Modules.Organization.Tests/LocationServiceTests.cs` | Tenant passed; create calls the new signature without organization |
| `tests/RestaurantManagement.Modules.Organization.Tests/OrganizationServiceEditTests.cs` | Tenant passed; edit calls the new signature without organization; the Waiter-only test became scenario 28 |
| `tests/RestaurantManagement.Modules.Organization.Tests/OrganizationServiceCreateTests.cs`, `OrganizationCreationScopeTests.cs` | Tenant passed (the scope tests use the real `TenantContext`); read-backs of settings use `IgnoreQueryFilters()` |
| `tests/RestaurantManagement.Web.Tests/AuditFieldsMigrationTests.cs`, `OrganizationDeleteRulesTests.cs`, `NoTenant.cs` (new helper) | Contexts get a no-organization tenant; database read-backs use `IgnoreQueryFilters()` |
| `tests/RestaurantManagement.Web.Tests/MenuRoleEndpointTests.cs` | Token carries the organization; list test uses `GET /api/menus`; create body no longer sets `OrganizationId` |

New test fixture (flagged): `tests/RestaurantManagement.Web.Tests/TenantApiFactory.cs` runs the real app on its own migrated PostgreSQL database from the existing `AuditFieldsPostgresFixture` container. No existing fixture runs real endpoints against PostgreSQL.
