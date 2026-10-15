# Scenarios: Hash verification codes

**Plan:** docs/plans/identity/2026-10-15-hash-verification-codes/plan.md
**Coverage:** 39 scenarios — 11 happy · 9 edge · 15 failure · 0 security · 4 guard

No Security-type scenarios: the verify and resend endpoints are anonymous and account-level (no organization data, no roles), so cross-tenant and role-rejection tests do not apply. The security properties of this fix (key, user binding, no readable code) are covered by scenarios 12, 13, 15, 27, 28 and 36–38.

"Existing, reseeded" means an existing test that now seeds a fingerprint instead of a readable code. It keeps its name and behaviour, and fails until T2 and T3 are done.

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Failure | A hash key that is not set is refused, and the error names `VerificationCode:HashKey` | `Validate_HashKeyMissing_FailsNamingHashKey` | `tests/RestaurantManagement.Shared.Tests/VerificationCodeOptionsValidatorTests.cs` |
| 2 | T1 | Failure | An empty hash key is refused, and the error names the key | `Validate_HashKeyEmpty_FailsNamingHashKey` | `tests/RestaurantManagement.Shared.Tests/VerificationCodeOptionsValidatorTests.cs` |
| 3 | T1 | Edge | A hash key one byte short (31 bytes) is refused | `Validate_HashKeyOf31Bytes_FailsNamingHashKey` | `tests/RestaurantManagement.Shared.Tests/VerificationCodeOptionsValidatorTests.cs` |
| 4 | T1 | Failure | The app refuses to start when the hash key is empty, and the error names the key | `Startup_VerificationHashKeyEmpty_FailsNamingHashKey` | `tests/RestaurantManagement.Web.Tests/StartupConfigurationTests.cs` |
| 5 | T1 | Failure | The app also refuses to start in Production when the hash key is empty | `Startup_VerificationHashKeyEmptyInProduction_FailsNamingHashKey` | `tests/RestaurantManagement.Web.Tests/StartupConfigurationTests.cs` |
| 6 | T1 | Edge | The app refuses to start when the hash key is 31 bytes | `Startup_VerificationHashKeyOf31Bytes_FailsNamingHashKey` | `tests/RestaurantManagement.Web.Tests/StartupConfigurationTests.cs` |
| 7 | T1 | Edge | A hash key of exactly 32 bytes is accepted | `Validate_HashKeyOfExactly32Bytes_Succeeds` | `tests/RestaurantManagement.Shared.Tests/VerificationCodeOptionsValidatorTests.cs` |
| 8 | T1 | Edge | Key length is counted in bytes: 16 accented characters (32 bytes) are accepted | `Validate_HashKeyOf16TwoByteCharacters_Succeeds` | `tests/RestaurantManagement.Shared.Tests/VerificationCodeOptionsValidatorTests.cs` |
| 9 | T1 | Happy | A randomly generated key (as made by `openssl rand -base64 32`) is accepted | `Validate_RandomBase64HashKey_Succeeds` | `tests/RestaurantManagement.Shared.Tests/VerificationCodeOptionsValidatorTests.cs` |
| 10 | T1 | Happy | With a valid key the app starts and reads the key from configuration | `Startup_ValidVerificationHashKey_BindsHashKeyFromConfiguration` | `tests/RestaurantManagement.Web.Tests/StartupConfigurationTests.cs` |
| 11 | T2 | Failure | A wrong code does not match the stored fingerprint | `IsMatch_WrongCode_ReturnsFalse` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationCodeHasherTests.cs` |
| 12 | T2 | Failure | The same code does not match a fingerprint made for another user | `IsMatch_SameCodeForAnotherUser_ReturnsFalse` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationCodeHasherTests.cs` |
| 13 | T2 | Failure | A fingerprint made with a different server key does not match | `IsMatch_HashMadeWithDifferentKey_ReturnsFalse` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationCodeHasherTests.cs` |
| 14 | T2 | Edge | An empty stored value never matches (and does not crash) | `IsMatch_EmptyStoredHash_ReturnsFalse` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationCodeHasherTests.cs` |
| 15 | T2 | Edge | A readable code stored instead of a fingerprint (old row) never matches, even the same code | `IsMatch_ReadableCodeStoredInsteadOfHash_ReturnsFalse` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationCodeHasherTests.cs` |
| 16 | T2 | Happy | The fingerprint of a 6-digit code does not contain the code | `ComputeHash_SixDigitCode_DoesNotContainTheCode` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationCodeHasherTests.cs` |
| 17 | T2 | Edge | The fingerprint is 64 lowercase hex characters | `ComputeHash_SixDigitCode_Returns64LowercaseHexCharacters` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationCodeHasherTests.cs` |
| 18 | T2 | Happy | The same user and code match their fingerprint | `IsMatch_SameUserAndCode_ReturnsTrue` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationCodeHasherTests.cs` |
| 19 | T2 | Happy | The running app provides the fingerprint helper to the services | `Startup_DefaultConfiguration_RegistersVerificationCodeHasher` | `tests/RestaurantManagement.Web.Tests/StartupConfigurationTests.cs` |
| 20 | T3 | Failure | A wrong code is rejected with 401 and the failed-attempt count rises by 1 (existing, reseeded) | `VerifyEmailAsync_IncorrectCode_IncrementsVerificationFailedAttempts` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 21 | T3 | Failure | While locked, even the correct code is rejected with 423 and the user stays unverified (existing, reseeded) | `VerifyEmailAsync_AccountLockedWithCorrectCode_ThrowsAndLeavesUserUnverified` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 22 | T3 | Failure | The last allowed wrong attempt locks verification (423) and retires the code (existing, reseeded) | `VerifyEmailAsync_FinalFailedAttemptReachesMax_InvalidatesCodeAndSetsLockout` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 23 | T3 | Failure | The correct code is rejected after it expires (existing, reseeded) | `VerifyEmailAsync_CodeExpired_ThrowsBusinessException` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 24 | T3 | Failure | The correct code is rejected once it is used (existing, reseeded) | `VerifyEmailAsync_CodeAlreadyUsed_ThrowsBusinessException` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 25 | T3 | Failure | A valid code of another account does not verify this account (existing, reseeded) | `VerifyEmailAsync_CodeBelongsToDifferentAccount_ThrowsBusinessException` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 26 | T3 | Failure | After a resend, the first code is rejected (existing, reseeded) | `VerifyEmailAsync_WithCodeInvalidatedByResend_ThrowsBusinessException` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 27 | T3 | Happy | After registration, the stored value is the fingerprint of the emailed code, not the code | `RegisterAsync_ValidRequest_StoresFingerprintOfEmailedCodeNotTheCode` | `tests/RestaurantManagement.Modules.Identity.Tests/RegistrationServiceTests.cs` |
| 28 | T3 | Happy | After a resend, the stored value is the fingerprint of the emailed code, not the code | `ResendVerificationCodeAsync_OutsideCooldown_StoresFingerprintOfEmailedCodeNotTheCode` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 29 | T3 | Happy | A valid code verifies the user and is marked used (existing, reseeded) | `VerifyEmailAsync_ValidCode_MarksUserVerifiedAndCodeUsed` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 30 | T3 | Happy | After a resend, the newest emailed code verifies the user | `VerifyEmailAsync_NewestCodeAfterResend_MarksUserVerified` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 31 | T3 | Edge | Once the lock time has passed, the correct code verifies the user (existing, reseeded) | `VerifyEmailAsync_LockoutExpired_EvaluatesCodeAndVerifiesUser` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 32 | T3 | Guard | A resend inside the cooldown sends no email and creates no code | `ResendVerificationCodeAsync_WithinCooldown_ReturnsAcknowledgementWithoutSendingEmail` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 33 | T3 | Guard | An unknown email and a wrong code get the same status and message | `VerifyEmailAsync_UnknownEmailAndInvalidCode_ReturnSameStatusAndMessage` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 34 | T3 | Guard | A resend marks every earlier unused code used and emails one new code | `ResendVerificationCodeAsync_OutsideCooldown_MarksEarlierCodesUsedAndEmailsNewCode` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 35 | T3 | Guard | A resend while locked is refused with 423 and sends no email | `ResendVerificationCodeAsync_AccountLocked_ThrowsAndSendsNoEmail` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceTests.cs` |
| 36 | T4 | Failure | After release, a still-unexpired code stored before release cannot verify its user | `VerifyEmailAsync_CodeStoredBeforeRelease_IsRejected` | `tests/RestaurantManagement.Web.Tests/RetireVerificationCodesMigrationTests.cs` |
| 37 | T4 | Happy | The release migration marks every code stored before release as used | `Migrate_CodesStoredBeforeRelease_MarksEveryCodeUsed` | `tests/RestaurantManagement.Web.Tests/RetireVerificationCodesMigrationTests.cs` |
| 38 | T4 | Happy | The release migration blanks every stored code, so no readable code is left | `Migrate_CodesStoredBeforeRelease_BlanksEveryStoredCode` | `tests/RestaurantManagement.Web.Tests/RetireVerificationCodesMigrationTests.cs` |
| 39 | T4 | Edge | The release migration keeps each code's creation time, so the resend cooldown still works | `Migrate_CodesStoredBeforeRelease_KeepsCreatedAt` | `tests/RestaurantManagement.Web.Tests/RetireVerificationCodesMigrationTests.cs` |

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 The stored code is not readable | 13, 14, 16, 17, 27, 28 |
| #2 A valid code verifies | 18, 19, 29, 30 |
| #3 A wrong code is counted | 11, 12, 20, 25 |
| #4 The lock still works | 21, 22, 31, 35 |
| #5 Expired or used codes are rejected | 23, 24 |
| #6 Only the newest code works | 26, 30, 34 |
| #7 Old codes are retired | 15, 36, 37, 38, 39 |
| #8 A missing secret is caught | 1–10 |
| Rule 2: constant-time comparison | Not testable reliably by timing. Checked at review: the match uses `CryptographicOperations.FixedTimeEquals` |
| Rule 5: other rules unchanged (cooldown, uniform responses) | 32, 33 (plus the existing tests in `VerificationServiceTests.cs` and `AuthUniformResponseTests.cs`) |
| Rule 7: the secret is never committed | Checked at review: no key value in `appsettings*.json`, `docker-compose.yml` or `.env.example` |
