# Review: Tighten user roles and add permissions in code

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 458 passed, 0 failed (Shared 90 · Organization 32 · Menu 42 · Web 151 · Identity 143)

Scope reviewed: commits 923cfc1..aae30d3. This is a re-review after fix commit aae30d3. Commit 8a91936 and the uncommitted `.claude/` changes were ignored, as asked.
Test edits since the baseline: only 895b454 (two Identity test files). The user approved it after Gate 2. No other test was edited, deleted, skipped or weakened.
No packages were added, so `dotnet list package --vulnerable` was not needed. No secrets were found in the diff.

**Earlier Fix first #1 (all-zeros default and hidden backfill): resolved.** I generated the SQL with `dotnet ef migrations script`. It now contains `SET NOT NULL`, the unique index and `CHECK (organization_id <> '0000…')`. It no longer contains an `UPDATE` or a `SET DEFAULT`. On a database with old rows, the migration now fails loudly. An empty organization is rejected even when EF Core (Entity Framework Core) sends `Guid.Empty`. Pinning `HasColumnName("organization_id")` is needed, because the SQLite test database does not use the snake_case naming convention. Npgsql (the PostgreSQL provider) already produces that name, so the snapshot is unchanged and `PendingMigrationTests` passes.

**Hand-edited migration: acceptable.** EF Core always adds `defaultValue: Guid.Empty` when a nullable column becomes non-nullable. Generating the migration again cannot remove it. Removing it by hand is the only way to get "no default, no backfill". The database holds no real data (CLAUDE.md), so the result is exactly what the plan intended. It still breaks the ef-migration skill's letter ("never hand-edit, except a `migrationBuilder.Sql` backfill"), so it needs a record (Watch out #1).

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | src/RestaurantManagement.Modules.Identity/Migrations/20261015233218_RequireOrganizationOnUserRoles.cs:18 | The `defaultValue` line was removed by hand, against the ef-migration skill. Running `migrations remove` and `add` again would quietly bring it back. | Note the hand edit and its reason in the B2 Delivered note. Optionally add a second exception to the skill: "drop the generated `defaultValue` when making a column non-nullable on a table with no data". |
| 2 | src/RestaurantManagement.Modules.Identity/Data/IdentityDbContext.cs:52-55 | No test covers the new check constraint. Scenario #13 only inserts an explicit NULL. | With the user's approval, have the tester add a Postgres scenario: "saving a role with `Guid.Empty` as the organization is rejected". |
| 3 | docs/plans/identity/2026-10-15-tighten-user-roles-and-permissions/batches/B2.md:32 | The Delivered note does not mention the check constraint, the pinned column name or fix commit aae30d3. | Add one sentence. |
| 4 | src/RestaurantManagement.Modules.Menu/Controllers/MenuController.cs:97,111 | The six roles are a string literal. They duplicate `MenuService.ReadRoles`, so renaming a role would not cause a compile error. The B4 guide asked for the `Roles` constants. | Use `Roles.Owner + "," + Roles.Admin + ...` (attributes accept this), or a shared `const string` in the Menu module. |
| 5 | src/RestaurantManagement.Modules.Menu/Services/MenuService.cs:223 | The static field `ReadRoles` is declared between methods. | Move it to the top with the other fields. |
| 6 | docs/plans/identity/2026-10-15-tighten-user-roles-and-permissions/plan.md:42-45 | The Checklist ticks have no commit hash. | Add the hashes: T1 92cc9da, T2 93bd816 + aae30d3, T3 28df430, T4 5f2cc0f. |
| 7 | docs/plans/identity/2026-10-15-tighten-user-roles-and-permissions/batches/B4.md (Files row) | `MenuServiceRoleAccessTests.cs` and `MenuRoleEndpointTests.cs` are new files, but no task's Files row lists them. | List them in the T4 Files row. |
| 8 | Commit 923cfc1 (body) | The body says `Scenarios: 1–40`, but the baseline has 45 scenarios. | For information only. No history rewrite is needed. |

## Check before merging
- **Migration `RequireOrganizationOnUserRoles` (Identity).** It has no default and no backfill, and it adds a check constraint. It fails on any database that still has role rows without an organization. Empty local databases before you apply it, as the spec says.
- **Fallback policy (Web/Auth/PermissionPolicyExtensions.cs).** Every endpoint now requires a signed-in user unless it has `[AllowAnonymous]`. Only the six auth actions have that attribute. Logout is anonymous by design, as the plan flags. Swagger is unaffected. Any future public endpoint must declare `[AllowAnonymous]` and needs a plan flag.
- **Permission handler (Web/Auth/PermissionAuthorizationHandler.cs).** It trusts every role claim in the token once an organization claim is present. That is safe only because `AccessTokenFactory` keeps just the roles for the selected organization. It also ignores `LocationId`. No endpoint uses the policies yet (see backlog/improvements/2026-10-15-apply-permission-policies-to-endpoints.md). Settle location scoping before endpoints start using them. Also, `IUserRoleLookup.GetRoleAsync` returns only one role per organization (see backlog/tech-debt/2026-10-15-user-role-lookup-single-role.md).

## Pull request description
**Tighten user roles and add permissions in code**

Replaces the broad "Staff" role with job-specific roles. Every role assignment now belongs to an organization. The change also adds permission-based access checks and makes every endpoint private by default.

**Shared**
- `Roles` is now Owner, Admin, Manager, Cashier, Waiter and Kitchen. Staff is removed.
- New `Permissions` list: members.manage, locations.manage, settings.manage, menu.manage, reports.view.
- New read-only `RolePermissions` map. Owner and Admin get all five permissions. Manager gets menu.manage and reports.view. Cashier, Waiter and Kitchen get none yet (Phase 1).

**Identity**
- `user_roles.organization_id` is NOT NULL, with no default. A check constraint rejects the all-zeros organization ID.
- New unique index on (user, organization, location, role) with `NULLS NOT DISTINCT`, so a duplicate "all locations" row is rejected.
- Removed the code that handled a missing organization.
- Marked the six auth actions (register, login, refresh, logout, verify, resend-verification) `[AllowAnonymous]`.

**Web**
- One authorization policy per permission. A policy needs a selected organization and a role that grants the permission. Otherwise the request gets 403.
- Fallback policy: an anonymous request gets 401 on any endpoint that does not declare access.

**Menu**
- All six roles may read their organization's menus (architect decision, 2026-10-15).
- Menu writes stay Owner and Admin only.

**Spec outcomes delivered:** #1 each role has exactly its permissions · #2 a missing permission gets 403 · #3 private by default (401) · #4 no duplicate "all locations" assignment · #5 no assignment without an organization.

**Risk: High.** Flags from plan.md:
- New Shared contract (`Roles` changed, `Permissions`, `RolePermissions`).
- Schema change on `user_roles` (NOT NULL, unique index, check constraint; no backfill).
- Auth change (fallback and permission policies; six public endpoints).
- Role change (Staff removed; menu reads opened to all six roles).

**Before applying:** empty any database that still holds old data (for example a local one), as the spec requires. Otherwise the migration fails.

**Test change after Gate 2:** 895b454 removed or updated two Identity tests that seeded a role without an organization. The user approved this, because the new rule makes that data impossible.

**Migration note:** the generated `defaultValue: Guid.Empty` was removed by hand from `RequireOrganizationOnUserRoles`. No data exists, so no default or backfill is wanted.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
