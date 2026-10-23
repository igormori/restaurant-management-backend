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
| M4 | A member with no stored access in the menu's organization, even with access in another organization | 403 | ✅ | `MenuServiceTests.GetMenusAsync_MemberWithNoAccessYet_ThrowsForbidden`, `MenuGridEndpointTests.CreateMenu_ManageEverywhereOnlyInAnotherOrganization_ReturnsForbidden` |
| M5 | Menu categories: create, read, change, delete by a member with no stored access | 403 (the Menus level at the category's location is needed: Read to read, Manage to write) | ✅ | `MenuCategoryServiceTests.CreateAsync_MemberWithNoAccessYet_ThrowsForbidden`, `MenuCategoryServiceTests.GetByIdAsync_MemberWithNoAccessYet_ThrowsForbidden`, `MenuCategoryServiceTests.GetAllAsync_MemberWithNoAccessYet_ThrowsForbidden`, `MenuCategoryServiceTests.UpdateAsync_MemberWithNoAccessYet_ThrowsForbidden`, `MenuCategoryServiceTests.DeleteAsync_MemberWithNoAccessYet_ThrowsForbidden` |
| M6 | No organization chosen in the token | 403 "organization required" | ✅ | `MenuTenantEndpointTests.MenuEndpoints_NoOrganizationInToken_ReturnsForbiddenOrganizationRequired` |
| M32 | A member with Menus Read at one location (a Waiter at Batel) reads that location's menus | Allowed; at a location where the member has no level: 403 | ✅ | `MenuGridEndpointTests.GetMenusByLocation_WaiterWithMenusReadAtBatel_ReadsBatelAndIsRefusedAtMercado` |
| M33 | Categories follow the Menus level | A member with Menus Read at the location lists its categories; a member with Menus None does not (403) | ✅ | `MenuGridEndpointTests.GetMenuCategoriesByLocation_MenusReadAllowedAndMenusNoneRefused` |
| M34 | Editing a menu | Menus Manage allows it (a Manager); Menus Read refuses it (a Cashier): 403 | ✅ | `MenuGridEndpointTests.EditMenu_ManagerWithMenusManageAllowedAndCashierWithMenusReadRefused` |
| M35 | A member with Menus Read only creates, edits, deletes, links or unlinks a menu, or creates, changes or deletes a category | 403; nothing stored, changed or removed | ✅ | `MenuGridEndpointTests.MenuAndCategoryWrites_MemberWithMenusReadOnly_ReturnsForbiddenAndChangesNothing` |
| M36 | The same Admin manages Menus at one location and reads them at another | Edits the menu linked to the first, reads the second's menus, and is refused editing a menu linked to the second | ✅ | `MenuGridEndpointTests.EditMenu_AdminManagesBatelAndReadsMercado_EditsBatelMenuReadsMercadoAndIsRefusedEditingMercadoMenu` |
| M37 | A level is lowered between two requests with the same token | The second request is refused: a change applies at once, with no new login | ✅ | `MenuGridEndpointTests.EditMenu_ManageLoweredToReadBetweenTwoRequestsWithTheSameToken_SecondRequestIsRefused` |
| M38 | The Owner with no stored level | Passes every menu and category action | ✅ | `MenuGridEndpointTests.CreateMenu_OwnerWithNoStoredLevels_ReturnsOk` |
| M39 | Manage everywhere in another organization | Gives nothing here: 403 on a menu action | ✅ | `MenuGridEndpointTests.CreateMenu_ManageEverywhereOnlyInAnotherOrganization_ReturnsForbidden` |
| M40 | A menu or category write with Manage at every involved location | Allowed. Involved: the named locations (every non-closed one when none is named) for creating a menu; every location the menu is linked to (every non-closed one when none) for editing or deleting it; the location itself for linking, unlinking and a category of that location (every non-closed one for a category without a location) | ✅ | `MenuGridAccessTests.MenuAndCategoryWrites_ManageAtEveryInvolvedLocation_Succeeds` |
| M41 | The same writes with Manage missing at one involved location (Read there, or no level) | 403; nothing stored, changed or removed | ✅ | `MenuGridAccessTests.MenuAndCategoryWrites_ManageMissingAtAnInvolvedLocation_ThrowsForbiddenAndChangesNothing` |
| M42 | Reads with the Menus level at every involved location | Allowed: a location's menus or categories need Read there; the full menu list, all categories, a menu's categories and a category without a location need Read at every non-closed location | ✅ | `MenuGridAccessTests.MenuAndCategoryReads_MenusReadAtEveryInvolvedLocation_Succeeds` |
| M43 | The same reads with the Menus level missing at one involved location | 403 | ✅ | `MenuGridAccessTests.MenuAndCategoryReads_MenusReadMissingAtAnInvolvedLocation_ThrowsForbidden` |
| M44 | Token whose user identifier is not a user ID, on any menu or menu category endpoint | 401 with the translated message | ✅ | `MenuUserIdentifierEndpointTests.MenuEndpoints_UserIdentifierNotAUserId_ReturnsUnauthorizedWithLocalizedMessage`, `MenuUserIdentifierEndpointTests.MenuCategoryEndpoints_UserIdentifierNotAUserId_ReturnsUnauthorizedWithLocalizedMessage` |

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
| M16 | A member who is allowed creates a menu with location IDs | Menu stored active; the token organization's locations are linked; IDs of other organizations' locations are ignored | ✅ | `MenuLocationEndpointTests.CreateMenu_LocationIdsOfTwoOrganizations_LinksOnlyTheTokenOrganizationsLocation` |
| M17 | Linking a menu to a location twice; unlinking one that is not linked | One link only; unlinking still answers OK and changes nothing | ✅ | `MenuLocationLinkTests.AttachMenuToLocationAsync_LinkedTwice_KeepsOneLink`, `MenuLocationLinkTests.DetachMenuFromLocationAsync_LocationNotLinked_AnswersOkAndChangesNothing` |
| M18 | Deleting a menu | Its categories and location links are removed with it | ✅ | `MenuDeleteCascadeTests.DeleteMenu_MenuWithCategoriesAndLocationLink_RemovesThemAndKeepsOtherMenusCategories` |
| M19 | A member who is allowed creates, reads, changes and deletes a category | Allowed; the location is kept when it belongs to the organization | ✅ | `MenuCategoryServiceTests.CreateAsync_WhenAccessIsAllowed_CreatesCategory`, `MenuCategoryServiceTests.CreateAsync_WhenLocationBelongsToOrganization_StoresLocationId`, `MenuCategoryServiceTests.GetByIdAsync_WhenAccessIsAllowed_ReturnsCategory`, `MenuCategoryServiceTests.UpdateAsync_WhenAccessIsAllowed_UpdatesCategory`, `MenuCategoryServiceTests.DeleteAsync_WhenAccessIsAllowed_DeletesCategory` |
| M20 | Category lists | Ordered by sort order, then name | ✅ | `MenuCategoryOrderTests.GetAllAsync_SortOrdersTieAndDiffer_ReturnsBySortOrderThenName`, `MenuCategoryOrderTests.GetByMenuIdAsync_SortOrdersTieAndDiffer_ReturnsBySortOrderThenName` |

## 4. Input rules
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| M21 | Menu name missing or over 100 characters | 400 with the translated message | ✅ | `MenuMessagesEndpointTests.CreateMenu_NameMissing_ReturnsLocalizedRequiredError`, `MenuMessagesEndpointTests.EditMenu_NameOver100Characters_ReturnsLocalizedMaxLengthError` |
| M22 | Menu description over 500 characters | 400 with the translated message; exactly 500 is accepted | ✅ | `MenuMessagesEndpointTests.EditMenu_DescriptionOver500Characters_ReturnsLocalizedMaxLengthError`, `MenuMessagesEndpointTests.EditMenu_DescriptionOf500Characters_IsAccepted` |
| M23 | Category name missing, description over 1000 characters, menu ID missing or not an ID | 400 with the translated message | ✅ | `MenuMessagesEndpointTests.CreateMenuCategory_NameMissing_ReturnsLocalizedRequiredError`, `MenuMessagesEndpointTests.UpdateMenuCategory_DescriptionOver1000Characters_ReturnsLocalizedMaxLengthError`, `RequiredIdFieldEndpointTests.IdMissingOrNull_ReturnsLocalizedRequiredError`, `RequiredIdFieldEndpointTests.IdNotAnId_ReturnsLocalizedRequiredError` |
| M24 | Category name over 255 characters | 400 with the translated message; exactly 255 is accepted | ✅ | `MenuMessagesEndpointTests.CreateMenuCategory_NameOver255Characters_ReturnsLocalizedMaxLengthError`, `MenuMessagesEndpointTests.CreateMenuCategory_NameOf255Characters_IsAccepted` |

## Open questions (no row until decided)
- **Categories of an unknown menu or location:** the lists answer 200 with an empty list, while
  an unknown menu elsewhere answers 404. Should these be 404 too?
