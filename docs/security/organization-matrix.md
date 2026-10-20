# Organization security matrix

The record of every rule the Organization module must keep, and the test that proves each one:
creating an organization and its plan, who may change an organization and its locations, and
that an organization only ever sees and changes its own data. The pipeline
(`.github/workflows/dotnet.yml`) runs every test named here, and
`.claude/scripts/check-security-matrices.py` fails the build when this file and the tests drift
apart.

## Rules for this file
The rules in the root `CLAUDE.md`, "Security matrices", apply: any change to Organization updates
this file in the same commit; new behaviour adds ❌ rows before it is built; no row is ✅ without
a named test; every test in `tests/RestaurantManagement.Modules.Organization.Tests/` and
`tests/RestaurantManagement.Web.Tests/Organization/` is named here and carries
`[Trait("Rule", "<ID>")]`; row IDs start with OR (rules) or ORD (schema and wiring) and are never
reused.

**Status:** ✅ covered by the named test · ⚠️ partly covered (the Expected column says what is
missing) · ❌ no test yet · 🐞 known bug, see the backlog item

**Where the tests live:** unit tests in `tests/RestaurantManagement.Modules.Organization.Tests/`,
endpoint and database tests in `tests/RestaurantManagement.Web.Tests/Organization/`. Choosing a
location in a request (the location access check) is in the Shared matrix, where that check lives.

## 1. Creating an organization
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| OR1 | Signed-in user creates an organization | Stored with the chosen currency; the creator becomes its Owner; the plan is TRIAL for 30 days with 1 location and default settings | ✅ | `OrganizationServiceCreateTests.CreateOrganizationAsync_WhenRoleAssignmentSucceeds_PersistsAllAndReturnsResponse`, `OrganizationSettingsTests.CreateOrganizationAsync_NewOrganization_ReturnsDefaultSettings`, `OrganizationFieldsEndpointTests.CreateOrganization_CurrencyCad_ReturnsAndStoresCad`, `OrganizationServiceReservedNameTests.CreateOrganizationAsync_NameMatchesFormerSentryTestHook_CreatesOrganization` |
| OR2 | Request body sends plan or trial fields | Ignored: the plan is always TRIAL | ✅ | `OrganizationPlanEndpointTests.CreateOrganization_BodyHasPlanAndTrialFields_StoresAndReturnsTrialPlan` |
| OR3 | Creator already belongs to an organization on the TRIAL plan | 400: cannot create another | ✅ | `OrganizationTenantFilterTests.CreateOrganizationAsync_TrialOwnerWithNoOrganizationInToken_ThrowsBadRequest` |
| OR4 | Creator's account does not exist | 401 | ✅ | `OrganizationServiceNotFoundTests.CreateOrganizationAsync_UserDoesNotExist_ThrowsUnauthorizedBusinessException` |
| OR5 | Owner role cannot be assigned, or the save fails after it | Nothing is kept: no organization, no settings, no dangling Owner role; the error is logged; a retry works | ✅ | `OrganizationServiceCreateTests.CreateOrganizationAsync_WhenRoleAssignmentFails_PersistsNoOrganizationOrSettings`, `OrganizationServiceCreateTests.CreateOrganizationAsync_WhenRoleAssignmentFails_ThrowsAndReturnsNoResponse`, `OrganizationServiceCreateTests.GetOrganizationsAsync_AfterFailedRoleAssignment_ReturnsEmptyList`, `OrganizationServiceCreateTests.CreateOrganizationAsync_AfterFailedRoleAssignment_SecondAttemptSucceeds`, `OrganizationServiceCreateTests.CreateOrganizationAsync_WhenRollbackFails_LogsError`, `OrganizationServiceCreateTests.CreateOrganizationAsync_WhenCommitFailsAfterRoleAssignmentSucceeds_RevokesRoleAndThrows`, `OrganizationServiceCreateTests.CreateOrganizationAsync_WhenCommitFailsAndRevokeAlsoFails_LogsSecondErrorAndThrowsOriginalException` |
| OR6 | The new organization's own records | Carry the new organization, never the creator's current one, and the creator as author | ✅ | `OrganizationCreationScopeTests.CreateOrganizationAsync_UserHasNoOrganization_SettingsGetNewOrganizationId`, `OrganizationCreationScopeTests.CreateOrganizationAsync_UserBelongsToAnotherOrganization_SettingsGetNewOrganizationNotUsers`, `OrganizationCreationScopeTests.CreateOrganizationAsync_SettingsSaved_CarryAuditFieldsOfCreator`, `OrganizationCreationScopeTests.CreateOrganizationAsync_OwnerRoleAssigned_AssignedWithNewOrganizationIdInsideScope`, `OrganizationCreationScopeTests.CreateOrganizationAsync_Completed_ScopeIsClosed`, `OrganizationCreationScopeTests.CreateOrganizationAsync_RoleAssignmentFails_ScopeIsClosedAndNothingPersisted` |

