# Review: Tenant Query Filters and Location Access Check

**Verdict:** Ready to merge
**Build:** pass (0 errors, 2 warnings that were already there)   **Tests:** 521 passed, 0 failed (Shared 106 · Organization 42 · Menu 42 · Identity 148 · Web 183)

Second and final review, compared against `development` (11 commits, baseline `86574f9`). The previous Fix first item is resolved in `8a90041`. The `IgnoreQueryFilters` calls on `LocationMenus` are gone, and `MenuService.cs` is now identical to its state before `1277467`. `grep IgnoreQueryFilters src/` finds only the two `OrganizationService` reads, which matches the list in `CLAUDE.md`. Since the baseline, one test line changed: `OrganizationId = CantinaId` in the seed data of `MenuServiceRoleAccessTests.cs:42`. The user approved it, and no assertion changed. No package was added, so the vulnerable package check was not needed. No secrets in the diff.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | Commit `eb5b5ee` (T3) | The suite was red when T3 was committed. Six `MenuServiceRoleAccessTests` cases failed until the seed fix in `8a90041` | Mention in the PR (pull request) that `eb5b5ee` to `1277467` do not pass on their own. Squash-merging avoids the issue |
| 2 | Commits `86574f9`, `1277467`, `f8feeb5`, `8a90041` | The scenario numbers in these commit bodies are wrong. `1–N` is a placeholder. `34-45` goes past the 40 scenarios. `1–5` is on T8, which has no scenarios | Fix the bodies if the history is rewritten, or use a squash commit message with the correct numbers (1–40) |
| 3 | Commit `7a9d307` | T4 and T5 share one commit. This is disclosed in B3 | Accept it. Next time, plan tasks that touch the same file as one task |
| 4 | `src/RestaurantManagement.Modules.Organization/Controllers/LocationController.cs:23,32`, `src/RestaurantManagement.Modules.Menu/Controllers/MenuCategoryController.cs:75` | The renamed actions have no `[Authorize(Roles = ...)]` of their own. This was already true before the branch | Already covered by `backlog/improvements/2026-10-15-apply-permission-policies-to-endpoints.md` |
| 5 | `MenuController.cs:96`, `MenuCategoryController.cs:75`, `LocationController.cs:23` | `GET /api/menus`, `/api/menu-categories` and `/api/locations` are not paginated. This was already true before the branch, and there is still no backlog file for it | Add `backlog/tech-debt/<date>-paginate-list-endpoints.md` with a maximum page size |
| 6 | `src/RestaurantManagement.Shared/Middleware/LocationAccessMiddleware.cs:48-50` | One `if` covers three refusals: unknown location, another organization's location, and no assignment. The result is correct but hard to read | Optional: split it into two guard clauses |

## Check before merging
1. **`TenantQueryFilterExtensions.cs`**: every tenant read in every module now depends on this expression builder. A mistake here would show every organization's data or hide all of it. Scenario 4 shows the organization value is not fixed to the first one used. Scenario 1 shows that a missing organization returns no rows, not all rows.
2. **`LocationAccessMiddleware` (`Program.cs:290`)**: adds a new 403 to every signed-in request that sends `X-Location-Id`, including Identity's organization selection. If a client keeps sending an old location header after switching organization, every request gets 403.
3. **Breaking routes (T3, T5)**: seven endpoint contracts change, and the old routes return 404 or 405. No frontend exists yet, so no client has to change at the same time. API (Application Programming Interface) docs and Swagger consumers should use the new routes.

## Pull request description
**Tenant query filters and location access check**

Reads of organization data are now limited to the signed-in user's organization automatically. Each module's DbContext (the class that talks to the database for that module) applies EF Core (Entity Framework Core) global query filters, so individual queries no longer have to remember to filter. The organization always comes from the token's `org_id` claim. The selected location comes from the `X-Location-Id` header and is checked against the user's role assignments.

**Shared**
- New `ITenantContext` / `TenantContext`: the organization comes from an open tenant scope first, then from the token. The location comes from the checked header.
- New `ApplyTenantQueryFilters`: filters every `TenantEntity` by organization and every `LocationEntity` by the selected location. With no organization, it returns no rows.
- New `LocationAccessMiddleware`: an invalid ID returns 400. No organization, an unknown or foreign location, or no assignment returns 403. Messages are in English and Portuguese.
- `IUserRoleLookup.HasLocationAccessAsync` added (Shared contract change).

**Menu**
- Filters applied. Another organization's menu or category now returns 404 instead of 403.
- `OrganizationId` removed from `CreateMenuRequest` and `CreateMenuCategoryRequest`.
- Routes: `GET /api/menus` (was `/api/menus/organization/{organizationId}`) and `GET /api/menu-categories` (was `/api/organizations/{organizationId}/menu-categories`).

**Organization**
- Filters applied. Two listed bypasses in `OrganizationService` (trial check and organization list), each limited to the user's own memberships.
- Routes: `GET`/`POST /api/locations` (was `/api/locations/{organizationId}`) and `PUT /api/organizations/edit` (was `/edit/{organizationId}`).

**Identity**
- `UserRoleLookup.HasLocationAccessAsync`: true when the user has a role in that organization for that location, or for all of its locations.

**Docs**
- The root `CLAUDE.md` gains a "Tenant data filtering" section: filter bypasses are allowed only in the listed places (currently two).

**Spec Outcomes delivered**
1. Lists show only the user's own organization
2. Another organization's record returns 404
3. A location the user is not assigned to returns 403
4. A role for all locations can select any location of the organization
5. A location of another organization returns 403
6. Saving without an organization fails

**Risk: High** (from `plan.md`)
- Breaking API contract on seven endpoints. No frontend exists yet, so no client needs to change at the same time.
- New 403 path on every authenticated request.
- Shared contract changes (`ITenantContext`, `SelectedLocation`, `IUserRoleLookup`).
- No migration and no new package.

**Notes**
- One read-only test was edited with the user's approval. The seeded `LocationMenu` in `MenuServiceRoleAccessTests` now sets `OrganizationId` (seed data only, no assertion changed).
- Commits `eb5b5ee` to `1277467` do not pass the suite on their own. Squash-merge is recommended.

**Known follow-ups**
- See the 2026-10-15 files in `backlog/`, including a menu category that accepts a foreign menu ID, and the fact that no entity belongs to a location yet.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
