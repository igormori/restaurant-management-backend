# Plan: Consistent verify/resend response format
Spec: docs/specs/identity/consistent-verify-resend-response-format.md

## Approach
Add one Identity response model with a single `Message` field. `IVerificationService` returns
that model instead of `string`, wrapping the same localized text. The two controller actions
return `ActionResult<MessageResponse>` so MVC serializes JSON; error paths stay in the
middleware.

## Changes
| File | New/Modified | What changes |
|---|---|---|
| src/RestaurantManagement.Modules.Identity/Models/MessageResponse.cs | New | `class MessageResponse { public string Message { get; set; } = null!; }` |
| src/RestaurantManagement.Modules.Identity/Services/IVerificationService.cs | Modified | Both methods return `Task<MessageResponse>` |
| src/RestaurantManagement.Modules.Identity/Services/VerificationService.cs | Modified | Return `new MessageResponse { Message = ... }` for UserVerified, VerificationCodeResent |
| src/RestaurantManagement.Modules.Identity/Controllers/AuthController.cs | Modified | Both actions typed `ActionResult<MessageResponse>`, still `Ok(response)` |
| tests/.../VerificationServiceTests.cs | Modified | Add two assertions on returned `Message` |
| tests/.../AuthControllerTests.cs | New | Controller tests for both success bodies |

## API
POST /api/auth/verify — body unchanged; 200 `{ "message": "<UserVerified>" }`; 400/401 unchanged.
POST /api/auth/resend-verification — body unchanged; 200 `{ "message": "<VerificationCodeResent>" }`; 400/404/429/500 unchanged.

## Data
No change. No migration.

## Authorization
No change: both endpoints stay unauthenticated and account-level, with no OrganizationId scoping.

## Steps
1. Add `MessageResponse` model -> verify: `dotnet build RestaurantManagement.sln`
2. Change both `IVerificationService` signatures -> verify: build fails only in service/controller
3. Wrap both service return values -> verify: `dotnet build RestaurantManagement.sln`
4. Type both controller actions -> verify: build passes
5. Add the two service-level `Message` assertions -> verify: `dotnet test`
6. Add `AuthControllerTests` with a substituted `IVerificationService` -> verify: `dotnet test`

## Tests
| Acceptance criterion | Test name and assertion |
|---|---|
| Verify, no matching valid code -> 401 JSON error | Existing `VerifyEmailAsync_CodeDoesNotMatch_ThrowsBusinessException`; unchanged |
| Verify, no account -> 400 JSON error | Existing `VerifyEmailAsync_UserNotFound_ThrowsBusinessException`; unchanged |
| Resend, unknown email -> 404 JSON error | Existing `ResendVerificationCodeAsync_UserNotFound_ThrowsBusinessException`; unchanged |
| Resend, already verified -> 400 JSON error | Existing `ResendVerificationCodeAsync_UserAlreadyVerified_ThrowsBusinessException`; unchanged |
| Resend within cooldown -> 429 JSON error | Existing `ResendVerificationCodeAsync_WithinCooldown_ThrowsBusinessException`; unchanged |
| Resend email send fails -> 500 JSON error | Existing `ResendVerificationCodeAsync_EmailSendingFails_ThrowsBusinessException`; unchanged |
| Verify success -> 200 JSON with `message` | `VerifyEmail_ValidRequest_ReturnsOkWithMessageBody`: `OkObjectResult.Value` is `MessageResponse`, `Message` non-empty |
| Resend success -> 200 JSON with `message` | `ResendVerification_ValidRequest_ReturnsOkWithMessageBody`: same assertion |
| Identical field names on both | `VerificationService` tests assert both methods return `MessageResponse` carrying UserVerified / VerificationCodeResent |

## Decisions
- Model named `MessageResponse` in Identity/Models, not Shared; no other module needs it.
- Ship as a breaking change: no `text/plain` shim, no versioned endpoint.
- Body carries `message` only; no `email` or `success` field.
- Message text is the existing localized string, unchanged.
- Success shape asserted by controller unit tests; no WebApplicationFactory harness exists to assert Content-Type over HTTP.

## Follow-ups
- No integration test project for Identity endpoints, so header/content-type behaviour is untested end to end.
- `docs/follow-ups.md` still lists the bare-string gap; remove that line once this ships.
- Pending uncommitted edits to `Entities/User.cs` and `VerificationServiceTests.cs` were reviewed as working-tree state, not as a diff.
