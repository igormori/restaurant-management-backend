# Scenarios: Forgot password takes the same time for every email

**Plan:** docs/plans/identity/2026-10-17-forgot-password-same-response-time/plan.md
**Coverage:** 25 scenarios — 7 happy · 8 edge · 6 failure · 4 security · 0 guard (scenarios 26 to 33 were added in fix rounds)

Test folders: unit tests (SQLite) in `tests/RestaurantManagement.Modules.Identity.Tests/`; endpoint, schema and wiring tests (PostgreSQL) in `tests/RestaurantManagement.Web.Tests/Identity/`. Short names below: Request = `PasswordResetServiceRequestTests.cs`, Processor = `PasswordResetEmailProcessorTests.cs`.

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Happy | After migrating a clean database the pending reset request table exists with its columns and its due-time index, and has no link to users | `Migrate_CleanDatabase_CreatesResetRequestTableWithDueTimeIndexAndNoUserLink` | `Web.Tests/Identity/PasswordResetRequestsTableTests.cs` |
| 2 | T2 | Edge | A request for a known, unknown, deactivated, unverified or reset-locked email adds exactly one queued request with the email as typed and creates no reset code | `RequestResetCodeAsync_AnyEmail_QueuesOneRequestAndCreatesNoCode` (5 cases) | Request |
| 3 | T2 | Security | A known and an unknown email run exactly the same database commands, so response times cannot tell them apart | `RequestResetCodeAsync_KnownAndUnknownEmail_RunTheSameDatabaseCommands` | Request |
| 4 | T2 | Security | The request never reads the users or the reset codes table | `RequestResetCodeAsync_KnownEmail_NeverReadsUsersOrResetCodes` | Request |
| 5 | T2 | Failure | When the email service throws, the acknowledgement is the same and the email service is not called during the request | `RequestResetCodeAsync_EmailServiceThrows_SameAcknowledgementAndEmailServiceNotCalled` | Request |
| 6 | T2 | Failure | When the email service never answers, the request still completes | `RequestResetCodeAsync_EmailServiceNeverAnswers_RequestStillCompletes` | Request |
| 7 | T2 | Security | Over HTTP a known and an unknown email get identical status and body, nothing is emailed during the request, and each request is queued | `ForgotPassword_KnownAndUnknownEmail_IdenticalStatusAndBody` | `Web.Tests/Identity/PasswordResetEndpointTests.cs` |
| 8 | T3 | Security | An unknown, deactivated or unverified email gets no email and no code, and its request is removed (3 cases) | `ProcessDueAsync_UnknownInactiveOrUnverifiedEmail_NoEmailNoCodeAndRequestRemoved` | Processor |
| 9 | T3 | Failure | A reset-locked account gets no email and no code; request removed | `ProcessDueAsync_ResetLocked_NoEmailNoCodeAndRequestRemoved` | Processor |
| 10 | T3 | Happy | An active, verified user gets one email with a 6-digit code, her name and the expiry; the request is removed | `ProcessDueAsync_ActiveVerifiedUser_EmailsSixDigitCodeWithNameAndExpiryAndRemovesRequest` | Processor |
| 11 | T3 | Happy | Only the keyed fingerprint of the emailed code is stored | `ProcessDueAsync_ActiveVerifiedUser_StoresOnlyProtectedFormOfCode` | Processor |
| 12 | T3 | Edge | A request inside the 60-second wait after a code gets no new code and no email | `ProcessDueAsync_CodeCreatedWithinResendWait_NoNewCodeNoEmailAndRequestRemoved` | Processor |
| 13 | T3 | Edge | An earlier unused code is invalidated, so only the newest works | `ProcessDueAsync_EarlierUnusedCode_IsInvalidatedSoOnlyTheNewestWorks` | Processor |
| 14 | T3 | Failure | A failed send keeps the request with one attempt and a retry in 1 minute; neither the code nor the email address is logged | `ProcessDueAsync_SendFails_KeepsRequestWithOneAttemptAndNothingSecretLogged` | Processor |
| 15 | T3 | Edge | The wait before the next try doubles with each failure: 1, 2, 4, 8 minutes (4 cases) | `ProcessDueAsync_SendFailsAfterEarlierAttempts_NextAttemptWaitDoubles` | Processor |
| 16 | T3 | Edge | A pass before the next attempt time does not retry | `ProcessDueAsync_PassBeforeNextAttemptTime_DoesNotRetry` | Processor |
| 17 | T3 | Happy | A send that failed is sent on the next due pass with a new code, and only that code works | `ProcessDueAsync_SendFailedEarlier_NextDuePassSendsNewCodeAndOnlyItWorks` | Processor |
| 18 | T3 | Failure | The fifth failed send marks the request failed, keeps it, and logs an error with the request id only | `ProcessDueAsync_FifthSendFails_MarksFailedKeepsRequestAndLogsOnlyItsId` | Processor |
| 19 | T3 | Edge | A request already marked failed is not tried again | `ProcessDueAsync_RequestMarkedFailed_IsNotTriedAgain` | Processor |
| 20 | T3 | Edge | A request claimed by another pass is skipped, so nobody gets two emails | `ProcessDueAsync_RequestClaimedByAnotherPass_IsSkipped` | Processor |
| 21 | T3 | Edge | One pass handles all due requests; an eligible email asked for twice plus an unknown email causes exactly one email | `ProcessDueAsync_SeveralDueRequests_EligibleEmailGetsExactlyOneEmail` | Processor |
| 22 | T3 | Happy | Over HTTP: forgot password, one processor pass, and the emailed code resets the password; exactly one email is sent | `ForgotPassword_ThenProcessorPass_EmailsOneCodeThatResetsThePassword` | `Web.Tests/Identity/PasswordResetEndpointTests.cs` |
| 23 | T4 | Failure | A pass that throws is logged without the code or email address, and the next run still calls the processor | `RunOnceAsync_ProcessorThrows_LogsErrorWithoutSecretsAndNextRunStillCallsProcessor` | `PasswordResetEmailWorkerTests.cs` |
| 24 | T4 | Happy | The worker is registered when switched on | `HostedServices_EmailWorkerEnabled_RegistersWorker` | `Web.Tests/Identity/PasswordResetEmailWorkerWiringTests.cs` |
| 25 | T4 | Happy | The worker can be switched off with `PasswordResetEmails:Enabled=false`, as every test host does (passes now only because the worker does not exist yet) | `HostedServices_EmailWorkerDisabledInTests_DoesNotRegisterWorker` | `Web.Tests/Identity/PasswordResetEmailWorkerWiringTests.cs` |
| 26 | T3 | Edge | One pass handles at most 100 due requests (oldest first); the rest wait for the next pass | `ProcessDueAsync_MoreDueRequestsThanBatchSize_HandlesOneBatchAndLeavesTheRest` | `PasswordResetEmailProcessorLimitsTests.cs` |
| 27 | T3 | Security | A pass removes failed requests older than 7 days (they hold a raw email address) and keeps newer ones | `ProcessDueAsync_FailedRequestsOlderAndNewerThanRetention_RemovesOnlyTheOlder` | `PasswordResetEmailProcessorLimitsTests.cs` |
| 28 | T4 | Happy | With the worker on and a 1-second interval, asking for a reset over HTTP is enough: the worker emails the user and removes the queued request, with no test calling the processor | `ForgotPassword_WorkerEnabledWithShortInterval_EmailsMariaAndRemovesRequestWithoutAnyoneCallingTheProcessor` | `Web.Tests/Identity/PasswordResetEmailWorkerEndpointTests.cs` |
| 29 | T3 | Failure | A claimed request whose code was already stored (a pass crashed before finishing) is dropped without sending again; a warning logs the request id only | `ProcessDueAsync_ClaimedRequestWithCodeAlreadyIssued_DropsItWithoutSendingAndLogsOnlyItsId` | `PasswordResetEmailProcessorCrashTests.cs` |
| 30 | T3 | Edge | The request is marked code-issued before the send and the mark is cleared when the send fails, so the retry still sends | `ProcessDueAsync_SendInProgressOrFailed_MarkSetThenClearedSoRetryStillSends` | `PasswordResetEmailProcessorCrashTests.cs` |
| 31 | T3 | Security | Two processors running a pass together over the same due requests on PostgreSQL send exactly one email per eligible request | `ProcessDueAsync_TwoProcessorsRunningTogether_SendExactlyOneEmailPerEligibleRequest` | `Web.Tests/Identity/PasswordResetEmailProcessorConcurrencyTests.cs` |
| 32 | T3 | Security | A pass that started long ago still claims each request for 5 minutes counted from the moment of the claim, so a slow email provider cannot make a later claim already expired | `ProcessDueAsync_PassStartedLongAgo_ClaimLastsFromTheMomentItIsTaken` | `PasswordResetEmailProcessorClaimTests.cs` |
| 33 | T3 | Security | A request that another instance changed after the pass loaded it (its code was already stored) is dropped without a second email, because the row is read again after the claim | `ProcessDueAsync_RowChangedByAnotherInstanceAfterLoading_ActsOnTheRowAsClaimed` | `PasswordResetEmailProcessorClaimTests.cs` |
| 34 | T4 | Failure | A reset email interval of 0 or below (0 and -5 tried) stops the app at startup with an error naming `PasswordResetEmails:IntervalSeconds` | `Startup_EmailWorkerIntervalZeroOrBelow_RefusesToStartNamingTheSetting` | `Web.Tests/Identity/PasswordResetEmailIntervalTests.cs` |
| 35 | T4 | Happy | A positive interval starts the app and registers the worker | `Startup_EmailWorkerIntervalAboveZero_StartsAndRegistersWorker` | `Web.Tests/Identity/PasswordResetEmailIntervalTests.cs` |

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 Response does not wait for email delivery | 5, 6, 7 |
| #2 Known and unknown emails cannot be told apart by time | 3, 4, 2, 7. Proved by comparing database commands, not by measuring time (timing tests are unreliable); the plan states this choice |
| #3 A failed delivery is not lost | 14, 15, 16, 17, 18, 23 |
| #4 Eligible email still gets its code | 10, 11, 17, 21, 22 |
| #5 Other emails get no email | 8, 9, 12 |

