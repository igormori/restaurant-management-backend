# Review: Custom roles per organization

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 1246 passed, 0 failed

Terms: DoD (Definition of Done: the checks that must pass to count as finished) · PR (pull request: the request to merge this branch into `development`) · cap (the caller's level in a module at every non-closed location).

This is a re-review at `22357c8`, after the read-guard move and the Identity `Services/` restructure.

**Checks run.** `dotnet build` passes with 0 errors. The 5 warnings are all in `tests/`. `dotnet test` passes: Shared 198, Organization 124, Menu 68, Identity 381, Web 475. `python3 .claude/scripts/check-security-matrices.py` passes. No package was added, so `dotnet list package --vulnerable` was not needed. There are no secrets in the diff.

**Test changes since the baseline `c9628ac`.**
- One file added: `Web.Tests/Identity/RoleReadAccessEndpointTests.cs`.
- In the other test files, only `using` lines and one fully qualified type name (`MemberAccessTestSupport`) changed, all from the restructure. No assertion was edited, deleted, skipped or weakened.
- `RoleServiceTestSupport.cs` now passes `Substitute.For<IAccessGuard>()`. The user approved this.

**Previous Watch out #1 is resolved.** `RoleService.ListAsync` and `GetAsync` (`Services/Roles/RoleService.cs:51`, `:61`) now call `IAccessGuard.RequireAnyAccessAsync()` themselves, right after reading the token organization. This matches `LocationService`. `RolesController` is HTTP only again, with one-line actions. Rows T132 and T133 still prove the 403 through the real endpoint.

**Restructure.**
- The architecture skill (section 4) now allows every new file location: `Services/<Feature>/` and `Templates/`. `enforce-structure.py` enforces this.
- Each namespace matches its folder, and DI (dependency injection) registrations in `Program.cs` use the new names.
- The embedded template resource name now reads `...Identity.Templates.<file>` in both the csproj and `InvitationEmailTemplate.cs:62`.
- Menu (5 files) and Organization (7 files) are still under the 10-file limit, so nothing else breaks the new rule.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | commit `561a5c1`; `src/RestaurantManagement.Modules.Identity/RestaurantManagement.Modules.Identity.csproj:23-24` | `561a5c1` does not build on its own (verified: `CS1566 Could not find file .../Services/invitation-email-en.html`). Its csproj still points at the old template paths, which `22357c8` fixes. Its title, "check role read access in the service", also does not match what it holds (file moves only). This breaks `git bisect` on this range | Squash-merge the PR, or squash `561a5c1` into `22357c8` before merging. Either way, `development` then never holds a commit that fails to build |
| 2 | `src/RestaurantManagement.Modules.Identity/Services/Roles/RoleService.cs:71-72`, `:110-111` | The name-taken check runs before `_rules.RequireAsync`. A caller without Members = Manage, even one with no access at all, gets 400 "name already used" for a taken name and 403 for a free one. Listing is blocked now, so this is the one way left to learn role names | Create: parse the levels, run the rules, then check the name. Rename: load the role, run the rules, then check the name. The existing tests still hold, because their 403 callers send names that are not taken |
| 3 | `src/RestaurantManagement.Modules.Identity/Services/Roles/RoleService.cs:184`, `:190-195` | `SaveRoleAsync` catches only the role-name index. Two edits that add the same module to one role at the same moment hit the (role, module) unique index, and the loser gets a 500 | Catch that index violation too and answer 409 or 400, or write a backlog item |
| 4 | `src/RestaurantManagement.Modules.Identity/Services/Roles/RoleService.cs:170` | `name.ToLower()` uses the request culture, but PostgreSQL `lower(name)` does not. The index still guards, so the only risk is a missed fast path | Use `ToLowerInvariant()` |
| 5 | `src/RestaurantManagement.Modules.Identity/Services/Roles/RoleService.cs:48` | There is no limit on how many roles an organization may create, and `GET api/roles` is not paginated (security skill, section 4). The locations list has the same gap | Add a backlog item for a per-organization role limit or for pagination |
| 6 | `src/RestaurantManagement.Modules.Identity/Services/Roles/IRoleService.cs:5-11` | The `///` summary lists every refusal except the new one: list and read return 403 for a member with no access. Since the guard now lives in the service, this summary is where a reader looks. It is also `///` on an Identity-internal type, which the clean-code skill allows only on Shared contracts (the same applies to `IRoleManagementRules.cs:5`, `Models/RoleModels.cs:3`) | Add "list and read need some access (403)", or shorten the summary to one "why" line |
| 7 | `backlog/tech-debt/2026-10-20-member-access-service-use-grid-builder.md:12`; `backlog/improvements/2026-10-16-login-lockout-per-ip-address.md:21` | The restructure updated these paths but not their line numbers. The added `using` lines shifted the code by one line. `MemberAccessService.cs:26,45` is now `:27,46`, and `SessionService.cs:175-180` is now `:176-181` | Update the two line references |
| 8 | `docs/plans/identity/2026-10-20-custom-roles/plan.md:3`, `:49`; `batches/B2.md:62`; `batches/B3.md:35`, `:64` | Still open from earlier reviews: Status says "In progress", and the backlog-deletion DoD item is unticked. The deletion was committed in `c9628ac` at the user's request. The Delivered notes still name `Services/RoleService.cs` and `Services/RoleManagementRules.cs`, which are now in `Services/Roles/` | Tick the DoD item with "committed in the baseline at the user's request" and set Status to Done. Add "(moved to `Services/Roles/` in `22357c8`)" to the Delivered notes |
| 9 | `src/RestaurantManagement.Modules.Identity/Services/Roles/RoleService.cs:14`; `Services/Invitations/InvitationService.cs:18-19` | The new feature `using` lines were added after the `RestaurantManagement.Shared.*` ones instead of in alphabetical order. The same happens in other moved files. Style only | Run `dotnet format` on the Identity project in a later cleanup |

## Check before merging
- **Commit `561a5c1` does not build**: merge with squash, or squash it into `22357c8` first. Otherwise `development` gets a commit that fails to compile (Watch out #1).
- **`RoleService.ListAsync` / `GetAsync` and `RoleManagementRules.RequireAsync`**: the first two are the only thing that stops a member with no access from reading roles. `RequireAsync` is the new rule for managing roles: the cap is the lowest level over non-closed locations, and a missing cell counts as None. A mistake in either one leaks role data or lets an Admin build a role stronger than her own access.
- **The restructure and migration `20261021023926_UniqueRoleNameIgnoringCase`**: the restructure renames 50 Identity namespaces and the embedded template resource names, so watch for broken references in open branches. The migration keeps the role-name index name, and `RoleService` relies on that name to turn a race into a 400. Later migrations must keep it.

## Pull request description
**Custom roles per organization, and Identity services grouped by feature**

The Owner can now create roles in their own organization, edit a role's default levels and rename roles. So can Admins with Members = Manage at every open location. Admins are capped at their own levels: they cannot create a role above their access, and they cannot edit or rename a role that is already above it. Any member with some access can list and read the roles. A member with no access gets 403. Every change is written to the audit log.

**Identity**
- New endpoints, all signed in: `GET api/roles`, `GET api/roles/{roleId}`, `POST api/roles`, `PUT api/roles/{roleId}/levels`, `PUT api/roles/{roleId}/name`.
- `RoleService` covers list, read, create, edit levels and rename, and checks for some access on list and read. `RoleManagementRules` decides who may manage roles, and the cap. Both live in `Services/Roles/`.
- Role names are unique per organization, ignoring letter case. A new stored `lower(name)` column and unique index enforce this (migration `UniqueRoleNameIgnoringCase`). Empty names, names over 100 characters and "Owner" in any letter case are refused.
- Editing levels never changes existing members' grids. Later invitations and grid edits use the new levels. Renaming updates every holder's label.
- Restructure (requested by the user, no behaviour change):
  - `Services/` is grouped into feature subfolders (`Invitations`, `Members`, `Passwords`, `Registration`, `Roles`, `Sessions`), with matching namespaces.
  - The invitation email templates moved to `Templates/`.
  - `Program.cs`, the test `using` lines and backlog paths are updated to match.
  - The architecture skill and `.claude/hooks/enforce-structure.py` now require this layout once a module's `Services/` passes 10 files.

**Shared**
- New audit actions: `role.created`, `role.levels_changed`, `role.renamed`.
- New messages, in English and Portuguese: `RoleNameAlreadyUsed`, `RoleNameInvalid`.

**Spec Outcomes delivered:** #1 to #10 (create, unique names, grids untouched, rename labels, Admin cap, stronger roles protected, Members = Manage required, no Owner role, tenant isolation, audit).

**Risk (from plan.md):** High. This adds new signed-in endpoints with a new authorization rule, a schema change to the role-name unique index (no data, pre-launch) and new Shared audit actions. There is no new package, no public endpoint, no backfill and no change to the token.

**Security matrices:** Identity rows T91–T133 were added (T30 now says "ignoring letter case"), plus Shared rows SH45–SH46. The matrix check passes.

**Backlog:** `backlog/features/2026-10-15-custom-roles.md` was removed because it is fully solved (committed in the baseline at the user's request). `backlog/features/2026-10-20-delete-or-retire-a-role.md` was added (out of scope).

**Commits and test changes to note:**
- `561a5c1` holds only the restructure's file moves, despite its title, and does not build on its own. `22357c8` holds the content changes for both the restructure and the role read guard; the user asked for one commit. A squash merge is recommended.
- `RoleServiceTestSupport.cs` now passes an `IAccessGuard` substitute, a test change the user approved.
- `dd2499b chore(claude): add the backlog command and backlog-worker agent` is a separate tooling change, included at the user's request.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
