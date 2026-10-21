# Scenarios: Custom roles per organization

**Plan:** docs/plans/identity/2026-10-20-custom-roles/plan.md
**Coverage:** 44 scenarios — 16 happy · 7 edge · 9 failure · 10 security · 2 guard

Terms: DoD (Definition of Done: the checks a task must pass to count as finished) · cap (the caller's level in a module at every non-closed location; an Admin may not set a role's default above it).

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Happy | The three role audit actions (created, levels changed, renamed) are in the fixed list the audit log filters by | `All_FixedListOfAuditedActions_ContainsTheRoleActions` | `tests/RestaurantManagement.Shared.Tests/Auth/RoleAuditActionsTests.cs` |
| 2 | T1 | Happy | The two role-name refusal messages (name taken, name not allowed) read as real text in English and Portuguese | `Localizer_RoleNameMessageKey_ReturnsRealTextInEachLanguage` | `tests/RestaurantManagement.Shared.Tests/Localization/RoleMessageKeysTests.cs` |
| 3 | T2 | Failure | The database refuses "waiter" when "Waiter" exists in the same organization, through the known index name | `SaveRole_SameNameInAnotherLetterCaseInOneOrganization_IsRejectedByTheCaseInsensitiveUniqueIndex` | `tests/RestaurantManagement.Web.Tests/Identity/AccessGridConstraintsTests.cs` |
| 4 | T2 | Guard | Another organization may still use the same role name | `SaveRole_SameNameInAnotherOrganization_IsAllowed` | `tests/RestaurantManagement.Web.Tests/Identity/AccessGridConstraintsTests.cs` |
| 5 | T3 | Happy | The Owner may set any levels for a role | `RequireAsync_OwnerProposesManageInEveryModule_Passes` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleManagementRulesTests.cs` |
| 6 | T3 | Happy | An Admin with Members = Manage at every non-closed location may set levels at or below her own | `RequireAsync_AdminWithMembersManageAtBothLocationsProposesLevelsAtOrBelowHers_Passes` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleManagementRulesTests.cs` |
| 7 | T3 | Failure | An Admin is refused a role level above her own (Cashier Manage without Cashier) | `RequireAsync_AdminWithoutCashierProposesCashierManage_Refuses403AboveOwnLevel` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleManagementRulesTests.cs` |
| 8 | T3 | Failure | An Admin is refused when renaming a role whose current level is above her own | `RequireAsync_AdminWithoutCashierRenamesARoleHoldingCashierManage_Refuses403AboveOwnLevel` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleManagementRulesTests.cs` |
| 9 | T3 | Security | Members = Manage at only one of two open locations is refused | `RequireAsync_MembersManageAtBatelOnlyWhileMercadoIsOpen_Refuses403AccessDenied` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleManagementRulesTests.cs` |
| 10 | T3 | Edge | A closed location where the Admin holds nothing does not lower her cap | `RequireAsync_MercadoIsClosedAndAdminHoldsNothingThere_TheMissingCellDoesNotLowerHerCap` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleManagementRulesTests.cs` |
| 11 | T3 | Security | A Manager, Cashier, Waiter or Kitchen member is refused any role change | `RequireAsync_CallerWithADefaultRoleThatLacksMembersManage_Refuses403AccessDenied` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleManagementRulesTests.cs` |
| 12 | T3 | Edge | With no open location only the Owner may manage roles | `RequireAsync_NoNonClosedLocationInTheOrganization_RefusesANonOwner` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleManagementRulesTests.cs` |
| 13 | T3 | Edge | Cashier Manage at one location but only Read at another: the lowest level is her cap | `RequireAsync_CashierManageAtBatelButReadAtMercado_RefusesCashierManage` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleManagementRulesTests.cs` |
| 14 | T4 | Happy | Creating "Marketing" stores the trimmed name and only the levels above None | `CreateAsync_MarketingWithLevels_StoresTheTrimmedNameAndOnlyTheLevelsAboveNone` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleServiceCreateTests.cs` |
| 15 | T4 | Failure | An empty, blank, "Owner" (any case) or over-100-character name is refused with 400 and nothing is saved | `CreateAsync_EmptyBlankOwnerOrTooLongName_Refuses400AndStoresNothing` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleServiceCreateTests.cs` |
| 16 | T4 | Failure | A name already used in another letter case is refused with 400 and nothing is saved | `CreateAsync_NameUsedInAnotherLetterCase_Refuses400AndStoresNothing` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleServiceCreateTests.cs` |
| 17 | T4 | Failure | An unknown module or level name is refused with 400 and nothing is saved | `CreateAsync_UnknownModuleOrLevelName_Refuses400AndStoresNothing` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleServiceCreateTests.cs` |
| 18 | T4 | Happy | The Owner creates "Marketing" through the endpoint and it appears in the role list with its levels | `CreateRole_OwnerCreatesMarketing_ReturnsItAndListsItWithItsLevels` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEndpointTests.cs` |
| 19 | T4 | Happy | An invitation can use the newly created role | `CreateInvitation_WithTheNewMarketingRole_Succeeds` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEndpointTests.cs` |
| 20 | T4 | Edge | Two creates of one name at the same moment: one succeeds, the other gets 400, never a server error | `CreateRole_TwoCreatesOfOneNameAtTheSameMoment_OneSucceedsAndTheOtherIsRefusedNeverAServerError` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEndpointTests.cs` |
| 21 | T4 | Security | An Admin creating a role stronger than her own access gets 403 and nothing is saved | `CreateRole_AdminWithoutCashierGivesTheRoleCashierManage_ReturnsForbiddenAndStoresNothing` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEndpointTests.cs` |
| 22 | T4 | Security | A Manager, Cashier, Waiter or Kitchen member creating a role gets 403 and nothing is saved | `CreateRole_CallerWithADefaultRoleThatLacksMembersManage_ReturnsForbiddenAndStoresNothing` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEndpointTests.cs` |
| 23 | T4 | Security | Reading another organization's role returns not found | `GetRole_RoleOfAnotherOrganization_ReturnsNotFound` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEndpointTests.cs` |
| 24 | T4 | Security | The role list shows only the signed-in organization's roles | `ListRoles_TwoOrganizationsWithRoles_ShowsOnlyTheTokenOrganizationsRoles` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEndpointTests.cs` |
| 25 | T4 | Happy | Creating a role writes one audit entry with who, when and the name and levels after | `CreateRole_OwnerCreatesMarketing_WritesOneAuditEntryWithTheCallerTheTimeAndTheLevelsAfter` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEndpointTests.cs` |
| 26 | T4 | Guard | Every role endpoint answers 401 without a token (routes are private by default, so this passes with the stub controller too) | `RoleEndpoints_NoAccessToken_ReturnUnauthorized` | `tests/RestaurantManagement.Web.Tests/Identity/RoleAuthEndpointTests.cs` |
| 27 | T5 | Happy | Editing Waiter's levels replaces its defaults; a level left out or None is not stored | `UpdateLevelsAsync_WaiterLevelsReplaced_StoresTheNewDefaultsOnly` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleServiceEditTests.cs` |
| 28 | T5 | Happy | Renaming Waiter to Server shows Server on every holder's label and changes no member's levels | `RenameAsync_WaiterToServer_HoldersShowServerAtTheirLocationAndKeepTheirLevels` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleServiceEditTests.cs` |
| 29 | T5 | Edge | A role may be renamed to its own name in another letter case | `RenameAsync_OwnNameInAnotherLetterCase_Renames` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleServiceEditTests.cs` |
| 30 | T5 | Failure | Renaming to another role's name, an empty or blank name or "OWNER" is refused with 400 and the name is kept | `RenameAsync_ExistingEmptyBlankOrOwnerName_Refuses400AndKeepsTheName` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleServiceEditTests.cs` |
| 31 | T5 | Happy | A level edit writes one audit entry with the levels before and after | `UpdateLevelsAsync_LevelsChanged_RecordsOneEntryWithTheLevelsBeforeAndAfter` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleServiceEditTests.cs` |
| 32 | T5 | Edge | A level edit that changes nothing writes no audit entry | `UpdateLevelsAsync_SameLevelsAsStored_RecordsNoEntry` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleServiceEditTests.cs` |
| 33 | T5 | Happy | A rename writes one audit entry with the names before and after | `RenameAsync_WaiterToServer_RecordsOneEntryWithTheNamesBeforeAndAfter` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleServiceEditTests.cs` |
| 34 | T5 | Edge | A rename to the same name writes no audit entry | `RenameAsync_SameNameAsStored_RecordsNoEntry` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleServiceEditTests.cs` |
| 35 | T5 | Happy | After the Owner changes Waiter's levels, every existing member's grid is identical | `UpdateLevels_OwnerChangesWaitersLevels_EveryMembersStoredGridStaysIdentical` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEditEndpointTests.cs` |
| 36 | T5 | Happy | A new invitation using Waiter after its levels were edited gets the new levels | `CreateInvitation_WaiterAfterItsLevelsWereEdited_GetsTheNewLevels` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEditEndpointTests.cs` |
| 37 | T5 | Happy | The Owner renames Waiter to Server through the endpoint and the role list shows Server | `RenameRole_OwnerRenamesWaiterToServer_TheRoleListShowsServerAndNoWaiter` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEditEndpointTests.cs` |
| 38 | T5 | Security | An Admin without Cashier editing or renaming the Cashier role gets 403 and the role is unchanged | `EditRole_AdminWithoutCashierEditsOrRenamesTheCashierRole_ReturnsForbiddenAndLeavesItUnchanged` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEditEndpointTests.cs` |
| 39 | T5 | Security | A Manager, Cashier, Waiter or Kitchen member editing or renaming a role gets 403 on both endpoints | `EditRole_CallerWithADefaultRoleThatLacksMembersManage_ReturnsForbiddenAndLeavesItUnchanged` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEditEndpointTests.cs` |
| 40 | T5 | Security | Editing or renaming another organization's role returns not found and that role is unchanged | `EditRole_RoleOfAnotherOrganization_ReturnsNotFoundAndLeavesItUnchanged` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEditEndpointTests.cs` |
| 41 | T4 | Happy | An Admin with Members = Manage everywhere creates a role within her levels and gets 200 (the real rule reads her cells from the database) | `CreateRole_AdminWithMembersManageEverywhereCreatesARoleWithinHerLevels_ReturnsOkAndStoresIt` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEndpointTests.cs` |
| 42 | T5 | Security | An Admin raising a role's default above her own level through the edit endpoint gets 403 and the levels are unchanged | `UpdateLevels_AdminWithoutCashierRaisesWaiterToCashierManage_ReturnsForbiddenAndLeavesItUnchanged` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEditEndpointTests.cs` |
| 43 | T5 | Failure | Editing the levels of, or renaming, a role that does not exist returns not found | `EditRole_UnknownRoleInTheOwnOrganization_ReturnsNotFound` | `tests/RestaurantManagement.Web.Tests/Identity/RoleEditEndpointTests.cs` |
| 44 | T5 | Failure | An unknown module or level name when editing a role's levels is refused with 400 and the levels are kept | `UpdateLevelsAsync_UnknownModuleOrLevelName_Refuses400AndKeepsTheLevels` | `tests/RestaurantManagement.Modules.Identity.Tests/RoleServiceEditTests.cs` |

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 Owner creates a role | 14, 18, 19 |
| #2 Names are unique | 2, 3, 15, 16, 17, 20, 29, 30, 44 (400 for bad levels) |
| #3 Editing levels leaves grids alone | 27, 35, 36 |
| #4 Rename updates labels | 28, 37 |
| #5 Admin works within the cap | 6, 7, 21, 41 |
| #6 Capped Admin cannot touch stronger roles | 8, 38, 42 |
| #7 Members level required | 9, 11, 22, 39 |
| #8 Owner is not editable as a role | 15, 30 (the name "Owner" in any letter case is refused on create and rename, so no Owner role can exist) |
| #9 Tenant isolation holds | 4, 23, 24, 40, 43 (unknown role, same 404) |
| #10 Changes are audited | 1, 25, 31, 32, 33, 34 |

