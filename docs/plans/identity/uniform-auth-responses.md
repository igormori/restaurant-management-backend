# Plan: Uniform auth responses
Spec: docs/specs/identity/uniform-auth-responses.md

## Approach
Reorder the three methods so every folded failure leaves through one generic response per
endpoint, while each check still runs instead of short-circuiting. Login always performs a
bcrypt verify (against a constant dummy hash when no user row exists) and tests IsVerified
after the password; verify always runs the code query; resend returns the same 200 message
for not-found, already-verified, cooldown and success. Three new resource keys carry the
generic messages; AuthController is unchanged.

## Changes
| File | New/Modified | What changes |
|---|---|---|
| `src/RestaurantManagement.Shared/Localization/SharedResource.resx` | Modified | Add `InvalidEmailOrPassword`, `InvalidEmailOrCode`, `VerificationResendAcknowledged`. |
| `src/RestaurantManagement.Shared/Localization/SharedResource.pt.resx` | Modified | Same three keys, pt values. |
| `src/RestaurantManagement.Modules.Identity/Services/SessionService.cs` | Modified | `LoginAsync`: keep 423 lockout guard; always verify password (dummy hash constant if no user); IsVerified checked after password; single 401. |
| `src/RestaurantManagement.Modules.Identity/Services/VerificationService.cs` | Modified | `VerifyEmailAsync`: query codes by `user?.Id ?? Guid.Empty`, one 401 guard. `ResendVerificationCodeAsync`: return generic message for not-found, verified, cooldown. |
| `tests/.../VerificationServiceTests.cs` | Modified | Update the four folded-case expectations; add uniformity assertions. |
| `tests/.../SessionServiceLoginTests.cs` | New | Login uniformity, lockout, success. |

## API
POST `/api/auth/login` — `LoginRequest` -> `AuthResponse`. 200 success; 401 `InvalidEmailOrPassword` (unknown email, unverified, wrong password); 423 lockout unchanged.
POST `/api/auth/verify` — `VerifyEmailRequest` -> 200 message; 401 `InvalidEmailOrCode` (unknown email, wrong/expired/used code).
POST `/api/auth/resend-verification` — `ResendVerificationRequest` -> 200 `VerificationResendAcknowledged` for unknown email, already verified, cooldown and success; 500 `EmailSendingFailed` unchanged.

## Data
No change. No migration.

## Authorization
Anonymous pre-auth endpoints; no role check. No OrganizationId scoping: a user has no organization before verification.

## Steps
1. Add the three keys to both `.resx` files -> verify: `dotnet build RestaurantManagement.sln`.
2. Update `VerificationServiceTests` folded-case expectations -> verify: `dotnet test` shows them failing.
3. Add `SessionServiceLoginTests` with unknown-email, wrong-password, unverified cases -> verify: `dotnet test` shows them failing.
4. `LoginAsync`: add `private const string DummyPasswordHash` (bcrypt hash at the production work factor) and always call `VerifyPassword` -> verify: `dotnet build`.
5. `LoginAsync`: move the IsVerified check below the password check; throw one 401 `InvalidEmailOrPassword` for all three cases; leave lockout counting on real users only -> verify: login tests pass.
6. `VerifyEmailAsync`: run the code query for `user?.Id ?? Guid.Empty`; one guard throws 401 `InvalidEmailOrCode` when user, code, or match is absent -> verify: verify tests pass.
7. `ResendVerificationCodeAsync`: return the generic message instead of throwing for not-found, already-verified and cooldown; keep the 500 on send failure -> verify: resend tests pass.
8. `dotnet format`, then `dotnet build RestaurantManagement.sln` and `dotnet test` -> verify: both green.

## Tests
| Acceptance criterion | Test name and assertion |
|---|---|
| Unknown email == verified account with wrong password | `LoginAsync_UnknownEmailAndWrongPassword_ReturnSameStatusAndMessage` — both throw 401, same `Message` |
| Unverified account with correct password == the two above | `LoginAsync_UnverifiedCorrectPassword_MatchesGenericFailure` — 401, message equals the wrong-password message |
| Unknown email == invalid/expired code on verify | `VerifyEmailAsync_UnknownEmailAndInvalidCode_ReturnSameStatusAndMessage` — both 401, same `Message` |
| Unknown email == already verified on resend | `ResendVerificationCodeAsync_UnknownEmailAndVerifiedAccount_ReturnSameMessage` — no throw, equal strings, no email sent |
| No wording distinguishes the folded cases | `AuthFailures_AcrossFoldedCases_UseOneMessagePerEndpoint` — messages per endpoint collapse to a single distinct value |
| Locked-out account keeps its distinct response | `LoginAsync_LockedAccount_Throws423WithUnlockTime` — 423, message differs from the generic one |
| Valid login still returns tokens | `LoginAsync_ValidCredentials_ReturnsTokens` — `Token` and `RefreshToken` non-empty |
| Valid code still verifies | `VerifyEmailAsync_ValidCode_MarksUserVerifiedAndCodeUsed` — existing test, unchanged |
| Resend outside cooldown still sends | `ResendVerificationCodeAsync_OutsideCooldown_SendsVerificationEmail...` — existing test, unchanged |

## Decisions
- Assumption: status codes are login 401, verify 401, resend 200 for every folded case.
- Assumption: resend cooldown (429) folds into the generic 200, removing the "please wait" signal.
- Assumption: lockout (423) stays distinct and untouched.
- Assumption: timing safety is structural (always hash, always query); no measured timing test.
- Guard clauses stay first but no longer return differentiated responses; the folded cases share one throw site.

## Follow-ups
- Lockout 423 and the resend SMTP delay still confirm that an account exists.
- No attempt limit on `/api/auth/verify` (already in docs/follow-ups.md).
- Verify and resend return bare strings instead of JSON (already in docs/follow-ups.md).
- `UserNotVerified`, `InvalidPassword` and `VerificationCodeRecentlySent` become unused keys.
