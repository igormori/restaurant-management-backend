# Web security matrix

The record of every rule the Web project (the app shell that hosts every module) must keep, and
the test that proves each one: how sign-in tokens are checked and refused, how permissions are
granted, HTTPS, secrets and startup checks, and the shape of error answers. The pipeline
(`.github/workflows/dotnet.yml`) runs every test named here, and
`.claude/scripts/check-security-matrices.py` fails the build when this file and the tests drift
apart.

## Rules for this file
The rules in the root `CLAUDE.md`, "Security matrices", apply: any change to Web updates this file
in the same commit; new behaviour adds ❌ rows before it is built; no row is ✅ without a named
test; every test in `tests/RestaurantManagement.Web.Tests/Web/` is named here and carries
`[Trait("Rule", "<ID>")]`; row IDs start with W (rules) or WD (wiring) and are never reused.

**Status:** ✅ covered by the named test · ⚠️ partly covered (the Expected column says what is
missing) · ❌ no test yet · 🐞 known bug, see the backlog item

**Where the tests live:** `tests/RestaurantManagement.Web.Tests/Web/`. Rules that Web enforces
for one module (rate limits on sign-in endpoints, the refresh cookie) are in that module's matrix.

## 1. Refusals and their answers
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| W1 | No access token on a protected endpoint | 401 with the translated "unauthorized" message as JSON, in the caller's language | ✅ | `PrivateByDefaultTests.UndeclaredEndpoint_NoAccessToken_ReturnsStandardUnauthorizedMessage`, `PrivateByDefaultTests.UndeclaredEndpoint_NoAccessTokenInPortuguese_ReturnsPortugueseUnauthorizedMessage` |
| W2 | Forged or expired access token | 401 with the translated "invalid token" message as JSON; never says why the token failed | ✅ | `InvalidTokenMessageTests.ProtectedEndpoint_TokenSignedWithAnotherKey_ReturnsInvalidTokenMessageInEnglish`, `InvalidTokenMessageTests.ProtectedEndpoint_TokenSignedWithAnotherKeyInPortuguese_ReturnsInvalidTokenMessageInPortuguese`, `InvalidTokenMessageTests.ProtectedEndpoint_ExpiredToken_ReturnsInvalidTokenMessageWithoutSayingItExpired` |
| W3 | Signed in but the stored access does not allow it | 403 with the translated "access denied" message as JSON | ✅ | `AccessDeniedMessageTests.MembersManagePolicy_WaiterWithoutMembersAccess_ReturnsAccessDeniedMessageInEnglish`, `AccessDeniedMessageTests.MembersManagePolicy_WaiterWithoutMembersAccessInPortuguese_ReturnsAccessDeniedMessageInPortuguese` |

## 2. Who may call what
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| W4 | Endpoint that declares no access rule | Private by default: refused without sign-in, open to any signed-in user; the public sign-in endpoints stay open | ✅ | `PrivateByDefaultTests.UndeclaredEndpoint_NoAccessToken_ReturnsUnauthorized`, `PrivateByDefaultTests.UndeclaredEndpoint_SignedInUser_ReturnsOk`, `PrivateByDefaultTests.PublicAuthEndpoint_NoAccessToken_IsNotRefusedAsUnauthorized` |
| W5 | Permission endpoint without a token | 401 | ✅ | `PermissionPolicyTests.MembersManagePolicy_NoAccessToken_ReturnsUnauthorized` |
| W6 | No organization chosen, even for an Owner | 403: access only counts inside the chosen organization | ✅ | `PermissionPolicyTests.MembersManagePolicy_OwnerButNoOrganizationSelected_ReturnsForbidden` |
| W7 | Organization chosen but no stored access in it, or the level at only some of its non-closed locations | 403 | ✅ | `PermissionPolicyTests.Policy_GridWithoutTheLevelAtEveryLocation_ReturnsForbidden` |
| W8 | A lower level than the policy needs (Read for a Manage policy), or a level in another module only | 403 | ✅ | `PermissionPolicyTests.Policy_GridWithoutTheLevelAtEveryLocation_ReturnsForbidden` |
| W9 | The Owner, or the needed level or more at every non-closed location of the chosen organization | Allowed; a policy is named after its module and level (for example `reports.read`) | ✅ | `PermissionPolicyTests.Policy_OwnerOrLevelAtEveryNonClosedLocation_ReturnsOk` |
| W10 | Every module and level of the access grid | Has a policy an endpoint can require (14: seven modules, Read and Manage) | ✅ | `PermissionCatalogPolicyTests.PolicyProvider_EveryModuleAndReadAndManage_HasAPolicyNamedModuleDotLevel` |
| W11 | The signed-in user and organization | Read only from the token; a missing or malformed ID counts as absent | ✅ | `ClaimsCurrentUserTests.UserId_NoHttpContext_IsNull`, `ClaimsCurrentUserTests.OrganizationId_NoHttpContext_IsNull`, `ClaimsCurrentUserTests.UserId_NameIdentifierClaimIsNotAGuid_IsNull`, `ClaimsCurrentUserTests.OrganizationId_OrganizationClaimIsNotAGuid_IsNull`, `ClaimsCurrentUserTests.OrganizationId_NoOrganizationClaim_IsNull`, `ClaimsCurrentUserTests.UserId_ValidNameIdentifierClaim_ReturnsTheUserId`, `ClaimsCurrentUserTests.OrganizationId_ValidOrganizationClaim_ReturnsTheOrganizationId` |

