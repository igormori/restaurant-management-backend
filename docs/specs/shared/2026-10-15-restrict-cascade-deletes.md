# Spec: Restrict Cascade Deletes

**Type:** Fix   **Date:** 2026-10-15   **Requested by:** Igor Mori

## Business Goals
- Business data is never lost silently; history is kept, in line with the standing rule "nothing in use is deleted".
- Removing something is always a deliberate step (close a location, deactivate a user), not a side effect.

## Objective
Stop the database from automatically deleting business data when its parent record is deleted, in the Organization and Identity modules. Keep automatic deletion only for data that is owned by one record and useless without it.

## Current State
Deleting an organization silently deletes its locations and its settings. Other links between records in these two modules have not been reviewed for the same risk. Locations can already be closed and users can already be deactivated, but nothing forces that path.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | Deleting an organization that still has locations or settings is refused instead of cascading. All other links in Organization and Identity are reviewed. |
| **How** should it behave? | See Rules below. |
| **Who** is impacted? | Owners and Admins (deactivation is the way to remove things); developers and operators who delete data directly. No new permissions. |
| **Where** is the impact? | Organization (organizations, locations, settings) and Identity (users, verification codes, and every other link reviewed). The change is enforced by the database itself, not only by the application. |

### Rules
| Parent | Child | Deleting the parent |
|---|---|---|
| Organization | Locations | Refused while locations exist |
| Organization | Organization settings | Refused while settings exist |
| User | Verification codes | Codes are deleted too (owned data, useless without the user) |
| Any other link in Organization or Identity | Any | Refused, unless the child is owned by one parent and useless without it; each exception must be listed in the change description |

- To remove a location, it is closed (Status = Closed); to remove a user, they are deactivated (IsActive = false). Both keep their history.
- Every link whose behaviour changes is listed in the change description.

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | An organization with locations cannot be deleted | Delete is refused by the database; the locations still exist |
| 2 | An organization with settings cannot be deleted | Delete is refused by the database; the settings still exist |
| 3 | Verification codes follow their user | Deleting a user also deletes that user's codes |
| 4 | Every other link in Organization and Identity has been reviewed | The change description lists each link changed and each one kept as cascade, with the reason |
| 5 | Existing data is untouched | No rows are added, changed or removed by the change |

## Out of Scope
| Item | Backlog file |
|---|---|
| Menu module: menus to location menus and menu categories still cascade; needs its own design | `/backlog/bugs/2026-10-15-menu-restrict-cascade-deletes.md` |

## Backlog Impact
| Backlog file | Result |
|---|---|
| None of the existing items are solved by this spec | Unchanged |
