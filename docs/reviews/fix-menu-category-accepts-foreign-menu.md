# Review: Reject menu categories that point at another organization's menu or location

**Branch:** `fix/menu-category-accepts-foreign-menu` against `development` (two commits: `54732ce`, `d8ed13b`)
**Verdict:** Ready to merge
**Build:** pass (0 warnings, 0 errors)   **Tests:** 598 passed, 0 failed (Shared 106, Organization 100, Menu 47, Identity 148, Web 197)

Small-change path: no spec, plan or scenarios, so Completeness was not checked. No migration,
no new package, no new public endpoint and no Shared contract change. No secrets in the diff.
Existing tests are unchanged: the test diff only adds tests. Both commits follow Conventional
Commits. The previous Watch out 1 (cross-tenant `LocationId`) is fixed in `d8ed13b`.
`UpdateMenuCategoryRequest` has no `MenuId` or `LocationId`, so only create needed the checks.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `src/RestaurantManagement.Modules.Menu/Services/MenuCategoryService.cs:42` | The menu check looks up by `Id` only and relies on the EF Core (Entity Framework Core) global tenant filter. That matches CLAUDE.md "Tenant data filtering", but the security skill says "a lookup by `Id` alone is a bug". The new location check (line 49) already compares `OrganizationId` explicitly, so the two checks are now inconsistent. | Optional extra safety, since `organizationId` is in scope: `AnyAsync(m => m.Id == request.MenuId && m.OrganizationId == organizationId)`. |
| 2 | `src/RestaurantManagement.Modules.Menu/Services/MenuCategoryService.cs:44,50` | The service throws `BusinessException(..., 404)`. The architecture skill says to return `null` and let the controller send 404. This matches the existing pattern in `MenuService.cs:99-198`, and `CreateAsync` returns a non-nullable type, so changing it here would widen the scope. | Keep as is. If the not-found convention is cleaned up for the Menu module, log it as tech debt. |
| 3 | `src/RestaurantManagement.Modules.Menu/Services/MenuCategoryService.cs:48` | `IOrganizationLookup.GetLocationAsync` does not look at `Location.Status`, so a category can be linked to a location of the organization that is no longer active. `MenuService` has the same behaviour, and the domain rule is not defined, so this is not a bug in this branch. | If closed locations should be rejected, log a backlog item in `backlog/improvements/`; do not fix it here. |
| 4 | `tests/RestaurantManagement.Modules.Menu.Tests/MenuCategoryServiceTests.cs:112-232` | The five new tests use FluentAssertions (`.Should()...`). The testing skill says new tests use Shouldly, which the Menu test project already references (`MenuAuditTests.cs`). The tests follow the file's existing style, so this is only a style note. | Optional: switch the new tests to Shouldly (`await Should.ThrowAsync<BusinessException>(act)`, `.ShouldBe(0)`). |
| 5 | `tests/RestaurantManagement.Modules.Menu.Tests/MenuCategoryServiceTests.cs:208-231` | `CreateAsync_WhenLocationBelongsToOrganization_StoresLocationId` checks only the response, not the stored row, even though its name says "Stores". | Optional: also read the row back with `database.CreateContext()` and check its `LocationId`. |

## Check before merging
- **Menu check, `MenuCategoryService.cs:41-44`:** the main tenant isolation fix. It is correct
  only because the `MenuDbContext` tenant filter applies to `Menus`. The test
  `CreateAsync_WhenMenuBelongsToAnotherOrganization_ThrowsNotFoundAndStoresNothing` proves it
  (404 and no row stored).
- **Location check, `MenuCategoryService.cs:46-51`:** this check crosses modules through
  `IOrganizationLookup` and compares the location's `OrganizationId` with the token's. An
  unknown location and another organization's location get the same 404, so the other
  organization's data is not revealed. Three new tests cover foreign, unknown and own location.
- **Response code change:** `POST` for menu categories with an unknown or foreign `MenuId` or
  `LocationId` now returns 404. Before, the category was saved (foreign ID) or the database
  foreign key failed (500 for an unknown menu). Clients that relied on the old behaviour will
  get a different answer.

## Pull request description

### Fix: reject menu categories that point at another organization's menu or location

Creating a menu category used to save whatever `MenuId` and `LocationId` the request sent.
The database only checks that the menu exists somewhere, and the location is in another
module with no foreign key, so a category in one organization could reference another
organization's menu or location. Creating a category now answers **404 Not Found** when the
menu, or the location if one is given, is not in the signed-in user's organization. Another
organization's menu or location gets the same answer as one that does not exist, so the other
organization's data is not revealed.

**Menu module**
- `MenuCategoryService.CreateAsync` checks that the menu exists in the token's organization
  (through the tenant query filter) and returns 404 if not (`54732ce`).
- `MenuCategoryService.CreateAsync` checks that the optional `LocationId` belongs to the
  token's organization (through `IOrganizationLookup`) and returns 404 if not (`d8ed13b`).
  Updating a category does not accept `MenuId` or `LocationId`, so it needed no change.
- Five new tests: foreign menu returns 404 and stores nothing; unknown menu returns 404;
  foreign location returns 404 and stores nothing; unknown location returns 404 and stores
  nothing; own location is saved.

**Housekeeping**
- Removes the resolved backlog item
  `backlog/bugs/2026-10-15-menu-category-accepts-foreign-menu-id.md`.

**Risk and flags:** low. No migration, no new package, no endpoint or Shared contract change.
Behaviour change: an unknown or foreign `MenuId` or `LocationId` on category creation now
returns 404 instead of being saved or failing with a 500.

**Known issue (already logged):** the `MenuNotFound` message has no entry in the
`SharedResource` resource files (`backlog/bugs/2026-10-15-menu-not-found-message-missing.md`).

**Build:** pass. **Tests:** 598 passed, 0 failed.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
