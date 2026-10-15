# Plan: Verify email on registration
Spec: docs/specs/identity/verify-email-on-registration.md

## Approach
The flow already exists end to end (`RegistrationService`, `VerificationService`, `SessionService`,
`UserVerificationCode`, the three `/api/auth/*` endpoints), so this is a gap-closing plan, not a
build: no new entity, no new endpoint, no new service. The work is (1) fixing the configuration
wiring that silently breaks two acceptance criteria — `SecurityOptions` is never registered and the
base `appsettings.json` binds the wrong key names for `EmailOptions`, so expiry/cooldown are not
actually configurable and email sending is misconfigured outside Development; (2) closing three
behavioural gaps — registration currently lets an SMTP failure bubble up as a 500 after the account
is already committed, resend invalidates only the newest unused code instead of all of them, and the
codes come from `new Random()`; (3) tightening the two request models, which leak their backing
fields (`_code`) as public JSON properties. Everything else in the spec is already satisfied and is
only covered by tests. The repo has **no test project at all**, so the largest single piece of work
is creating `tests/RestaurantManagement.Modules.Identity.Tests` and proving all thirteen acceptance
criteria there.

## Changes
| File | New/Modified | What changes |
|---|---|---|
| `src/RestaurantManagement.Web/Program.cs` | Modified | Register `SecurityOptions` against the `Security` section; delete the `Configure<JwtOptions>(GetSection("Security"))` line (wrong options type bound to the wrong section). |
| `src/RestaurantManagement.Web/appsettings.json` | Modified | Rename `Email` keys to match `EmailOptions`: `SmtpServer`->`SmtpHost`, `SenderEmail`->`FromEmail`, `SenderName`->`FromName`, `Username`->`SmtpUser`, `Password`->`SmtpPass`. Leave `SmtpUser`/`SmtpPass` empty (injected as env vars). |
| `src/RestaurantManagement.Modules.Identity/Services/RegistrationService.cs` | Modified | Guard clauses first; generate the code with `RandomNumberGenerator`; wrap `SendVerificationEmailAsync` in try/catch that logs and swallows so registration still returns 200 with an unverified account (AC 13); inject `ILogger<RegistrationService>`. |
| `src/RestaurantManagement.Modules.Identity/Services/VerificationService.cs` | Modified | Resend invalidates **all** unused codes for the user, not just the newest; generate the code with `RandomNumberGenerator`; set `user.UpdatedAt` when verifying. Cooldown, already-verified and email-failure handling stay as they are. |
| `src/RestaurantManagement.Modules.Identity/Models/VerifyEmailRequest.cs` | Modified | Make the `_code` backing field private (currently a public auto-property, so it ships in the JSON contract and Swagger); make `Code` a non-nullable `string` to match `[Required]`. |
| `src/RestaurantManagement.Modules.Identity/Models/ResendVerificationRequest.cs` | Modified | Delete the unused public `_code` property. |
| `src/RestaurantManagement.Shared/Services/Email/EmailService.cs` | Modified | `SendVerificationEmailAsync`: remove the dead first `body` assignment (it prints `SmtpPort` as "minutes" and a stray "Wait, mixing options" line) and build the body once. |
| `tests/RestaurantManagement.Modules.Identity.Tests/RestaurantManagement.Modules.Identity.Tests.csproj` | New | xUnit, FluentAssertions, NSubstitute, `Microsoft.EntityFrameworkCore.Sqlite`, project reference to the Identity module. |
| `tests/RestaurantManagement.Modules.Identity.Tests/IdentityTestDatabase.cs` | New | Small helper: opens a SQLite in-memory connection, builds `IdentityDbContext`, calls `EnsureCreated`. |
| `tests/RestaurantManagement.Modules.Identity.Tests/RegistrationServiceTests.cs` | New | AC 1, 10, 13. |
| `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` | New | AC 2-8, 11 (verification half), 12. |
| `tests/RestaurantManagement.Modules.Identity.Tests/SessionServiceVerificationTests.cs` | New | AC 9, 11 (login half). |
| `RestaurantManagement.sln` | Modified | Add the test project (`dotnet sln add`). |

## API
No route, verb or payload changes. Documented contract after this plan:

**POST `/api/auth/register`** — `RegisterRequest` { Email, Password, FirstName, LastName, PhoneNumber? } -> `AuthResponse` (UserId, Email, PhoneNumber, FirstName, LastName; `Token`/`RefreshToken` stay null).
- 200 account created unverified, verification email dispatched
- 400 `EmailAlreadyRegistered`; 400 model validation failure
- 200 even if SMTP send fails (logged, not surfaced — see Decisions)

**POST `/api/auth/verify`** — `VerifyEmailRequest` { Email, Code } -> `200` with a localized message string.
- 400 `UserNotFound` (no account for that email)
- 401 `InvalidOrExpiredCode` (no unused, unexpired code, or code mismatch — covers expired and already-used)
- 400 model validation failure

