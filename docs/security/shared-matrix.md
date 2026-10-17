# Shared security matrix

The record of every rule the Shared project must keep, and the test that proves each one. Shared
holds the protections every module relies on: the role-to-permission map, the automatic
organization filter and audit stamping on every database write, choosing a location, the audit
log, error answers and translated messages, configuration checks and the emails. The pipeline
(`.github/workflows/dotnet.yml`) runs every test named here, and
`.claude/scripts/check-security-matrices.py` fails the build when this file and the tests drift
apart.

## Rules for this file
The rules in the root `CLAUDE.md`, "Security matrices", apply: any change to Shared updates this
file in the same commit; new behaviour adds ❌ rows before it is built; no row is ✅ without a
named test; every test in `tests/RestaurantManagement.Shared.Tests/` and
`tests/RestaurantManagement.Web.Tests/Shared/` is named here and carries `[Trait("Rule", "<ID>")]`;
row IDs start with SH (rules) or SHD (schema and wiring) and are never reused.

**Status:** ✅ covered by the named test · ⚠️ partly covered (the Expected column says what is
missing) · ❌ no test yet · 🐞 known bug, see the backlog item

**Where the tests live:** unit tests in `tests/RestaurantManagement.Shared.Tests/`, endpoint and
database tests in `tests/RestaurantManagement.Web.Tests/Shared/`.

## 1. Roles and permissions
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| SH1 | What each role may do | Owner and Admin: all five permissions. Manager: manage menus and view reports only. Cashier, Waiter, Kitchen: none | ✅ | `RolePermissionsTests.GetPermissions_Owner_ReturnsAllFivePermissions`, `RolePermissionsTests.GetPermissions_Admin_ReturnsAllFivePermissions`, `RolePermissionsTests.GetPermissions_Manager_ReturnsMenuManageAndReportsViewOnly`, `RolePermissionsTests.HasPermission_ManagerAndMenuManage_ReturnsTrue`, `RolePermissionsTests.HasPermission_ManagerAndMembersManage_ReturnsFalse`, `RolePermissionsTests.GetPermissions_Cashier_ReturnsNoPermissions`, `RolePermissionsTests.GetPermissions_Waiter_ReturnsNoPermissions`, `RolePermissionsTests.GetPermissions_Kitchen_ReturnsNoPermissions` |
| SH2 | Unknown or retired role; a caller trying to add a permission | No permissions; the map cannot be changed | ✅ | `RolePermissionsTests.GetPermissions_UnknownRole_ReturnsNoPermissions`, `RolePermissionsTests.GetPermissions_RetiredStaffRole_ReturnsNoPermissions`, `RolePermissionsTests.GetPermissions_CallerTriesToAddPermissionToResult_IsRefused` |
| SH3 | The catalogs | Exactly the five product permissions and the six product roles | ✅ | `RolePermissionsTests.All_ProductPermissionList_IsExactlyTheFiveSpecPermissions`, `RolePermissionsTests.Roles_DeclaredRoleNames_AreExactlyTheSixProductRoles` |

