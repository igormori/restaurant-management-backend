# Base entity and audit interceptor

## Summary
Every stored row gets the same tenant and audit fields, filled automatically on save instead of by hand in each service. This removes the main source of cross-tenant bugs.

## Rules
- Roles: no new permissions; behaviour applies to every authenticated write regardless of role.
- Tenancy: a tenant row is saved under the current user's organization, or under the explicit tenant scope when one is open.
- The explicit tenant scope replaces the user's organization for that save. It is used only when creating an organization.
- Added rows get Id (if empty), CreatedAt, CreatedByUserId and OrganizationId from the current user.
- Added tenant rows already carrying a different OrganizationId are rejected; nothing is written.
- Modified rows get UpdatedAt and UpdatedByUserId; CreatedAt and CreatedBy stay unchanged.
- Changing OrganizationId on an existing tenant row is rejected.
- All timestamps come from an injectable clock and are UTC.
- Saving a tenant row with no current organization and no explicit tenant scope is rejected.
- Organizations and user_roles get audit fields only, with no tenant enforcement.
- Current organization comes from a token claim set at login (single org: automatic; several: user selects).

## Data
| Field | Type | Required | Validation |
|---|---|---|---|
| CreatedByUserId | user id, nullable | No | Set by system, never by caller |
| UpdatedByUserId | user id, nullable | No | Set by system, never by caller |
| UpdatedAt | UTC timestamp | Yes | Set by system; missing today on menu_categories, location_menus |
| Organization claim | organization id | Yes | Present in token after login or org selection |

## Acceptance criteria
- Saving a new tenant row with another organization's ID is rejected and nothing is written
- Changing OrganizationId on an existing row is rejected
- Saving a tenant row with no current organization and no explicit scope is rejected
- Inside an explicit tenant scope, a new row gets the scope's organization ID, even when it differs from the user's
- A new menu by a user in Org A gets CreatedAt, CreatedByUserId and OrganizationId of Org A
- Updating a menu sets UpdatedAt and UpdatedByUserId and leaves CreatedAt unchanged
- With a fixed clock, every timestamp equals the fixed time with Kind = Utc
- Saving a new organization's settings and Owner role inside its scope gives both the new organization's ID
- Existing rows in menus, menu_categories, location_menus, locations, organization_settings and user_roles keep their data after migration

## Out of scope
- Select-organization endpoint and refresh-token behaviour (separate task)
- Location claim and location scoping (task 6)
- Automatic query filtering by organization
- Backfilling CreatedByUserId for existing rows

## Open questions
- Scenario 8 (user_roles row saved) has no expected result; truncated. Assumed: only audit fields set, OrganizationId untouched.
- Does this task include the login org claim? Assumed: yes, claim only; selection and refresh are separate.
- Are Modified user_roles/organization rows checked for OrganizationId changes? Assumed: no, audit only.
