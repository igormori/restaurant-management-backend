# Plan: Restyle verification email
Spec: docs/specs/identity/restyle-verification-email.md

## Approach
The finished Figma markup already exists in the working tree at
`src/RestaurantManagement.Shared/Templates/verification-email.html` (currently untracked — it must be
committed as part of this change). So this is not a "write some HTML in C#" task: it is wiring that file
into `EmailService` and substituting four tokens. The file ships as an **embedded resource** in the
`RestaurantManagement.Shared` assembly, matching how the only other static text asset in this repo
(`Localization/SharedResource.resx`) is shipped, and a small static `VerificationEmailTemplate` class
reads it once and does four `string.Replace` calls for `{{FIRST_NAME}}`, `{{CODE}}`,
`{{EXPIRY_MINUTES}}` and `{{EMAIL}}`. `IEmailService.SendVerificationEmailAsync` grows two parameters
(`firstName`, `expiryMinutes`) so that `Shared` never reaches into Identity for `User.FirstName` or
binds Identity's `SecurityOptions`; both callers already hold both values, and `{{EMAIL}}` is filled
from the `to` parameter that is already there. No new package, no templating engine, no config keys, no
database change. `EmailService.SendVerificationEmailAsync` becomes a pass-through, so recipient, subject
and the per-call-site error handling are untouched.

## Changes
| File | New/Modified | What changes |
|---|---|---|
| `src/RestaurantManagement.Shared/Templates/verification-email.html` | New (exists untracked — commit it) | The Figma-accurate template. **Not authored by this plan and not to be restyled by it.** Only change if a token or copy fix is needed. |
| `src/RestaurantManagement.Shared/RestaurantManagement.Shared.csproj` | Modified | Add `<ItemGroup><EmbeddedResource Include="Templates\verification-email.html" /></ItemGroup>`. Logical resource name will be `RestaurantManagement.Shared.Templates.verification-email.html`. |
| `src/RestaurantManagement.Shared/Services/Email/VerificationEmailTemplate.cs` | New | Static class. `public const string Subject = "Verify your email";`, a `private static readonly string TemplateHtml = LoadTemplate();` that reads the manifest resource stream and throws a descriptive `InvalidOperationException` if it is missing, `private const` for each `{{TOKEN}}`, and `public static string Build(string to, string firstName, string code, int expiryMinutes)` doing the blank-name fallback, HTML-encoding and the four replaces. |
| `src/RestaurantManagement.Shared/Services/Email/IEmailService.cs` | Modified | `Task SendVerificationEmailAsync(string to, string firstName, string code, int expiryMinutes);` (was `(string to, string code)`). `SendEmailAsync` unchanged. |
| `src/RestaurantManagement.Shared/Services/Email/EmailService.cs` | Modified | `SendVerificationEmailAsync` body replaced by `await SendEmailAsync(to, VerificationEmailTemplate.Subject, VerificationEmailTemplate.Build(to, firstName, code, expiryMinutes));`. The inline `<h1>{code}</h1>` string and the local `subject` literal are deleted. `SendEmailAsync` untouched. |
| `src/RestaurantManagement.Modules.Identity/Services/RegistrationService.cs` | Modified | One line: `await _emailService.SendVerificationEmailAsync(user.Email, user.FirstName, verificationCode, _securityOptions.VerificationCodeExpiryMinutes);`. The surrounding try/catch-and-log stays exactly as it is. |
| `src/RestaurantManagement.Modules.Identity/Services/VerificationService.cs` | Modified | One line in `ResendVerificationCodeAsync`: `await _emailService.SendVerificationEmailAsync(user.Email, user.FirstName, code, _securityOptions.VerificationCodeExpiryMinutes);`. The try/catch that throws `BusinessException(EmailSendingFailed, 500)` stays as it is. |
| `tests/RestaurantManagement.Shared.Tests/RestaurantManagement.Shared.Tests.csproj` | New | xUnit 2.9.2, FluentAssertions 6.12.2, NSubstitute 6.2.0, Microsoft.NET.Test.Sdk 17.12.0, xunit.runner.visualstudio 2.8.2, coverlet.collector 6.0.2, `<Using Include="Xunit" />`, project reference to `src/RestaurantManagement.Shared`. Mirrors the Identity test csproj minus the EF Sqlite package. No test-asset copying needed — the template travels inside the referenced assembly. |
| `tests/RestaurantManagement.Shared.Tests/VerificationEmailTemplateTests.cs` | New | All body-content acceptance criteria (see Tests). |
| `tests/RestaurantManagement.Modules.Identity.Tests/RegistrationServiceTests.cs` | Modified | Fix the 3 `SendVerificationEmailAsync` mock/assert call sites for the new 4-arg signature; strengthen the happy-path assertion to check first name and configured expiry are passed through. |
| `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` | Modified | Fix the 4 `SendVerificationEmailAsync` call sites; strengthen the resend happy-path assertion the same way; add `ResendVerificationCodeAsync_EmailSendingFails_ThrowsBusinessException` (missing today, needed for the "error handling unchanged" criterion). |
| `RestaurantManagement.sln` | Modified | `dotnet sln add tests/RestaurantManagement.Shared.Tests/RestaurantManagement.Shared.Tests.csproj` |