## 2. Each organization sees only its own data
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| SH4 | Reading data | Only the current organization's rows; none when no organization is known; another organization's row by ID is not found | ✅ | `TenantQueryFilterTests.Query_NoCurrentOrganization_ReturnsNoRows`, `TenantQueryFilterTests.Query_RowsInTwoOrganizations_ReturnsOnlyCurrentOrganizationRows`, `TenantQueryFilterTests.FindById_RowOfAnotherOrganization_ReturnsNull`, `TenantQueryFilterTests.Query_TwoContextsOnTheSameModel_EachSeesOnlyItsOwnOrganization` |
| SH5 | A location is chosen | Only that location's rows; none chosen: every location of the organization, and no other organization's | ✅ | `TenantQueryFilterTests.Query_LocationSelected_ReturnsOnlyThatLocationsRows`, `TenantQueryFilterTests.Query_NoLocationSelected_ReturnsEveryLocationOfTheOrganizationOnly` |
| SH6 | Where the current organization comes from | The token, or an explicit scope while a new organization is created; a scope never leaks into another request and never comes back once closed | ✅ | `TenantContextTests.OrganizationId_NoScopeOpen_ReturnsTheTokenOrganization`, `TenantContextTests.OrganizationId_ScopeOpenedForAnotherOrganization_ReturnsTheScopedOrganization`, `TenantScopeTests.OrganizationId_NoScopeBegun_IsNull`, `TenantScopeTests.Begin_Called_OrganizationIdIsTheScopedOrganization`, `TenantScopeTests.OrganizationId_ReadAfterAwaits_StillTheScopedOrganization`, `TenantScopeTests.Begin_CalledInAnotherAsyncFlow_DoesNotLeakIntoCallerFlow`, `TenantScopeTests.Dispose_ScopeDisposed_OrganizationIdIsNullAgain`, `TenantScopeTests.Dispose_NestedScopeDisposed_RestoresOuterOrganization`, `TenantScopeDisposeTests.Dispose_CalledAgainAfterANewScopeBegan_KeepsTheNewScope`, `TenantScopeDisposeTests.Dispose_OuterScopeWhileInnerIsOpen_KeepsTheInnerOrganization`, `TenantScopeDisposeTests.Dispose_OuterScopeThenInnerScope_LeavesNoScope` |
| SH7 | Saving a new row | Gets the current organization and creator; a row carrying another organization, or with no organization known, is refused with 403 and nothing in that save is written | ✅ | `AuditSaveChangesInterceptorTests.SaveChangesAsync_NewTenantRowCarriesAnotherOrganizationId_Throws403AndWritesNothing`, `AuditSaveChangesInterceptorTests.SaveChanges_NewTenantRowCarriesAnotherOrganizationId_Throws403AndWritesNothing`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_ValidOrganizationRowBatchedWithMismatchedDish_WritesNeitherRow`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_ScopeOrganizationDiffersFromEntityOrganization_Throws403AndWritesNothing`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_NoCurrentOrganizationAndNoScope_Throws403AndWritesNothing`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_NewDishByUserInOrgA_GetsOrganizationAndCreatorOfOrgA`, `AuditSaveChangesInterceptorTests.SaveChanges_NewDishByUserInOrgA_GetsOrganizationAndAuditFields`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_NewRowAlreadyCarriesCurrentOrganizationId_Saves`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_ScopeOpenWithDifferentUserOrganization_RowGetsScopeOrganization`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_ScopeOpenAndUserHasNoOrganization_SavesUnderScopeOrganization` |
| SH8 | Changing or deleting a stored row | Another organization's row, or moving a row to another organization, or no organization known: 403 and the stored row is unchanged; the own organization's row may be deleted | ✅ | `AuditSaveChangesInterceptorTests.SaveChangesAsync_OrganizationIdChangedOnExistingRow_Throws403AndKeepsStoredOrganization`, `AuditSaveChangesInterceptorExistingRowTests.SaveChangesAsync_DetachedDishUpdatedWithAnotherOrganizationId_Throws403AndKeepsStoredOrganization`, `AuditSaveChangesInterceptorExistingRowTests.SaveChangesAsync_DishOfAnotherOrganizationModified_Throws403AndKeepsStoredPrice`, `AuditSaveChangesInterceptorExistingRowTests.SaveChangesAsync_DishOfAnotherOrganizationRemoved_Throws403AndKeepsRow`, `AuditSaveChangesInterceptorExistingRowTests.SaveChangesAsync_ExistingDishModifiedWithNoOrganizationAndNoScope_Throws403AndKeepsStoredPrice`, `AuditSaveChangesInterceptorExistingRowTests.SaveChangesAsync_OwnDishRemoved_DeletesRow` |
| SH9 | Who and when on every row | Creation and update time (UTC) and user are set by the server; a creator sent by the caller is replaced; creation fields never change on update | ✅ | `AuditSaveChangesInterceptorTests.SaveChangesAsync_NewTenantRow_UpdatedAtEqualsCreatedAtAndUpdatedByEqualsCreatedBy`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_NewRowWithEmptyId_GetsNewId`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_NewRowWithPresetId_KeepsPresetId`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_CallerSuppliesCreatedByUserId_OverwrittenWithCurrentUser`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_NewNonTenantRowWithNoOrganizationAndNoScope_SavesWithAuditFields`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_NewNonTenantRowWithNoSignedInUser_SavesWithNullAuditUsers`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_ExistingDishModified_SetsUpdatedAtAndUpdatedByUserId`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_ExistingDishModified_LeavesCreatedAtAndCreatedByUnchanged`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_ExistingNonTenantRowModifiedWithNoOrganization_SetsUpdatedAudit`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_NewRowWithFixedClock_CreatedAtAndUpdatedAtEqualFixedTimeAsUtc`, `AuditSaveChangesInterceptorTests.SaveChangesAsync_ModifiedRowWithFixedClock_UpdatedAtEqualsFixedTimeAsUtc`, `AuditSaveChangesInterceptorExistingRowTests.SaveChangesAsync_DetachedDishUpdated_KeepsStoredCreatedAtAndCreatedBy` |

