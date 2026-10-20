# Spec: Per-location access grid

**Type:** Feature   **Date:** 2026-10-19   **Requested by:** Igor Mori

Slice 1 of 3. Slice 2 (invite a member by email, set and edit their grid) and slice 3 (filter lists by location access) are separate specs.

## Business Goals
- Let an organization decide, per person, what they may see and do at each location.
- Replace the fixed role checks with one rule that is the same in every module.
- Stop refusing staff who hold a role at only one location.

## Objective
Every member of an organization gets an access grid (location x module -> level) that is stored and used to allow or refuse each action, instantly.

## Current State
- Access uses fixed role lists in each service. Only an Owner or Admin held for all locations can change menus and categories; the Manager's menu-management permission is never used.
- Menu reads need a role held for all locations, so staff with a one-location role are refused (403, "forbidden").
- Every member sees every location. Categories are visible only to Owner and Admin.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | Each member has a stored grid. Every action is allowed or refused from that grid. It replaces role-based checks for locations, menus, categories and organization settings. |
| **How** should it behave? | See the Rules tables below. |
| **Who** is impacted? | Owner, Admin, Manager, Cashier, Waiter, Kitchen. |
| **Where** is the impact? | Identity (access decisions), Organization (locations, settings), Menu (menus, categories). The other modules in the grid are not built yet and will follow the same rule. |

**Grid**
| Item | Rule |
|---|---|
| Modules | Locations, Menus (includes categories, later products), Cashier, Orders, Kitchen, Reports, Members |
| Levels | None (no access), Read (see), Manage (create, update, delete; includes Read) |
| Check | An action at a location needs the module's level at that location: Read to read, Manage to create, update or delete |
| Freshness | Read from the stored grid on every request, so a change applies at once. The login token holds only the user and organization, not roles |

**Owner and roles**
| Item | Rule |
|---|---|
| Owner | Manage on everything at every location, cannot be restricted. Only the Owner creates and deletes locations and edits organization settings and billing |
| Roles | Roles are stored per organization, not fixed in the product. Each new organization gets Admin, Manager, Cashier, Waiter and Kitchen with the default levels below. The Owner is not a role: not editable, always full access |
| Presets | Choosing a role fills the grid with its default levels; any cell can then be changed |
| Several presets | The higher level per cell wins |
| Role vs. levels | A role holds default levels only. Actual levels are per member and location: Maria can be Admin at A with Manage and Admin at B with Read (label "Admin" at both) |
| Stored grid | The final grid is stored. Editing a role's default levels later does not change existing members' grids |
| Role labels | For each member and location, the role names given are stored with the grid (e.g. Maria at A: Waiter, Cashier), for display and filtering only. Access is decided by the grid alone. Renaming a role updates every member's label |
| New location | Only the Owner has access until access is granted to other members. There is no "all locations" grid |
| New Admin | Has no location access until the Owner grants it. Example: Manage on everything at A and B, Read at C, Cashier = None |
| Example | Waiter + Cashier at A, Cashier lowered to Read, Reports set to None |

**Default levels per role (cells not listed are None)**
| Role | Locations | Menus | Cashier | Orders | Kitchen | Reports | Members |
|---|---|---|---|---|---|---|---|
| Admin | Manage | Manage | Manage | Manage | Manage | Manage | Manage |
| Manager | Read | Manage | - | Read | Read | Manage | - |
| Cashier | Read | Read | Manage | Read | - | - | - |
| Waiter | Read | Read | - | Manage | - | - | - |
| Kitchen | Read | Read | - | Read | Manage | - | - |

**Delegation (applies in slice 2)**
| Rule | Detail |
|---|---|
| Who | An Admin with Members = Manage may add other Admins and set their grids |
| Limits | Cannot change their own grid; can grant at most their own level at each location; can never edit the Owner |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | One-location staff can read that location's menus | A Waiter with Menus = Read at A reads A's menus; no 403 |
| 2 | No access elsewhere | The same Waiter is refused at location B |
| 3 | Manage allows writes, Read does not | Manager with Menus = Manage edits a menu; a Cashier with Read is refused the same edit |
| 4 | Categories follow the grid | A member with Menus = Read sees categories; one with None does not |
| 5 | Owner is unrestricted and unrestrictable | Owner passes every check; no grid change can lower the Owner |
| 6 | Only the Owner creates or deletes locations and edits organization settings | Admin with Locations = Manage updates a location but is refused create, delete and settings edits |
| 7 | New Admin starts with no access | A new Admin is refused everywhere until granted |
| 8 | Several presets merge to the higher level | Waiter + Cashier cell values equal the higher of each |
| 9 | Stored grid is stable | Editing a role's default levels leaves existing grids unchanged; renaming a role updates members' labels only |
| 9a | New organization gets default roles | A new organization has the five roles with the table's levels |
| 9b | New location is Owner-only | A new location is refused to every non-Owner until granted |
| 9c | Same role, different levels per location | Maria (Admin) manages menus at A; at B she reads menus but is refused edits; her label is Admin at both |
| 10 | Changes apply at once | After a level is lowered, the member's next request is refused without a new login |
| 11 | Token carries no roles | The token holds only user and organization |
| 12 | Tenant isolation holds | A member never reaches another organization's data, whatever the grid |
| 13 | Grid changes are audited | Each grid change records who, when, before and after |

## Out of Scope
| Item | Backlog file |
|---|---|
| Invite a member by email; set and edit their grid; delegation enforced; member role change | `/backlog/features/2026-10-19-invite-member-and-edit-access-grid.md` |
| Create, edit and rename roles (e.g. "Marketing") by the Owner or Admins | `/backlog/features/2026-10-15-custom-roles.md` |
| Filter lists by the locations the member can access | `/backlog/features/2026-10-19-filter-lists-by-location-access.md` |

## Backlog Impact
| Backlog file | Result |
|---|---|
| `/backlog/features/2026-10-15-custom-roles.md` | Updated (stored roles and default levels done here; creating and editing roles stays) |
| `/backlog/improvements/2026-10-15-apply-permission-policies-to-endpoints.md` | Removed (fully solved for existing endpoints) |
| `/backlog/tech-debt/2026-10-15-no-location-owned-entities.md` | Already merged into the slice 3 item; not touched here |
| `/backlog/features/2026-10-16-member-role-change.md` | Updated (rules decided here; the change feature stays in slice 2) |
