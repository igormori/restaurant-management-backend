# Scenarios: Organization and Location Fields

**Plan:** docs/plans/organization/2026-10-15-organization-and-location-fields/plan.md
**Coverage:** 25 scenarios — 7 happy · 5 edge · 7 failure · 2 security · 4 guard

Test files (paths shortened below):
- `OrgTests/` = `tests/RestaurantManagement.Modules.Organization.Tests/` (unit tests, SQLite in memory)
- `WebTests/` = `tests/RestaurantManagement.Web.Tests/` (endpoint tests, real PostgreSQL)

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Failure | Creating an organization with no currency, an empty one, an unsupported one (EUR) or a number as text ("1") is rejected with a message on the currency | `Validate_CurrencyMissingOrNotSupported_ReturnsCurrencyError` | `OrgTests/OrganizationCurrencyValidationTests.cs` |
| 2 | T1 | Failure | Through the API, a create request with no currency returns 400 mentioning the currency and stores no organization | `CreateOrganization_CurrencyMissing_ReturnsBadRequestAndStoresNothing` | `WebTests/OrganizationFieldsEndpointTests.cs` |
| 3 | T1 | Happy | Creating "Poutine Palace" in CAD returns CAD, and the organization list shows CAD afterwards | `CreateOrganization_CurrencyCad_ReturnsAndStoresCad` | `WebTests/OrganizationFieldsEndpointTests.cs` |
| 4 | T1 | Edge | Editing Cantina da Nonna (BRL) with a body that also says USD keeps BRL: currency can never be changed after creation | `EditOrganization_BodyIncludesAnotherCurrency_KeepsStoredCurrency` | `WebTests/OrganizationFieldsEndpointTests.cs` |
| 5 | T1 | Security | Ana, a member of Cantina (BRL) and Maple Leaf Bistro (CAD), lists only those two, each with its own currency; Liberty Diner (USD), another group's organization, is not listed | `GetOrganizations_MemberOfTwoOrganizations_ReturnsOnlyThoseWithTheirOwnCurrency` | `WebTests/OrganizationFieldsEndpointTests.cs` |
| 6 | T2 | Failure | An edit with a missing, out-of-range (120 or -1) or 3-decimal service charge, a missing or out-of-range discount limit, or a missing or malformed cutoff ("4:00", "25:00", "quatro") is rejected with a message on that setting only | `Validate_SettingMissingOutOfRangeOrMalformed_ReturnsErrorForThatSetting` | `OrgTests/OrganizationSettingsTests.cs` |
| 7 | T2 | Guard | Settings at the limits (0 and 100, cutoffs 00:00 and 23:59) and normal values (12.5 %, 7.25 %) are accepted | `Validate_SettingsWithinLimits_IsValid` | `OrgTests/OrganizationSettingsTests.cs` |
| 8 | T2 | Happy | A new organization is stored with service charge 0, discount approval limit 0 and cutoff 04:00 (read back through the organization list) | `CreateOrganizationAsync_NewOrganization_ReturnsDefaultSettings` | `OrgTests/OrganizationSettingsTests.cs` |
| 9 | T2 | Happy | An Owner sets service charge 10, discount limit 15.5 and cutoff 05:30; the organization list returns those values | `EditOrganization_ValidSettings_StoresAndReturnsThem` | `WebTests/OrganizationFieldsEndpointTests.cs` |
| 10 | T2 | Security | An edit signed in to Cantina whose body names Maple Leaf Bistro changes only Cantina's settings; Maple Leaf Bistro keeps 0 and 0 | `EditOrganization_BodyNamesAnotherOrganization_ChangesOnlyTokenOrganizationSettings` | `WebTests/OrganizationFieldsEndpointTests.cs` |
| 11 | T3 | Failure | A location create or edit with a missing, unknown ("America/Curitiba_Centro") or Windows-style time zone, an unknown or numeric type, or a missing, empty, too long (11 characters) or non-alphanumeric code ("PAU-01") is rejected with a message on that field only | `Validate_LocationFieldMissingOrInvalid_ReturnsErrorForThatField` | `OrgTests/LocationFieldsTests.cs` |
| 12 | T3 | Guard | Locations in America/Sao_Paulo, America/Toronto and America/New_York, with type omitted, Store or CentralKitchen, and codes up to exactly 10 characters, are accepted | `Validate_ValidTimeZoneTypeAndCode_IsValid` | `OrgTests/LocationFieldsTests.cs` |
| 13 | T3 | Happy | Creating a location without a type stores a Store, with its time zone, and the code " pau1 " stored as "PAU1" (read back through the location list) | `CreateLocationAsync_TypeOmittedAndLowercaseCode_ReturnsStoreTimeZoneAndUppercaseCode` | `OrgTests/LocationFieldsTests.cs` |
| 14 | T3 | Happy | Editing a location to Toronto, central kitchen and code "yyz1" returns those values, with the code in uppercase | `EditLocationAsync_NewTimeZoneTypeAndCode_ReturnsThem` | `OrgTests/LocationFieldsTests.cs` |
| 15 | T3 | Failure | In Cantina, which has "MM" (open) and "BT" (closed), creating another "MM", "mm" or "BT" returns 400 and adds no location | `CreateLocation_CodeAlreadyUsedInOrganization_ReturnsBadRequestAndStoresNothing` | `WebTests/LocationCodeEndpointTests.cs` |
| 16 | T3 | Happy | Cantina can create "MM" when only Trattoria do Sol, another organization, uses "MM" | `CreateLocation_CodeUsedOnlyByAnotherOrganization_ReturnsOkWithCode` | `WebTests/LocationCodeEndpointTests.cs` |
| 17 | T3 | Failure | Editing Batel ("BT") to "MM", already used by another Cantina location, returns 400 and Batel keeps "BT" | `EditLocation_CodeUsedByAnotherLocationOfOrganization_ReturnsBadRequestAndKeepsCode` | `WebTests/LocationCodeEndpointTests.cs` |
| 18 | T3 | Edge | Five requests creating code "MOE" at the same moment: none gets a server error, exactly one is stored and the other four get 400 | `CreateLocation_SameCodeSentConcurrently_StoresOneAndRejectsOthersWithoutServerError` | `WebTests/LocationCodeEndpointTests.cs` |
| 19 | T3 | Guard | A Manager, Cashier, Waiter or Kitchen user editing a location is refused (403) and the location is unchanged | `EditLocationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndChangesNothing` | `OrgTests/LocationFieldsTests.cs` |
| 20 | T4 | Failure | Asking for the business day with an unknown, Windows-style ("E. South America Standard Time"), empty or missing time zone is refused | `GetBusinessDate_UnknownTimeZone_ThrowsArgumentException` | `OrgTests/BusinessDayCalculatorTests.cs` |
| 21 | T4 | Happy | With cutoff 04:00, a sale at 02:30 local on 15 Oct belongs to 14 Oct, in São Paulo and in Toronto | `GetBusinessDate_SaleAt0230LocalBeforeCutoff_ReturnsPreviousDay` | `OrgTests/BusinessDayCalculatorTests.cs` |
| 22 | T4 | Edge | With cutoff 04:00: exactly 04:00 local, noon, 23:30 local when UTC is already the next day, 01:00 in New York, and 02:30 just after daylight saving ends in Toronto. With other cutoffs: 02:30 with a 00:00 cutoff stays on the same day, 05:30 with a 06:00 cutoff goes to the previous day, 05:00 exactly at a 05:00 cutoff stays on the same day | `GetBusinessDate_AtOrAfterCutoffOrAcrossUtcMidnight_ReturnsLocalBusinessDay` | `OrgTests/BusinessDayCalculatorTests.cs` |
| 23 | T1 | Guard | Through the API, a create request with a number (1) instead of a currency code returns 400 and stores no organization; already true today because currency is sent as text, and must stay true | `CreateOrganization_CurrencyAsNumber_ReturnsBadRequestAndStoresNothing` | `WebTests/OrganizationFieldsEndpointTests.cs` |
| 24 | T3 | Edge | Editing a central kitchen without sending a type keeps it a central kitchen (decided by igormori at Gate 2: an omitted type keeps the stored type) | `EditLocationAsync_TypeOmitted_KeepsStoredType` | `OrgTests/LocationFieldsTests.cs` |
| 25 | T3 | Edge | Editing a closed location without sending a status keeps it closed; today it silently reopens as Active (decided by igormori at Gate 2: any optional field left out of a location edit keeps its stored value) | `EditLocation_StatusOmitted_KeepsStoredStatus` | `WebTests/LocationCodeEndpointTests.cs` |

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 Currency is required | 1, 2, 3, 23 |
| #2 Time zone is required and valid | 11, 12 |
| #3 Defaults are set (service charge 0, discount limit 0, cutoff 04:00, type Store; defaults apply on create only, an edit keeps stored values) | 8, 13, 24, 25 |
| #4 Code unique per organization | 15, 17, 18 |
| #5 Code reusable across organizations | 16 |
| #6 Service charge validated | 6, 7 |
| #7 Business day is correct in each country | 21, 22 |