## Existing tests updated
None rewritten. The existing `SaveRole_SameNameTwiceInOneOrganization_IsRejectedByUniqueIndex` still passes and now shares matrix row T30 with scenario 3. Matrix row T30 was reworded to "ignoring letter case" and is ❌ until scenario 3 passes.

## Notes
- Scenarios 4 and 26 pass before the feature is built: 4 because the current index already allows another organization to reuse a name, 26 because every route is private by default and the stub controller carries `[Authorize]`.
- A refused edit or create is proven "nothing saved" at the level that owns the check: names and levels in the service tests (unit), the cap and Members level in the rules tests, and the HTTP statuses, tenant isolation and database race in Web.Tests.
- Scenarios 41 to 44 were added after the test review for T4 and T5 DoD items that had no test (Admin within cap gets 200, Admin raising a level through edit, unknown role id, bad level names on edit). They take the plan over the 40-scenario budget for size L; 30 now also covers a name over 100 characters.
- Scenario 20 may be settled by the name pre-check alone and never reach the database index; either way a lost race must answer 400, never 500.
- `RolesController.cs` is a complete stub: it only passes each call to the role service, so it holds no logic. Its routes and `[Authorize]` are final.
- Decided (user answer plus spec): an Admin cannot give a role a level above her own, and, as the spec says, cannot edit or rename a role whose current level is already above her cap, even if her new levels are within it. Scenarios 7, 8, 38 and 42 prove this.
- Matrix rows added: identity T91–T131 (T30 reworded), shared SH45–SH46.
