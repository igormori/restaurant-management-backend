# Menu security matrix

The record of every rule the Menu module must keep, and the test that proves each one: who may
create, change and read menus and menu categories, and that an organization only ever sees and
changes its own. The pipeline (`.github/workflows/dotnet.yml`) runs every test named here, and
`.claude/scripts/check-security-matrices.py` fails the build when this file and the tests drift
apart.

## Rules for this file
The rules in the root `CLAUDE.md`, "Security matrices", apply: any change to Menu updates this
file in the same commit; new behaviour adds ❌ rows before it is built; no row is ✅ without a
named test; every test in `tests/RestaurantManagement.Modules.Menu.Tests/` and
`tests/RestaurantManagement.Web.Tests/Menu/` is named here and carries `[Trait("Rule", "<ID>")]`;
row IDs start with M (rules) or MD (wiring) and are never reused.

**Status:** ✅ covered by the named test · ⚠️ partly covered (the Expected column says what is
missing) · ❌ no test yet · 🐞 known bug, see the backlog item

**Where the tests live:** unit tests in `tests/RestaurantManagement.Modules.Menu.Tests/`,
endpoint and database tests in `tests/RestaurantManagement.Web.Tests/Menu/`.

## 1. Who may do what
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| M1 | Manager, Cashier, Waiter or Kitchen creates or deletes a menu | 403; nothing stored or removed | ✅ | `MenuServiceRoleAccessTests.CreateMenuAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbidden`, `MenuServiceRoleAccessTests.DeleteMenuAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndKeepsMenu`, `MenuRoleEndpointTests.CreateMenu_RoleOtherThanOwnerOrAdmin_ReturnsForbidden` |
| M2 | Manager, Cashier, Waiter or Kitchen edits a menu, or links or unlinks a location | 403; nothing changes | ✅ | `MenuServiceRoleAccessTests.EditMenuAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndKeepsMenu`, `MenuServiceRoleAccessTests.AttachMenuToLocationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndStoresNoLink`, `MenuServiceRoleAccessTests.DetachMenuFromLocationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndKeepsLink` |
| M3 | Any of the six roles held for all locations lists the menus; any of the six roles held for a location, or for all locations, lists that location's menus | Allowed | ✅ | `MenuServiceRoleAccessTests.GetMenusAsync_AnyRoleOfTheOrganization_ReturnsItsMenus`, `MenuServiceRoleAccessTests.GetMenusByLocationAsync_AnyRoleOfTheOrganization_ReturnsLocationMenus`, `MenuRoleEndpointTests.GetMenus_AnyRole_ReturnsOk`, `MenuRoleEndpointTests.GetMenusByLocation_AnyRole_ReturnsOk`, `MenuMultiRoleAccessTests.GetMenusAsync_WaiterForAllLocations_ReturnsMenus`, `MenuMultiRoleAccessTests.GetMenusByLocationAsync_WaiterAtBatel_ReturnsBatelMenus`, `MenuMultiRoleAccessTests.GetMenusByLocationAsync_WaiterForAllLocations_ReturnsMenusOfAnyLocation`, `MenuMultiRoleEndpointTests.GetMenus_WaiterAssignedOnlyToBatel_ReadsBatelMenusButNotTheFullList` |
| M31 | A user whose roles are all for one location lists all the menus, or lists another location's menus | 403 | ✅ | `MenuMultiRoleAccessTests.GetMenusAsync_WaiterAssignedOnlyToOneLocation_ThrowsForbidden`, `MenuMultiRoleAccessTests.GetMenusByLocationAsync_WaiterAtBatelAsksForCentro_ThrowsForbidden`, `MenuMultiRoleEndpointTests.GetMenus_WaiterAssignedOnlyToBatel_ReadsBatelMenusButNotTheFullList` |
| M4 | User with no role in the menu's organization | 403, even with a role in another organization | ✅ | `MenuServiceTests.GetMenusAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden`, `MenuServiceRoleAccessTests.GetMenusAsync_WaiterOfAnotherOrganization_ThrowsForbidden`, `MenuServiceRoleAccessTests.GetMenusByLocationAsync_WaiterOfAnotherOrganization_ThrowsForbidden` |
| M5 | Menu categories: create, read, change, delete | Owner or Admin only; a user with no role gets 403 | ✅ | `MenuCategoryServiceTests.CreateAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden`, `MenuCategoryServiceTests.GetByIdAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden`, `MenuCategoryServiceTests.GetAllAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden`, `MenuCategoryServiceTests.UpdateAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden`, `MenuCategoryServiceTests.DeleteAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden` |
| M6 | No organization chosen in the token | 403 "organization required" | ✅ | `MenuTenantEndpointTests.MenuEndpoints_NoOrganizationInToken_ReturnsForbiddenOrganizationRequired` |
| M25 | Admin or Owner for all locations who also holds another role (Waiter at one location), writes menus or menu categories | Allowed, whichever order the roles were stored in | ✅ | `MenuMultiRoleAccessTests.CreateMenuAsync_AdminOrOwnerForAllLocationsPlusWaiterAtOneLocation_CreatesMenuInEveryOrder`, `MenuMultiRoleAccessTests.CreateAsync_AdminForAllLocationsPlusWaiterAtOneLocation_CreatesCategoryInEveryOrder`, `MenuMultiRoleEndpointTests.CreateMenu_AdminForAllLocationsPlusWaiterAtBatel_ReturnsOkInEveryStorageOrder` |
| M26 | User holding two roles and neither is Owner or Admin (Waiter with Cashier, Kitchen or Manager) writes menus or menu categories | 403, whichever order; a Manager never manages menus, alone or combined | ✅ | `MenuMultiRoleAccessTests.CreateMenuAsync_TwoRolesNeitherOwnerNorAdmin_ThrowsForbiddenInEveryOrder`, `MenuMultiRoleAccessTests.CreateAsync_TwoRolesNeitherOwnerNorAdmin_ThrowsForbiddenInEveryOrder` |
| M27 | Admin for one location only, or at three locations as separate assignments, writes menus or menu categories | 403: menus belong to the whole organization, so only a role for all locations counts; nothing changes | ✅ | `MenuMultiRoleAccessTests.CreateMenuAsync_AdminAssignedOnlyToOneLocation_ThrowsForbidden`, `MenuMultiRoleAccessTests.DeleteMenuAsync_AdminAssignedToThreeLocationsSeparately_ThrowsForbiddenAndKeepsMenu`, `MenuMultiRoleAccessTests.CreateAsync_AdminAssignedOnlyToOneLocation_ThrowsForbidden`, `MenuMultiRoleEndpointTests.CreateMenu_AdminAssignedOnlyToBatel_ReturnsForbidden` |
| M28 | Admin role held only in another organization, plus Waiter here, writes a menu | 403: a role in another organization never counts | ✅ | `MenuMultiRoleEndpointTests.CreateMenu_AdminOfAnotherOrganizationPlusWaiterHere_ReturnsForbidden` |
| M29 | Admin assigned to one location (alone or with another role, either order) reads a menu category of that location, or lists that location's categories | Allowed | ✅ | `MenuMultiRoleAccessTests.GetByIdAsync_AdminOfBatelPlusWaiterAtCentro_ReadsBatelCategoryInEveryOrder`, `MenuMultiRoleAccessTests.GetByLocationIdAsync_AdminOfBatelForBatel_ReturnsBatelCategories` |
| M30 | Admin assigned to one location reads a category of another location or without a location, lists another location's categories, or lists all categories; Waiter reads a category | 403: a role for one location counts only there; categories without a location and the full lists need Owner or Admin for all locations; only Owner or Admin read categories | ✅ | `MenuMultiRoleAccessTests.GetByIdAsync_AdminOfBatelPlusWaiterAtCentro_ThrowsForbiddenForCentroCategoryInEveryOrder`, `MenuMultiRoleAccessTests.GetByIdAsync_AdminOfBatelOnCategoryWithoutLocation_ThrowsForbidden`, `MenuMultiRoleAccessTests.GetByLocationIdAsync_AdminOfBatelForCentro_ThrowsForbidden`, `MenuMultiRoleAccessTests.GetAllAsync_AdminOfBatelOnly_ThrowsForbidden`, `MenuMultiRoleAccessTests.GetByIdAsync_WaiterAtBatelOnBatelCategory_ThrowsForbidden` |