## 3. Choosing a location (the `X-Location-Id` header)
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| SH10 | Header is not an ID; no organization in the token; a location the user may not work at, or another organization's | 400 or 403, and the request goes no further | ✅ | `LocationAccessMiddlewareTests.InvokeAsync_HeaderIsNotAValidId_ThrowsBadRequestAndStops`, `LocationAccessMiddlewareTests.InvokeAsync_NoOrganizationInToken_ThrowsForbiddenAndStops`, `LocationAccessMiddlewareTests.InvokeAsync_LocationUserMayNotWorkIn_ThrowsForbiddenAndStops`, `LocationAccessEndpointTests.GetMenus_WaiterAssignedOnlyToBatelSelectsMercadoMunicipal_ReturnsForbidden`, `LocationAccessEndpointTests.GetMenus_ManagerOfAllLocationsSelectsAnotherOrganizationsLocation_ReturnsForbidden` |
| SH11 | A location the user works at, or a role for all locations | Allowed, and data is limited to that location; no header: no location limit; header on an anonymous request is ignored | ✅ | `LocationAccessMiddlewareTests.InvokeAsync_UserAssignedToTheLocation_StoresLocationAndContinues`, `LocationAccessMiddlewareTests.InvokeAsync_NoHeader_ContinuesWithoutLocationLimit`, `LocationAccessMiddlewareTests.InvokeAsync_AnonymousRequestWithHeader_IgnoresHeaderAndContinues`, `LocationAccessEndpointTests.GetMenus_ManagerOfAllLocationsSelectsAnyCantinaLocation_ReturnsOk` |

