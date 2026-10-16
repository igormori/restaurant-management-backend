# Scenarios: Localize All Client-Facing Messages

**Plan:** docs/plans/shared/2026-10-15-localize-client-messages/plan.md
**Coverage:** 23 scenarios — 1 happy · 4 edge · 13 failure · 2 security · 3 guard

Languages: en = English, pt = Portuguese. Each "en/pt" scenario is one test run with both languages.
Guard rows: a few English rows already pass today because their English text does not change.
They act as guards inside a Failure test, and the Portuguese rows of the same test fail until the
task is done. These rows are: #4 en, #9 en, #15 both en rows, #16 en.

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Happy | Each new message (menu not found, menu category not found, phone, unexpected error, hex color, currency, number range, 2 decimals, HH:mm time, location type, letters and digits, time zone, web address) reads as real English and Portuguese text, never its code | `Localizer_NewMessageKey_ReturnsExpectedTextInEachLanguage` | `tests/RestaurantManagement.Shared.Tests/Localization/SharedResourceCatalogTests.cs` |
| 2 | T1 | Edge | Brazilian Portuguese (pt-BR) gets the Portuguese text; an unsupported language (French) gets English | `Localizer_RegionalOrUnsupportedCulture_ReturnsPortugueseOrEnglishFallback` | `tests/RestaurantManagement.Shared.Tests/Localization/SharedResourceCatalogTests.cs` |
| 3 | T1 | Guard | Existing messages ("Unauthorized user.", required field, maximum length) keep today's text in both languages | `Localizer_ExistingMessageKey_KeepsTodaysText` | `tests/RestaurantManagement.Shared.Tests/Localization/SharedResourceCatalogTests.cs` |
| 4 | T2 | Failure | An unexpected server failure answers 500 with "Ocorreu um erro inesperado." in pt and the English text in en | `InvokeAsync_UnexpectedExceptionInRequestCulture_ReturnsLocalizedUnexpectedErrorMessage` | `tests/RestaurantManagement.Shared.Tests/Middleware/ExceptionHandlingMiddlewareLocalizationTests.cs` |
| 5 | T2 | Guard | Outside Development, the 500 answer never shows the failure's internal details | `InvokeAsync_UnexpectedExceptionInProduction_HidesExceptionDetail` | `tests/RestaurantManagement.Shared.Tests/Middleware/ExceptionHandlingMiddlewareLocalizationTests.cs` |
| 6 | T2 | Failure | A real login request that fails unexpectedly, sent in Portuguese, gets the 500 with the Portuguese message | `Login_UnexpectedFailureInPortuguese_ReturnsServerErrorWithPortugueseMessage` | `tests/RestaurantManagement.Web.Tests/UnexpectedErrorEndpointTests.cs` |
| 7 | T3 | Failure | Editing a menu that does not exist answers 404 with "Menu não encontrado." (pt) or "Menu not found." (en), never `MenuNotFound` | `EditMenu_MenuDoesNotExist_ReturnsNotFoundWithLocalizedMessage` | `tests/RestaurantManagement.Web.Tests/MenuMessagesEndpointTests.cs` |
| 8 | T3 | Security | Reading, editing or deleting another organization's menu category answers 404 with the translated "menu category not found" | `MenuCategoryById_CategoryOfAnotherOrganization_ReturnsNotFoundWithLocalizedMessage` | `tests/RestaurantManagement.Web.Tests/MenuMessagesEndpointTests.cs` |
| 9 | T4 | Failure | Creating a menu without a name gives the "name is required" error in the requested language | `CreateMenu_NameMissing_ReturnsLocalizedRequiredError` | `tests/RestaurantManagement.Web.Tests/MenuMessagesEndpointTests.cs` |
| 10 | T4 | Edge | Editing a menu with a name over 100 characters gives the "maximum 100 characters" error in the requested language | `EditMenu_NameOver100Characters_ReturnsLocalizedMaxLengthError` | `tests/RestaurantManagement.Web.Tests/MenuMessagesEndpointTests.cs` |
| 11 | T4 | Failure | Creating a menu category without a name gives the "name is required" error in the requested language | `CreateMenuCategory_NameMissing_ReturnsLocalizedRequiredError` | `tests/RestaurantManagement.Web.Tests/MenuMessagesEndpointTests.cs` |
| 12 | T4 | Edge | Editing a menu category with a description over 1000 characters gives the "maximum 1000 characters" error in the requested language | `UpdateMenuCategory_DescriptionOver1000Characters_ReturnsLocalizedMaxLengthError` | `tests/RestaurantManagement.Web.Tests/MenuMessagesEndpointTests.cs` |
| 13 | T5 | Failure | Registering with a phone number containing letters gives the phone error in the requested language | `Register_InvalidPhoneNumber_ReturnsLocalizedPhoneError` | `tests/RestaurantManagement.Web.Tests/IdentityMessagesEndpointTests.cs` |
| 14 | T5 | Failure | Logging in with a missing email, a badly formed email or a password under 8 characters gives the matching field error in the requested language | `Login_InvalidField_ReturnsLocalizedFieldError` | `tests/RestaurantManagement.Web.Tests/IdentityMessagesEndpointTests.cs` |
| 15 | T6 | Failure | An invalid brand color when creating or editing an organization gives the hex color error in the requested language | `OrganizationRequest_InvalidColor_ReturnsLocalizedHexColorError` | `tests/RestaurantManagement.Web.Tests/OrganizationMessagesEndpointTests.cs` |
| 16 | T6 | Failure | Creating an organization without a name gives the "name is required" error in the requested language | `CreateOrganization_NameMissing_ReturnsLocalizedRequiredError` | `tests/RestaurantManagement.Web.Tests/OrganizationMessagesEndpointTests.cs` |
| 17 | T6 | Failure | In Portuguese, an unsupported currency (EUR), an invalid logo web address, a service charge of 150%, a discount limit with 3 decimals and a day cutoff of 25:00 each give the Portuguese field error | `OrganizationRequest_InvalidSettingInPortuguese_ReturnsPortugueseFieldError` | `tests/RestaurantManagement.Web.Tests/OrganizationMessagesEndpointTests.cs` |
| 23 | T6 | Edge | Editing an organization with a name over 200 characters (pt) or a description over 1000 characters (en) gives the maximum-length error in the requested language | `EditOrganization_TextOverMaximumLength_ReturnsLocalizedMaxLengthError` | `tests/RestaurantManagement.Web.Tests/OrganizationMessagesEndpointTests.cs` |
| 18 | T7 | Failure | Creating or editing a location with a missing name, an unknown type, a code with a dash, an unknown time zone or a postal code over 20 characters gives the field error in the requested language | `LocationRequest_InvalidField_ReturnsLocalizedFieldError` | `tests/RestaurantManagement.Web.Tests/LocationMessagesEndpointTests.cs` |
| 19 | T8 | Security | A correctly signed sign-in pass whose user identifier is not a user ID gets 401 (not 500) with "Usuário não autorizado!" (pt) or "Unauthorized user." (en) on listing, creating, editing and deleting locations | `LocationEndpoints_UserIdentifierNotAUserId_ReturnsUnauthorizedWithLocalizedMessage` | `tests/RestaurantManagement.Web.Tests/LocationMessagesEndpointTests.cs` |
| 20 | T9 | Guard | The English and Portuguese catalogs hold exactly the same messages and none is empty | `Catalog_EnglishAndPortuguese_HoldSameKeysWithText` | `tests/RestaurantManagement.Web.Tests/MessageCatalogCompletenessTests.cs` |
| 21 | T9 | Failure | Every field rule on every request in every module uses a message from the catalog; the check lists each field that does not | `RequestModels_ValidationAttributes_UseCatalogKeys` | `tests/RestaurantManagement.Web.Tests/MessageCatalogCompletenessTests.cs` |
| 22 | T9 | Failure | Every message the code looks up exists in the catalog; the check lists each missing one and where it is used | `SourceCode_LocalizerLookups_ExistInCatalog` | `tests/RestaurantManagement.Web.Tests/MessageCatalogCompletenessTests.cs` |

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 "Menu not found" is readable | 1, 2, 7, 22 |
| #2 "Menu category not found" is translated | 1, 8 |
| #3 Phone format error is translated | 1, 13 |
| #4 Menu category field errors are translated | 11, 12 |
| #5 Organization color errors are translated | 1, 15 |
| #6 Previously untranslated field errors are translated | 9, 10, 14, 16, 17, 18, 21, 23 (organization selection: scenario 21 only, see note) |
| #7 Unexpected-error message is translated | 1, 4, 5, 6 |
| #8 Invalid user identifier gives 401 | 19 |
| #9 No message is missing in either language | 20, 21, 22 |