## 2. Other organizations' data
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| M7 | Another organization's menu, by its ID | 404 with the translated message, as if it did not exist; nothing changes | ✅ | `MenuTenantEndpointTests.MenuById_MenuOfAnotherOrganization_ReturnsNotFoundAndLeavesItUnchanged`, `MenuMessagesEndpointTests.EditMenu_MenuDoesNotExist_ReturnsNotFoundWithLocalizedMessage` |
| M8 | Another organization's category, by its ID | 404 with the translated message; nothing changes | ✅ | `MenuTenantEndpointTests.MenuCategoryById_CategoryOfAnotherOrganization_ReturnsNotFoundAndLeavesItUnchanged`, `MenuMessagesEndpointTests.MenuCategoryById_CategoryOfAnotherOrganization_ReturnsNotFoundWithLocalizedMessage` |
| M9 | Lists of menus and categories | Only the token organization's | ✅ | `MenuTenantEndpointTests.GetMenus_MenusInTwoOrganizations_ReturnsOnlyTokenOrganizationsMenus`, `MenuTenantEndpointTests.GetMenuCategories_CategoriesInTwoOrganizations_ReturnsOnlyTokenOrganizationsCategories` |
| M10 | Request body names another organization | Ignored: stored in the token organization | ✅ | `MenuTenantEndpointTests.CreateMenu_BodyNamesAnotherOrganization_StoresMenuInTokenOrganization`, `MenuTenantEndpointTests.CreateMenuCategory_BodyNamesAnotherOrganization_StoresCategoryInTokenOrganization` |
| M11 | Category on another organization's menu or location, or on one that does not exist | 404; nothing stored | ✅ | `MenuCategoryServiceTests.CreateAsync_WhenMenuBelongsToAnotherOrganization_ThrowsNotFoundAndStoresNothing`, `MenuCategoryServiceTests.CreateAsync_WhenMenuDoesNotExist_ThrowsNotFound`, `MenuCategoryServiceTests.CreateAsync_WhenLocationBelongsToAnotherOrganization_ThrowsNotFoundAndStoresNothing`, `MenuCategoryServiceTests.CreateAsync_WhenLocationDoesNotExist_ThrowsNotFoundAndStoresNothing` |
| M12 | Menus of another organization's location | 404, as if the location did not exist | ✅ | `MenuLocationEndpointTests.GetMenusByLocation_LocationOfAnotherOrganization_ReturnsNotFoundWithoutTheMenu` |
| M13 | Linking a menu to another organization's location | 404; no link stored | ✅ | `MenuLocationEndpointTests.AttachMenuToLocation_LocationOfAnotherOrganization_ReturnsNotFoundAndStoresNoLink` |
| M14 | Old routes with the organization in the address | 404 | ✅ | `MenuTenantEndpointTests.OldOrganizationInUrlRoute_Called_ReturnsNotFound` |
| M15 | Every write | Stamped with the token organization, the user and the time; a write that carries or changes to another organization, or has no organization, is refused with 403 and nothing is written | ✅ | `MenuAuditTests.SaveChangesAsync_NewMenuByUserInOrgA_StampsOrganizationAndCreationAudit`, `MenuAuditTests.SaveChangesAsync_UpdatedMenu_SetsUpdateAuditAndKeepsCreatedFields`, `MenuAuditTests.SaveChangesAsync_NewMenuCarriesAnotherOrganizationId_Throws403AndWritesNothing`, `MenuAuditTests.SaveChangesAsync_UserInOrgBUpdatesOrgAMenu_Throws403AndMenuUnchanged`, `MenuAuditTests.SaveChangesAsync_OrganizationIdChangedOnStoredMenu_Throws403AndMenuStaysInOrgA`, `MenuAuditTests.SaveChangesAsync_NewMenuWithoutCurrentOrganization_Throws403OrganizationRequired`, `MenuAuditTests.SaveChangesAsync_NewMenuCategory_StampsOrganizationAndUpdatedAt`, `MenuAuditTests.SaveChangesAsync_NewMenuCategoryWithoutLocation_KeepsLocationNull`, `MenuAuditTests.SaveChangesAsync_NewLocationMenu_GetsOrganizationFromUserAndUpdatedAt`, `MenuAuditTests.SaveChangesAsync_UpdatedMenuCategory_SetsUpdatedAtAndKeepsCreatedAt` |