## 2. Who may do what
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| OR7 | Owner or Admin edits the organization and its settings | Allowed | ✅ | `OrganizationServiceEditTests.EditOrganizationAsync_WhenUserIsOwnerOfOrganization_EditsSuccessfully`, `OrganizationServiceEditTests.EditOrganizationAsync_WhenUserIsAdminOfOrganization_EditsSuccessfully`, `OrganizationFieldsEndpointTests.EditOrganization_ValidSettings_StoresAndReturnsThem` |
| OR8 | Any other role, or no role, edits the organization | 403 | ✅ | `OrganizationServiceEditTests.EditOrganizationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbidden`, `OrganizationServiceEditTests.EditOrganizationAsync_WhenUserHasNoRoleForOrganization_ThrowsForbidden` |
| OR9 | Owner or Admin creates, edits or closes a location | Allowed; closing keeps the location (status Closed), never deletes it | ✅ | `LocationServiceTests.CreateLocationAsync_WhenUserIsOwnerOfOrganization_CreatesLocation`, `LocationServiceTests.CreateLocationAsync_WhenUserIsAdminOfOrganization_CreatesLocation`, `LocationServiceTests.EditLocationAsync_WhenUserIsOwnerOfOrganization_EditsLocation`, `LocationServiceTests.EditLocationAsync_WhenUserIsAdminOfOrganization_EditsLocation`, `LocationServiceTests.DeleteLocationAsync_WhenUserIsOwnerOfOrganization_ClosesLocation`, `LocationServiceTests.DeleteLocationAsync_WhenUserIsAdminOfOrganization_ClosesLocation` |
| OR10 | Any other role, or no role, creates, edits or closes a location | 403; nothing changes | ✅ | `LocationServiceTests.CreateLocationAsync_WhenUserHasWaiterRoleOrNoRole_ThrowsForbidden`, `LocationServiceTests.CreateLocationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndCreatesNothing`, `LocationServiceTests.EditLocationAsync_WhenUserHasWaiterRoleOrNoRole_ThrowsForbidden`, `LocationFieldsTests.EditLocationAsync_RoleOtherThanOwnerOrAdmin_ThrowsForbiddenAndChangesNothing`, `LocationServiceTests.DeleteLocationAsync_WhenUserHasWaiterRoleOrNoRole_ThrowsForbidden` |
| OR11 | Listing locations | Any role of the organization may; a user with no role in it gets 403 | ✅ | `LocationListTests.GetLocationsAsync_UserHasAnyRoleInOrganization_ReturnsItsLocations`, `LocationListTests.GetLocationsAsync_UserHasNoRoleInOrganization_ThrowsForbiddenNotAMember` |
| OR12 | No organization chosen in the token | 403 "organization required" | ✅ | `OrganizationTenantEndpointTests.OrganizationEndpoints_NoOrganizationInToken_ReturnsForbiddenOrganizationRequired` |
| OR13 | Token whose user identifier is not a user ID | 401 with the translated message | ✅ | `LocationMessagesEndpointTests.LocationEndpoints_UserIdentifierNotAUserId_ReturnsUnauthorizedWithLocalizedMessage` |
| OR31 | Admin for all locations who also holds another role (Waiter at one location) edits or closes any location, creates a location or edits the organization | Allowed at every location, also one added later, whichever order the roles were stored in | ✅ | `OrganizationMultiRoleAccessTests.EditLocationAsync_AdminForAllLocationsPlusWaiterAtBatelEditsCentro_EditsInEveryOrder`, `OrganizationMultiRoleAccessTests.DeleteLocationAsync_AdminForAllLocationsPlusWaiterAtBatelClosesCentro_ClosesInEveryOrder`, `OrganizationMultiRoleAccessTests.CreateLocationAsync_AdminForAllLocationsPlusWaiterAtBatel_CreatesInEveryOrder`, `OrganizationMultiRoleAccessTests.EditOrganizationAsync_AdminForAllLocationsPlusWaiterAtBatel_EditsInEveryOrder`, `OrganizationMultiRoleEndpointTests.EditLocation_AdminForAllLocationsPlusWaiterAtBatelEditsCentro_ReturnsOkInEveryStorageOrder` |
| OR32 | Waiter for all locations plus Admin at one location edits or closes a location | Allowed at that location only; 403 at any other, whichever order; nothing changes | ✅ | `OrganizationMultiRoleAccessTests.EditLocationAsync_WaiterForAllLocationsPlusAdminAtBatel_EditsBatelInEveryOrder`, `OrganizationMultiRoleAccessTests.EditLocationAsync_WaiterForAllLocationsPlusAdminAtBatelEditsCentro_ThrowsForbiddenInEveryOrder`, `OrganizationMultiRoleAccessTests.DeleteLocationAsync_AdminAssignedOnlyToBatelClosesCentro_ThrowsForbiddenAndKeepsLocationActive`, `OrganizationMultiRoleEndpointTests.EditLocation_AdminAssignedOnlyToBatelEditsCentro_ReturnsForbidden` |
| OR33 | Admin for one location only creates a location or edits the organization | 403: these belong to the whole organization, so only a role for all locations counts | ✅ | `OrganizationMultiRoleAccessTests.CreateLocationAsync_AdminAssignedOnlyToBatel_ThrowsForbidden`, `OrganizationMultiRoleAccessTests.EditOrganizationAsync_AdminAssignedOnlyToBatel_ThrowsForbidden`, `OrganizationMultiRoleEndpointTests.OrganizationWideEndpoints_AdminAssignedOnlyToBatel_ReturnsForbidden` |
| OR34 | Two roles and neither is Owner or Admin (Waiter with Cashier, Manager with Kitchen), or an Admin role held only in another organization | 403 when editing a location or the organization, whichever order | ✅ | `OrganizationMultiRoleAccessTests.EditLocationAsync_TwoRolesNeitherOwnerNorAdmin_ThrowsForbiddenInEveryOrder`, `OrganizationMultiRoleAccessTests.EditOrganizationAsync_TwoRolesNeitherOwnerNorAdmin_ThrowsForbiddenInEveryOrder`, `OrganizationMultiRoleEndpointTests.EditOrganization_AdminOfAnotherOrganizationPlusWaiterHere_ReturnsForbidden` |
| OR35 | Waiter assigned to one location only lists locations | Allowed: any role in the organization may list | ✅ | `OrganizationMultiRoleAccessTests.GetLocationsAsync_WaiterAssignedOnlyToOneLocation_ReturnsLocations` |