**POST `/api/auth/resend-verification`** — `ResendVerificationRequest` { Email } -> `200` with a localized message string.
- 404 `UserNotFound`
- 400 `UserAlreadyVerified`
- 429 `VerificationCodeRecentlySent` (inside the cooldown window)
- 500 `EmailSendingFailed` (send failed; the new code was already persisted)

**POST `/api/auth/login`** — unchanged, but the relevant failure is confirmed:
- 401 `UserNotVerified` when `IsVerified == false`; no `Token`/`RefreshToken` in the response.

All errors are rendered by `ExceptionHandlingMiddleware` as `{ "error": "<localized message>" }`.

## Data
No entity or schema change. `UserVerificationCode` already carries exactly the spec's fields
(`UserId`, `Code`, `ExpiresAt`, `IsUsed` default false, plus `CreatedAt` which the cooldown needs),
`User.IsVerified` already exists, and the table/index (`ix_user_verification_codes_user_id`,
cascade FK to `users`) are in `20260203053311_InitialCreate`.

**No migration is required by this plan.** The only schema change worth considering is a unique index
on `users.email`, which is listed under Decisions and risks and is deliberately *not* part of the
plan. If it is accepted, add `modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();` to
`IdentityDbContext.OnModelCreating` and run, from the repo root:

```
dotnet ef migrations add UniqueUserEmail --project src/RestaurantManagement.Modules.Identity --startup-project src/RestaurantManagement.Web --context IdentityDbContext
```

## Authorization
All three endpoints are anonymous by design and stay anonymous: a caller registering or verifying has
no token yet, so `[Authorize]` cannot apply.

**OrganizationId scoping is deliberately absent and that is correct here.** Verification proves
ownership of an email address, not membership of a tenant; a user has no `UserRole` row (and hence no
`OrganizationId`) at this point in the flow. Every query in this feature is keyed by `User.Id` or
`User.Email` only — `_db.Users.FirstOrDefaultAsync(u => u.Email == ...)` and
`_db.UserVerificationCodes.Where(v => v.UserId == user.Id ...)`. No other module's DbContext,
service or entity is touched, and no new Shared contract is needed. Spec open question 6 asks this
be confirmed; the plan assumes yes.

## Steps
1. Create `tests/RestaurantManagement.Modules.Identity.Tests` (xUnit + FluentAssertions + NSubstitute
   + `Microsoft.EntityFrameworkCore.Sqlite`, project reference to
   `src/RestaurantManagement.Modules.Identity`) and `dotnet sln add` it -> verify: `dotnet test` runs
   and reports 0 tests instead of "no test projects".
2. Add `IdentityTestDatabase` (open SQLite in-memory connection, `UseSqlite`, `EnsureCreated`,
   `IDisposable`) plus a helper to build a `SecurityOptions` with explicit expiry/cooldown values ->
   verify: a throwaway test that saves and reads back a `User` passes.
3. Write the failure-case tests first, all expected to fail or already pass against current code:
   AC 1-9 (see Tests table) -> verify: `dotnet test`; expect AC 1-9 to pass except any that expose the
   bugs fixed in steps 5-7.
4. Write the happy-path tests AC 10-13 -> verify: `dotnet test`; AC 12 (invalidate *all* previous
   unused codes) and AC 13 (registration survives an SMTP failure) must fail now — they are the
   reproducing tests for steps 5 and 6.
5. `RegistrationService`: inject `ILogger<RegistrationService>`, keep the duplicate-email guard at the
   top, replace `new Random().Next(...)` with
   `RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString()`, and wrap the
   `SendVerificationEmailAsync` call in try/catch that logs the failure and returns normally ->
   verify: AC 13 and AC 10 pass.
6. `VerificationService`: in `ResendVerificationCodeAsync` replace the single-`lastCode`
   invalidation with `foreach` over all `!v.IsUsed` codes for the user (keep the cooldown check,
   which still reads the newest code by `CreatedAt`, before it); use `RandomNumberGenerator` for the
   new code; in `VerifyEmailAsync` also set `user.UpdatedAt = DateTime.UtcNow` -> verify: AC 12 and
   AC 2-8, 11 pass.
7. Clean up the two request models (private `_code` backing field in `VerifyEmailRequest`, `Code`
   becomes non-nullable `string`; drop the stray public `_code` from `ResendVerificationRequest`) ->
   verify: `dotnet build` and `dotnet test` pass; check Swagger no longer shows a `_code` field on
   either request.
8. `Program.cs`: add `builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));`
   and delete `builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Security"));`
   -> verify: run the app, temporarily set `Security:ResendCooldownSeconds` to a distinct value and
   confirm a second resend is rejected/allowed according to that value, not the 60 s default.