## 3. Menus and categories
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| M16 | Owner or Admin creates a menu with location IDs | Menu stored active; the token organization's locations are linked; IDs of other organizations' locations are ignored | ✅ | `MenuLocationEndpointTests.CreateMenu_LocationIdsOfTwoOrganizations_LinksOnlyTheTokenOrganizationsLocation` |
| M17 | Linking a menu to a location twice; unlinking one that is not linked | One link only; unlinking still answers OK and changes nothing | ✅ | `MenuLocationLinkTests.AttachMenuToLocationAsync_LinkedTwice_KeepsOneLink`, `MenuLocationLinkTests.DetachMenuFromLocationAsync_LocationNotLinked_AnswersOkAndChangesNothing` |
| M18 | Deleting a menu | Its categories and location links are removed with it | ✅ | `MenuDeleteCascadeTests.DeleteMenu_MenuWithCategoriesAndLocationLink_RemovesThemAndKeepsOtherMenusCategories` |
| M19 | Owner or Admin creates, reads, changes and deletes a category | Allowed; the location is kept when it belongs to the organization | ✅ | `MenuCategoryServiceTests.CreateAsync_WhenUserIsOwnerOrAdmin_CreatesCategory`, `MenuCategoryServiceTests.CreateAsync_WhenLocationBelongsToOrganization_StoresLocationId`, `MenuCategoryServiceTests.GetByIdAsync_WhenUserIsOwnerOfOrganization_ReturnsCategory`, `MenuCategoryServiceTests.UpdateAsync_WhenUserIsAdminOfOrganization_UpdatesCategory`, `MenuCategoryServiceTests.DeleteAsync_WhenUserIsOwnerOfOrganization_DeletesCategory` |
| M20 | Category lists | Ordered by sort order, then name | ✅ | `MenuCategoryOrderTests.GetAllAsync_SortOrdersTieAndDiffer_ReturnsBySortOrderThenName`, `MenuCategoryOrderTests.GetByMenuIdAsync_SortOrdersTieAndDiffer_ReturnsBySortOrderThenName` |

