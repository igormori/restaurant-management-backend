# Scenarios: Resend verification answers every email the same way

**Plan:** docs/plans/identity/2026-10-19-resend-verification-same-response/plan.md
**Coverage:** 25 scenarios — 4 happy · 7 edge · 8 failure · 5 security · 1 guard

Test folders: unit tests (on SQLite, the in-memory test database) are in `tests/RestaurantManagement.Modules.Identity.Tests/`;
endpoint and PostgreSQL tests are in `tests/RestaurantManagement.Web.Tests/Identity/`. File names below are relative to those two folders.
Matrix row IDs refer to `docs/security/identity-matrix.md`. This is a public, account-level endpoint with no tenant data and no role, so no
cross-tenant or role-rejection scenario applies (module `CLAUDE.md`).

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Edge | A queued request must have a purpose (password reset or verification); the database column is required | `Migrate_CleanDatabase_PurposeColumnIsRequired` (D25) | `Web.Tests/Identity/PasswordResetRequestsTableTests.cs` |
| 2 | T1 | Guard | "Forgot password" still queues its request as a password reset request | `RequestResetCodeAsync_AnyEmail_QueuesTheRequestWithThePasswordResetPurpose` (P1) | `PasswordResetServiceRequestTests.cs` |
| 3 | T1 | Failure | A password reset pass leaves a due verification request alone: not claimed, not tried, no reset email, no reset code | `ProcessDueAsync_DueVerificationRequest_IsLeftUntouchedByAResetPass` (P28) | `PasswordResetEmailProcessorPurposeTests.cs` |
| 4 | T1 | Edge | A password reset pass does not purge an old failed verification request | `ProcessDueAsync_OldFailedVerificationRequest_IsNotPurgedByAResetPass` (P28) | `PasswordResetEmailProcessorPurposeTests.cs` |
| 5 | T2 | Failure | Unknown, already verified or verification-locked email: no code, no email, request removed | `ProcessDueAsync_UnknownVerifiedOrLockedEmail_NoEmailNoCodeAndRequestRemoved` (3 cases, RS7) | `VerificationEmailProcessorTests.cs` |
| 6 | T2 | Failure | Account that got a code less than 60 seconds ago: no new code, no email, request removed | `ProcessDueAsync_CodeCreatedWithinResendWait_NoNewCodeNoEmailAndRequestRemoved` (RS7) | `VerificationEmailProcessorTests.cs` |
| 7 | T2 | Failure | Email fails to send: request is kept and retried after 1 minute, not lost | `ProcessDueAsync_SendFails_KeepsRequestAndSchedulesRetryInOneMinute` (RS9) | `VerificationEmailProcessorTests.cs` |
| 8 | T2 | Security | A sending failure is logged with the user ID, never the email address or the code | `ProcessDueAsync_SendFails_LogsTheUserIdAndNeverTheEmailAddressOrCode` (RS11) | `VerificationEmailProcessorTests.cs` |
| 9 | T2 | Edge | A retry after a failed send goes out even though its own failed code is only seconds old (the 60-second wait does not block a retry) | `ProcessDueAsync_RetryAfterFailedSend_SendsDespiteItsOwnRecentCodeAndRemovesRequest` (RS9) | `VerificationEmailProcessorTests.cs` |
| 10 | T2 | Edge | Fifth failed send: request marked failed and kept, never retried again | `ProcessDueAsync_FifthSendFails_MarksRequestFailedAndKeepsIt` (RS9) | `VerificationEmailProcessorTests.cs` |
| 11 | T2 | Edge | A pass crashed after storing the code: the request is dropped without a second email | `ProcessDueAsync_ClaimedRequestWithCodeAlreadyIssued_DropsItWithoutSending` (RS10) | `VerificationEmailProcessorTests.cs` |
| 12 | T2 | Edge | A request another pass is handling is skipped, so nobody gets two emails | `ProcessDueAsync_RequestClaimedByAnotherPass_IsSkipped` (RS10) | `VerificationEmailProcessorTests.cs` |
| 13 | T2 | Happy | Eligible unverified account gets exactly one email with a 6-digit code, first name and expiry; request removed | `ProcessDueAsync_EligibleAccount_EmailsOneSixDigitCodeWithNameAndExpiryAndRemovesRequest` (RS8) | `VerificationEmailProcessorTests.cs` |
| 14 | T2 | Happy | Only the keyed fingerprint of the emailed code is stored, never the readable code | `ProcessDueAsync_EligibleAccount_StoresOnlyTheFingerprintOfTheEmailedCode` (RS8) | `VerificationEmailProcessorTests.cs` |
| 15 | T2 | Edge | Account whose verification lock has expired gets a code; failed-attempt counter reset and lock cleared | `ProcessDueAsync_LockExpired_IssuesCodeAndClearsFailedAttemptsAndLock` (RS4) | `VerificationEmailProcessorTests.cs` |
| 16 | T2 | Failure | A code issued before the queued resend is cancelled: verifying with it is refused with 401 | `VerifyEmailAsync_CodeIssuedBeforeAQueuedResend_IsRefusedWithA401` (V7) | `VerificationEmailProcessorCodeTests.cs` |
| 17 | T2 | Happy | The code from the queued resend email verifies the account | `VerifyEmailAsync_CodeFromAQueuedResend_MarksUserVerified` (V7) | `VerificationEmailProcessorCodeTests.cs` |
| 18 | T2 | Failure | The password reset processor fails: the verification processor still runs in the same worker pass | `RunOnceAsync_ResetProcessorThrows_VerificationProcessorStillRuns` (D26) | `PasswordResetEmailWorkerBothProcessorsTests.cs` |
| 19 | T2 | Failure | The verification processor fails: the reset processor still runs, and the log has no email address or code | `RunOnceAsync_VerificationProcessorThrows_ResetProcessorStillRunsAndLogsNoSecrets` (D26) | `PasswordResetEmailWorkerBothProcessorsTests.cs` |
| 20 | T2 | Happy | End to end on PostgreSQL: the running worker emails a queued verification request and the code verifies the account through the endpoint | `Verify_QueuedVerificationRequestWithWorkerRunning_CodeInTheEmailVerifiesMaria` (D27) | `Web.Tests/Identity/VerificationEmailWorkerEndpointTests.cs` |
| 21 | T3 | Security | Eligible, unknown, verified, locked and within-wait emails all get the same status and body from the endpoint (reproduces the bug: eligible got a different message today) | `ResendVerification_EveryAccountState_ReturnsTheSameStatusAndBody` (RS1) | `Web.Tests/Identity/VerificationResendEndpointTests.cs` |
| 22 | T3 | Security | Resend only queues one verification request with the email as typed (5 account states): no code created, no account changed | `ResendVerificationCodeAsync_AnyEmail_QueuesOneVerificationRequestAndChangesNothingElse` (5 cases, RS2) | `VerificationServiceResendTests.cs` |
| 23 | T3 | Security | An eligible email and an unknown email run exactly the same database commands, so response times cannot tell them apart | `ResendVerificationCodeAsync_EligibleAndUnknownEmail_RunTheSameDatabaseCommands` (RS3) | `VerificationServiceResendTests.cs` |
| 24 | T3 | Security | The verification service no longer takes an email service, so it can neither send nor wait for an email | `VerificationService_Constructor_TakesNoEmailService` (RS2) | `VerificationServiceResendTests.cs` |
| 25 | T3 | Failure | With a failing email service the endpoint still returns the same 200 answer as for an unknown email, never a 500 | `ResendVerification_EmailServiceFails_ReturnsTheSameAnswerAsAnUnknownEmail` (RS6) | `Web.Tests/Identity/VerificationResendEndpointTests.cs` |

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 All emails get the same answer | 21, 22 |
| #2 Response does not wait for email delivery | 24 (no email dependency, so slow sending cannot delay the request), 25 (failing sending) |
| #3 Emails cannot be told apart by time | 23 (same database commands, the proof used instead of wall-clock timing, as for forgot password P20) |
| #4 A failed delivery is not lost | 7, 9, 10 |
| #5 Eligible account still gets its code | 13, 14, 17, 20 |
| #6 Other accounts get no email | 5, 6 |
| #7 Matrix rows RS1 to RS6 describe the new behaviour | RS1 to RS6 rewritten, RS7 to RS11 added; checked by `python3 .claude/scripts/check-security-matrices.py` (passes now; rows turn from ❌ to ✅ when their tests pass) |