9. `appsettings.json`: rename the `Email` keys to the `EmailOptions` property names (Development
   already uses the correct names) -> verify: with `ASPNETCORE_ENVIRONMENT=Production`-style config,
   `IOptions<EmailOptions>.Value.SmtpHost` is non-empty (log it once or check via debugger/Swagger
   registration call).
10. `EmailService.SendVerificationEmailAsync`: delete the dead first `body` assignment and keep one
    body -> verify: `dotnet build`; register a user in Development and confirm the logged email body
    contains only the code block.
11. `dotnet format`, then `dotnet build RestaurantManagement.sln` and `dotnet test` -> verify: both
    green, all 13 acceptance-criterion tests passing.

## Tests
Failure cases first. All service-level, using the SQLite in-memory `IdentityDbContext` and an
NSubstitute `IEmailService`.

| Acceptance criterion | Test |
|---|---|
| Duplicate email registration rejected, no email sent | `RegisterAsync_EmailAlreadyRegistered_ThrowsBusinessExceptionAndSendsNoEmail` — seeds a user, expects `BusinessException` (400) and `emailService.DidNotReceive().SendVerificationEmailAsync(...)` |
| Code not matching any unused, unexpired credential rejected | `VerifyEmailAsync_CodeDoesNotMatch_ThrowsBusinessException` — seeds a valid code, submits a different one, expects 401 and `IsVerified == false` |
| Expired code rejected | `VerifyEmailAsync_CodeExpired_ThrowsBusinessException` — seeds a code with `ExpiresAt = UtcNow.AddMinutes(-1)`, submits the correct value |
| Already-used code rejected | `VerifyEmailAsync_CodeAlreadyUsed_ThrowsBusinessException` — seeds `IsUsed = true`, submits the correct value |
| Unknown email rejected on verify | `VerifyEmailAsync_UserNotFound_ThrowsBusinessException` |
| Unknown email rejected on resend | `ResendVerificationCodeAsync_UserNotFound_ThrowsBusinessException` — expects 404 and no email sent |
| Resend for verified account rejected | `ResendVerificationCodeAsync_UserAlreadyVerified_ThrowsBusinessException` — expects 400 and no email sent |
| Resend inside cooldown rejected | `ResendVerificationCodeAsync_WithinCooldown_ThrowsBusinessException` — seeds a code with `CreatedAt = UtcNow`, cooldown 60 s, expects 429, no new code row, no email sent |
| Unverified user cannot log in | `LoginAsync_UserNotVerified_ThrowsBusinessExceptionAndIssuesNoTokens` — correct password, `IsVerified = false`, expects 401 and `user.RefreshTokenHash` still null |
| Valid registration creates unverified account + sends email | `RegisterAsync_ValidRequest_CreatesUnverifiedUserAndSendsVerificationEmail` — asserts persisted `IsVerified == false`, exactly one `UserVerificationCode` with `ExpiresAt` ~ now + configured minutes, and `SendVerificationEmailAsync(email, thatCode)` received once |
| Correct code verifies account, marks code used, login then succeeds | `VerifyEmailAsync_ValidCode_MarksUserVerifiedAndCodeUsed` plus `LoginAsync_AfterVerification_ReturnsTokens` (same database, verify then log in with the correct password, assert non-null `Token` and `RefreshToken`) |
| Resend outside cooldown invalidates old code, issues and emails a new one | `ResendVerificationCodeAsync_OutsideCooldown_InvalidatesPreviousCodesAndSendsNewOne` — seeds two unused codes with `CreatedAt = UtcNow.AddMinutes(-5)`, asserts both become `IsUsed`, a new unused code exists with a different value, and one email sent with that new value |
| Registration survives a failed verification email | `RegisterAsync_EmailSendingFails_StillCreatesUnverifiedUser` — `emailService.SendVerificationEmailAsync(...).Throws(new Exception())`, asserts no exception escapes, user persisted with `IsVerified == false`, and a code row exists so resend can work |

## Decisions and risks
Spec open questions, answered as decided here — each is a sanity-check for the reviewer:

- **Link vs. code (open question 1): keeping the 6-digit code.** The spec's Out of scope section says
  so explicitly, so the plan does not touch the credential format. If the product answer is a
  clickable link, this plan is the wrong shape: it would need a `GET /api/auth/verify?token=...`
  endpoint, an opaque high-entropy token (not 6 digits), a configured frontend base URL, and the
  `Email` field would leave the verification request. Confirm before implementation starts.
