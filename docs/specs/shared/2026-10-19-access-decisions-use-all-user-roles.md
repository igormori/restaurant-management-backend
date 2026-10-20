# Spec: Access decisions use all of a user's roles

**Type:** Fix   **Date:** 2026-10-19   **Requested by:** igormori (backlog item `2026-10-15-user-role-lookup-single-role`)

## Business Goals
- A user is allowed or refused the same way every time, whatever order the database returns their roles in.
- Permissions add up across roles, as the business rule says, so a Manager who is also a Waiter at one location keeps every Manager permission.

## Objective
Every access check on menus, locations and organizations considers all roles the user holds in the organization, not one arbitrary role.

## Current State
- A user can hold several roles in one organization (for example Manager for all locations and Waiter for one location).
- The shared check that tells the Menu and Organization modules "what role does this user have here" returns only the first role it finds.
- Which role is "first" depends on database row order, so the same user can be allowed on one request and refused on the next.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | The shared role check returns every role the user holds in the organization, and the menu, menu category, location and organization access decisions use all of them. |
| **How** should it behave? | See Rules. |
| **Who** is impacted? | Users with two or more roles in one organization. Users with one role see no change. |
| **Where** is the impact? | Identity (provides the roles), Menu (menus, menu categories), Organization (organizations, locations). |

### Rules
| # | Rule |
|---|---|
| 1 | An action at a location is allowed when at least one of the user's roles that applies there grants it (permissions add up). |
| 1a | A role for one location counts only at that location; a role for all locations counts at every location. |
| 2 | Roles held in a different organization never count. |
| 3 | A user with no role in the organization is refused, as today. |
| 4 | The result does not depend on the order of the user's roles. |
| 5 | Actions that belong to the organization as a whole (not to one location, e.g. menus today) count only roles assigned for all locations. Roles assigned to specific locations never count for them, even when the user holds that role at every current location as separate assignments. |
| 6 | An all-locations assignment also covers locations added later; a specific-location assignment does not. |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | Manager for all locations plus Waiter for one location can manage menus at every location | Allowed at every location, whichever role was stored first |
| 2 | Waiter plus Cashier (no management permission) cannot manage menus | Refused, whichever role was stored first |
| 3 | Admin or Owner combined with any other role keeps full access | Allowed in every ordering |
| 4 | A role held only in another organization grants nothing here | Refused |
| 5 | Same decision for every ordering of the same roles | Repeating the check with roles stored in each order gives one result |
| 6 | Users with a single role behave as before | Existing allow and refuse results unchanged |
| 7 | Admin for all locations plus Waiter at location A | Admin permissions at A, B and C; Waiter adds nothing |
| 8 | Waiter for all locations plus Manager at location A only | Manager permissions at A only, not at B or C |
| 9 | Waiter for all locations plus Manager at A only | Refused for organization-wide actions such as managing menus |
| 10 | Manager at A, B and C as separate assignments | Refused for organization-wide actions |
| 11 | Admin for all locations | Allowed for organization-wide actions and at a newly added location |
| 12 | Applies to menu, menu category, location and organization actions | Outcomes 1 and 2 hold for each of these areas |

## Out of Scope
| Item | Backlog file |
|---|---|
| Narrowing menu links and categories by selected location | `/backlog/tech-debt/2026-10-15-no-location-owned-entities.md` (already listed) |

## Backlog Impact
| Backlog file | Result |
|---|---|
| `/backlog/tech-debt/2026-10-15-user-role-lookup-single-role.md` | Removed (fully solved) |