Plan Definition of Done items covered by existing tests (kept, only their request data updated):
role rejection for Manager, Cashier, Waiter and Kitchen on organization edit
(`EditOrganizationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbidden`) and location create
(`CreateLocationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndCreatesNothing`); another
organization's location returns 404 on edit and stays unchanged
(`LocationById_LocationOfAnotherOrganization_ReturnsNotFoundAndLeavesItUnchanged`). Role checks
live in the services, so they are tested at unit level only (scenario 19 for location edit), not
again through the endpoints. The location edit has only two optional fields, `type` (scenario 24) and
`status` (scenario 25); every other edit field is required and is rejected when missing. Windows-style time zone names are likewise checked at unit level
(scenario 11) and not again through the API.

## Existing tests updated
The new required fields force these changes; no assertion was changed or weakened.
- `TenantApiFactory`: `SeedOrganizationAsync` takes a currency and `SeedLocationAsync` takes a code and a time zone (no default in the helpers); callers in `OrganizationTenantEndpointTests`, `LocationAccessEndpointTests` and `MenuTenantEndpointTests` pass them.
- `OrganizationTenantEndpointTests`: the location and organization request bodies carry time zone, code and the three settings, so they still pass validation and reach the 404, 403 and tenant checks they test.
- `AuditFieldsMigrationTests` scenario 7: reads back only the columns that existed at the audit migration, because the new columns do not exist at that point of the migration history.
- Organization unit tests (`OrganizationServiceCreateTests`, `OrganizationCreationScopeTests`, `OrganizationTenantFilterTests`, `OrganizationServiceEditTests`, `LocationServiceTests`, `OrganizationLookupTests`): requests and seeded rows set a currency, time zone, code and settings.
