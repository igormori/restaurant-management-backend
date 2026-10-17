# Spec: Sign-in token carries roles only for the organization in use

**Type:** Fix   **Date:** 2026-10-16   **Requested by:** igormori

## Business Goals
- A restaurant's access is never granted by a role the user holds in a different restaurant.
- Keep organizations fully separated, even for people who work for more than one.

## Objective
When a user has not yet chosen an organization, the access token (the signed proof of identity the app sends with every request) carries no roles, so a role held in one organization cannot count as proof of access in another.

## Current State
- A user with roles in several organizations (for example Owner of one restaurant and Waiter in another) who has not chosen one gets a token listing the roles of all their organizations, tied to none of them.
- Such a token passes any check that looks only at the role name, such as "Owner or Admin", for either restaurant.
- Today the services re-check the role per organization and the permission checks refuse tokens with no organization, so no leak is known. The risk is any check that trusts the role alone.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | A token issued without a chosen organization carries no roles unless it is tied to exactly one organization. |
| **How** should it behave? | See Rules. |
| **Who** is impacted? | Users with roles in two or more organizations, between signing in and choosing an organization. Single-organization users see no change. |
| **Where** is the impact? | Identity module: sign-in, token refresh and organization choice. |

### Rules
| # | Rule |
|---|---|
| 1 | A token tied to an organization carries only the roles the user holds in that organization (unchanged). |
| 2 | A user whose roles all belong to one organization gets a token tied to it, with those roles (unchanged). |
| 3 | A user with roles in two or more organizations who has not chosen one gets a token with no organization and no roles. |
| 4 | The list of organizations to choose from is still returned at sign-in, so the user can choose and then receive a token with that organization's roles. |
| 5 | A user with no roles gets a token with no organization and no roles (unchanged). |

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | No cross-organization roles in the token | Owner of Org A and Waiter of Org B signs in: the token has no role and no organization |
| 2 | Role-only checks refuse that token | The same token is refused on an action that requires Owner or Admin |
| 3 | Choosing an organization restores its roles | After choosing Org B, the token has only Waiter and Org B; after choosing Org A, only Owner and Org A |
| 4 | Single-organization users unaffected | A user with roles only in Org A gets a token with Org A and those roles |
| 5 | Token refresh follows the same rules | Refreshing a token with no chosen organization for a two-organization user gives no roles |

## Out of Scope
| Item | Backlog file |
|---|---|
| None found | n/a |

## Backlog Impact
| Backlog file | Result |
|---|---|
| `/backlog/bugs/2026-10-15-token-roles-from-all-organizations.md` | Removed (fully solved) |