## 3. HTTPS
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| W12 | Outside Development, no certificate path | App refuses to start, naming `Https:CertificatePath` | ✅ | `HttpsHostingExtensionsTests.UseHttpsOnlyOutsideDevelopment_ProductionWithoutCertificate_ThrowsNamingCertificatePath`, `StartupConfigurationTests.Startup_NoCertificateInProduction_FailsNamingHttpsCertificatePath` |
| W13 | Certificate file missing | App refuses to start, naming the setting | ✅ | `HttpsHostingExtensionsTests.UseHttpsOnlyOutsideDevelopment_ProductionWithCertificateFileMissing_ThrowsNamingCertificatePath` |
| W14 | Outside Development | Only HTTPS is served; plain HTTP is refused, even when HTTP addresses are configured | ✅ | `HttpsHostingExtensionsTests.UseHttpsOnlyOutsideDevelopment_ProductionWithCertificate_ServesHttps`, `HttpsHostingExtensionsTests.UseHttpsOnlyOutsideDevelopment_ProductionWithCertificate_RefusesPlainHttpOnSamePort`, `HttpsHostingExtensionsTests.UseHttpsOnlyOutsideDevelopment_ProductionWithHttpUrlsConfigured_DoesNotServePlainHttp` |
| W15 | No port configured | Listens on 8443 | ✅ | `HttpsHostingExtensionsTests.UseHttpsOnlyOutsideDevelopment_ProductionWithoutPort_ListensOnPort8443` |
| W16 | Development | Starts without a certificate | ✅ | `HttpsHostingExtensionsTests.UseHttpsOnlyOutsideDevelopment_DevelopmentWithoutCertificate_StartsWithoutThrowing` |

## 4. Secrets and startup checks
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| W17 | Key Vault address | Must be an absolute https address, else the app refuses to start; none set adds no secret source | ✅ | `KeyVaultHostingExtensionsTests.AddKeyVaultSecrets_UriNotAbsoluteHttps_ThrowsNamingKeyVaultUri`, `KeyVaultHostingExtensionsTests.AddKeyVaultSecrets_UriNotSet_AddsNoConfigurationSource` |
| W18 | Token signing key missing, under 32 bytes, or the placeholder in production; token lifetime not positive | App refuses to start, naming the setting | ✅ | `StartupConfigurationTests.Startup_JwtKeyMissingOrUnder32Bytes_FailsNamingJwtKey`, `StartupConfigurationTests.Startup_PlaceholderJwtKeyInProduction_FailsNamingJwtKey`, `StartupConfigurationTests.Startup_NonPositiveJwtExpireMinutes_FailsNamingJwtExpireMinutes` |
| W19 | Code fingerprint key missing or under 32 bytes | App refuses to start, naming the setting | ✅ | `StartupConfigurationTests.Startup_VerificationHashKeyEmpty_FailsNamingHashKey`, `StartupConfigurationTests.Startup_VerificationHashKeyEmptyInProduction_FailsNamingHashKey`, `StartupConfigurationTests.Startup_VerificationHashKeyOf31Bytes_FailsNamingHashKey` |
| W20 | Error tracking (Sentry) | Sample rate outside 0–1: app refuses to start, naming the setting. Unset in production: 0.1. A configured rate is used, 0 included. Debug output off in production | ✅ | `StartupConfigurationTests.Startup_SentryTracesSampleRateInvalid_FailsNamingSentryTracesSampleRate`, `StartupConfigurationTests.Startup_SentryTracesSampleRateUnsetInProduction_DefaultsTo0Point1`, `StartupConfigurationTests.Startup_SentryTracesSampleRateConfiguredInProduction_UsesConfiguredValue`, `StartupConfigurationTests.Startup_SentryTracesSampleRateConfiguredInDevelopment_UsesConfiguredValue`, `StartupConfigurationTests.Startup_SentryTracesSampleRateAtLowerBoundary_AcceptsZero`, `StartupConfigurationTests.Startup_SentryDebugInProduction_IsOff` |
| W21 | API documentation page (Swagger) | Served only in Development; not served in production: 401 (private by default, so the document is refused without sign-in) | ✅ | `SwaggerAvailabilityTests.SwaggerDocument_InDevelopment_IsServed`, `SwaggerAvailabilityTests.SwaggerDocument_InProduction_IsNotServed` |

## 5. Answers to callers
| ID | Case | Expected | Status | Test |
|---|---|---|---|---|
| W22 | Invalid request | 400 with a translated title; status and field errors kept | ✅ | `ValidationTitleEndpointTests.Register_InvalidRequest_ReturnsLocalizedValidationTitle`, `ValidationTitleEndpointTests.Register_InvalidRequest_KeepsStatusAndFieldErrorsInBody` |
| W23 | Browser request from an allowed origin | May send credentials (cookies) | ✅ | `RefreshTokenCookieEndpointTests.Cors_PreflightFromAllowedOrigin_AllowsCredentials` |
| W24 | Browser request from any other origin | Gets no cross-origin permission | ✅ | `CorsUnknownOriginTests.Cors_PreflightFromUnknownOrigin_GetsNoCrossOriginPermission` |

## 6. Wiring tests
These protect the app setup rather than a rule users meet.

| ID | Test | Protects |
|---|---|---|
| WD1 | `StartupConfigurationTests.Startup_ValidVerificationHashKey_BindsHashKeyFromConfiguration` | The code fingerprint key is read from configuration |
| WD2 | `StartupConfigurationTests.Startup_DefaultConfiguration_RegistersVerificationCodeHasher` | The code fingerprint service is registered |
| WD3 | `StartupConfigurationTests.Startup_DefaultConfiguration_JwtExpireMinutesIs15` | The access token lifetime binds from configuration (rule: Identity L19) |