## Existing tests updated
Forced by the plan's changes (the verification service stops taking an email service, and resend only queues):
- `VerificationServiceTests.cs`, `VerificationEmailFailureLogPrivacyTests.cs`, `VerificationServiceEmailFailureLogTests.cs` (file deleted): the 14 resend tests that expected an email, `VerificationCodeResent` or a 500 are removed. Their behaviour moved to scenarios 5 to 17 (RS1 to RS4, RS6, V7 and the register log test R12 stays).
- `VerificationServiceTests.cs`, `AuthUniformResponseTests.cs`, `SessionServiceVerificationTests.cs`: build the service through the new helper `TestVerificationService.Create`, which compiles with and without an email service in the constructor. The unused `emailService` argument of `CreateSut` in `VerificationServiceTests.cs` is left as is.
- `TestVerificationService.Create` (new test helper) matches the service constructor by type, so it is exempt from read-only status after Gate 2: B2 step 2 relies on it. It must not be changed to weaken a test.
- `PasswordResetRequestsTableTests.Migrate_CleanDatabase_CreatesResetRequestTableWithDueTimeIndexAndNoUserLink` (D18): expected column list now includes `purpose`. It fails until T1.
- `AuthControllerTests.ResendVerification_ValidRequest_ReturnsOkWithMessageBody`: only its Rule tag moved from RS2 to RS1 (`--fix-tags`); it still passes.
- `IdentityTestDatabase.cs` (`QueueAwareIdentityDbContext`): also maps `Purpose` for SQLite tests until the module maps it itself.
