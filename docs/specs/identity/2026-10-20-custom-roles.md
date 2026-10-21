# Spec: Custom roles per organization

**Type:** Feature   **Date:** 2026-10-20   **Requested by:** Igor Mori

Builds on the roles, grid and delegation rules in `docs/specs/shared/2026-10-19-per-location-access-grid.md` and `docs/specs/shared/2026-10-20-invite-member-and-edit-access-grid.md`.

## Business Goals
- Let each organization shape roles to its own staff (for example "Marketing"), not only the five standard ones.
- Let owners delegate this safely, without anyone creating a role stronger than their own access.

## Objective
The Owner, and Admins allowed to, can create roles, edit a role's default levels and rename roles of their own organization, with every change audited.

## Current State
- Each organization has Admin, Manager, Cashier, Waiter and Kitchen, each with default levels (module -> None, Read or Manage).
- Nobody can create, edit or rename a role. The five roles are fixed in practice.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | A role can be created, have its default levels edited, and be renamed. |
| **How** should it behave? | See the Rules tables below. |
| **Who** is impacted? | Owner, and Admins with Members = Manage (they manage roles); members who hold a renamed role. |
| **Where** is the impact? | Identity (roles, member role labels), Shared (audit log). |

**Roles**
| Rule | Detail |
|---|---|
| Content | A role has a name and one default level per module (Locations, Menus, Cashier, Orders, Kitchen, Reports, Members). It has no locations: it only fills a member's grid |
| Name | Unique within the organization, ignoring upper or lower case. Cannot be empty or the name "Owner" |
| Standard roles | Admin, Manager, Cashier, Waiter and Kitchen can be edited and renamed like any other role |
| Owner | Not a role. It cannot be created, edited or renamed through this feature |
| Edit levels | Changes the role's defaults only. Existing members' grids stay exactly as they are; the new defaults apply to later invitations and grid edits |
| Rename | Every member's stored role label for that role shows the new name at once. No member's access changes |
| Visibility | A member sees only the roles of their own organization |

**Who may manage roles**
| Rule | Detail |
|---|---|
| Who | The Owner, or an Admin with Members = Manage |
| Cap | An Admin cannot set a module's default level above their own level for that module at every non-closed location. A role already above that cap cannot be edited or renamed by them |
| Tenancy | Another organization's role is reported as not found |

**Audit**
| Action | Recorded |
|---|---|
| Create, edit levels, rename | Who, when, and the values before and after |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | Owner creates a role | The Owner creates "Marketing" with chosen levels; it appears in the organization's role list and can be chosen when inviting or editing a member |
| 2 | Names are unique | Creating or renaming to an existing name (any letter case), an empty name or "Owner" is refused and nothing is saved |
| 3 | Editing levels leaves grids alone | After a role's levels change, every existing member's grid is identical to before; a new invitation using the role gets the new levels |
| 4 | Rename updates labels | After renaming Waiter to Server, members who held Waiter show Server at each location, and their access is unchanged |
| 5 | Admin works within the cap | An Admin with Members = Manage creates a role at or below their own levels; a role with a level above their own is refused and nothing is saved |
| 6 | Capped Admin cannot touch stronger roles | An Admin cannot edit or rename a role holding a level above their own |
| 7 | Members level required | A member without Members = Manage is refused every role change |
| 8 | Owner is not editable as a role | No request can create, edit or rename an Owner role |
| 9 | Tenant isolation holds | Reading or changing another organization's role returns not found; role lists never include other organizations' roles |
| 10 | Changes are audited | Create, level edit and rename each appear in the audit log with who, when, before and after |

## Out of Scope
| Item | Backlog file |
|---|---|
| Deleting or retiring a role | `/backlog/features/2026-10-20-delete-or-retire-a-role.md` |
| Making someone an Owner; protecting the last Owner | `/backlog/features/2026-10-16-member-role-change.md` |

## Backlog Impact
| Backlog file | Result |
|---|---|
| `/backlog/features/2026-10-15-custom-roles.md` | Removed (fully solved) |

## Questions and Assumptions
**[Decided]**
- Who may manage roles: the Owner and Admins with Members = Manage, capped at their own levels (answered by the requester).

**[Assumed]**
- Role names are unique per organization, case-insensitive: two roles with one name would confuse labels and invitations.
- The cap compares each module with the Admin's level at every non-closed location, matching the rule for actions not tied to one location.
- Deleting a role is not requested, so it is out of scope: roles in use cannot be deleted anyway.