## Existing tests updated
Forced by the plan's changes (the request no longer creates codes or sends email):
- `PasswordResetServiceRequestTests.cs`: the old tests (unknown, inactive or unverified, email fails, active user emailed, only fingerprint stored, wait, reset locked) tested behaviour that moves to the processor. They are replaced by scenarios 2–6 and 8–12 and 14 above; nothing is dropped. Matrix rows P1–P5 changed to match.
- `PasswordResetServiceResetTests.cs`, `PasswordResetInactiveUserTests.cs`, `PasswordAuditFailureTests.cs`: they got a code by calling the forgot-password request and reading the email. They now store the code the way the processor does, so they keep proving the reset rules and stay green after T2. Assertions unchanged.
- `PasswordResetEndpointTests.cs`: the two reset tests (`ResetPassword_ValidCode_*`) store the code directly instead of asking for it; `ForgotPassword_KnownAndUnknownEmail_IdenticalStatusAndBody` now expects no email during the request (scenario 7).
- Test helpers: `IdentityTestDatabase.CreateContext` accepts interceptors and returns a context that also knows the queue table (`QueueAwareIdentityDbContext`, removable after T1); new helpers `CapturingLogger.cs`, `QueryCommandRecorder.cs`; `TestHostSettings.cs` switches the new worker off for test hosts.

## Fix round
Scenario 1 (D18) now also expects the `code_issued_at` column; the user approved this one assertion change.

Fix round 2 (after the second review): scenarios 32 and 33 (claim from the real clock, row read again after the claim) are new. Scenario 31 keeps its name and assertion but the user approved changing its body so the two passes really overlap.

## Decisions at Gate 2
- Outcome #2 (no timing difference) is accepted as proved by comparing database commands (scenarios 3 and 4), not by measuring time.
- The `QueueAwareIdentityDbContext` test helper is accepted and stays after T1, because tests are read-only after Gate 2.