## 4. Audit log
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| SH12 | Reading the audit log | Needs "view reports" in the chosen organization (403 otherwise); 401 without a token | ✅ | `AuditLogEndpointTests.GetAuditLog_WithoutReportsViewInSelectedOrganization_ReturnsForbidden`, `AuditLogEndpointTests.GetAuditLog_NoToken_ReturnsUnauthorized`, `AuditLogEndpointTests.GetAuditLog_RoleWithReportsView_ReturnsOk` |
| SH13 | Whose entries are shown | Only the token organization's, even when the query names another | ✅ | `AuditLogEndpointTests.GetAuditLog_EntriesInTwoOrganizationsAndQueryNamesTheOther_ReturnsOnlyTokenOrganizationsEntries`, `AuditLogStorageTests.ReadEntries_EntryRecordedInCantinaReadInTrattoriaScope_FindsNothing`, `AuditLogRecordingEndpointTests.EditOrganization_ServiceChargeRateChanged_EntryVisibleOnlyToSameOrganization` |
| SH14 | Filters and pages | Newest first; date range ends included; action filter; a reversed range or unknown action is 400 with a translated message; at most 100 per page, 50 by default | ✅ | `AuditLogEndpointTests.GetAuditLog_NoFilters_ReturnsEntriesNewestFirst`, `AuditLogEndpointTests.GetAuditLog_FilterGiven_ReturnsOnlyMatchingEntriesIncludingRangeEnds`, `AuditLogEndpointTests.GetAuditLog_InvalidFilter_ReturnsBadRequestWithLocalizedMessage`, `AuditLogEndpointTests.GetAuditLog_PageSizeAboveLimitOrNotGiven_ReturnsCappedOrDefaultPage` |
| SH15 | A stored entry | Can never be changed or removed | ✅ | `AuditLogStorageTests.SaveChanges_StoredEntryChangedOrRemoved_IsRefusedAndRowUnchanged` |
| SH16 | Recording an entry | Organization from the token (never from the changed item), user, time, address, values before and after | ✅ | `AuditLogStorageTests.RecordAsync_AllFieldsGiven_StoresEveryField`, `AuditLogStorageTests.RecordAsync_NoLocationReasonOrBeforeValue_StoresEntryWithThoseEmpty`, `AuditLogStorageTests.RecordAsync_ChangedItemBelongsToAnotherOrganization_StoresEntryInTokenOrganization`, `AuditLogRecordingEndpointTests.DeleteLocation_ActiveLocation_EntryShowsActiveToClosed` |

## 5. Errors and messages
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| SH17 | A business rule refuses a request | Its status and message, as JSON | ✅ | `ExceptionHandlingMiddlewareTests.InvokeAsync_BusinessException_ReturnsMessagePropertyWithExceptionMessage` |
| SH18 | Something unexpected fails | 500 with a translated generic message; the error detail only in Development | ✅ | `ExceptionHandlingMiddlewareTests.InvokeAsync_UnexpectedException_ReturnsMessageProperty`, `ExceptionHandlingMiddlewareLocalizationTests.InvokeAsync_UnexpectedExceptionInRequestCulture_ReturnsLocalizedUnexpectedErrorMessage`, `ExceptionHandlingMiddlewareLocalizationTests.InvokeAsync_UnexpectedExceptionInProduction_HidesExceptionDetail`, `UnexpectedErrorEndpointTests.Login_UnexpectedFailureInPortuguese_ReturnsServerErrorWithPortugueseMessage` |
| SH19 | Every message | Exists in English and Portuguese; validation and code use only catalog keys; regional languages fall back to Portuguese or English; existing texts unchanged | ✅ | `MessageCatalogCompletenessTests.Catalog_EnglishAndPortuguese_HoldSameKeysWithText`, `MessageCatalogCompletenessTests.RequestModels_ValidationAttributes_UseCatalogKeys`, `MessageCatalogCompletenessTests.SourceCode_LocalizerLookups_ExistInCatalog`, `SharedResourceCatalogTests.Localizer_NewMessageKey_ReturnsExpectedTextInEachLanguage`, `SharedResourceCatalogTests.Localizer_RegionalOrUnsupportedCulture_ReturnsPortugueseOrEnglishFallback`, `SharedResourceCatalogTests.Localizer_ExistingMessageKey_KeepsTodaysText` |
| SH20 | A required ID is missing, null or not an ID | 400 on that field, translated (used by Identity S2 and Menu M23) | ✅ | `RequiredIdFieldEndpointTests.IdMissingOrNull_ReturnsLocalizedRequiredError`, `RequiredIdFieldEndpointTests.IdNotAnId_ReturnsLocalizedRequiredError` |

