# Review: Access decisions use all of a user's roles

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 1131 passed, 0 failed (Shared 184, Menu 92, Organization 152, Identity 307, Web 396)

Re-review after the user-requested fix round (commits `80b765f`, `e038beb`, `60df7c0`, `32e29d2`, `a065566`). The first full run had 1 failure in Web.Tests (Web.Tests: the endpoint and database test project). Three later runs passed 396 of 396, so that test is likely flaky (it fails some runs and not others). Its name was not captured (see Watch out #3). Matrix check (`python3 .claude/scripts/check-security-matrices.py`): passes. No package changes, so no vulnerable-package scan was needed.

What the fix round changed, checked against the diff:
- **Backlog item:** `backlog/tech-debt/2026-10-15-user-role-lookup-single-role.md` is back in the branch history and deleted only in the working tree, as the rules ask. Previous Watch out #1 is solved. The plan's DoD (Definition of Done: the checks that must pass to count as finished) line now says what happened.
- **Line limit 200 → 300** (`e038beb`): limited to the hook, the clean-code skill and three agent files. This was the user's choice. It is outside the plan's tasks, so it is listed in the pull request description.
- **Category read rule** (`32e29d2`, spec Rule 7): `MenuAccessChecker.CheckAdminAtLocationAsync` calls `AccessDecision.AllowsAtLocation` with Owner/Admin for a category that has a location. For a category without one it calls `AllowsOrganizationWide`. It is used only by `GetByIdAsync` and `GetByLocationIdAsync`. `GetAllAsync`, `GetByMenuIdAsync` and every write still call `RequireOrganizationAdminAsync`. Tenancy holds:
  - `GetByIdAsync` loads the category through the tenant query filter, so `category.OrganizationId` is always the token's organization.
  - `GetByLocationIdAsync` lists categories through the same filter, so a location from another organization returns an empty list.
  - A Waiter or another non-Admin role at the location is still refused (scenario 45). Both role orders are tested (scenarios 39 and 40).
  - M27 is back to writes only. M29 (allowed) and M30 (refused) name the 7 new tests. Each test exists, carries its `Rule` tag and asserts the row's Expected result.
- **Test change:** since the baseline (`6331e1d`), the only change under `tests/` is in `MenuMultiRoleAccessTests.cs`. Old scenario 39, which the previous review asked for, is replaced by scenarios 39–45, with written approval from the user. No other test was edited, deleted, skipped or weakened.
- **Cleanups** (`a065566`): previous Watch out #2, #3, #4, #6 and #8 are solved. The user chose to skip #5 (`assignments == null` guards kept for the mocks) and #7 (scenario numbers in the bodies of `8e8bb72` and `8148426`).

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `src/RestaurantManagement.Modules.Menu/Services/MenuAccessChecker.cs:35` | The method names mix styles: `RequireOrganizationAdminAsync` next to `CheckAdminAtLocationAsync` and `CheckReadAsync`. All three throw on refusal. Also, `CheckAdminAtLocationAsync` accepts an Owner too. | Rename it to `RequireAdminAtLocationAsync` (and `CheckReadAsync` to `RequireAnyRoleAsync`) so they read alike |
| 2 | `src/RestaurantManagement.Modules.Organization/Services/OrganizationService.cs` (300 lines) | The file is exactly at the new 300-line limit. The next line added to it will be blocked by the hook. | Plan a split of `OrganizationService` (for example, settings editing in its own service) before its next feature |
| 3 | `tests/RestaurantManagement.Web.Tests` | 1 test failed in the first full `dotnet test` run, then passed in three runs in a row. Nothing in this branch's diff points to it. | If CI (continuous integration: the automatic build and test run) shows it again, record its name and add a backlog item for the flaky test |

## Check before merging
- **Location Admins can now read categories** (`MenuCategoryService.cs:79,121`). This is the only new grant in the fix round. An Admin assigned to location A reads A's categories, and only A's: never another location's, never a location-less one, never the full lists. Each case is tested. Confirm that the full lists (`GetAllAsync`, `GetByMenuIdAsync`) refusing that Admin is acceptable for now. Filtering them by location is recorded in `backlog/tech-debt/2026-10-15-no-location-owned-entities.md`.
- **Shared contract change (`IUserRoleLookup`):** `GetRoleAsync` is removed and replaced by `GetRoleAssignmentsAsync`. The organization filter in `UserRoleLookup` is what stops a role held in another organization from counting (T27–T29).
- **Token still ignores location:** controller `[Authorize]` checks and permission policies still read token role claims that carry no location (`backlog/bugs/2026-10-19-token-roles-ignore-location-scope.md`). Only the service checks are location-aware.

## Pull request description
**Access decisions use all of a user's roles**

A user can hold several roles in one organization (for example Admin for all locations plus Waiter at one location). Until now the shared role lookup returned one arbitrary role, the first row the database gave back, so the same user could be allowed on one request and refused on the next. Access checks now consider every role the user holds in the organization:
- An organization-wide action (menus, menu category writes and full category lists, creating a location, editing the organization) needs Owner or Admin **for all locations**.
- A location action (edit, close) also accepts Owner or Admin for that exact location.
- Reading menu categories (decided by the user after review): an Admin assigned to a location may read that location's categories, one by one or as that location's list. Never another location's, and never a category without a location.
- A role held in another organization never counts.

What changed per module:
- **Shared:** new `RoleAssignmentDto` and the pure `AccessDecision` helper (`AllowsOrganizationWide`, `AllowsAtLocation`). `IUserRoleLookup.GetRoleAsync` is replaced by `GetRoleAssignmentsAsync`.
- **Identity:** `UserRoleLookup.GetRoleAssignmentsAsync` returns every assignment in the requested organization.
- **Menu:** permission checks moved into the new `MenuAccessChecker` (split approved by the user). Menu writes, category writes and full category lists need Owner/Admin for all locations. A single category, or a location's categories, can also be read by an Admin of that location. Menu reads need any role in the organization.
- **Organization:** `LocationService` and `OrganizationService.EditOrganizationAsync` use the helper. Status codes and messages are unchanged.
- **Tooling (user decision):** the production file line limit is raised from 200 to 300 (hook, clean-code skill, agent files).

Spec Outcomes delivered: 1 (tested with Admin per the Gate 1 decision: a Manager never manages menus), 2–12. Rule 7 (category reads) was added to the spec after review.

Risk: **High.** A Shared contract changed and every menu, location and organization authorization decision changed. No migration, no new package, no new endpoint, no token or controller attribute change.

Backlog: `backlog/tech-debt/2026-10-15-user-role-lookup-single-role.md` is solved; its deletion is left for you to commit. New `backlog/bugs/2026-10-19-token-roles-ignore-location-scope.md` (proposed). `backlog/tech-debt/2026-10-15-no-location-owned-entities.md` now notes the category lists.

Security matrices updated: Shared SH29–SH33, Identity T27–T29, Menu M3 and M25–M30, Organization OR31–OR35.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
