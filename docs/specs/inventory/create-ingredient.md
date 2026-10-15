# Create ingredient

## Problem
Owners and Admins need to build up a catalogue of ingredients per Location so that,
later, stock can be tracked and low-stock conditions can be surfaced. Today there is no
way to register an ingredient anywhere in the system.

## Scope
- Module(s) affected: Inventory (new — `Modules.Inventory` does not exist yet in `src/`;
  see Open questions). Reads Organization/Location data only through the existing
  `IOrganizationLookup` contract in Shared, and reads role data only through the existing
  `IUserRoleLookup` contract in Shared. No other module's database is queried directly.
- Roles allowed: Owner, Admin. Staff cannot create ingredients (read-only per the wider
  feature request; retrieval is covered by a separate spec).

## Behaviour
1. An authenticated user creates an ingredient by supplying the Organization it belongs
   to, the Location it is stocked at, its name, its unit of measure, its current stock
   level, its minimum-stock threshold, and its cost per unit.
2. The user must hold the Owner or Admin role in the given Organization. A Staff role, no
   role at all, or a role in a different Organization is rejected.
3. The given Location must exist and must belong to the given Organization. A Location
   from a different Organization, or a Location that does not exist, is rejected.
4. A newly created ingredient is always active (`IsActive = true`); there is no way to
   create an ingredient as inactive.
5. The ingredient's low-stock indicator is derived, not entered directly: it is true only
   when a minimum-stock threshold greater than zero has been set and the current stock is
   at or below that threshold. A minimum stock of zero means no threshold is tracked, so
   the indicator is always false regardless of current stock.
6. On success, the newly created ingredient — including its generated identifier, creation
   timestamp, and computed low-stock indicator — is returned to the caller.

## Data
Request:
| Field | Type | Required | Validation |
|---|---|---|---|
| OrganizationId | Guid | required | must be an Organization the caller has an Owner or Admin role in |
| LocationId | Guid | required | must exist and belong to OrganizationId |
| Name | string | required | 1–100 characters after trimming; not empty/whitespace only |
| Unit | enum (Kilogram, Liter, Unit, Box, Gram, Milliliter) | required | must be one of the defined values |
| CurrentStock | decimal | required | >= 0 |
| MinimumStock | decimal | required | >= 0 |
| CostPerUnit | decimal | required | >= 0 |

Response (in addition to the request fields): Id, IsActive (always true on creation),
CreatedAt, UpdatedAt, IsLowStock (computed as described in Behaviour §5).

Note on the "foreign key" language in the originating request: per this codebase's
module-isolation rules, `OrganizationId` and `LocationId` are stored on the ingredient as
plain identifiers only — never as a database foreign key into another module's tables.
Their existence and org/location relationship are validated at request time through the
existing `IOrganizationLookup` contract (`OrganizationExistsAsync`,
`GetLocationIdsInOrganizationAsync` / `GetLocationAsync`), the same pattern the Menu
module already uses for its own Location references.

## Acceptance criteria
- Creating with an empty, whitespace-only, or missing Name is rejected.
- Creating with a Name longer than 100 characters is rejected.
- Creating with a negative CurrentStock, MinimumStock, or CostPerUnit is rejected.
- Creating with a Unit value outside the defined set is rejected.
- Creating without a LocationId is rejected.
- Creating with a LocationId that belongs to a different Organization than the one
  supplied is rejected.
- A Staff user cannot create an ingredient.
- A user with no role (or no role in the supplied OrganizationId) cannot create an
  ingredient, including a user who belongs to a different Organization entirely.
- A valid request from an Owner or Admin creates the ingredient under the given
  OrganizationId and LocationId, defaults IsActive to true, and returns the created
  ingredient.
- When MinimumStock is greater than 0 and CurrentStock is at or below MinimumStock at the
  moment of creation, the returned ingredient has IsLowStock = true; in every other case
  (including MinimumStock = 0) it is false.

## Out of scope
- Listing, retrieving by id, updating, or deactivating ingredients — separate specs (see
  Open questions; the full "Ingredient Management" request is larger than fits one spec).
- Any stock-adjustment operation (receiving stock, consumption, manual correction) beyond
  setting the initial CurrentStock value at creation time.
- Automatic stock deduction from sales.
- Purchase orders or nota fiscal scanning.
- Any delivery mechanism (push notification, email, etc.) for low-stock conditions — this
  spec only defines the IsLowStock value on the ingredient itself.
- Associating ingredients with dishes or menu items.
- Creating a Location or Organization — both must already exist.
- Restricting an Admin's write access to a single assigned Location (see Open questions).

## Open questions
- `Modules.Inventory` does not exist in `src/` today. This spec assumes it will be created
  to hold this feature (naming it `inventory` as the user's request implied); flagging
  since that is a larger step than adding an endpoint to an existing module.
- The originating request says "Admin: full access, scoped to their assigned Location" and
  "Staff: read-only, scoped to their assigned Location." The current cross-module contract
  (`IUserRoleLookup.GetRoleAsync(userId, organizationId)`) only returns an
  organization-level role and has no way to ask "which Location is this user assigned
  to?" — even though the underlying `UserRole` entity in Identity already has a nullable
  `LocationId` field, it isn't exposed through any Shared contract. This spec therefore
  treats Admin as Organization-wide for creation (matching how the existing Menu module's
  permission checks work today), the same as Owner. Enforcing true Location-scoped
  permissions requires extending an Identity contract in Shared, which is a decision and a
  change beyond this Inventory-only spec — confirm before building location-scoped
  authorization, otherwise the "scoped to assigned Location" requirement will not actually
  be enforced.
- Should creating an ingredient be rejected if the target Location is not Active (e.g.
  Closed, TemporarilyClosed, UnderConstruction)? The request doesn't say, and the existing
  Menu module does not check Location status when attaching a menu to one. Assumed: not
  rejected (Location status is not checked) for consistency with that existing behaviour.
- The originating request specifies HTTP 403 for a LocationId belonging to another
  Organization. The existing Menu module returns 404 ("LocationNotFound") in the
  equivalent case, to avoid confirming a location's existence to a caller who has no
  relationship to it. This spec describes only the required outcome ("rejected") and
  leaves the status-code convention to be confirmed/decided at the planning stage.
- The full "Ingredient Management" feature as described (create, paginated list, get by
  id, update, soft-delete, three-role location-scoped permissions, low-stock flag) would
  need well over the ~10-acceptance-criteria guideline for one spec. This document covers
  creation only. Recommend separate follow-up specs: `list-ingredients` (including the
  Owner-sees-all-locations vs. Admin/Staff-scoped-to-one-location behaviour and
  pagination, which has no existing convention in this codebase to follow), `get-ingredient`,
  `update-ingredient`, and `deactivate-ingredient`.