## 4. Input rules
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| M21 | Menu name missing or over 100 characters | 400 with the translated message | ✅ | `MenuMessagesEndpointTests.CreateMenu_NameMissing_ReturnsLocalizedRequiredError`, `MenuMessagesEndpointTests.EditMenu_NameOver100Characters_ReturnsLocalizedMaxLengthError` |
| M22 | Menu description over 500 characters | 400 with the translated message; exactly 500 is accepted | ✅ | `MenuMessagesEndpointTests.EditMenu_DescriptionOver500Characters_ReturnsLocalizedMaxLengthError`, `MenuMessagesEndpointTests.EditMenu_DescriptionOf500Characters_IsAccepted` |
| M23 | Category name missing, description over 1000 characters, menu ID missing or not an ID | 400 with the translated message | ✅ | `MenuMessagesEndpointTests.CreateMenuCategory_NameMissing_ReturnsLocalizedRequiredError`, `MenuMessagesEndpointTests.UpdateMenuCategory_DescriptionOver1000Characters_ReturnsLocalizedMaxLengthError`, `RequiredIdFieldEndpointTests.IdMissingOrNull_ReturnsLocalizedRequiredError`, `RequiredIdFieldEndpointTests.IdNotAnId_ReturnsLocalizedRequiredError` |
| M24 | Category name over 255 characters | 400 with the translated message; exactly 255 is accepted | ✅ | `MenuMessagesEndpointTests.CreateMenuCategory_NameOver255Characters_ReturnsLocalizedMaxLengthError`, `MenuMessagesEndpointTests.CreateMenuCategory_NameOf255Characters_IsAccepted` |

## Open questions (no row until decided)
- **Staff and categories:** all six roles may read menus (M3), but only Owner and Admin may even
  read categories (M5), so a Waiter or Kitchen user cannot see a menu's sections. Intended?
- **Categories of an unknown menu or location:** the lists answer 200 with an empty list, while
  an unknown menu elsewhere answers 404. Should these be 404 too?