## API
**No HTTP change.** No route, verb, request model, response model or status code is added, removed or
altered. `POST /api/auth/register`, `POST /api/auth/verify` and `POST /api/auth/resend-verification`
keep the exact contract documented in `docs/plans/identity/verify-email-on-registration.md`, including
their failure codes (register: 400 `EmailAlreadyRegistered`, 200 even when SMTP fails; resend: 404
`UserNotFound`, 400 `UserAlreadyVerified`, 429 `VerificationCodeRecentlySent`, 500 `EmailSendingFailed`).

The only contract that changes is internal, in `RestaurantManagement.Shared`:

```
- Task SendVerificationEmailAsync(string to, string code);
+ Task SendVerificationEmailAsync(string to, string firstName, string code, int expiryMinutes);
```

Breaking for compilation only; the two callers and the existing test doubles are the complete set of
usages (verified by grep — 2 production call sites, 7 test call sites).

### Tokens in `verification-email.html`
| Token | Filled from | Occurrences |
|---|---|---|
| `{{CODE}}` | `code` parameter | **2** — the hidden preheader `<div>` and the code box. `string.Replace` fills both, which is intended. |
| `{{FIRST_NAME}}` | `firstName` parameter, after blank-fallback + HTML-encode | 1 (`Hi {{FIRST_NAME}},`) |
| `{{EXPIRY_MINUTES}}` | `expiryMinutes.ToString()` | 1 |
| `{{EMAIL}}` | the existing `to` parameter, after HTML-encode — **no new parameter needed** | 1 (footer) |

### Copy actually in the file (use these exact strings in assertions)
`RISTORANTE` · `Confirm your email` · `Hi {{FIRST_NAME}},` ·
`Enter the code below to finish setting up your account. The code expires in {{EXPIRY_MINUTES}} minutes.` ·
`If you didn't create an account, you can ignore this email and nothing will happen.` ·
`&ndash; The Ristorante team` · `Questions? Reply to this email and we'll help.` ·
`This message was sent to {{EMAIL}} because an account was created with this address.`

> **Assertion trap:** in the source file the expiry sentence is wrapped across two lines — `...expires in\n        {{EXPIRY_MINUTES}} minutes.` So `Contain("expires in 30 minutes")` will **fail**. Assert
> `Contain("30 minutes")` instead. Likewise the sign-off renders as `&ndash; The Ristorante team`, so
> assert `Contain("The Ristorante team")`, not the whole line.

## Data
**No entity, property, EF configuration or index change.** Nothing is persisted by this feature; the
template is rendered per send from values the callers already have in memory (`User.Email`,
`User.FirstName`, the generated code, `SecurityOptions.VerificationCodeExpiryMinutes`).

**No migration is required.** Do not run `dotnet ef migrations add` for this feature.

## Authorization
**No role check and no `OrganizationId` scoping — deliberately, and unchanged from today.** The
verification email is sent during registration and resend, before the user has any `UserRole` row and
therefore before any `OrganizationId` exists (spec Scope: "Roles allowed: not applicable"). Both
endpoints are anonymous and stay anonymous.

Module-boundary compliance, which is the relevant constraint here instead:
- `EmailService`, `VerificationEmailTemplate` and the template file live in `Shared` and take
  `firstName` and `expiryMinutes` as parameters. They must not reference
  `RestaurantManagement.Modules.Identity`, must not inject `IdentityDbContext`, and must not bind
  `IOptions<SecurityOptions>` themselves to re-derive the expiry.
