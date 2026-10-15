# Plan: Auth rate limiting
Spec: docs/specs/identity/auth-rate-limiting.md

## Approach
Web registers the built-in rate limiter with one fixed-window policy per auth endpoint, partitioned by client IP.
AuthController opts each action in by policy name; forwarded headers resolve the client IP first.
The rejection handler writes 429, Retry-After and a localized message, and reports once per IP, endpoint and window.

## Decisions
- One `AuthRateLimit` config section holds the five limits and `TrustedProxies`. (confirm)
- Windows are integer seconds (`WindowSeconds`), like `ResendCooldownSeconds`.
- Sentry dedup uses `IMemoryCache` keyed by policy and IP, expiring at Retry-After.
- Integration tests substitute Identity services; no database or Testcontainers needed. (confirm)
- A missing remote IP falls into one shared `unknown` partition.

## Contract
- API: `POST /api/auth/{login,register,refresh,verify,resend-verification}` -> unchanged | adds 429.
- Data: No change. No migration.
- Auth: All callers limited per IP. No OrganizationId scoping, per spec.

## Changes
| File | What changes |
|---|---|
| (new) `Shared/Options/AuthRateLimitOptions.cs` | Five window settings plus `TrustedProxies` list. |
| (new) `Shared/Options/AuthRateLimitOptionsValidator.cs` | `IValidateOptions`: positive limits/windows, parsable IP or CIDR. |
| (new) `Shared/RateLimiting/AuthRateLimitPolicies.cs` | Policy name constants for the five endpoints. |
| `Shared/Localization/SharedResource.resx` + `.pt.resx` | Add `TooManyRequests` message. |
| `Identity/Controllers/AuthController.cs` | `[EnableRateLimiting]` with matching policy on each action. |
| (new) `Web/RateLimiting/AuthRateLimitingExtensions.cs` | Registers options, forwarded headers, limiter policies, rejection handler. |
| (new) `Web/RateLimiting/IRateLimitRejectionReporter.cs` + `RateLimitRejectionReporter.cs` | Deduplicated Sentry warning via `IHub`; endpoint and IP tags only. |
| `Web/Program.cs` | Call extension; `UseForwardedHeaders` first, `UseRateLimiter` before authentication; `public partial class Program`. |
| `Web/appsettings.json` | `AuthRateLimit` section with spec defaults, empty `TrustedProxies`. |
| (new) `tests/RestaurantManagement.Web.Tests/` | Project, `AuthApiFactory`, `TestRemoteIpStartupFilter`, `AuthRateLimitingTests`. |
| (new) `tests/RestaurantManagement.Shared.Tests/AuthRateLimitOptionsValidatorTests.cs` | Validator tests. |
| `RestaurantManagement.sln` | Add Web.Tests project. |

## Tasks

### Commit 1: Add Web integration test harness
1. [x] Create Web.Tests with Mvc.Testing, xUnit, FluentAssertions, NSubstitute; add to sln — `tests/RestaurantManagement.Web.Tests/*.csproj`, `RestaurantManagement.sln`
2. [x] Add `public partial class Program`, `AuthApiFactory` substituting Identity services, IP startup filter — `Web/Program.cs`, `AuthApiFactory.cs`, `TestRemoteIpStartupFilter.cs` -> verify: `dotnet test`

### Commit 2: Add auth rate limit configuration with startup validation
3. [x] Add options, validator, policy constants, validator tests — `Shared/Options/*`, `Shared/RateLimiting/AuthRateLimitPolicies.cs`, `AuthRateLimitOptionsValidatorTests.cs`
4. [x] Bind with `ValidateOnStart`, add appsettings defaults; factory sets high test limits — `AuthRateLimitingExtensions.cs`, `Program.cs`, `appsettings.json`, `AuthApiFactory.cs` -> verify: app fails to start with `Login:PermitLimit=0`

### Commit 3: Rate limit auth endpoints per client IP
5. [x] Register forwarded headers trusting only `TrustedProxies` — `AuthRateLimitingExtensions.cs`, `Program.cs`
6. [x] Register five fixed-window IP policies and annotate actions — `AuthRateLimitingExtensions.cs`, `AuthController.cs`
7. [x] Add rejection handler: 429, Retry-After seconds, localized message — `AuthRateLimitingExtensions.cs`, both resx files
8. [x] Add deduplicated Sentry reporter; call it from the handler — `Web/RateLimiting/*Reporter.cs`, `AuthRateLimitingExtensions.cs`
9. [x] Add integration tests below — `AuthRateLimitingTests.cs` -> verify: `dotnet test`, `dotnet format`

## Tests
| Acceptance criterion (short) | Test name |
|---|---|
| Over limit -> 429 with Retry-After | `Login_LimitExceeded_Returns429WithRetryAfter` |
| Localized message | `Login_LimitExceededWithPortugueseCulture_ReturnsLocalizedMessage` |
| Other endpoints unaffected | `Register_LoginLimitExhausted_ReturnsOk` |
| Other IPs unaffected | `Login_OtherIpExhaustedLimit_ReturnsOk` |
| Untrusted X-Forwarded-For ignored | `Login_ForwardedForFromUntrustedSender_PartitionsByRemoteIp` |
| Trusted X-Forwarded-For used | `Login_ForwardedForFromTrustedProxy_PartitionsByForwardedIp` |
| Invalid config fails startup | `Validate_NonPositiveLimitWindowOrMalformedProxy_Fails` |
| No lockout effect | `Login_RateLimited_DoesNotCallLoginService` |
| One Sentry warning per window | `Login_RepeatedRejectionsInWindow_SendsOneSentryWarning` |
| No body, email or token in event | `Login_Rejected_SentryEventHasOnlyEndpointAndIp` |
| Within limit unchanged | `Login_WithinLimit_ReturnsServiceResponse` |
| Config change takes effect | `Login_ConfiguredLimitOverride_RejectsAfterConfiguredCount` |

## Follow-ups
- Sentry `Debug = true` and `TracesSampleRate = 1.0` apply in every environment.
- `ExceptionHandlingMiddleware` returns `{ error }`; other errors return `{ message }`.
- `respoonse` typo in `AuthController.ResendVerification`.
