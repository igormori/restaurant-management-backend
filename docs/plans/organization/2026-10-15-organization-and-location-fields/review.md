# Review: Organization and Location Fields

**Verdict:** Fix first — the code is sound and unchanged since the first review, but `plan.md` is still not closed out, and the new backlog note reports a test gap that does not exist.
**Build:** pass (0 errors, 1 old warning)   **Tests:** 593 passed, 0 failed

Second pass. Reviewed range: `f9e96ec..HEAD` (7 commits). Local `main` is stale, so open the pull
request against `development`. Since the first review, only `plan.md`, `review.md` and one backlog
file changed; no file in `src/` or `tests/` changed (tests are unchanged since baseline `eeeb711`).
Task hashes and the "All spec Outcomes met" item from the first Fix first are now done.

## Fix first
| # | File:line | Problem | Change |
|---|---|---|---|
| 1 | docs/plans/organization/2026-10-15-organization-and-location-fields/plan.md:3,47 | Status is still "In progress" and the DoD (Definition of Done: the checks that must pass to count as finished) item "cross-tenant and role rejection tests" is still unticked. It is met. Cross-tenant: `PUT`/`DELETE api/locations/{id}` in `tests/RestaurantManagement.Web.Tests/OrganizationTenantEndpointTests.cs:82-100` (scenario 18, its body carries a valid `timeZone` and `code`, so the 404 comes from the tenant filter, not validation), list (scenario 22), create (scenario 23). Role rejections: `LocationFieldsTests.cs:226`, `LocationServiceTests.cs:304`, `OrganizationServiceEditTests.cs:134` | Tick line 47 and set Status to Done |
| 2 | backlog/improvements/2026-10-15-add-cross-tenant-test-for-location-edit.md:1 | Says no test covers an Owner of organization A editing organization B's location. `LocationById_LocationOfAnotherOrganization_ReturnsNotFoundAndLeavesItUnchanged` (PUT case) covers exactly that and checks B's row is unchanged | Delete this backlog file |

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | src/RestaurantManagement.Modules.Organization/Services/OrganizationService.cs:245 | The new shared mapper is named `ToResponse`. The architecture skill requires a private static `MapToResponse` | Rename it to `MapToResponse` |
| 2 | src/RestaurantManagement.Modules.Organization/Services/BusinessDayCalculator.cs:18 | The IANA (Internet Assigned Numbers Authority) check differs from `Models/IanaTimeZoneAttribute.cs:18`. The calculator only rejects names with spaces, so Windows ids with no space (e.g. `UTC-11`) still pass. The attribute requires a `/` or `UTC`. One rule now lives in two places | Use one shared check (for example an internal static `IanaTimeZone.TryFind`) in both places |
| 3 | src/RestaurantManagement.Modules.Organization/Services/LocationService.cs:163 | Every unique violation on save turns into "code already used", even one that is not about the code | Also match `ConstraintName == "ix_locations_organization_id_code"` |
| 4 | src/RestaurantManagement.Modules.Organization/Migrations/20261016020844_AddOrganizationSettingsFields.cs:19 | The database default for `business_day_cutoff` is 00:00. The spec and entity default is 04:00. EF (Entity Framework) always writes 04:00, but any raw insert (e.g. `OrganizationDeleteRulesTests`) gets midnight | Add `.HasDefaultValue(new TimeOnly(4, 0))` in `OrganizationDbContext` and regenerate the migration. This is safe because there is no data yet |
| 5 | src/RestaurantManagement.Shared/Localization/SharedResource.resx:114 | The Shared project changed but is not listed in T3's "Files" row. This is flagged in the T3 Delivered note. It is only a message resource, not a contract | Fine as is. Mention it in the pull request |

## Check before merging
- **Location code uniqueness** (`LocationService.cs`, migration `AddLocationTimeZoneTypeAndCode`). It relies on the tenant query filter for the pre-check and on the PostgreSQL unique index for races (scenario 18). The migration also drops `ix_locations_organization_id`; the new composite index starts with `organization_id`, so it still covers that lookup.
- **Location edit contract change.** `EditLocationRequest.Status` is now nullable, and an omitted `status` or `type` keeps the stored value (Gate 2 decision). Before, a closed location silently reopened. `timeZone` and `code` are now required on every edit, so existing API clients must send them.
- **Organization list** (`OrganizationService.GetOrganizationsAsync`). It now loads entities with `IgnoreQueryFilters` and maps them in memory. This is still limited to the user's membership ids, but the query shape changed in a listed bypass location.

## Pull request description
**Organization and location fields for Brazil, Canada and the USA**

Adds the fields Phase 1 needs, with no country defaults: currency per organization, plus time zone,
type and code per location. Also adds service charge, discount approval limit and business-day
cutoff settings, and a business-day calculator.

**Organization module**
- `POST api/organizations/create` requires `currency` (`BRL`, `USD`, `CAD`). Currency cannot be changed later; edit ignores it.
- `PUT api/organizations/edit` requires `serviceChargeRate` and `discountApprovalLimit` (0–100, 2 decimals) and `businessDayCutoff` (`HH:mm`). New organizations get 0, 0 and `04:00`.
- Create, edit and `GET api/organizations` return currency and the three settings.
- `POST/PUT api/locations` require `timeZone` (IANA name; Windows names are rejected) and `code` (up to 10 letters or digits, stored uppercase, unique per organization, closed locations included). `type` is `Store` (the default) or `CentralKitchen`. On edit, an omitted `type` or `status` keeps the stored value.
- `IBusinessDayCalculator`: a UTC moment, an IANA zone and a cutoff give the business date. There is no endpoint yet (backlog/features/2026-10-15-expose-location-business-day.md).
- Three schema-only migrations: `AddOrganizationCurrency`, `AddOrganizationSettingsFields`, `AddLocationTimeZoneTypeAndCode` (unique index on organization and code).

**Shared:** a new `LocationCodeAlreadyUsed` message (English and Portuguese).

**Spec Outcomes delivered:** #1 currency required · #2 valid time zone required · #3 defaults · #4 code unique per organization · #5 code reusable across organizations · #6 service charge validated · #7 business day correct in São Paulo and Toronto.

**Risk:** Medium. Three schema-only migrations on Organization tables. There is no real data and no backfill.
**Flags:** create-organization, edit-organization and location create/edit contracts changed. Migrations add required columns and a unique index. There is no new package, Shared contract, public endpoint, or role or auth change.

**Backlog added:** unlocalized client messages, expose location business day, paginate organization and location lists, Organization `CLAUDE.md` follow-ups.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