Notes:
- Organization selection: its only rule is "organization ID is required" on an ID field that is
  never empty after reading the request, so the translated error cannot be produced through the
  endpoint. Scenario 21 proves the rule uses a catalog message. Backlog:
  `backlog/bugs/2026-10-15-select-organization-missing-id-not-rejected.md`.
  **Decision (Gate 2):** option A, accepted by the user: scenario 21 is the proof for this
  case; the endpoint fix is deferred to the backlog item.
- T8 cross-tenant check (another organization's location returns 404) is already covered by the
  existing `OrganizationTenantEndpointTests.LocationById_LocationOfAnotherOrganization_ReturnsNotFoundAndLeavesItUnchanged`.
- T5 "login credential failures still return the same generic 401" is already covered by the
  existing `AuthUniformResponseTests` and `SessionServiceLoginTests`.

## Existing tests updated
- `tests/RestaurantManagement.Shared.Tests/ExceptionHandlingMiddlewareTests.cs`: the test request
  now carries the localization services, because the middleware reads the message catalog from
  the request (T2). Assertions unchanged.
- `tests/RestaurantManagement.Web.Tests/TestAccessTokens.cs`: new `CreateWithSubject` signs a pass
  with any user identifier text (needed by scenario 19); the existing `Create` now calls it, same
  claims as before.

## Helpers added
- `tests/RestaurantManagement.Web.Tests/LocalizedResponses.cs`: sends a request with an
  `Accept-Language` header and reads `message` or the field errors from the JSON body.