## 3. Other organizations' data
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| OR14 | Another organization's location, by its ID | 404, as if it did not exist; nothing changes | ✅ | `OrganizationTenantEndpointTests.LocationById_LocationOfAnotherOrganization_ReturnsNotFoundAndLeavesItUnchanged`, `OrganizationTenantFilterTests.GetLocationAsync_LocationOfAnotherOrganization_ReturnsNull` |
| OR15 | Lists of locations | Only the token organization's | ✅ | `OrganizationTenantEndpointTests.GetLocations_LocationsInTwoOrganizations_ReturnsOnlyTokenOrganizationsLocations` |
| OR16 | Request body names another organization | Ignored: the token organization is created into or changed | ✅ | `OrganizationTenantEndpointTests.CreateLocation_BodyNamesAnotherOrganization_StoresLocationInTokenOrganization`, `OrganizationTenantEndpointTests.EditOrganization_SignedInToCantina_EditsCantinaAndLeavesTrattoriaUnchanged`, `OrganizationFieldsEndpointTests.EditOrganization_BodyNamesAnotherOrganization_ChangesOnlyTokenOrganizationSettings` |
| OR17 | Listing the user's organizations | Every organization the user belongs to, with its own plan and currency, and no other | ✅ | `OrganizationFieldsEndpointTests.GetOrganizations_MemberOfTwoOrganizations_ReturnsOnlyThoseWithTheirOwnCurrency`, `OrganizationTenantFilterTests.GetOrganizationsAsync_SignedInToOneOfTwoOrganizations_ShowsPlanDetailsOfBoth`, `OrganizationServiceNotFoundTests.GetOrganizationsAsync_UserDoesNotExist_ThrowsUnauthorizedBusinessException` |
| OR18 | Old routes with the organization in the address | Not routed | ✅ | `OrganizationTenantEndpointTests.OldOrganizationInUrlRoute_Called_IsNotRouted` |
| OR19 | Organization or its settings missing when edited | 404 | ✅ | `OrganizationServiceNotFoundTests.EditOrganizationAsync_OrganizationDoesNotExist_ThrowsNotFoundBusinessException`, `OrganizationServiceNotFoundTests.EditOrganizationAsync_OrganizationHasNoSettings_ThrowsNotFoundBusinessException` |
| OR20 | Other modules asking about organizations (lookup) | Only the requested organizations, with name and active flag; unknown ones omitted | ✅ | `OrganizationLookupTests.GetOrganizationsAsync_RequestedIdsExist_ReturnsNamesAndActiveFlags`, `OrganizationLookupTests.GetOrganizationsAsync_OrganizationNotRequested_IsNotReturned`, `OrganizationLookupTests.GetOrganizationsAsync_RequestedIdDoesNotExist_IsOmitted`, `OrganizationLookupTests.GetOrganizationsAsync_EmptyIdList_ReturnsEmptyList`, `OrganizationLookupTests.GetOrganizationsAsync_OrganizationCreatedWithoutSettingIsActive_ReportsActive` |

