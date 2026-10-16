# Review: Docs list the current six roles instead of Staff

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 800 passed, 0 failed

Small change (from `/small`): no spec or plan folder, so it was checked against the skills only.
Diff: `development...fix/docs-list-current-roles`, one commit `5ae98bd`, docs only (no code,
tests, migrations or packages changed). No vulnerable-package check was needed.

Domain skill section 4 checked against `src/RestaurantManagement.Shared/Roles.cs`,
`Shared/Auth/RolePermissions.cs` and `Shared/Auth/Permissions.cs`:
- Six fixed roles defined as constants in code, so "organizations cannot create, edit or delete roles": **accurate**.
- "Admin: all permissions, like the Owner": **accurate** (`Permissions.All` for both).
- "Manager: menu management and reports": **accurate** for the map (`menu.manage`, `reports.view`).
- "Cashier, Waiter and Kitchen: no management permissions; may read menus": **accurate**
  (empty permission set; `MenuService.cs:236` `ReadRoles` and `MenuController.cs:97,111` allow them to read).

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `.claude/skills/domain/SKILL.md:53` | "Owner: the only system role" was written to contrast with editable Admin/Staff. Now every role is fixed, so "only system role" reads as if the other five could be changed. | Reword to "Owner: can do everything at every location..." and drop "the only system role" and "Cannot be edited or deleted" (the next bullet already covers it). |
| 2 | `.claude/skills/domain/SKILL.md:58` | "Cashier, Waiter and Kitchen: no management permissions" reads like a permanent rule. The roles spec (`docs/specs/identity/2026-10-15-tighten-user-roles-and-permissions.md`, Rule 5 and Out of Scope) says their permissions come in Phase 1. | Add "their own permissions come in Phase 1 **[confirm]**". |
| 3 | `.claude/skills/domain/SKILL.md:56` | "organizations cannot create, edit or delete roles" is true today, but `backlog/features/2026-10-15-custom-roles.md` proposes exactly that. The spec writer may read it as a settled "no". | Append "(custom roles: see backlog, not decided)" or mark **[confirm]**. |
| 4 | `CLAUDE.md:4` | "(access is checked by permission)" overstates today's code. Menu, location and organization writes still check role names (`MenuController.cs:27`, `MenuService.cs:230`, `LocationService.cs:48`), so a Manager cannot manage menus yet. This is already tracked in `backlog/improvements/2026-10-15-apply-permission-policies-to-endpoints.md`. The line is also about 140 characters long, while the rest of the file wraps at about 100. | Rewrap the line. Optionally write "access is moving to permission checks" until that backlog item ships. |
| 5 | `.claude/skills/security/SKILL.md:21,68` | Not in this diff. It still tells agents to declare `[Authorize(Roles = ...)]` on every action, which contradicts architecture skill section 3 ("never by checking a role name"). It is the same risk the deleted backlog item described: agents writing role checks. | Write a new backlog item (tech-debt, low) or extend the apply-permission-policies item. Do not widen this change. |
| 6 | `backlog/tech-debt/2026-10-15-docs-list-old-staff-role.md` | Deleted in the commit. The backlog skill section 5 says only the user deletes items. Deleting in the branch so the delete lands with the merge matches "deleted when that work is merged". | Confirm you want it deleted. The item is fully solved: no "Staff" role remains in `CLAUDE.md` or `.claude/skills/`. The `Staff` row at `domain/SKILL.md:45` is the planned employees module, not a role. |

## Check before merging
1. Domain skill section 4 (`.claude/skills/domain/SKILL.md:53-59`): the spec writer treats this
   as business rules, so findings 1-3 decide whether future specs treat custom roles and
   Phase 1 permissions as closed.
2. `CLAUDE.md:4`: every agent reads it. "Checked by permission" is not yet true for menu,
   location and organization writes (finding 4).

## Pull request description
**docs(claude): list the current six roles instead of Staff**

The root `CLAUDE.md`, `README.md` and the domain skill still named the old Staff role. Agents
read these files before planning and coding, so the old list could lead them to write checks for
a role that no longer exists.

What changed:
- `CLAUDE.md`: roles are now Owner, Admin, Manager, Cashier, Waiter, Kitchen; access is checked by permission.
- `README.md`: the same role list in the feature list.
- `.claude/skills/domain/SKILL.md` section 4: replaces "Admin and Staff, editable defaults" and "Custom roles"
  with the five fixed roles and what each one may do, matching `RolePermissions`.
- Removes the backlog item `backlog/tech-debt/2026-10-15-docs-list-old-staff-role.md`, solved by this change.

Risk: low. Docs only. No code, tests, migrations, packages or endpoints changed. Build passes; 800 tests pass.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
