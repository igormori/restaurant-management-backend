# Plan: Auth hardening
Spec: docs/specs/identity/auth-hardening.md

## Approach
Startup checks: a JWT options validator with ValidateOnStart, plus two Web hosting extensions (Sentry, HTTPS-only Kestrel) that throw while building. SessionService adds a locked check to the single generic 401, and sends the lockout message through an injected IHub with the user ID. Configuration defaults, the resx cleanup and the MailKit bump finish it.

## New files
| Module | Folder | File | Purpose |
|---|---|---|---|
| Shared | Options | JwtOptionsValidator.cs | Key length, placeholder outside Development, ExpireMinutes > 0 |
| Shared | Options | HttpsOptions.cs | Port, CertificatePath, CertificatePassword bound from `Https` section |
| Web | Hosting | SentryHostingExtensions.cs | Debug only in Development; validated TracesSampleRate with environment default |
| Web | Hosting | HttpsHostingExtensions.cs | Outside Development: Kestrel HTTPS-only listener; missing certificate throws |
| Shared.Tests | (root) | JwtOptionsValidatorTests.cs | Validator rules, environment substituted |
| Web.Tests | (root) | StartupConfigurationTests.cs | Startup failures; Sentry Debug and rate per environment |
| Web.Tests | (root) | HttpsHostingExtensionsTests.cs | Real Kestrel, test certificate: HTTPS served, HTTP refused |

## Modified files
| Module | Folder | File | What changes |
|---|---|---|---|
| Web | (root) | Program.cs | Call both extensions; AddOptions JwtOptions with validator, ValidateOnStart |
| Web | (root) | appsettings.json | ExpireMinutes 15; empty `Https` section with injection comment |
| Shared | Options | JwtOptions.cs | ExpireMinutes default 15; PlaceholderKey constant |
| Shared | (root) | RestaurantManagement.Shared.csproj | MailKit to latest version without advisory |
| Shared | Localization | SharedResource.resx, SharedResource.pt.resx | Remove AccountLockedUntil |
| Identity | Services | SessionService.cs | Inject IHub; locked joins generic 401; message uses user ID |
| Identity | (root) | CLAUDE.md | Line 10: locked account now gets the generic 401 |
| Identity.Tests | (root) | SessionServiceLoginTests.cs | Replace 423 test; lockout message test; pass IHub |
| Identity.Tests | (root) | SessionService{Verification,Refresh}Tests.cs, AuthUniformResponseTests.cs | Pass IHub substitute to constructor |

## Signatures
```csharp
// Shared/Options/JwtOptions.cs (added)
public const string PlaceholderKey = "your-super-secret-key-at-least-32-characters-long-for-development";
// Shared/Options/JwtOptionsValidator.cs
public class JwtOptionsValidator : IValidateOptions<JwtOptions>
    public JwtOptionsValidator(IHostEnvironment environment)
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
// Shared/Options/HttpsOptions.cs
public class HttpsOptions { int Port; string CertificatePath; string CertificatePassword }  // { get; set; }
// Web/Hosting/SentryHostingExtensions.cs
public static class SentryHostingExtensions
    public static WebApplicationBuilder AddSentryTracking(this WebApplicationBuilder builder)
// Web/Hosting/HttpsHostingExtensions.cs
public static class HttpsHostingExtensions
    public static WebApplicationBuilder UseHttpsOnlyOutsideDevelopment(this WebApplicationBuilder builder)
// Identity/Services/SessionService.cs (constructor gains IHub)
public SessionService(IdentityDbContext db, IOptions<SecurityOptions> securityOptions,
    IOptions<JwtOptions> jwtOptions, IStringLocalizer<SharedResource> localizer, IHub hub)
```

## Patterns followed
- Validator plus ValidateOnStart: AuthRateLimitOptionsValidator.cs, AuthRateLimitingExtensions.cs.
- Web builder extension per concern: Web/RateLimiting/AuthRateLimitingExtensions.cs.
- IHub injection for testable Sentry events: RateLimitRejectionReporter.cs.
- Startup tests via WebApplicationFactory overrides: Web.Tests/AuthRateLimitingTests.cs.

## Flags
- New Web folder `Hosting/`; Web has no module folders.
- Tests use FluentAssertions (user decision); no Shouldly.
- Locked login still verifies the password and skips failed-attempt counting, so timing matches.
- PlaceholderKey duplicates the appsettings.json string; the two must stay in sync.
- HTTPS-only uses explicit Kestrel Listen, overriding ASPNETCORE_URLS outside Development.
- HTTPS port default 8443 when unset (API runs in Docker, non-root container).
- Sentry rate validated eagerly while building, not via ValidateOnStart, since UseSentry needs it.
- MailKit version picked at implementation via `dotnet list package --vulnerable`.
- SessionService keeps its `> 0 ? : 60` expiry fallback; validator makes it unreachable.
- Identity CLAUDE.md edit approved by the user.

## Follow-ups
- VerificationService still returns a distinct 423 with unlock time, revealing account existence.
- SessionService line 95 has a stray `;;`.
- OrganizationService line 64 throws on name TEST_SENTRY in every environment.

## Scenarios
Tests use FluentAssertions (user decision). Test files: Shared.Tests/JwtOptionsValidatorTests.cs (1-9), Identity.Tests/SessionServiceLoginTests.cs (10-13), Web.Tests/StartupConfigurationTests.cs (14-23), Web.Tests/HttpsHostingExtensionsTests.cs (24-30). Helper: Web.Tests/TestHttpsCertificate.cs.