## 4. Organization fields and settings
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| OR21 | Currency missing, not BRL, USD or CAD, or sent as a number | 400; nothing stored | ✅ | `OrganizationCurrencyValidationTests.Validate_CurrencyMissingOrNotSupported_ReturnsCurrencyError`, `OrganizationFieldsEndpointTests.CreateOrganization_CurrencyMissing_ReturnsBadRequestAndStoresNothing`, `OrganizationFieldsEndpointTests.CreateOrganization_CurrencyAsNumber_ReturnsBadRequestAndStoresNothing` |
| OR22 | Edit sends another currency | Ignored: the currency never changes after creation | ✅ | `OrganizationFieldsEndpointTests.EditOrganization_BodyIncludesAnotherCurrency_KeepsStoredCurrency` |
| OR23 | Service charge or discount limit outside 0–100, cutoff not HH:mm, or missing | 400 for that setting; values at the limits accepted | ✅ | `OrganizationSettingsTests.Validate_SettingMissingOutOfRangeOrMalformed_ReturnsErrorForThatSetting`, `OrganizationSettingsTests.Validate_SettingsWithinLimits_IsValid`, `OrganizationMessagesEndpointTests.OrganizationRequest_InvalidSettingInPortuguese_ReturnsPortugueseFieldError` |
| OR24 | Name missing, text over its maximum, colour not #RRGGBB | 400 with the translated message | ✅ | `OrganizationMessagesEndpointTests.CreateOrganization_NameMissing_ReturnsLocalizedRequiredError`, `OrganizationMessagesEndpointTests.EditOrganization_TextOverMaximumLength_ReturnsLocalizedMaxLengthError`, `OrganizationMessagesEndpointTests.OrganizationRequest_InvalidColor_ReturnsLocalizedHexColorError` |
| OR25 | A setting changes | One audit entry with the values before and after; no entry when nothing changed or the edit was refused; a failed audit write keeps the change and logs the error | ✅ | `OrganizationServiceAuditTests.EditOrganizationAsync_OneSettingChanged_RecordsOneSettingsChangedEntry`, `OrganizationServiceAuditTests.EditOrganizationAsync_SettingsUnchanged_RecordsNothing`, `OrganizationServiceAuditTests.EditOrganizationAsync_EditRejected_RecordsNothing`, `OrganizationServiceAuditTests.EditOrganizationAsync_RecordingFails_KeepsSettingsSavedAndLogsError` |

