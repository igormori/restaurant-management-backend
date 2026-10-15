# Select organization

## Summary
A user who belongs to several organizations has no organization in their token after login. They need to pick one and receive a token scoped to it, so tenant saves work for them.

## Rules
- Roles: any authenticated, active, verified user (Owner, Admin, Staff) may select among their own organizations.
- Tenancy: a user can select only an organization where they hold a role; any other is rejected.
- Rejection for a non-member and for a non-existent organization is identical, so existence is not revealed.
- Login for a user with several organizations returns a token without the organization claim plus the list of choices.
- Selecting returns a new token carrying that organization's claim and a new refresh token.
- Refreshing a token keeps the organization last selected; it never falls back to no organization.
- A user may select again at any time to switch; the new token replaces the old organization.
- Deactivated, unverified or locked users are rejected, as in login and refresh.
- The login choices list each organization's ID and name; names come from a Shared organization lookup contract, never from Organization's tables.
- Inactive organizations cannot be selected; they get the same rejection as a non-member.
- After selection the token carries only the roles the user holds in the selected organization.

## Data
| Field | Type | Required | Validation |
|---|---|---|---|
| OrganizationId (select request) | organization id | Yes | Non-empty; user must belong to it |
| Organizations (login response) | list of id and name | Multi-org only | Only organizations where user has a role |
| Token (select response) | access and refresh token | Yes | Contains organization claim |

## Acceptance criteria
- Selecting without authentication is rejected
- Selecting an empty organization ID is rejected
- Selecting an organization the user has no role in is rejected
- Selecting a non-existent organization gives the same rejection as a non-member one
- A deactivated user cannot select an organization
- A user cannot select another user's organization even with a valid organization ID
- Login for a two-organization user returns no organization claim and lists both choices
- Selecting a member organization returns a token whose claim is that organization
- Refreshing after selection returns a token with the same organization claim
- Selecting a second organization yields a token with the second organization; refresh keeps it
- Selecting an inactive organization gives the same rejection as a non-member one
- Login choices include each organization's name
- After selection the token has only the roles held in the selected organization

## Out of scope
- Creating, joining or leaving organizations
- Location claim and location scoping
- Per-organization permission changes beyond the roles the user already holds
- Remembering the last organization across separate logins
