# Plan: Verify attempt limit
Spec: docs/specs/identity/verify-attempt-limit.md

## Approach
Two new columns on `User` track verification failures separately from the login lockout fields, and two
new `SecurityOptions` values configure the limit. `VerificationService` checks lockout as its first guard
after loading the user, increments on any failed verify, and resets on success or resend. The lockout
trip mirrors `SessionService.LoginAsync`: set the expiry, zero the counter, throw 423.

## Changes
| File | New/Modified | What changes |
|---|---|---|
| `Entities/User.cs` | Modified | Add `VerificationFailedAttempts` (int, 0) and `VerificationLockedUntil` (DateTime?). |
| `Shared/Options/SecurityOptions.cs` | Modified | Add `MaxVerificationAttempts = 5`, `VerificationLockoutDurationMinutes = 15`. |
| `Web/appsettings.json` | Modified | Same two keys under `Security`. |
| `Shared/Localization/SharedResource.resx` + `.pt.resx` | Modified | Add `VerificationLockedUntil` with a `{0}` time placeholder. |
| `Services/VerificationService.cs` | Modified | Lockout guard, increment/trip, reset on success and on resend. |
| `Migrations/` | New | Generated migration adding the two columns. |
| `tests/.../IdentityTestDatabase.cs` | Modified | `TestSecurityOptions.Create` gains the two new parameters. |
| `tests/.../VerificationServiceTests.cs` | Modified | `CreateSut` passes them through; add the tests below. |

## API
No route, request, or response change. Verify and resend gain 423 when the account is in verification lockout.

## Data
`Users` gains `VerificationFailedAttempts` (integer, not null, default 0) and `VerificationLockedUntil`
(timestamp, null). No new entity, no index.

```
dotnet ef migrations add AddVerificationAttemptTracking --project src/RestaurantManagement.Modules.Identity --startup-project src/RestaurantManagement.Web --context IdentityDbContext
```

## Authorization
Anonymous, as today. No OrganizationId scoping: the account has no role or organization before verification.

## Steps
1. Add the two `User` properties -> verify: `dotnet build RestaurantManagement.sln`.
2. Add the two `SecurityOptions` properties and the `appsettings.json` keys -> verify: build.
3. Add the `VerificationLockedUntil` resource to both resx files -> verify: build.
4. Generate the migration with the command above -> verify: migration file lists both columns only.
5. In `VerifyEmailAsync`, after the user guard, throw 423 `VerificationLockedUntil` when `VerificationLockedUntil > UtcNow` -> verify: test 3.
6. On a failed verify, increment; at `>= MaxVerificationAttempts` set the expiry, zero the counter, mark all unused codes `IsUsed`, save, throw 423 -> verify: tests 1, 2.
7. On success, set both fields to zero/null before saving -> verify: tests 6, 7, 9.
8. In `ResendVerificationCodeAsync`, add the same lockout guard after the `IsVerified` guard, before the cooldown check -> verify: test 4.
9. Reset both fields when the new code is issued -> verify: tests 8, 10.
10. Extend `TestSecurityOptions.Create` and `CreateSut`, add the tests -> verify: `dotnet test`, `dotnet format`.

## Tests
| Acceptance criterion | Test name and assertion |
|---|---|
| Wrong code rejected, count increments | `VerifyEmailAsync_IncorrectCode_IncrementsVerificationFailedAttempts` — throws 401, reloaded count is 1 |
| Reaching max invalidates code and locks | `VerifyEmailAsync_FinalFailedAttemptReachesMax_InvalidatesCodeAndSetsLockout` — code `IsUsed`, `VerificationLockedUntil` in the future |
| Locked account rejected without checking code | `VerifyEmailAsync_AccountLockedWithCorrectCode_ThrowsAndLeavesUserUnverified` — throws 423, `IsVerified` false |
| Resend rejected during lockout | `ResendVerificationCodeAsync_AccountLocked_ThrowsAndSendsNoEmail` — throws 423, `DidNotReceive` send, no new code row |
| Never failed or lockout expired is evaluated normally | `VerifyEmailAsync_LockoutExpired_EvaluatesCodeAndVerifiesUser` — past `VerificationLockedUntil`, `IsVerified` true |
| Wrong code max−1 times then correct verifies | `VerifyEmailAsync_CorrectCodeAfterFailuresBelowMax_MarksUserVerified` — `IsVerified` true |
| Success resets the count to zero | `VerifyEmailAsync_SuccessAfterPriorFailures_ResetsVerificationFailedAttempts` — count 0, `VerificationLockedUntil` null |
| Successful resend resets the count | `ResendVerificationCodeAsync_OutsideLockout_ResetsVerificationFailedAttempts` — count 0, new code issued |
| Correct code on first attempt unaffected | `VerifyEmailAsync_CorrectCodeOnFirstAttempt_MarksUserVerified` — `IsVerified` true, count 0 |
| After lockout expires, resend works from zero | `ResendVerificationCodeAsync_LockoutExpired_SendsNewCodeAndResetsAttempts` — `Received(1)` send, count 0 |

## Decisions
- Two columns on `User`, not a new entity: one row per account, no history required.
- Lockout rejections use 423 and a new `VerificationLockedUntil` message, matching login lockout.
- The attempt that trips the lockout returns 423, not 401, so the caller knows to wait.
- Counter is zeroed when the lockout is set, as `SessionService` does; lockout state is the timestamp alone.
- No transaction or row lock: two concurrent verifies near the limit can both pass the guard.

## Follow-ups
- Concurrent verify calls can race the counter; needs optimistic concurrency or a row lock.
- Verify and resend return bare strings while errors return JSON (already in docs/follow-ups.md).
- No email notifies the user that verification was locked out.
