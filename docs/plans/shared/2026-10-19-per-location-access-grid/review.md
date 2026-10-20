# Review: Per-location access grid

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 1073 passed, 0 failed (Shared 175 · Organization 123 · Menu 68 · Identity 316 · Web 391)

Second review, after the fix round (`e45ed44`, `2860a10`). All three earlier Fix first items are resolved:
- **Matrix rows (old #1, #2):** every row that was ✅ on `development` is ✅ again in all five matrices. The only rows that are not ✅ are the design rows (D, ORD, SHD, WD), which have no status column. I checked F5 (`SessionServiceSelectOrganizationTests...MembershipOfSelectedOrganizationRemoved_ThrowsUnauthorized`) and OR14 (`OrganizationTenantEndpointTests.LocationById_LocationOfAnotherOrganization_...`) by hand: both assert their row's Expected result. `python3 .claude/scripts/check-security-matrices.py` passes.
- **`OrganizationService.cs` (old #3):** the three forwarding wrappers are gone. The file is 299 lines and the behaviour is unchanged.

Also checked: since the baseline `4ddf2a7` only one test line has changed (`OrganizationGridEndpointTests.cs:127`, approved by the user); the fix round touched no tests; no packages were added; no secrets are in the diff; every batch has its Delivered notes; the new item `backlog/tech-debt/2026-10-20-fill-testing-skill-fixtures-table.md` follows the backlog skill (`proposed`, its evidence is real at `.claude/skills/testing/SKILL.md:56-69`, no duplicate found).

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `docs/plans/shared/2026-10-19-per-location-access-grid/plan.md:51-60` | Still open from the first review. Checklist items are ticked without their commit hashes | Add the hashes: T1 `c7bd8c5`, T2 `60b5518`, T3 `5d7f59c`, T4 `3f998b2`, T5 `f5f2ea9`, T6 `1c272f9`, T7 `1eca618`, T8 `f90c515`, T9 `7d7732e`, T10 `46e0008` |
| 2 | commits `c2a4960`, `46e0008`, `e45ed44`, `2860a10` | `c2a4960` (`chore(claude)`) also deletes four retired test files (745 lines). `46e0008` also changes workflow step 4 in the root `CLAUDE.md`. `46e0008` and both fix-round commits have no scenarios but say `Scenarios: 1–3` | List both process changes and the test deletions in the pull request description (done below), or reword the commits before merging |
| 3 | `.claude/skills/security/SKILL.md:21` (and the reviewer checklist) | The skill still says every action declares its permission through an authorization policy. Menu, location and organization actions now use a plain `[Authorize]`, and the service asks `IAccessGuard` | Update section 2 and the checklist: actions tied to a location are checked in the service through `IAccessGuard`; whole-organization actions use the `<module>.<level>` policies |
| 4 | `src/RestaurantManagement.Modules.Organization/Services/LocationService.cs:169-173`, `src/RestaurantManagement.Modules.Menu/Services/MenuService.cs:223-230` | Cells at a Closed location still count. An Admin with Locations Manage there can reopen it with an edit. Non-Owners cannot edit a menu linked to a Closed location | Confirm with the user that reopening is not "creating" a location (spec Outcome #6). If Closed links should not count, write a backlog item |
| 5 | `backlog/features/2026-10-19-*.md`, `backlog/features/2026-10-15-custom-roles.md`, `backlog/tech-debt/` (all uncommitted) | The branch deletes the location-owned-entities item (`634c1e9`), but the items it was merged into and the `custom-roles` update are still uncommitted. So is the deletion of `apply-permission-policies-to-endpoints.md` | User's call: commit them before merging, or that evidence is lost |
| 6 | `src/RestaurantManagement.Shared/Controllers/AuditLogController.cs:11` | The magic string `"reports.read"` must match the name built in `PermissionPolicyExtensions.cs`. A rename only fails at run time | Add one Shared helper or set of constants for policy names, and use it in both places |
| 7 | `src/RestaurantManagement.Modules.Menu/Services/MenuService.cs:50-56`; `src/RestaurantManagement.Shared/Localization/SharedResource.resx:105`, `SharedResource.pt.resx:105` | The if/else repeats what `RequireManageMenuAsync` already does (an empty set means every location). The `UserNotAdminOrOwner` key is no longer used | Replace the if/else with `await _access.RequireManageMenuAsync(namedLocations);` and remove the unused key from both files |

## Check before merging
1. **Every authorization decision now reads the database on each request** (T3, T6). The token carries no roles. `AccessGuard` and `LocationAccessMiddleware` both read the grid through `MemberAccessLookup`, so a bug there either locks everyone out or lets everyone in. The `user_roles` table is dropped (pre-launch, no data).
2. **"Every non-closed location" rule** (decided at Gate 1). The audit log, the full menu and category lists, and menus without links need the level at every non-closed location. So adding a location immediately locks every Admin and Manager out of those actions until they are given access there.
3. **`MemberAccessService` does not check who calls it.** Nothing calls it in this slice. Slice 2 must add the guard and the delegation rules before any endpoint uses it.

## Pull request description
**Per-location access grid (slice 1 of 3)**

Every member of an organization now has a stored access grid. For each location and module (Locations, Menus, Cashier, Orders, Kitchen, Reports, Members), the grid holds a level: None, Read or Manage. Every request is allowed or refused from that grid, read fresh from the database, so a change applies at once with no new login. The Owner is a flag on the membership and passes every check. Roles are presets stored per organization.

**What changed per module**
- **Shared:** grid types and a pure decision (`AccessGridDecision`); a request-scoped `IAccessGuard`; new contracts `IMemberAccessLookup` and `IOrganizationAccessSetup`; `IOrganizationLookup.GetNonClosedLocationIdsAsync`; `X-Location-Id` is accepted only when the member has a cell at that location (or is the Owner); new audit action `member.access_changed`. Removed `IUserRoleLookup`, `IUserRoleAssigner`, `AccessDecision`, `RoleAssignmentDto`, `Permissions`, `RolePermissions` and `Roles`.
- **Identity:** `organization_members` (with the Owner flag) replaces `user_roles`. New tables `organization_roles` and `organization_role_levels` (five default roles per new organization), `member_location_levels` and `member_location_roles`. Three migrations. Tokens carry only the user and the organization. `MemberAccessService` sets a member's access from presets plus changed cells (audited); no endpoint uses it yet.
- **Web:** one policy per module and level (14, named like `reports.read`), decided by the guard. The level is needed at every non-closed location.
- **Organization:** only the Owner creates or closes locations and edits the organization. Editing a location needs Locations Manage at that location. Listing needs any access. Creating an organization adds the Owner and the default roles.
- **Menu:** reads need Menus Read and writes need Menus Manage at the location involved. Actions across locations need it at every involved location, or at every non-closed location. Role attributes removed from `MenuController`.

**Spec Outcomes delivered:** #1–#13, including #9a, #9b and #9c (security matrices M32–M43, OR36–OR43, SH34–SH42, T30–T42, W3–W10). Every row that was proven on `development` is still proven.

**Risk and flags (from `plan.md`):** High. The token's role claims are gone. Shared contracts were added, changed and removed. Authorization changed in every module. There are three Identity migrations, including the drop of `user_roles`. Pre-launch, so there is no data and no backfill. No new package, integration or public endpoint.

**Also in this branch:** the tester rule and workflow step 4 (`CLAUDE.md`) now let rewritten existing tests fail until their task is built. Four retired test files were deleted in `c2a4960`. One user-approved test change: `OrganizationGridEndpointTests` reads the organization back by Id.

**Backlog:** `custom-roles` and `member-role-change` updated. `apply-permission-policies-to-endpoints` is solved; its deletion is left for the user to commit. Slice 2 (invite member, edit the grid) and slice 3 (filter lists by location) are in `backlog/features/2026-10-19-*.md`. New: `backlog/tech-debt/2026-10-20-fill-testing-skill-fixtures-table.md`.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
