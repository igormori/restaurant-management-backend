# Spec: Tenant query filters and location access check

**Type:** Feature   **Date:** 2026-10-15   **Requested by:** igormori
**Source:** Phase 0 — Task 6   **Size:** L   **Depends on:** Task 1 (base entity), Task 5 (user roles)

## Business Goals
- One restaurant group must never see or change another's data, even if a developer forgets a check.
- Staff assigned to one location (unit) only see that location's data when working there.

## Objective
Make organization separation automatic for every read, and check that a user may work in the location they select.

## Current State
- Each query must remember to filter by organization by hand. One forgotten filter exposes another organization's data.
- There is no check that a user may act in a given location.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | Every read of organization data is automatically limited to the signed-in user's organization, and to the selected location when one is chosen. Selecting a location is checked against the user's role assignments. |
| **How** should it behave? | See Rules. |
| **Who** is impacted? | All signed-in users; developers (separation no longer depends on remembering a filter). |
| **Where** is the impact? | Shared module and every module's data access; every request that selects a location. |

### Rules
| # | Rule |
|---|---|
| 1 | Every read of organization-owned data returns only the current user's organization. |
| 2 | When a request selects a location, location-owned data is limited to that location. |
| 3 | The organization always comes from the user's sign-in token, never from the request body or URL (web address). |
| 4 | A user may select a location only if they have a role assignment for that location, or for all locations of the organization. Otherwise the request is refused (403). |
| 5 | A record from another organization is answered as "not found" (404), never "forbidden", so its existence is not revealed. |
| 6 | Saving organization-owned data without a signed-in user fails. |
| 7 | Turning the automatic filter off is allowed only in places listed in the project rules (CLAUDE.md). |

### Decisions from the ticket
- The selected location is sent in a request header named X-Location-Id.
- The rule listing where the filter may be turned off is added to CLAUDE.md.

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | Lists show only own organization | With menus in Org A and Org B, a user in Org A sees only Org A's menus |
| 2 | Other organization's record is hidden | A user in Org A requesting an Org B menu by ID gets 404 |
| 3 | Unassigned location refused | A user assigned only to Batel selecting Mercado Municipal gets 403 |
| 4 | "All locations" works | A user with an all-locations role can select any location of their organization |
| 5 | Foreign location refused | Selecting a location from another organization gets 403 |
| 6 | No anonymous saves | Saving organization data with no signed-in user fails |

## Out of Scope
| Item | Backlog file |
|---|---|
| None | — |