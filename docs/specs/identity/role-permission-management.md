# Role and permission management

## Problem
Today an organization member's authority is a single free-text `Role` string
(`Owner`/`Admin`/`Staff` by convention, not enforced) on one `UserRole` row per user per
organization. Owners cannot create roles for their own kitchen/floor structure (Chef,
Waiter, Cashier, Cooker) without a code change, and there is no way to grant or restrict
access at the level of an individual capability (e.g. "can view reports" vs. "can close
orders") or to a single Location. Owners need to define custom roles built from a fixed
catalog of permissions, assign more than one role to a user, and scope any role to a
specific Location or to the whole Organization.

## Scope
- Module(s) affected: Identity (owns Role, Permission, RolePermission, and the
  user-to-role assignment data — the existing `UserRole` entity is restructured to
  reference a Role instead of holding a free-text string).
- Consumers affected but not designed here: every module that currently asks "does this
  user hold Owner/Admin/Staff in this Organization" through the `IUserRoleLookup`
  contract in Shared (today: Organization, Menu) will need to ask "does this user hold
  permission X at Location/Organization Y" instead once this ships. Updating those call
  sites is out of scope for this document — see Open questions.
- Location and Organization identity are referenced by ID only (`OrganizationId`,
  `LocationId`); their existence/relationship is confirmed through the existing
  `IOrganizationLookup` contract, the same pattern already used by `UserRole`.
- Roles allowed to manage roles/assignments: any user holding the
  `identity.roles.manage` permission, except where a rule below narrows that further
  (system role, Owner assignment).

This feature is larger than the ~10-acceptance-criteria guideline for one spec once role
CRUD, permission assignment, user-role assignment guards, onboarding seeding, and legacy
migration are all included. This document defines the full data shape and business rules
(the parts every other piece depends on) and keeps acceptance criteria to the guard rules
that most directly protect data integrity. Recommend splitting the remaining surface into
follow-up specs before implementation: `manage-roles` (role CRUD + permission
assignment API), `assign-user-roles` (user-role assignment/removal API), `check-permission`
(the contract other modules call to authorize an action), and `migrate-legacy-roles`
(one-time migration of existing `UserRole.Role` string values). See Open questions.

## Behaviour
1. A Role is one of three kinds: System (exactly the Owner role; never editable or
   deletable, holds every permission everywhere), Default (Admin and Staff; created
   automatically for every newly registered Organization; editable and deletable
   afterward), or Custom (created by an Owner for their Organization; any name and any
   combination of permissions).
2. Permissions come from a fixed catalog defined in code, identified by a stable code
   (e.g. `menu.dishes.write`). Permissions cannot be created, edited, or deleted through
   any API; a role can only be given permissions that exist in the catalog.
3. A user can hold more than one role at the same time, including more than one role in
   the same Organization.
4. Each role a user holds is assigned either globally (applies at every Location in the
   Organization) or to one specific Location. A role scoped to Location A grants nothing
   at Location B.
5. When checking whether a user may perform an action at a given Location, the system
   merges the permissions of every role that user holds where the role's assignment is
   either global or matches that Location, and allows the action if the required
   permission is present anywhere in that merged set.
6. The system (Owner) role cannot be edited or deleted, and its permission set cannot be
   changed, regardless of who is asking.
7. Default roles (Admin, Staff) can be edited (renamed, description changed, permissions
   changed) or deleted by anyone holding `identity.roles.manage`, same as a custom role,
   once they exist.
8. A custom or default role belongs to exactly one Organization and is never visible or
   assignable outside it.
9. Assigning or removing the Owner role for a user requires the acting user to already
   hold the Owner role themselves; holding `identity.roles.manage` alone is not enough.
10. An Organization must always retain at least one user with an active Owner-role
    assignment; an action that would remove the last one is rejected.
11. A user who already belongs to an Organization (holds at least one role in it) must
    always keep at least one role in that Organization; removing their last role there is
    rejected. This does not apply to a user who has not yet joined any Organization (see
    Open questions on the verified-but-org-less state).
12. When a new Organization is registered, its Admin and Staff default roles are created
    for it automatically, with the permission sets described under Data, before the
    registration is considered complete.

## Data
Role
- Name — string, required, 1–50 characters after trimming.
- Description — string, optional.
- Kind — System, Default, or Custom (exactly one System role exists overall: Owner).
- OrganizationId — required for Default and Custom roles; not applicable to the System
  role, which is not owned by any single Organization (see Open questions — this is the
  point in the original request most in tension with "at least one Owner per
  Organization").
- Active/inactive state, creation and last-updated timestamps.

Permission (fixed catalog, not user-editable)
- Code — stable identifier, format `module.resource.action`.
- Description — human-readable explanation, for display when building a role.
- Module — grouping label for display only (e.g. `menu`, `orders`, `identity`).

Initial catalog:
| Code | Module |
|---|---|
| inventory.ingredients.read | inventory |
| inventory.ingredients.write | inventory |
| inventory.ingredients.delete | inventory |
| inventory.stock.read | inventory |
| inventory.stock.write | inventory |
| menu.dishes.read | menu |
| menu.dishes.write | menu |
| menu.dishes.delete | menu |
| menu.menus.read | menu |
| menu.menus.write | menu |
| orders.read | orders |
| orders.create | orders |
| orders.close | orders |
| reports.view | reports |
| reports.export | reports |
| organization.settings.manage | organization |
| organization.locations.manage | organization |
| identity.users.manage | identity |
| identity.roles.manage | identity |

Note: `inventory.*` and `orders.*` codes reference modules that do not exist in `src/`
yet (see Open questions); they can be catalogued now but cannot be enforced by any
consumer until those modules exist.

Default role permission sets:
- Admin: every catalog permission except `organization.settings.manage`.
- Staff: `menu.dishes.read`, `menu.menus.read`, `orders.read` only.

Role-to-permission association: each role has a set of permission codes drawn only from
the catalog above; assigning a code not in the catalog is rejected.

User-role assignment: for each (user, role) pairing — Location (optional; absent means
the role applies to every Location in the role's Organization), who assigned it, and
when.

## Acceptance criteria
- Creating a role with an empty, whitespace-only, or >50-character name is rejected.
- Assigning a permission code that is not in the catalog to a role is rejected.
- A user without `identity.roles.manage` cannot create, edit, or delete a role, or
  assign/remove a role for another user.
- Editing or deleting the system Owner role is rejected for every caller, including one
  who holds `identity.roles.manage`.
- Assigning or removing the Owner role by a caller who does not themselves hold the
  Owner role is rejected, even if they hold `identity.roles.manage`.
- Removing the Owner-role assignment that is the last remaining Owner for an Organization
  is rejected.
- Removing a user's last remaining role assignment within an Organization they already
  belong to is rejected.
- A custom or default role created for one Organization cannot be assigned to, viewed by,
  or edited by a caller from a different Organization.
- A user holding two different roles (e.g. Chef with `inventory.ingredients.read` and
  Waiter with `orders.create`) is authorized for both permissions.
- A role assignment scoped to Location A does not authorize the equivalent action for
  the same user at Location B.
- A role assignment with no Location authorizes its permissions at every Location in the
  Organization.
- Registering a new Organization creates its Admin and Staff default roles automatically,
  with the permission sets defined under Data, before registration completes.

## Out of scope
- The API surface for role CRUD, role-permission editing, user-role assignment, and the
  permission catalog listing (routes, request/response shapes) — left to follow-up specs
  per the note under Scope.
- The exact contract other modules call to ask "is this user authorized," and updating
  Organization's and Menu's existing Owner/Admin/Staff string checks to use it — a
  separate spec, since it changes behavior in modules other than Identity.
- One-time migration of existing `UserRole.Role` string values into the new model — a
  separate spec (see Open questions; the source data shape differs from what the
  originating request assumed).
- Audit logging of role or permission changes.
- Time-limited or temporary role assignments.
- Permission inheritance between roles (e.g. a role "extending" another).
- Row-level security beyond Location scoping (e.g. per-table-row ownership).
- Any UI for managing roles or permissions.
- Adding the `Modules.Inventory` or `Modules.Orders` modules referenced by the permission
  catalog — they do not exist yet.

## Open questions
- **System Owner role's Organization scope is self-contradictory as specified.** The
  request states the system role's `OrganizationId` is null ("null for system roles
  only"), implying one shared Owner role reused across every Organization, yet also
  requires "at least one Owner must always exist per Organization" and that Owner
  assignment/removal be restricted per Organization. This spec assumes the Owner *role
  definition* is a single global row (or equivalent) and that per-Organization ownership
  lives entirely in the user-role assignment's `OrganizationId`, never on the Role row
  itself. Needs confirmation before data modeling proceeds, since it changes how "does
  this Organization have an Owner" is queried.
- **Conflict with the existing verified-but-org-less user state.** Per this module's own
  notes, a user has no role and no Organization between email verification and creating
  or joining their first Organization — that is normal, not an error. The request's rule
  "a user must always have at least one role — removing the last role is not allowed"
  would forbid that state if read literally. This spec assumes the rule means "at least
  one role per Organization the user already belongs to," not "at least one role
  globally at all times," but this should be confirmed.
- **The existing `UserRole.Role` field is already a free-text string, not a fixed
  enum.** `Roles.Owner/Admin/Staff` in Shared are plain constants used by convention;
  there is no database-level enum. The "migration" is therefore restructuring `UserRole`
  from a string `Role` column to a `RoleId` reference plus introducing `Role`,
  `Permission`, and `RolePermission`, not converting a real enum type. Flagging in case
  the migration spec assumed otherwise.
- **This is not an Identity-only change in practice.** `IUserRoleLookup.GetRoleAsync`
  (a single string) is called today by both the Organization module (owner/admin checks
  on organization edit) and the Menu module (dish/menu category authorization). Moving to
  multi-role, permission-based, Location-scoped authorization means those call sites stop
  getting an answer that matches the new model unless the Shared contract itself changes
  shape and both modules are updated to call it differently. The request frames this as
  "extend Modules.Identity," but a faithful implementation touches at least Organization
  and Menu's authorization checks too. Needs a decision: ship a new contract alongside the
  old one during a transition period, or update all consumers in the same effort.
- **How Identity learns an Organization was registered, without referencing the
  Organization module.** Today the Organization module calls into Identity's
  `IUserRoleAssigner` (a Shared contract Identity implements) right after creating an
  Organization, to assign the Owner role. Default-role seeding (Admin/Staff created "when
  a new Organization is registered") would need to follow the same direction: the
  Organization module calls a Shared contract that Identity implements, not the reverse.
  This spec assumes that pattern continues but does not decide whether seeding happens
  through an extended `IUserRoleAssigner` or a new contract method — that's a planning
  decision.
- **`inventory.*` and `orders.*` permission codes reference modules that do not exist in
  `src/` yet** (`docs/specs/inventory/create-ingredient.md` exists as a spec, but
  `Modules.Inventory` and `Modules.Orders` have not been built). This spec catalogs those
  codes as data but they are unenforceable until those modules exist. Confirm whether the
  catalog should ship now (inert) or be added incrementally as each module is built.
- Whether "editable and deletable" for Default roles includes deleting the last Admin
  role outright even while active Admin-role assignments exist for users — the request
  says Default roles are deletable by Owner but does not say what happens to existing
  assignments of a deleted role. Assumed out of scope for this document; belongs in the
  `manage-roles` follow-up spec.
