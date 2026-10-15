# Scenarios: Tighten user roles and add permissions in code

**Plan:** docs/plans/identity/2026-10-15-tighten-user-roles-and-permissions/plan.md
**Coverage:** 45 scenarios — 13 happy · 5 edge · 9 failure · 3 security · 15 guard

Where a row lists several roles, one test checks each role separately (one test case per role).

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Failure | The retired "Staff" role gets no permissions | `GetPermissions_RetiredStaffRole_ReturnsNoPermissions` | `tests/RestaurantManagement.Shared.Tests/Auth/RolePermissionsTests.cs` |
| 2 | T1 | Failure | A role name the product does not know ("Sommelier") gets no permissions | `GetPermissions_UnknownRole_ReturnsNoPermissions` | `tests/RestaurantManagement.Shared.Tests/Auth/RolePermissionsTests.cs` |
| 3 | T1 | Failure | Asking "may a Manager manage members?" answers no | `HasPermission_ManagerAndMembersManage_ReturnsFalse` | `tests/RestaurantManagement.Shared.Tests/Auth/RolePermissionsTests.cs` |
| 4 | T1 | Edge | A Cashier has no permissions yet (they come in Phase 1) | `GetPermissions_Cashier_ReturnsNoPermissions` | `tests/RestaurantManagement.Shared.Tests/Auth/RolePermissionsTests.cs` |
| 5 | T1 | Edge | A Waiter has no permissions yet (Phase 1) | `GetPermissions_Waiter_ReturnsNoPermissions` | `tests/RestaurantManagement.Shared.Tests/Auth/RolePermissionsTests.cs` |
| 6 | T1 | Edge | A Kitchen user has no permissions yet (Phase 1) | `GetPermissions_Kitchen_ReturnsNoPermissions` | `tests/RestaurantManagement.Shared.Tests/Auth/RolePermissionsTests.cs` |
| 7 | T1 | Happy | An Owner has exactly the five permissions | `GetPermissions_Owner_ReturnsAllFivePermissions` | `tests/RestaurantManagement.Shared.Tests/Auth/RolePermissionsTests.cs` |
| 8 | T1 | Happy | An Admin has exactly the five permissions | `GetPermissions_Admin_ReturnsAllFivePermissions` | `tests/RestaurantManagement.Shared.Tests/Auth/RolePermissionsTests.cs` |
| 9 | T1 | Happy | A Manager has exactly "manage menus" and "view reports" | `GetPermissions_Manager_ReturnsMenuManageAndReportsViewOnly` | `tests/RestaurantManagement.Shared.Tests/Auth/RolePermissionsTests.cs` |
| 10 | T1 | Happy | Asking "may a Manager manage menus?" answers yes | `HasPermission_ManagerAndMenuManage_ReturnsTrue` | `tests/RestaurantManagement.Shared.Tests/Auth/RolePermissionsTests.cs` |
| 11 | T1 | Happy | The product's permission list is exactly: members.manage, locations.manage, settings.manage, menu.manage, reports.view | `All_ProductPermissionList_IsExactlyTheFiveSpecPermissions` | `tests/RestaurantManagement.Shared.Tests/Auth/RolePermissionsTests.cs` |
| 12 | T1 | Edge | Code that reads a role's permissions cannot add a permission to it while the app runs | `GetPermissions_CallerTriesToAddPermissionToResult_IsRefused` | `tests/RestaurantManagement.Shared.Tests/Auth/RolePermissionsTests.cs` |
| 13 | T2 | Failure | Saving a role assignment with no organization is rejected by the database | `InsertUserRole_NoOrganization_IsRejectedByNotNullRule` | `tests/RestaurantManagement.Web.Tests/UserRoleConstraintsTests.cs` |
| 14 | T2 | Failure | Giving a user the same role for "all locations" of the same organization twice is rejected | `InsertUserRole_SecondAllLocationsRowSameOrganizationAndRole_IsRejectedByUniqueIndex` | `tests/RestaurantManagement.Web.Tests/UserRoleConstraintsTests.cs` |
| 15 | T2 | Failure | Giving a user the same role for the same specific location twice is rejected | `InsertUserRole_SecondRowSameSpecificLocationAndRole_IsRejectedByUniqueIndex` | `tests/RestaurantManagement.Web.Tests/UserRoleConstraintsTests.cs` |
| 16 | T2 | Guard | A user may be Manager for "all locations" and also Manager for one specific location | `InsertUserRole_SameRoleAllLocationsAndOneSpecificLocation_BothRowsKept` | `tests/RestaurantManagement.Web.Tests/UserRoleConstraintsTests.cs` |
| 17 | T2 | Guard | A user may hold two different roles in the same organization and location | `InsertUserRole_SameOrganizationAndLocationDifferentRole_BothRowsKept` | `tests/RestaurantManagement.Web.Tests/UserRoleConstraintsTests.cs` |
| 18 | T2 | Guard | A user may hold the same "all locations" role in two different organizations | `InsertUserRole_SameAllLocationsRoleInAnotherOrganization_BothRowsKept` | `tests/RestaurantManagement.Web.Tests/UserRoleConstraintsTests.cs` |
| 19 | T3 | Failure | Calling an action that needs "manage members" without signing in is refused as not signed in (401) | `MembersManagePolicy_NoAccessToken_ReturnsUnauthorized` | `tests/RestaurantManagement.Web.Tests/PermissionPolicyTests.cs` |
| 20 | T3 | Security | A Manager, Cashier, Waiter or Kitchen user calling an action that needs "manage members" is refused (403) | `MembersManagePolicy_RoleWithoutMembersManage_ReturnsForbidden` | `tests/RestaurantManagement.Web.Tests/PermissionPolicyTests.cs` |
| 21 | T3 | Security | A user who belongs to several organizations and has not selected one is refused (403), even if one of their roles is Owner | `MembersManagePolicy_OwnerRolesButNoOrganizationSelected_ReturnsForbidden` | `tests/RestaurantManagement.Web.Tests/PermissionPolicyTests.cs` |
| 22 | T3 | Security | A user who selected an organization where they hold no role is refused (403) | `MembersManagePolicy_OrganizationSelectedButNoRoleInIt_ReturnsForbidden` | `tests/RestaurantManagement.Web.Tests/PermissionPolicyTests.cs` |
| 23 | T3 | Happy | An Owner or Admin of the selected organization may use an action that needs "manage members" | `MembersManagePolicy_OwnerOrAdminOfSelectedOrganization_ReturnsOk` | `tests/RestaurantManagement.Web.Tests/PermissionPolicyTests.cs` |
| 24 | T3 | Happy | A Manager may use an action that needs "manage menus" | `MenuManagePolicy_Manager_ReturnsOk` | `tests/RestaurantManagement.Web.Tests/PermissionPolicyTests.cs` |
| 25 | T3 | Happy | A Manager may use an action that needs "view reports" | `ReportsViewPolicy_Manager_ReturnsOk` | `tests/RestaurantManagement.Web.Tests/PermissionPolicyTests.cs` |
| 26 | T3 | Edge | A user who is both Waiter and Manager in the same organization may use an action that needs "manage menus" | `MenuManagePolicy_WaiterAndManagerInSameOrganization_ReturnsOk` | `tests/RestaurantManagement.Web.Tests/PermissionPolicyTests.cs` |
| 27 | T3 | Failure | Calling an action that declares no access rule without signing in is refused (401) | `UndeclaredEndpoint_NoAccessToken_ReturnsUnauthorized` | `tests/RestaurantManagement.Web.Tests/PrivateByDefaultTests.cs` |
| 28 | T3 | Failure | That refusal carries the usual "not signed in" message | `UndeclaredEndpoint_NoAccessToken_ReturnsStandardUnauthorizedMessage` | `tests/RestaurantManagement.Web.Tests/PrivateByDefaultTests.cs` |
| 29 | T3 | Guard | A signed-in user may still call an action that declares no access rule | `UndeclaredEndpoint_SignedInWaiter_ReturnsOk` | `tests/RestaurantManagement.Web.Tests/PrivateByDefaultTests.cs` |
| 30 | T3 | Guard | Register, login, refresh, logout, verify email and resend code all still work without signing in (not refused, no server error) | `PublicAuthEndpoint_NoAccessToken_IsNotRefusedAsUnauthorized` | `tests/RestaurantManagement.Web.Tests/PrivateByDefaultTests.cs` |
| 31 | T4 | Happy | The product's roles are exactly Owner, Admin, Manager, Cashier, Waiter and Kitchen ("Staff" is gone) | `Roles_DeclaredRoleNames_AreExactlyTheSixProductRoles` | `tests/RestaurantManagement.Shared.Tests/Auth/RolePermissionsTests.cs` |
| 32 | T4 | Happy | Every role of an organization (all six) can read that organization's menus | `GetMenusByOrganizationAsync_AnyRoleOfTheOrganization_ReturnsItsMenus` | `tests/RestaurantManagement.Modules.Menu.Tests/MenuServiceRoleAccessTests.cs` |
| 33 | T4 | Happy | Every role of an organization (all six) can read the menus of one of its locations | `GetMenusByLocationAsync_AnyRoleOfTheOrganization_ReturnsLocationMenus` | `tests/RestaurantManagement.Modules.Menu.Tests/MenuServiceRoleAccessTests.cs` |
| 34 | T4 | Guard | A Waiter of another organization cannot read this organization's menus (refused, 403 today; see backlog/bugs/2026-10-15-menu-other-organization-returns-403.md) | `GetMenusByOrganizationAsync_WaiterOfAnotherOrganization_ThrowsForbidden` | `tests/RestaurantManagement.Modules.Menu.Tests/MenuServiceRoleAccessTests.cs` |
| 35 | T4 | Guard | A Manager, Cashier, Waiter or Kitchen user still cannot create a menu | `CreateMenuAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbidden` | `tests/RestaurantManagement.Modules.Menu.Tests/MenuServiceRoleAccessTests.cs` |
| 36 | T4 | Guard | A Manager, Cashier, Waiter or Kitchen user still cannot delete a menu, and the menu stays | `DeleteMenuAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndKeepsMenu` | `tests/RestaurantManagement.Modules.Menu.Tests/MenuServiceRoleAccessTests.cs` |
| 37 | T4 | Happy | Every role (all six) gets past the role filter of "list an organization's menus" | `GetMenusByOrganization_AnyRole_ReturnsOk` | `tests/RestaurantManagement.Web.Tests/MenuRoleEndpointTests.cs` |
| 38 | T4 | Happy | Every role (all six) gets past the role filter of "list a location's menus" | `GetMenusByLocation_AnyRole_ReturnsOk` | `tests/RestaurantManagement.Web.Tests/MenuRoleEndpointTests.cs` |
| 39 | T4 | Guard | A Manager, Cashier, Waiter or Kitchen user is stopped by the role filter of "create a menu" | `CreateMenu_RoleOtherThanOwnerOrAdmin_ReturnsForbidden` | `tests/RestaurantManagement.Web.Tests/MenuRoleEndpointTests.cs` |
| 40 | T4 | Guard | A signed-in user with no role in the organization cannot read its menus (existing test) | `GetMenusByOrganizationAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden` | `tests/RestaurantManagement.Modules.Menu.Tests/MenuServiceTests.cs` |
| 41 | T4 | Guard | A Waiter (or a user with no role) still cannot create a location (existing test, Staff swapped for Waiter) | `CreateLocationAsync_WhenUserHasWaiterRoleOrNoRole_ThrowsForbidden` | `tests/RestaurantManagement.Modules.Organization.Tests/LocationServiceTests.cs` |
| 42 | T4 | Guard | A Waiter (or a user with no role) still cannot edit a location (existing test, Staff swapped for Waiter) | `EditLocationAsync_WhenUserHasWaiterRoleOrNoRole_ThrowsForbidden` | `tests/RestaurantManagement.Modules.Organization.Tests/LocationServiceTests.cs` |
| 43 | T4 | Guard | A Waiter (or a user with no role) still cannot delete a location (existing test, Staff swapped for Waiter) | `DeleteLocationAsync_WhenUserHasWaiterRoleOrNoRole_ThrowsForbidden` | `tests/RestaurantManagement.Modules.Organization.Tests/LocationServiceTests.cs` |
| 44 | T4 | Guard | A Waiter still cannot edit the organization (existing test, Staff swapped for Waiter) | `EditOrganizationAsync_WhenUserHasWaiterRoleForOrganization_ThrowsForbidden` | `tests/RestaurantManagement.Modules.Organization.Tests/OrganizationServiceEditTests.cs` |
| 45 | T4 | Guard | A Waiter of another organization cannot read the menus of this organization's location (refused, 403 today; same backlog bug as #34) | `GetMenusByLocationAsync_WaiterOfAnotherOrganization_ThrowsForbidden` | `tests/RestaurantManagement.Modules.Menu.Tests/MenuServiceRoleAccessTests.cs` |

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 Each role has exactly its permissions | 1–12, 31 |
| #2 Missing permission is refused (Manager on a "manage members" action gets 403) | 20 (plus 19, 21–26) |
| #3 Private by default (anonymous on an undeclared action gets 401) | 27, 28 (plus 29, 30) |
| #4 No duplicate "all locations" assignment | 14 (plus 15–18) |
| #5 No assignment without an organization | 13 |
| T4 architect decision: all six roles read their organization's menus; writes stay Owner and Admin | 32–45 |