- The Identity services read `user.FirstName` from their own `IdentityDbContext` and
  `_securityOptions.VerificationCodeExpiryMinutes` from their already-injected
  `IOptions<SecurityOptions>`; both are already present in both classes, so no new dependency is added
  to either constructor.
- No new `Shared` contract interface is needed: the data flows Identity -> Shared as method arguments,
  which is the allowed direction.

## Steps
1. `git add src/RestaurantManagement.Shared/Templates/verification-email.html` and add the
   `<EmbeddedResource Include="Templates\verification-email.html" />` item to
   `RestaurantManagement.Shared.csproj` -> verify: `dotnet build RestaurantManagement.sln` green, and
   the resource is present (`strings`/ILSpy, or just rely on step 4's tests, which fail loudly if the
   logical name is wrong).
2. Create `tests/RestaurantManagement.Shared.Tests` (packages per the Changes table, project reference
   to `src/RestaurantManagement.Shared`) and `dotnet sln add` it -> verify: `dotnet test` discovers the
   new project and reports 0 tests for it.
3. Add `VerificationEmailTemplateTests.cs` with the failure/edge cases first — blank first name,
   whitespace-only first name, non-default expiry — then the content cases -> verify: `dotnet build`
   fails to compile because `VerificationEmailTemplate` does not exist yet (expected; these are the
   red tests).
4. Add `src/RestaurantManagement.Shared/Services/Email/VerificationEmailTemplate.cs`. `LoadTemplate()`:
   `GetManifestResourceStream(ResourceName)`, guard `if (stream is null) throw new
   InvalidOperationException($"Embedded resource '{ResourceName}' was not found...");`, then
   `new StreamReader(stream).ReadToEnd()`. `Build(...)`: normalize first
   (`var greetingName = string.IsNullOrWhiteSpace(firstName) ? DefaultGreetingName : firstName.Trim();`),
   then `WebUtility.HtmlEncode` on `greetingName` and `to`, then four chained `string.Replace` calls on
   `TemplateHtml` -> verify: `dotnet test --filter VerificationEmailTemplateTests` all green (this also
   proves the embedded-resource name from step 1).
5. Change the signature in `IEmailService.cs` and make `EmailService.SendVerificationEmailAsync` a
   one-statement pass-through to `VerificationEmailTemplate.Subject` / `.Build(...)` -> verify:
   `dotnet build` now fails only at the Identity call sites and Identity test call sites (expected).
6. Update `RegistrationService` and `VerificationService` to pass `user.FirstName` and
   `_securityOptions.VerificationCodeExpiryMinutes`; change nothing else in either method, especially
   not the try/catch blocks -> verify: `dotnet build src/RestaurantManagement.Web` green.
7. Update the 7 `SendVerificationEmailAsync` references in the Identity tests to the 4-arg signature
   and tighten the two happy-path assertions to `Received(1).SendVerificationEmailAsync(user.Email,
   "New", code.Code, 15)` -> verify: `dotnet test` green.
8. Add `ResendVerificationCodeAsync_EmailSendingFails_ThrowsBusinessException` to
   `VerificationServiceTests` -> verify: it passes without touching production code (it pins existing
   behaviour).
9. Run the app in Development with `Email:SmtpHost` empty, register a user, and read the `[Body]:`
   warning that `EmailService` logs; save it as `.html` and open it in a browser, and in a real client
   if one is available -> verify: no `{{` token survives, and the code/email/minutes are real values.
10. `dotnet format`, then `dotnet build RestaurantManagement.sln` and `dotnet test` -> verify: both
    green. `dotnet format` must not touch the `.html` file.

## Tests
Failure and edge cases first. Items 1-6 are `tests/RestaurantManagement.Shared.Tests/VerificationEmailTemplateTests.cs`
(plain string assertions on `VerificationEmailTemplate.Build(...)`, no mocks needed); items 7-8 are in
the existing Identity test project with an NSubstitute `IEmailService`.

