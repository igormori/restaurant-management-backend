# Review: Restrict Cascade Deletes

**Verdict:** Fix first — the code change is correct and fully tested, but the branch also carries an unrelated rewrite of the agent workflow (`.claude/`, `CLAUDE.md`, `backlog/`). After merge, that rewrite points to files that are not committed.
**Build:** pass (0 errors, 9 existing warnings)   **Tests:** 285 passed, 0 failed

Reviewed against base `development` (6 commits, `0f41f33..00e5012`). Test files have not changed since the baseline commit `0f41f33`. No packages were added, so the vulnerable-package check was not needed.

## Fix first
| # | File:line | Problem | Change |
|---|---|---|---|
| 1 | commit `00e5012` (`.claude/agents/*.md`, `.claude/commands/*.md`, `CLAUDE.md`, `backlog/README.md`) | Out of scope. This commit is titled "tick overall definition of done", but it also deletes `implementer.md`, `pr-reviewer.md`, `tester.md`, `test-reviewer.md`, `plan-reviewer.md` and `commands/feature.md`, and it rewrites `CLAUDE.md` and four commands. No plan task lists any of these files. The baseline commit `0f41f33` also moves 7 backlog files and 2 agent files. | Take every `.claude/`, `CLAUDE.md` and `backlog/` move or README change off this branch (rebase or revert), and ship them as their own `chore(claude)` slice. Keep only `docs/plans/.../plan.md` in the "tick DoD" commit. |
| 2 | `CLAUDE.md:12-13`, `.claude/commands/implement.md:13`, `.claude/commands/test.md:6` | Once merged, these files reference skills (`architecture`, `clean-code`, `domain`, `testing`) and subagents (`implement/`, `test/`) that are untracked here, while the old agent files are deleted. On a fresh clone, `/implement` and `/test` have no implementer or tester to call. | If the tooling change stays (against Fix 1), commit the untracked `.claude/skills/{architecture,clean-code,domain,testing}/` and `.claude/agents/{implement,test}/` in the same commit. Otherwise drop it here, as Fix 1 says. |
| 3 | `.claude/settings.json:44` | The new PreToolUse hook (a command that runs before every Write or Edit) runs `.claude/hooks/enforce-structure.py`. That file is untracked, so on every other clone each Write or Edit runs a script that does not exist. | Commit `.claude/hooks/enforce-structure.py` with the settings change in the tooling slice, or remove the hook block from this branch. |
| 4 | `docs/specs/shared/2026-10-15-restrict-cascade-deletes.md:46` | The spec's Out of Scope item points to `backlog/bugs/2026-10-15-menu-restrict-cascade-deletes.md`, but that file is untracked. The Menu cascade risk would only be recorded on this laptop. The same applies to `backlog/tech-debt/2026-10-15-generic-postgres-test-fixture-name.md`. | Add both backlog files to the branch in a `docs(shared)` commit. |

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `src/RestaurantManagement.Modules.Organization/Migrations/20261015204401_RestrictOrganizationCascadeDeletes.cs:13-36` | Re-adding a foreign key makes PostgreSQL scan the whole child table, and it locks writes on `locations` and `organization_settings` while it runs. That is fine at today's size, but the cost grows with the data. | Run it in a low-traffic window. No code change needed now. |
| 2 | `src/RestaurantManagement.Modules.Organization/Data/OrganizationDbContext.cs:23-33` | EF Core (Entity Framework Core) now refuses a delete before it reaches the database: removing a tracked organization with loaded locations throws `InvalidOperationException`, not a database error. No delete path exists today. | If someone later adds an "delete organization" feature, they must handle both errors. That needs its own spec. |
| 3 | `src/RestaurantManagement.Modules.Identity/Data/IdentityDbContext.cs:34-38` | The Restrict rule has no "why" comment, but the Cascade rule below it has one. Readers may not see that Restrict was a deliberate choice. | Optional: add a one-line why comment, e.g. "role assignments are access history; deactivate the user instead". |
| 4 | untracked `settings.json` (repository root) | This file is not part of any task and not under `.claude/`. It may be a stray file. | Check it, then delete it or move it into the tooling slice. Do not merge it by accident. |

## Check before merging
- **The two migrations on populated tables** (Organization `RestrictOrganizationCascadeDeletes`, Identity `RestrictUserRoleCascadeDelete`). Both are schema-only and generated. Each drops and re-adds only the intended foreign keys, and scenarios 6 and 12 prove no rows change. `Down` restores Cascade.
- **`fk_user_roles_users_user_id` is now Restrict.** Any operator script or future code that hard-deletes users must remove their role rows first. `UserRoleAssigner` (removes single roles) is not affected, and all 86 Identity tests pass.
- **The tooling rewrite in `00e5012`** (Fix 1 to 3): it changes how every future agent run works, and it is incomplete without the untracked files.

## Pull request description

**fix: refuse deletes that would silently wipe tenant data (Organization, Identity)**

Until now, deleting an organization also deleted its locations and settings without warning, and deleting a user also deleted their role assignments. The database now refuses those deletes. Things are removed the deliberate way: close a location (Status = Closed) or deactivate a user (IsActive = false). Both keep their history.

**Organization**
- `locations.organization_id → organizations`: changed from Cascade to **Restrict**, because locations hold business data.
- `organization_settings.organization_id → organizations`: changed from Cascade to **Restrict**, because settings hold the plan limits.
- No link in this module is kept as Cascade.

**Identity**
- `user_roles.user_id → users`: changed from Cascade to **Restrict**, because role assignments are tenant access data and must not disappear silently.
- `user_verification_codes.user_id → users`: **kept as Cascade** (now set explicitly), because codes belong to one user and are useless without them.

**Spec Outcomes delivered:** #1 an organization with locations cannot be deleted · #2 an organization with settings cannot be deleted · #3 verification codes are deleted with their user · #4 every link reviewed, as listed above · #5 existing data untouched (schema-only migrations, proven on PostgreSQL with seeded rows).

**Risk:** High (from plan.md). Foreign key delete rules change on tables that already hold data. There is no backfill and no row is touched. No endpoint, role, auth or tenant query changes.

**Out of scope:** the Menu module still cascades (menus to location menus and menu categories). It is tracked in `backlog/bugs/2026-10-15-menu-restrict-cascade-deletes.md`.

**Tests:** 12 new PostgreSQL (Testcontainers) scenarios in `OrganizationDeleteRulesTests` and `IdentityDeleteRulesTests`. Full suite: 285 passed.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