| # | Scenario | Given | When | Then |
|---|---|---|---|---|
| 1 | Empty Jwt key fails | Development, Key "" | Validate | Failed; message contains `Jwt:Key` |
| 2 | 31-byte key fails | Development, Key 31 x 'k' | Validate | Failed; message contains `Jwt:Key` |
| 3 | Placeholder key fails in Production | Production, Key = JwtOptions.PlaceholderKey | Validate | Failed; message contains `Jwt:Key` |
| 4 | Placeholder key fails in Staging | Staging, Key = PlaceholderKey | Validate | Failed; message contains `Jwt:Key` |
| 5 | Non-positive ExpireMinutes fails | Production, strong key, ExpireMinutes 0 and -5 | Validate | Failed; message contains `Jwt:ExpireMinutes` |
| 6 | Placeholder key allowed in Development | Development, Key = PlaceholderKey | Validate | Succeeded |
| 7 | Exactly 32-byte key passes | Production, Key 32 x 'k' | Validate | Succeeded |
| 8 | Length counted in UTF-8 bytes | Production, Key 17 x 'é' (17 chars, 34 bytes) | Validate | Succeeded |
| 9 | Valid Production options pass | Production, 34-char key, ExpireMinutes 15 | Validate | Succeeded |
| 10 | Locked account, correct password, same 401 as wrong password | locked@test.com locked 15 more min; verified@test.com unlocked | Login locked with correct password; login verified with wrong password | Both throw BusinessException 401, message `InvalidEmailOrPassword`, identical |
| 11 | Locked account, wrong password: no counting, lock unchanged | Locked 10 min, FailedAttempts 0, max attempts 1 | Login with wrong password | 401; FailedAttempts still 0; LockedUntil unchanged (within 1 s) |
| 12 | Lockout Sentry message has user ID, no email | marina.souza@test.com, max attempts 1, IHub substitute | Login with wrong password | One captured event; message contains user.Id; does not contain the email |
| 13 | Default access token lifetime 15 min | JwtOptions without ExpireMinutes set | Login with valid credentials | Token ValidTo about now + 15 min (+-1 min) |
| 14 | Startup fails on missing or short Jwt key | Development, Jwt:Key "" or 31 bytes | Create host | Startup exception mentioning `Jwt:Key` |
| 15 | Startup fails on placeholder key in Production | Production, valid cert, Jwt:Key = placeholder | Create host | Startup exception mentioning `Jwt:Key` |
| 16 | Startup fails on non-positive ExpireMinutes | Development, Jwt:ExpireMinutes 0 or -3 | Create host | Startup exception mentioning `Jwt:ExpireMinutes` |
| 17 | Startup fails on out-of-range Sentry rate | Development, Sentry:TracesSampleRate 1.5 or -0.1 | Create host | Startup exception mentioning `Sentry:TracesSampleRate` |
| 18 | Sentry Debug off in Production | Production, valid config | Resolve SentryAspNetCoreOptions | Debug false |
| 19 | Sentry rate defaults to 0.1 outside Development | Production, rate unset | Resolve options | TracesSampleRate 0.1 |
| 20 | Sentry rate read from config in Production | Production, rate "0.5" | Resolve options | TracesSampleRate 0.5 |
| 21 | Sentry rate read from config in Development | Development, rate "0.25" | Resolve options | TracesSampleRate 0.25 |
| 22 | Sentry rate lower boundary accepted | Development, rate "0" | Resolve options | TracesSampleRate 0.0, no startup failure |
| 23 | Default access token lifetime in config | Development, no overrides | Resolve JwtOptions | ExpireMinutes 15 (appsettings.json) |
| 24 | HTTPS extension: no certificate fails | Production, no Https settings | UseHttpsOnlyOutsideDevelopment | InvalidOperationException mentioning `Https:CertificatePath` |
| 25 | HTTPS extension: certificate file missing fails | Production, CertificatePath to a file that does not exist | UseHttpsOnlyOutsideDevelopment | InvalidOperationException mentioning `Https:CertificatePath` |
| 26 | Development needs no certificate | Development, no Https settings | Extension, then start | No exception |
| 27 | Production serves HTTPS | Production, test certificate, free port | GET https://127.0.0.1:port/ping | 200 "pong" |
| 28 | Production refuses plain HTTP on the HTTPS port | Same as 27 | GET http://127.0.0.1:port/ping | HttpRequestException |
| 29 | ASPNETCORE_URLS http ignored in Production | Production, certificate, `urls` = http://127.0.0.1:otherPort | GET http://127.0.0.1:otherPort/ping | HttpRequestException |
| 30 | Default HTTPS port 8443 | Production, certificate, Https:Port unset | Start, read server addresses | Single address `https://...:8443` |

Not covered by a test (and why): MailKit advisory (checked by `dotnet list package --vulnerable`, not behaviour); `AccountLockedUntil` resx removal; PlaceholderKey equals the appsettings.json string (would already pass); locked account with expired `LockedUntil` logging in, Development Sentry default (Debug true, 1.0), and valid rate 1.0 (all pass against current code, so they cannot fail first).

## Tasks
1. [x] JWT options validation, 15-minute default lifetime, ValidateOnStart wiring — scenarios 1–9, 13–16, 23
2. [x] Locked account returns the generic 401; lockout Sentry message uses user ID; remove AccountLockedUntil resx; Identity CLAUDE.md — scenarios 10–12
3. [x] Sentry hosting extension (Debug only in Development, validated TracesSampleRate) — scenarios 17–22
4. [x] HTTPS-only Kestrel listener outside Development, `Https` appsettings section — scenarios 24–30 (Program.cs call NOT wired: blocked, see implementer report)
5. [x] MailKit upgrade to latest version without advisory — no scenario (checked with `dotnet list package --vulnerable`)
