# Custom roles per organization

**Status:** proposed
**Severity:** medium
**Origin:** docs/specs/shared/2026-10-19-per-location-access-grid.md · Out of Scope (later slice)
**Date:** 2026-10-15

## Problem
Roles are stored per organization with default levels (per-location access grid spec), but nobody can create, edit or rename them. The Owner or Admins should be able to create custom roles (e.g. "Marketing") and edit or rename the existing ones.

## Decisions (2026-10-19)
- Roles live in the database per organization; each new organization gets Admin, Manager, Cashier, Waiter, Kitchen. The Owner is not an editable role.
- A role is a preset of default levels (location x module). Editing a role's levels never changes existing members' grids; renaming updates every member's label (stored role names per member and location).
- Assumption (not decided): an Admin with Members = Manage may create roles, capped at their own levels, like the delegation rule for grids.

## Evidence
- `docs/specs/shared/2026-10-19-per-location-access-grid.md` — Out of Scope and Future State (Roles)
- Original finding: docs/plans/identity/2026-10-15-tighten-user-roles-and-permissions/plan.md

## Acceptance criteria
- [ ] The Owner can create, edit and rename roles of their organization
- [ ] An Admin can do the same within the delegation limits decided in its spec
- [ ] Editing a role's levels leaves existing members' grids unchanged; renaming updates their labels
- [ ] Another organization's roles are never visible or editable

## Related
backlog/features/2026-10-19-invite-member-and-edit-access-grid.md