## 6. Configuration checks
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| SH21 | Token signing key and lifetime | Refused when empty, under 32 bytes, the placeholder outside Development, or a lifetime that is not positive; 32 bytes (counted in bytes, not characters) accepted | ✅ | `JwtOptionsValidatorTests.Validate_EmptyKey_FailsNamingJwtKey`, `JwtOptionsValidatorTests.Validate_KeyOf31Bytes_FailsNamingJwtKey`, `JwtOptionsValidatorTests.Validate_PlaceholderKeyInProduction_FailsNamingJwtKey`, `JwtOptionsValidatorTests.Validate_PlaceholderKeyInStaging_FailsNamingJwtKey`, `JwtOptionsValidatorTests.Validate_NonPositiveExpireMinutes_FailsNamingJwtExpireMinutes`, `JwtOptionsValidatorTests.Validate_PlaceholderKeyInDevelopment_Succeeds`, `JwtOptionsValidatorTests.Validate_KeyOfExactly32Bytes_Succeeds`, `JwtOptionsValidatorTests.Validate_Key17MultiByteCharsTotaling34Bytes_Succeeds`, `JwtOptionsValidatorTests.Validate_StrongKeyAndPositiveExpiryInProduction_Succeeds` |
| SH22 | Code fingerprint key | Refused when missing, empty or under 32 bytes; 32 bytes and a random key accepted | ✅ | `VerificationCodeOptionsValidatorTests.Validate_HashKeyMissing_FailsNamingHashKey`, `VerificationCodeOptionsValidatorTests.Validate_HashKeyEmpty_FailsNamingHashKey`, `VerificationCodeOptionsValidatorTests.Validate_HashKeyOf31Bytes_FailsNamingHashKey`, `VerificationCodeOptionsValidatorTests.Validate_HashKeyOfExactly32Bytes_Succeeds`, `VerificationCodeOptionsValidatorTests.Validate_HashKeyOf16TwoByteCharacters_Succeeds`, `VerificationCodeOptionsValidatorTests.Validate_RandomBase64HashKey_Succeeds` |
| SH23 | Rate limits and trusted proxies | Limits and windows must be positive; a proxy must be a valid address or network | ✅ | `AuthRateLimitOptionsValidatorTests.Validate_DefaultOptions_Succeeds`, `AuthRateLimitOptionsValidatorTests.Validate_NonPositivePermitLimit_Fails`, `AuthRateLimitOptionsValidatorTests.Validate_NonPositiveWindow_Fails`, `AuthRateLimitOptionsValidatorTests.Validate_PasswordPolicyLimitZeroOrNegative_FailsNamingTheSetting`, `AuthRateLimitOptionsValidatorTests.Validate_MalformedProxy_Fails`, `AuthRateLimitOptionsValidatorTests.Validate_ValidProxy_Succeeds` |

## 7. Emails
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| SH24 | Verification and reset emails | Contain the name (a generic greeting when blank), the code, the configured expiry and the recipient; no placeholder left; subjects fixed | ✅ | `VerificationEmailTemplateTests.Build_WithFirstName_GreetingContainsFirstName`, `VerificationEmailTemplateTests.Build_FirstNameIsBlank_UsesGenericGreeting`, `VerificationEmailTemplateTests.Build_WithCode_BodyContainsTheCode`, `VerificationEmailTemplateTests.Build_NonDefaultExpiryMinutes_StatesConfiguredMinutes`, `VerificationEmailTemplateTests.Build_WithRecipient_FooterContainsRecipientAddress`, `VerificationEmailTemplateTests.Build_WithValidInput_LeavesNoUnsubstitutedTokens`, `VerificationEmailTemplateTests.Build_WithValidInput_ContainsAllDesignContentElements`, `VerificationEmailTemplateTests.Subject_IsUnchanged`, `PasswordResetEmailTemplateTests.Build_ValidInput_ContainsNameCodeExpiryAndIgnoreNotice` |
| SH25 | A name with HTML characters in the reset email | Shown as text, never as markup | ✅ | `PasswordResetEmailTemplateTests.Build_FirstNameWithHtmlCharacters_IsHtmlEncoded` |
| SH26 | A name or address with HTML characters in the verification email | Shown as text, never as markup | ❌ | |
| SH27 | Development without a mail server | The email is logged with recipient and subject, never the body (so codes stay out of logs) | ✅ | `EmailServiceDevelopmentLogTests.SendEmailAsync_DevelopmentWithoutSmtpHost_LogsRecipientAndSubjectButNotBody` |