| Acceptance criterion | Test |
|---|---|
| Blank/missing first name does not produce a broken greeting | `Build_FirstNameIsBlank_UsesGenericGreeting` — `Build("a@b.com", "", "123456", 15)` -> body contains `Hi there,` and does **not** contain `Hi ,`. Theory rows for `""`, `" "`, `null`. |
| Configured expiry other than the default is stated | `Build_NonDefaultExpiryMinutes_StatesConfiguredMinutes` — `expiryMinutes: 30` -> body contains `30 minutes` (see the assertion trap above) and does not contain `15 minutes` |
| Body contains the actual code, not a placeholder | `Build_WithCode_BodyContainsTheCode` — `code: "482913"` -> body contains `482913`. Plus `Build_WithValidInput_LeavesNoUnsubstitutedTokens` — body does not contain `{{`, which is the real "no placeholder string" guarantee now that the source is a token file |
| Body contains the actual recipient address in the footer | `Build_WithRecipient_FooterContainsRecipientAddress` — `to: "diner@example.com"` -> body contains `This message was sent to diner@example.com` |
| Body contains the first name in the greeting when present | `Build_WithFirstName_GreetingContainsFirstName` — `firstName: "Giulia"` -> body contains `Hi Giulia,` |
| Body contains every required content element | `Build_WithValidInput_ContainsAllDesignContentElements` — one test asserting the body contains `RISTORANTE`, `Confirm your email`, `15 minutes`, the code, `didn't create an account`, `The Ristorante team`, `Reply to this email`, `This message was sent to` |
| Registration and resend produce the same styled content (no divergence) | Both call sites are proven to route through the one template with equivalent arguments: `RegisterAsync_ValidRequest_SendsVerificationEmailWithFirstNameAndConfiguredExpiry` (modified existing) and `ResendVerificationCodeAsync_OutsideCooldown_SendsVerificationEmailWithFirstNameAndConfiguredExpiry` (modified existing) each assert `Received(1).SendVerificationEmailAsync(user.Email, user.FirstName, <the persisted code>, 15)` |
| Existing behaviour preserved: recipient, subject, and per-call-site failure handling | Subject: `Subject_IsUnchanged` asserts `VerificationEmailTemplate.Subject == "Verify your email"`. Recipient: covered by the two `Received(1).SendVerificationEmailAsync(user.Email, ...)` assertions above. Failure handling: `RegisterAsync_EmailSendingFails_StillCreatesUnverifiedUser` (existing, signature updated) and `ResendVerificationCodeAsync_EmailSendingFails_ThrowsBusinessException` (new — mock throws, expects `BusinessException` with `StatusCode == 500` and the new code row still persisted) |

Not an acceptance criterion but worth one test, since it is the failure mode of the embedded-resource
choice: every `Build_*` test would fail with `TypeInitializationException` if the csproj item or the
logical resource name were wrong, so the wiring is already covered — no separate test needed.

## Decisions and risks
The spec listed five open questions. **Three of them are now answered by
`Templates/verification-email.html` itself rather than by a judgement call in this plan**; the
remaining two still need the user.

### Resolved by the template file (no longer open)
- **Exact colors, typography, spacing (spec open question 3): resolved.** The file carries the real
  values — red `#CE2B37`, green `#009246`, code box `#faf7f7` on `#eadcdc`, heading `#221f1f`, body
  `#333333`, note/sign-off `#555555`, footer `#8c8c8c`, page `#f2f2f2`, `Helvetica, Arial, sans-serif`
  throughout, 600px width, 32px gutters. **An earlier draft of this plan proposed hand-built C#
  constants with guessed hexes; that is deleted.** Nothing in C# defines a color any more, and no test
  asserts on a hex, so visual changes are a pure edit to the `.html` file.
- **HTML client compatibility (spec open question 4): resolved, and it confirms the approach.** The
  file is already nested `<table role="presentation">` layout with fully inline styles, no `<style>`
  block, no media queries, a fixed 600px shell with `max-width:100%`, spacer cells using
  `font-size:0; line-height:0`, and a hidden preheader `<div>` for the inbox preview. That is the
  Outlook-desktop-safe pattern, so the compatibility bar is settled by the artifact rather than by this
  plan. **Do not "modernize" it to divs/flex.**
- **Branding (spec open question 2): resolved as static.** `RISTORANTE` is literal text in the file
  with no token, which matches the reasoning that verification happens before the user has any
  organization to white-label from. Making it per-tenant would mean adding a `{{BRAND}}` token, a
  parameter and a config key — out of scope, flagging only.

