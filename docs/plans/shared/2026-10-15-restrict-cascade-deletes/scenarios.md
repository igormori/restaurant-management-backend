# Scenarios: Restrict Cascade Deletes

**Plan:** docs/plans/shared/2026-10-15-restrict-cascade-deletes/plan.md
**Coverage:** 12 scenarios — 4 happy · 5 edge · 3 failure · 0 security

Security: no endpoint, role, authorization or tenant query changes (plan Flags), so no cross-tenant or role rejection tests apply. All tests run on real PostgreSQL (Testcontainers) and delete with raw SQL, to prove the database itself enforces the rule.

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Failure | Deleting an organization that still has an open location is refused, and the location is still there | `DeleteOrganization_HasActiveLocation_IsRefusedAndLocationKept` | `tests/RestaurantManagement.Web.Tests/OrganizationDeleteRulesTests.cs` |
| 2 | T1 | Edge | Deleting an organization whose only location is closed is still refused; the closed location keeps its history | `DeleteOrganization_OnlyLocationIsClosed_IsRefusedAndLocationKept` | `tests/RestaurantManagement.Web.Tests/OrganizationDeleteRulesTests.cs` |
| 3 | T1 | Failure | Deleting an organization that has settings (and no locations) is refused, and the settings are still there | `DeleteOrganization_HasSettingsAndNoLocations_IsRefusedAndSettingsKept` | `tests/RestaurantManagement.Web.Tests/OrganizationDeleteRulesTests.cs` |
| 4 | T1 | Happy | An organization with no locations and no settings can still be deleted | `DeleteOrganization_NoLocationsAndNoSettings_IsDeleted` | `tests/RestaurantManagement.Web.Tests/OrganizationDeleteRulesTests.cs` |
| 5 | T1 | Happy | Every link in the Organization module (locations, settings) is set to refuse deletes, and there are no other links | `Migrate_OrganizationForeignKeys_AllRestrictDeletes` | `tests/RestaurantManagement.Web.Tests/OrganizationDeleteRulesTests.cs` |
| 6 | T1 | Edge | Applying the change to a database that already holds organizations, settings and locations adds, changes or removes no rows | `Migrate_OrganizationTablesWithRows_NoRowAddedChangedOrRemoved` | `tests/RestaurantManagement.Web.Tests/OrganizationDeleteRulesTests.cs` |
| 7 | T2 | Failure | Deleting a user who still has a role is refused, and the role is still there | `DeleteUser_HasRole_IsRefusedAndRoleKept` | `tests/RestaurantManagement.Web.Tests/IdentityDeleteRulesTests.cs` |
| 8 | T2 | Edge | Deleting a deactivated user who still has a role is also refused; deactivation keeps their history | `DeleteUser_DeactivatedUserHasRole_IsRefusedAndRoleKept` | `tests/RestaurantManagement.Web.Tests/IdentityDeleteRulesTests.cs` |
| 9 | T2 | Happy | Deleting a user with verification codes and no roles also deletes all their codes, used and unused | `DeleteUser_HasVerificationCodesAndNoRoles_UserAndCodesDeleted` | `tests/RestaurantManagement.Web.Tests/IdentityDeleteRulesTests.cs` |
| 10 | T2 | Edge | Deleting one user leaves another user's verification codes untouched | `DeleteUser_AnotherUserHasVerificationCode_OtherUsersCodeKept` | `tests/RestaurantManagement.Web.Tests/IdentityDeleteRulesTests.cs` |
| 11 | T2 | Happy | Every link in the Identity module is reviewed: roles refuse deletes, verification codes are deleted with their user, and there are no other links | `Migrate_IdentityForeignKeys_RolesRestrictAndCodesCascade` | `tests/RestaurantManagement.Web.Tests/IdentityDeleteRulesTests.cs` |
| 12 | T2 | Edge | Applying the change to a database that already holds users, roles and codes adds, changes or removes no rows | `Migrate_IdentityTablesWithRows_NoRowAddedChangedOrRemoved` | `tests/RestaurantManagement.Web.Tests/IdentityDeleteRulesTests.cs` |

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 Organization with locations cannot be deleted | 1, 2 |
| #2 Organization with settings cannot be deleted | 3 |
| #3 Verification codes follow their user | 9, 10 |
| #4 Every other link reviewed | 5, 11 (database delete rules); the change description itself is checked at review (plan DoD) |
| #5 Existing data untouched | 6, 12 |
