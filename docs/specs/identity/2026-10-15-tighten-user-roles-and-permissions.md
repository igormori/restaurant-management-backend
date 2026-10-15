# Spec: Tighten user roles and add permissions in code

**Type:** Feature   **Date:** 2026-10-15   **Requested by:** igormori
**Source:** Phase 0 — Task 5   **Size:** M   **Depends on:** —

## Business Goals
- Give each job in a restaurant only the access it needs: a waiter and a cashier must not have the same rights.
- Make sure every role assignment belongs to an organization, so no access exists "outside" a restaurant.
- Make every part of the system private by default: nobody uses it without signing in, unless a feature is deliberately public.

## Objective
Replace the broad "Staff" role with job-specific roles, require every role assignment to belong to an organization, and check access by named permissions.

## Current State
- Roles are Owner, Admin and Staff. "Staff" covers every employee, whatever their job.
- A role assignment can exist without an organization.
- An endpoint (a single action the system offers, such as "list menus") that does not declare who may use it is open by default.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | New roles: Owner, Admin, Manager, Cashier, Waiter, Kitchen. Every role assignment belongs to an organization. Access is checked by permission, and every endpoint requires a signed-in user unless marked public. |
| **How** should it behave? | See Rules. |
| **Who** is impacted? | All users who sign up from now on. No existing users or role assignments are affected, because none exist (see Decisions). |
| **Where** is the impact? | Identity module (roles and access); every endpoint (default sign-in requirement). |

### Rules
| # | Rule |
|---|---|
| 1 | Every role assignment belongs to exactly one organization. |
| 2 | A role assignment with no location means "all locations of the organization". |
| 3 | A user cannot hold the same role assignment twice for the same organization and location, including "all locations". |
| 4 | Permissions are fixed by the product (not created by users): members.manage, locations.manage, settings.manage, menu.manage, reports.view. |
| 5 | Owner and Admin have all permissions. Manager has menu.manage and reports.view. Cashier, Waiter and Kitchen get their permissions in Phase 1. |
| 6 | A user without the required permission is refused (403: forbidden). |
| 7 | A request without a signed-in user is refused (401: not signed in), unless the endpoint is explicitly public. |

### Decisions from the ticket
- Permissions live in code (a fixed list and a role-to-permission map), not in database tables.
- One access policy per permission; a default policy requires a signed-in user everywhere.
- The "no duplicate" rule must also treat "all locations" (empty) as a value, so two "all locations" rows for the same user and organization are blocked.
- No existing data to convert (igormori, 2026-10-15): the app is not live anywhere and the database was emptied on 2026-10-15, so there are no users, no role assignments without an organization and no "Staff" users to move. No data migration is needed.
- Any other environment holding old data (for example a teammate's local database) must be emptied before this change is applied.

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | Each role has exactly its permissions | Checking every role against the map shows exactly its listed permissions |
| 2 | Missing permission is refused | A Manager calling an action that needs members.manage gets 403 |
| 3 | Private by default | An anonymous request to an endpoint with no access declaration gets 401 |
| 4 | No duplicate "all locations" assignment | Inserting a second identical row for Org A with no location is rejected |
| 5 | No assignment without an organization | Saving a role row with no organization is rejected |

## Open Questions
None.

## Out of Scope
| Item | Backlog file |
|---|---|
| Permissions for Cashier, Waiter and Kitchen | Planned for Phase 1 (no backlog file) |