### Still needs the user
- **Signature (spec open question 1): extended to `(string to, string firstName, string code, int
  expiryMinutes)`.** This follows the spec's stated assumption. Positional `string to, string firstName,
  string code` means three adjacent strings — a caller that swaps two of them compiles cleanly. The
  alternative the spec allows (a single `VerificationEmailContent` object) removes that risk at the cost
  of a new DTO for two call sites; not worth it with only two callers, both pinned by tests.
  **Override if you want the object.** `{{EMAIL}}` needs no parameter of its own — it is filled from the
  existing `to`.
- **Reply-To (spec open question 5): no `Reply-To` header is added.** The footer copy "Questions? Reply
  to this email and we'll help." is treated as content only, per the spec. Replies land in whatever
  `EmailOptions.FromEmail` is set to, which in `appsettings.Development.json` is a Mailtrap sandbox
  address and in production may be a no-reply mailbox. **Risk: the email promises a reply channel that
  nobody monitors.** The fix is a mailbox/config decision (add `EmailOptions.ReplyToEmail`, set
  `message.ReplyTo` in `SendEmailAsync`), which the spec puts outside this feature.

### Implementation choices worth a second opinion
- **Embedded resource, not copy-to-output, not a C# const string.** The repo has no existing
  copy-to-output static asset to copy the pattern from; the closest precedent is
  `Localization/SharedResource.resx`, which the SDK embeds into the `Shared` assembly. Embedding means
  no runtime path resolution, nothing to configure at publish time, and — the deciding factor — the
  template is visible to `RestaurantManagement.Shared.Tests` purely through the project reference, with
  no asset-copy plumbing. A copy-to-output `Content` item would work but adds a file-on-disk dependency
  and `AppContext.BaseDirectory` handling; a C# const string would throw away the syntax highlighting,
  diffability and designer-editability of a real `.html` file, which is the whole point of having it.
  **Cost of embedding: editing the template requires a rebuild**, and a typo in the logical resource
  name fails at first use rather than at build (mitigated by the guard clause and by the test suite).
- **The template is read once into a `private static readonly string`**, not re-read per send. This is
  simpler than re-reading, not a caching layer, but it does mean a broken resource surfaces as
  `TypeInitializationException` wrapping the descriptive `InvalidOperationException`. Say the word if
  you would rather read per call for a cleaner stack trace.
- **Substitution is four chained `string.Replace` calls** with the tokens as private consts — no
  templating engine, no regex, no dictionary loop. Four fixed tokens do not justify anything more. The
  `{{`-free assertion is what protects against a token being renamed in the HTML without the C# being
  updated.
- **`firstName` and `to` are HTML-encoded** with `WebUtility.HtmlEncode` before substitution. This still
  applies exactly as before — moving the markup into a file does not change the injection risk, since
  the values are still spliced into a live HTML document. `code` is server-generated digits and
  `expiryMinutes` is an `int`, so neither needs encoding. Substitution order matters slightly: encode
  the inputs first, then replace, so that an encoded value can never itself contain a token.
- **A second test project (`RestaurantManagement.Shared.Tests`) is added**, since `Shared` has none
  today. Plain unit tests — no database, no Testcontainers, no `WebApplicationFactory` — so no new CI
  infrastructure, just a csproj and an sln entry.
- **Expiry is always rendered as plural "minutes"**, and the word is now baked into the HTML
  (`The code expires in {{EXPIRY_MINUTES}} minutes.`), so with
  `VerificationCodeExpiryMinutes = 1` it reads "1 minutes". Fixing it would mean moving the word into
  the token — i.e. editing the designed copy — so it is deliberately left alone.
- **This change breaks compilation for anything outside the repo that implements `IEmailService`.**
  Grep shows only `EmailService` and NSubstitute doubles, so the blast radius is contained, but it is a
  public interface in `Shared` and the change is not source-compatible.
- **The `.html` file is currently untracked.** If it is not committed, the build still succeeds locally
  (the file is on disk) but fails in CI or on a fresh clone with a missing-`EmbeddedResource` error.
  Step 1 exists specifically to prevent that.
- **The plan does not add a plain-text alternative body**, does not localize the copy, and does not
  change the subject — all explicitly out of scope in the spec. Note that an HTML-only email with no
  text part scores worse with some spam filters; raise as a follow-up if deliverability matters.