## 8. Schema and wiring tests
| ID | Test | Protects |
|---|---|---|
| SHD1 | `AuditFieldsMigrationTests.Migrate_MenuTablesWithRows_MenusKeepTheirData` | The audit-fields migration keeps menu data |
| SHD2 | `AuditFieldsMigrationTests.Migrate_LocationMenusInTwoOrganizations_OrganizationIdBackfilledFromTheirMenu` | Location links get their menu's organization |
| SHD3 | `AuditFieldsMigrationTests.Migrate_LocationMenusWithRows_UpdatedAtBackfilledFromCreatedAtAndDataKept` | Location links keep their data |
| SHD4 | `AuditFieldsMigrationTests.Migrate_MenuCategoriesWithRows_UpdatedAtBackfilledFromCreatedAtAndDataKept` | Categories keep their data |
| SHD5 | `AuditFieldsMigrationTests.Migrate_MenuTablesWithRows_NewColumnsEndUpNotNull` | New audit columns end up filled |
| SHD6 | `AuditFieldsMigrationTests.Migrate_MenuTablesWithoutRows_Succeeds` | The migration runs on empty tables |
| SHD7 | `AuditFieldsMigrationTests.Migrate_OrganizationTablesWithRows_RowsKeepTheirDataWithNullAuditUsers` | Organization data kept |
| SHD8 | `AuditFieldsMigrationTests.Migrate_UserRolesWithRows_RowsKeepTheirDataWithNullAuditUsers` | Role data kept |
| SHD9 | `AuditFieldsMigrationTests.Migrate_AllMenuMigrations_ModelHasNoPendingChanges` | Menu database matches the code |
| SHD10 | `AuditFieldsMigrationTests.Migrate_AllOrganizationMigrations_ModelHasNoPendingChanges` | Organization database matches the code |
| SHD11 | `AuditFieldsMigrationTests.Migrate_AllIdentityMigrations_ModelHasNoPendingChanges` | Identity database matches the code |
| SHD12 | `AuditInterceptorWiringTests.IdentityDbContext_Resolved_HasAuditInterceptorAttached` | Identity writes go through the organization and audit checks |
| SHD13 | `AuditInterceptorWiringTests.MenuDbContext_Resolved_HasAuditInterceptorAttached` | Menu writes go through them |
| SHD14 | `AuditInterceptorWiringTests.OrganizationDbContext_Resolved_HasAuditInterceptorAttached` | Organization writes go through them |
| SHD15 | `AuditInterceptorWiringTests.ICurrentUser_Resolved_IsClaimsCurrentUser` | The current user is read from the token |
| SHD16 | `AuditInterceptorWiringTests.ITenantScope_ResolvedFromTwoRequestScopes_IsTheSameInstance` | One organization scope serves the whole app |
| SHD17 | `AuditLogStorageTests.Migrate_AuditLogMigrations_CreatesOrganizationAndTimeIndex` | The audit log is indexed by organization and time |
| SHD18 | `AuditLogStorageTests.Migrate_AuditLogMigrations_ModelHasNoPendingChanges` | The audit log database matches the code |

## Related findings (tracked elsewhere)
- The map gives Manager "manage menus" (SH1), but Menu still checks Owner or Admin by role name
  (Menu M1), so a Manager cannot manage menus: `backlog/improvements/2026-10-15-apply-permission-policies-to-endpoints.md`.
