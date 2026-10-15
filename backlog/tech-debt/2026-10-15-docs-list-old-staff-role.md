# Project docs still list the old Staff role

**Found during:** docs/plans/identity/2026-10-15-tighten-user-roles-and-permissions/plan.md
**Date:** 2026-10-15

## Description
The root `CLAUDE.md` ("Roles: Owner, Admin, Staff") and `.claude/skills/architecture/SKILL.md` section 3 still name Staff as a role. After the roles-and-permissions change, the roles are Owner, Admin, Manager, Cashier, Waiter, Kitchen, and access is checked by permission. The domain skill may need the same update.

## Why it matters
Agents read these files before planning and coding; an outdated role list leads them to write new Staff checks for a role that no longer exists.
