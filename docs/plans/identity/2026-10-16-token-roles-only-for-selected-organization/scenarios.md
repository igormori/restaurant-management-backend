# Scenarios: Sign-in token carries roles only for the organization in use

**Plan:** docs/plans/identity/2026-10-16-token-roles-only-for-selected-organization/plan.md
**Coverage:** 8 scenarios — 0 happy · 0 edge · 2 failure · 0 security · 6 guard

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Failure | Owner of one restaurant and Waiter of another signs in: the token has no roles | `LoginAsync_UserWithRolesInTwoOrganizations_TokenHasNoRoleClaims` | `tests/RestaurantManagement.Modules.Identity.Tests/SessionServiceOrganizationClaimTests.cs` |
| 2 | T1 | Guard | The same user's token also has no organization (already true) | `LoginAsync_UserWithRolesInTwoOrganizations_TokenHasNoOrganizationClaim` | `tests/RestaurantManagement.Modules.Identity.Tests/SessionServiceOrganizationClaimTests.cs` |
| 3 | T1 | Guard | The same no-organization token is refused (403) on an action that needs Owner or Admin. Passes today because the service already refuses; it cannot fail from the bug, so Outcome 2 is really proven by #1 (no role in the token) | `CreateMenu_TokenOfOwnerInOneAndWaiterInAnotherOrganizationBeforeChoosing_Returns403` (passes today: the service already refuses; kept as a guard for role-only checks) | `tests/RestaurantManagement.Web.Tests/Identity/TokenRolesEndpointTests.cs` |
| 4 | T1 | Failure | Refreshing a token with no chosen organization for a two-organization user gives no roles and no organization | `RefreshTokenAsync_TwoOrganizationsNoneChosen_NewTokenHasNoRolesAndNoOrganization` | `tests/RestaurantManagement.Modules.Identity.Tests/SessionServiceOrganizationClaimTests.cs` |
| 5 | T1 | Guard | Choosing the second organization gives only that organization's role (Waiter) and its ID, never Owner from the first | `SelectOrganizationAsync_OwnerInCantinaStaffInTrattoria_TokenHasOnlyRolesOfSelectedOrganization`, `SelectOrganizationAsync_MemberOrganization_TokenCarriesThatOrganizationId` (existing) | `tests/RestaurantManagement.Modules.Identity.Tests/SessionServiceSelectOrganizationTests.cs` |
| 6 | T1 | Guard | Choosing the first organization gives only its roles, none from the second | `SelectOrganizationAsync_TwoRolesInSelectedOrganization_TokenHasBothRolesAndNoneFromOthers` (existing) | `tests/RestaurantManagement.Modules.Identity.Tests/SessionServiceSelectOrganizationTests.cs` |
| 7 | T1 | Guard | A user with two roles (one at a location) in one organization gets that organization and both roles | `LoginAsync_UserWithTwoRolesInOneOrganization_TokenCarriesBothRoles` | `tests/RestaurantManagement.Modules.Identity.Tests/SessionServiceOrganizationClaimTests.cs` |
| 8 | T1 | Guard | Sign-in still lists the active organizations to choose from; a user with no roles gets no organization | `LoginAsync_UserWithTwoOrganizations_ListsBothChoicesWithNamesAndNoOrganizationClaim`, `LoginAsync_UserWithNoRoles_TokenHasNoOrganizationClaim` (existing) | `tests/RestaurantManagement.Modules.Identity.Tests/SessionServiceSelectOrganizationTests.cs`, `SessionServiceOrganizationClaimTests.cs` |

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 No cross-organization roles in the token | 1, 2 |
| #2 Role-only checks refuse that token | 3 |
| #3 Choosing an organization restores its roles | 5, 6 |
| #4 Single-organization users unaffected | 7, 8 |
| #5 Token refresh follows the same rules | 4 |

## Existing tests updated
None.
