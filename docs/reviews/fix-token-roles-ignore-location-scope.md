# Review: Keep one-location roles out of the access token (re-review)

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 1138 passed, 0 failed (Shared 184, Menu 96, Organization 152, Identity 309, Web 397)

Scope: 3 commits (`3887999`, `e5543cc`, `ab70983`), 10 files. No plan folder (bug fix from `backlog/bugs/2026-10-19-token-roles-ignore-location-scope.md`), so Completeness was not checked. `check-security-matrices.py` passes. No packages, migrations, new endpoints or Shared contracts; no secrets. The backlog file deletion is uncommitted, as intended.

## Earlier findings
| Earlier | Status |
|---|---|
| Fix first 1: menu list endpoints refuse location-only staff | Fixed. `MenuController.cs:97,111` use plain `[Authorize]`; the service decides (`CheckReadAllAsync`, `CheckReadAtLocationAsync`), matching the user's decision |
| Fix first 2: Menu matrix not updated, no endpoint test | Fixed. M3 reworded, new M31, endpoint test `GetMenus_WaiterAssignedOnlyToBatel_ReadsBatelMenusButNotTheFullList` |
| Watch out 1: audit-log endpoint tests | Closed by user decision (not added) |
| Watch out 2: S5 and F6 wording | Fixed (`identity-matrix.md:128,141`) |
| Watch out 3: note in the permission-policies backlog item | Fixed |
| Watch out 4: no `Scenarios:` line | Still none; acceptable, no scenarios file |

## Checks asked for
- **Hand-built token.** `TestAccessTokens.Create(services, JoaoId, CantinaId)` gives `nameidentifier`, `sub`, `org_id` and no role claim. `AccessTokenFactory` gives the same for a one-location Waiter, plus `jti`, `email` and `name`, which no authorization check reads. Faithful. The factory output itself is proven by L14 (`LoginAsync_OnlyAdminForOneLocation_TokenCarriesNoRoleClaims`).
- **Other `[Authorize(Roles=...)]`.** Only `MenuController.cs:27,41,55,69,83` (`Owner,Admin` on create, edit, delete, link, unlink). Every one of those service methods calls `RequireOrganizationAdminAsync`, which already counts only all-locations roles, so the attribute refuses nobody the service would allow. `AuditLogController.cs:11` (`reports.view`) is the intended refusal. Category endpoints use plain `[Authorize]`, so Rule 7 (location Admin reads that location's categories) still works.
- **Matrices.** M3 names 8 tests; the 4 service and endpoint "AnyRole" tests seed all-locations roles (`RoleIn` sets no location), the 3 new service tests and the endpoint test match the row. M31 names 2 service tests (full list refused, other location refused) and the endpoint test (full list 403). L14 names the 3 new or renamed tests and drops the old name. S5 and F6 text matches the factory; they are proven through L14, not by their own tests.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `docs/specs/shared/2026-10-19-access-decisions-use-all-user-roles.md:33` | Rule 5 still names "menus today" as organization-wide, but reading one location's menus now follows Rule 1a. The decision lives only in the matrix (M3). | Add a rule like Rule 7: "Decided by the user: any role for a location, or for all locations, reads that location's menus; the full list needs a role for all locations." |
| 2 | `tests/RestaurantManagement.Web.Tests/Menu/MenuMultiRoleEndpointTests.cs:13-16` | Class summary says "The token carries Admin so the endpoint's first filter passes". No longer true for the new test, and the read endpoints have no role filter now. | Reword in the next change to this file. |
| 3 | `tests/RestaurantManagement.Web.Tests/Menu/MenuMultiRoleEndpointTests.cs:58` | Asserts only 200 for Batel; no menu is linked to Batel, so an empty list passes. The service test `GetMenusByLocationAsync_WaiterAtBatel_ReturnsBatelMenus` covers the content. | Optional: link a menu to Batel and assert its ID in the body. |

## Check before merging
- `AccessTokenFactory.cs:46`: auth change. Every location-only user now has no role claims. Any client screen that reads role claims will treat them as role-less.
- `MenuAccessChecker.cs:50` and the flipped test `GetMenusAsync_WaiterAssignedOnlyToOneLocation_ThrowsForbidden`: location-only staff now get 403 on `GET /api/menus` (allowed before). Intended per the user's decision; the client must call `GET /api/menus/location/{id}` for them.
- Future `[Authorize(Roles=...)]` or permission policies on location-level actions will wrongly refuse location-only staff. The backlog note covers this; reviewers should watch for it.

## Pull request description
**fix(identity): keep one-location roles out of the access token**

Problem: the access token listed every role a user holds in the organization, including roles held for one location. Permission policies such as `reports.view` read those claims, so an Admin for one location could read the whole organization's audit log. This broke spec `docs/specs/shared/2026-10-19-access-decisions-use-all-user-roles.md` Rule 5.

Changes:
- Identity: `AccessTokenFactory` adds only role claims held for all locations (login, organization selection and refresh). Matrix L14, S5, F6 updated; new L14 tests.
- Menu: the two read endpoints drop their role filter; the service decides. `GET /api/menus/location/{id}` is allowed for any of the six roles held for that location or for all locations, and refused for another location. `GET /api/menus` needs a role held for all locations. Matrix M3 updated, new row M31, service and endpoint tests.
- Backlog: the permission-policies item notes that location-level actions must check access in the service.

Effect: location-only staff no longer pass token-based checks. They keep reading their location's menus but can no longer list every menu of the organization.

Risk: high (auth/role change). Solves `backlog/bugs/2026-10-19-token-roles-ignore-location-scope.md` (deletion committed separately by the user).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