## 5. Locations
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| OR26 | Location field missing or invalid (name, IANA time zone, type, code) | 400 for that field, translated | ✅ | `LocationFieldsTests.Validate_LocationFieldMissingOrInvalid_ReturnsErrorForThatField`, `LocationFieldsTests.Validate_ValidTimeZoneTypeAndCode_IsValid`, `LocationMessagesEndpointTests.LocationRequest_InvalidField_ReturnsLocalizedFieldError` |
| OR27 | Location type omitted; code in lower case | Type Store on create, unchanged on edit; code stored in upper case; status unchanged when omitted | ✅ | `LocationFieldsTests.CreateLocationAsync_TypeOmittedAndLowercaseCode_ReturnsStoreTimeZoneAndUppercaseCode`, `LocationFieldsTests.EditLocationAsync_NewTimeZoneTypeAndCode_ReturnsThem`, `LocationFieldsTests.EditLocationAsync_TypeOmitted_KeepsStoredType`, `LocationCodeEndpointTests.EditLocation_StatusOmitted_KeepsStoredStatus` |
| OR28 | Location code already used in the organization, also when sent at the same moment | 400; nothing stored; another organization may use the same code | ✅ | `LocationCodeEndpointTests.CreateLocation_CodeAlreadyUsedInOrganization_ReturnsBadRequestAndStoresNothing`, `LocationCodeEndpointTests.EditLocation_CodeUsedByAnotherLocationOfOrganization_ReturnsBadRequestAndKeepsCode`, `LocationCodeEndpointTests.CreateLocation_SameCodeSentConcurrently_StoresOneAndRejectsOthersWithoutServerError`, `LocationCodeEndpointTests.CreateLocation_CodeUsedOnlyByAnotherOrganization_ReturnsOkWithCode` |
| OR29 | A location's status changes | One audit entry; none when unchanged or refused; a failed audit write keeps the location closed and logs the error | ✅ | `LocationServiceAuditTests.ChangeLocationStatus_StatusChangesFromActive_RecordsOneLocationStatusChangedEntry`, `LocationServiceAuditTests.ChangeLocationStatus_StatusUnchanged_RecordsNothing`, `LocationServiceAuditTests.ChangeLocationStatus_RequestRejected_RecordsNothing`, `LocationServiceAuditTests.DeleteLocationAsync_RecordingFails_KeepsLocationClosedAndLogsError` |
| OR30 | Business day of a sale | Uses the location's own time zone; before the cutoff it counts for the previous day; unknown time zones are refused | ✅ | `BusinessDayCalculatorTests.GetBusinessDate_UnknownTimeZone_ThrowsArgumentException`, `BusinessDayCalculatorTests.GetBusinessDate_SaleAt0230LocalBeforeCutoff_ReturnsPreviousDay`, `BusinessDayCalculatorTests.GetBusinessDate_AtOrAfterCutoffOrAcrossUtcMidnight_ReturnsLocalBusinessDay` |

## 6. Schema tests
| ID | Test | Protects |
|---|---|---|
| ORD1 | `OrganizationDeleteRulesTests.DeleteOrganization_HasActiveLocation_IsRefusedAndLocationKept` | An organization with locations or settings cannot be deleted (history is kept) |
| ORD2 | `OrganizationDeleteRulesTests.DeleteOrganization_OnlyLocationIsClosed_IsRefusedAndLocationKept` | Same, with only a closed location |
| ORD3 | `OrganizationDeleteRulesTests.DeleteOrganization_HasSettingsAndNoLocations_IsRefusedAndSettingsKept` | Same, with only settings |
| ORD4 | `OrganizationDeleteRulesTests.DeleteOrganization_NoLocationsAndNoSettings_IsDeleted` | An empty organization can be deleted |
| ORD5 | `OrganizationDeleteRulesTests.Migrate_OrganizationForeignKeys_AllRestrictDeletes` | Every Organization foreign key restricts deletes |
| ORD6 | `OrganizationDeleteRulesTests.Migrate_OrganizationTablesWithRows_NoRowAddedChangedOrRemoved` | The delete-rule migration keeps existing rows |

## Open questions (no row until decided)
- **Trial limits are stored but never enforced:** a TRIAL organization gets "1 location" and a
  30-day end date, but nothing stops it creating more locations or keeps it from working after
  the trial ends. Which limits should apply, and what happens at the end of the trial?
- **Who counts as a trial user (OR3):** the module notes say a user who *owns* a TRIAL
  organization cannot create another. The code refuses anyone with *any* role in one, so a
  Waiter at a trial restaurant cannot open their own business. Which is right?