- **Registration does not fail when the email cannot be sent (open question 3).** The spec's last
  acceptance criterion says the account must still exist unverified, so the plan catches and logs the
  send failure and returns 200. Today the exception escapes and the caller gets a 500 *after* the
  account was committed — the worst of both. This makes registration deliberately inconsistent with
  resend, which returns 500 `EmailSendingFailed`. If the reviewer prefers consistency, the
  alternative is to roll back the account and fail registration; that is a one-line change of the
  same catch block but a different user experience (the user must re-register rather than resend).
- **Account-existence disclosure is left as-is (open question 4).** Verify returns `UserNotFound` vs
  `InvalidOrExpiredCode`, and resend returns 404 vs 429 vs 400 — all probeable. Worse, `LoginAsync`
  checks `IsVerified` *before* the password, so anyone can discover that an address is registered but
  unverified without knowing the password. Making these indistinguishable would change AC 5, 6 and 7
  from "rejected with a specific error" to "generic response", which contradicts the current spec
  wording, so the plan keeps the current behaviour. Flagging it as a security decision for the owner.
- **Expiry 15 min / cooldown 60 s kept as defaults (open question 5)** — but note they are currently
  *not* configurable at all: `SecurityOptions` is never registered in `Program.cs`
  (`Configure<JwtOptions>(GetSection("Security"))` binds the wrong type), so the `Security` section in
  both appsettings files is dead config and the defaults in the class win. Step 8 fixes this; changing
  the values then actually takes effect.
- **`EmailOptions` binding is broken in the base `appsettings.json`** (`SmtpServer`/`SenderEmail`/
  `SenderName`/`Username`/`Password` vs the expected `SmtpHost`/`FromEmail`/`FromName`/`SmtpUser`/
  `SmtpPass`). Development happens to use the right names, so this is invisible locally and breaks in
  production: `SmtpHost` is empty, the dev-mode log shortcut does not apply outside Development, and
  `ConnectAsync("")` throws. This is the real reason AC 10 ("triggers a verification email") can fail
  in production today.
- **`appsettings.Development.json` contains live Mailtrap SMTP credentials committed to the repo**
  (`SmtpUser`/`SmtpPass`). Out of scope for this feature and not changed by the plan, but it violates
  "never commit secrets" and should be rotated and moved to user-secrets/env vars.
- **No unique index on `users.email`.** Duplicate registration is prevented only by an application-
  level `AnyAsync` check, so two concurrent registrations for the same address can both succeed. AC 1
  is satisfied under normal load and no acceptance criterion requires the index, so it is excluded
  from the plan; the migration command is given in Data if the reviewer wants it.
- **Code generation switches to `RandomNumberGenerator.GetInt32`.** `new Random()` is not suitable for
  a security credential. Six digits is only ~1M possibilities with a 15-minute window and **no
  attempt limit on `/api/auth/verify`** — the login lockout counter does not apply to verification,
  so the code is brute-forceable. The spec does not require an attempt limit, so the plan does not add
  one; flagging it as the main security gap in the feature as specified.
- **Resend reuses `IsUsed` to mean "invalidated".** A code revoked by a resend becomes
  indistinguishable from one actually consumed by the user. Sufficient for the spec (it can never be
  used again) and avoids a schema change; the alternative is a separate `InvalidatedAt`/status column.
- **Cooldown is measured from the newest code's `CreatedAt`, used or not.** Consequence: a user who
  registers and immediately clicks resend is refused for 60 seconds, because registration itself
  issued a code. Spec says "between two resend requests", so this is stricter than the literal
  wording. Confirm it is acceptable.
- **The code-issuance logic stays duplicated** in `RegistrationService` and `VerificationService`
  (generate + persist + send, ~6 lines each). Extracting a shared issuer service would couple the two
  services or add an abstraction no acceptance criterion needs; the plan keeps a small private
  `GenerateVerificationCode()` in each class instead. Easy to revisit if a third caller appears.
- **No clock abstraction.** Expiry and cooldown tests control time by seeding `CreatedAt`/`ExpiresAt`
  in the past rather than injecting `TimeProvider`, so no production code changes just for tests.
- **Test infrastructure choice: SQLite in-memory, not Testcontainers.** CLAUDE.md prescribes
  WebApplicationFactory + Testcontainers for integration tests, but every acceptance criterion here is
  service-level behaviour and SQLite needs no Docker in CI. Cost: the tests do not exercise Npgsql,
  the snake_case naming convention, or database constraints. If HTTP status codes and middleware
  rendering must also be proven, add a Testcontainers-based `RestaurantManagement.Web.Tests` later —
  that is a separate decision, not part of this plan.
- **Endpoint responses are bare strings.** `/api/auth/verify` and `/api/auth/resend-verification`
  return `Ok("<localized message>")`, i.e. `text/plain`, while errors come back as JSON
  `{ "error": ... }`. Inconsistent for clients, but the spec says nothing about response shape, so the
  controller is unchanged. Wrapping both in a small `MessageResponse` model would be a one-file
  addition if the reviewer wants it.
