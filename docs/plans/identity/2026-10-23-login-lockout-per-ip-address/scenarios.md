# Scenarios: Login lockout per IP address

**Plan:** docs/plans/identity/2026-10-23-login-lockout-per-ip-address/plan.md
**Coverage:** 35 scenarios — 2 happy · 12 edge · 5 failure · 12 security · 4 guard (plus 9 existing tests updated, listed below)

Test files: unit tests (`tests/RestaurantManagement.Modules.Identity.Tests/`, on SQLite) and endpoint or database tests (Web = `tests/RestaurantManagement.Web.Tests/Identity/`, on PostgreSQL). `LockoutHarness.cs` (new helper in the Identity unit tests) drives the real login on SQLite so tests read as a story.

## T1 — Per-address and whole-account login lock
| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Security | After 5 wrong passwords from the kitchen tablet, the correct password from the manager's laptop still signs in | `LoginAsync_AddressLocked_CorrectPasswordFromAnotherAddressSignsIn` | `SessionServiceLockoutTests.cs` |
| 2 | T1 | Edge | 19 wrong passwords spread over five addresses do not lock the account (one under the limit of 20) | `LoginAsync_NineteenWrongPasswordsFromFiveAddresses_DoesNotLockTheAccount` | `SessionServiceLockoutTests.cs` |
| 3 | T1 | Security | 20 wrong passwords from five addresses lock the whole account for 15 minutes; every address is refused | `LoginAsync_TwentyWrongPasswordsFromFiveAddresses_LocksTheAccountForEveryAddress` | `SessionServiceLockoutTests.cs` |
| 4 | T1 | Security | Wrong passwords during an address lock are not counted, do not extend the lock and do not push the account to its limit | `LoginAsync_AddressLocked_WrongPasswordsAreNotCountedAndDoNotExtendTheLock` | `SessionServiceLockoutTests.cs` |
| 5 | T1 | Guard | A correct password restarts that address's count: four more wrong passwords do not lock it | `LoginAsync_CorrectPasswordAfterFourWrong_RestartsThatAddressCount` | `SessionServiceLockoutTests.cs` |
| 6 | T1 | Security | A correct password from one address does not reduce the account's count: 20 in total still lock it | `LoginAsync_CorrectPasswordFromOneAddress_LeavesTheAccountCountUntouched` | `SessionServiceLockoutTests.cs` |
| 7 | T1 | Guard | Another account's wrong passwords from the same address never count toward this account | `LoginAsync_AnotherAccountFailedFromTheSameAddress_DoesNotCountForThisAccount` | `SessionServiceLockoutTests.cs` |
| 8 | T1 | Edge | 19 wrong passwords more than an hour ago do not count toward the account limit | `LoginAsync_NineteenWrongPasswordsMoreThanAnHourAgo_DoNotCountTowardTheAccountLimit` | `SessionServiceLockoutTests.cs` |
| 9 | T1 | Edge | A new wrong password removes stored failures older than the window | `LoginAsync_NewFailureAfterTheWindow_RemovesFailuresOlderThanTheWindow` | `SessionServiceLockoutTests.cs` |
| 10 | T1 | Security | Once the 15-minute address lock has expired, the correct password from that address signs in | `LoginAsync_AddressLockExpired_CorrectPasswordFromThatAddressSignsIn` | `SessionServiceLockoutTests.cs` |
| 11 | T1 | Edge | A wrong password with no known address is counted under the address "unknown" | `LoginAsync_WrongPasswordWithNoKnownAddress_IsStoredAsUnknown` | `SessionServiceLockoutTests.cs` |
| 12 | T1 | Security | Locking the whole account sends one monitoring alert with the user ID and no email or IP address | `LoginAsync_AccountLimitReached_SentryMessageHasUserIdAndNoEmail` | `SessionServiceLockoutTests.cs` |
| 13 | T1 | Security | End to end through a trusted proxy: 5 wrong passwords from one forwarded address lock only that address; the other signs in | `Login_FiveWrongPasswordsFromOneForwardedAddress_OtherForwardedAddressSignsInAndThatOneIsRefused` | `LoginLockoutEndpointTests.cs` |
| 14 | T1 | Security | A forwarded address from a sender that is not a trusted proxy is ignored: the lock lands on the real sender | `Login_ForwardedForFromUntrustedSender_LockLandsOnTheRealSender` | `LoginLockoutEndpointTests.cs` |
| 15 | T1 | Guard | A wrong password, an address lock and an account lock all get the same status and body | `Login_WrongPasswordAddressLockedAndAccountLocked_AllGetTheSameStatusAndBody` | `LoginLockoutEndpointTests.cs` |
| 16 | T1 | Edge | The database has a login failures table with its columns and an index by user and time | `Migrate_CleanDatabase_CreatesLoginFailuresTableWithUserAndTimeIndex` | `LoginFailuresTableTests.cs` |
| 17 | T1 | Edge | Deleting a user deletes that user's stored login failures | `Migrate_CleanDatabase_LoginFailuresGoWithTheirUser` | `LoginFailuresTableTests.cs` |
| 18 | T1 | Edge | The old per-account failed-attempt column is gone from users | `Migrate_CleanDatabase_UsersHasNoFailedAttemptsColumn` | `LoginFailuresTableTests.cs` |
| 34 | T1 | Security | One address repeating 5 wrong passwords after each of its locks expires (20 in an hour) never locks the whole account; each address adds at most 5 | `LoginAsync_OneAddressRepeatsFiveWrongPasswordsAfterEachLockExpires_NeverLocksTheAccount` | `SessionServiceLockoutTests.cs` |
| 36 | T1 | Edge | The database has an index on login failures by failed-at alone, for the cleanup of expired rows | `Migrate_CleanDatabase_CreatesAnIndexOnLoginFailuresFailedAt` | `LoginFailuresFailedAtIndexTests.cs` |

## T2 — Account-locked email
| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 19 | T2 | Security | Locking the whole account queues exactly one email for the account owner | `LoginAsync_AccountLimitReached_QueuesOneAccountLockedEmailForTheOwner` | `AccountLockedEmailQueueTests.cs` |
| 20 | T2 | Guard | Locking only one address queues no email | `LoginAsync_OnlyTheAddressIsLocked_QueuesNoEmail` | `AccountLockedEmailQueueTests.cs` |
| 21 | T2 | Security | Wrong passwords during the account lock queue no further email | `LoginAsync_WrongPasswordsDuringTheAccountLock_QueueNoFurtherEmail` | `AccountLockedEmailQueueTests.cs` |
| 22 | T2 | Edge | Locking the account again more than 24 hours after the last lock email (already sent) queues a new email | `LoginAsync_AccountLockedAgainMoreThan24HoursAfterTheLastEmail_QueuesANewEmail` | `AccountLockedEmailQueueTests.cs` |
| 35 | T2 | Security | Locking the account again within 24 hours of the last lock email (already sent and removed) queues no new email | `LoginAsync_AccountLockedAgainWithin24HoursOfTheLastEmail_QueuesNoNewEmail` | `AccountLockedEmailQueueTests.cs` |
| 37 | T2 | Failure | A claimed account-locked request whose send already started (app crashed) is dropped without sending again and logged by ID only | `ProcessDueAsync_ClaimedRequestWhoseSendAlreadyStarted_DropsItWithoutSendingAndLogsOnlyItsId` | `AccountLockedEmailProcessorCrashTests.cs` |
| 38 | T2 | Failure | The send-started mark is set during the send and cleared when the send fails, so the retry still sends | `ProcessDueAsync_SendInProgressOrFailed_MarkSetThenClearedSoRetryStillSends` | `AccountLockedEmailProcessorCrashTests.cs` |
| 39 | T2 | Edge | The database has a nullable send-started column on pending emails, apart from the code-issued column | `Migrate_CleanDatabase_PendingEmailsHasANullableSendStartedAtColumn` | `PendingEmailSendStartedColumnTests.cs` |
| 23 | T2 | Happy | The processor sends an English email with the first name, the 15-minute pause and the advice to reset the password, then removes the request | `ProcessDueAsync_DueRequest_SendsEnglishEmailWithFirstNameAndResetAdviceAndRemovesRequest` | `AccountLockedEmailProcessorTests.cs` |
| 24 | T2 | Security | The email holds no code, no link and no IP address | `ProcessDueAsync_DueRequest_EmailHoldsNoCodeNoLinkAndNoIpAddress` | `AccountLockedEmailProcessorTests.cs` |
| 25 | T2 | Edge | A first name containing HTML is shown as text | `ProcessDueAsync_FirstNameWithHtml_IsShownAsText` | `AccountLockedEmailProcessorTests.cs` |
| 26 | T2 | Failure | A failed send is retried after 1, 2, 4 and 8 minutes (four cases) | `ProcessDueAsync_SendFails_RetriesAfterGrowingWait` | `AccountLockedEmailProcessorTests.cs` |
| 27 | T2 | Failure | The fifth failed send marks the request failed and keeps it | `ProcessDueAsync_FifthSendFails_MarksTheRequestFailedAndKeepsIt` | `AccountLockedEmailProcessorTests.cs` |
| 28 | T2 | Failure | A failed send is logged without the email address | `ProcessDueAsync_SendFails_LogsWithoutTheEmailAddress` | `AccountLockedEmailProcessorTests.cs` |
| 29 | T2 | Failure | A request for an account that no longer exists is removed and nothing is sent | `ProcessDueAsync_AccountNoLongerExists_RemovesRequestAndSendsNothing` | `AccountLockedEmailProcessorTests.cs` |
| 30 | T2 | Edge | Password reset and verification requests are left untouched (two cases) | `ProcessDueAsync_RequestOfAnotherPurpose_IsLeftUntouchedAndSendsNothing` | `AccountLockedEmailProcessorTests.cs` |
| 31 | T2 | Edge | The worker still runs the account-locked processor when the other two fail | `RunOnceAsync_ResetAndVerificationProcessorsThrow_AccountLockedProcessorStillRuns` | `PendingEmailWorkerAccountLockedTests.cs` |
| 32 | T2 | Failure | When the account-locked processor fails, the worker logs without the email address and the others still run | `RunOnceAsync_AccountLockedProcessorThrows_LogsWithoutEmailAndOtherProcessorsStillRun` | `PendingEmailWorkerAccountLockedTests.cs` |
| 33 | T2 | Happy | End to end with the worker running: 20 wrong passwords across addresses send exactly one email to the account | `Login_TwentyWrongPasswordsAcrossAddressesWithWorkerRunning_EmailsTheAccountExactlyOnce` | `AccountLockedEmailEndpointTests.cs` |

## T3 — Identity security notes
No automated test: the task changes three documents. The matrix check (`python3 .claude/scripts/check-security-matrices.py`) covers the matrix header; the other two files and the match with the shipped values (scenario E9) are checked at review.

## Existing tests updated
Only what the plan's changes force (the dropped `users.failed_attempts` column and the new lock rules). They may fail until T1 is built, like new tests.
| # | Task | Test | What changed |
|---|---|---|---|
| E1 | T1 | `SessionServiceLoginTests.LoginAsync_FourWrongPasswords_CountsFourAttemptsWithoutLocking` | Reads stored failures instead of the counter; four from one address count and lock nothing |
| E2 | T1 | `SessionServiceLoginTests.LoginAsync_FifthWrongPassword_LocksThatAddressFor15MinutesAndNotTheAccount` (renamed) | The 5th wrong password locks that address for 15 minutes, not the account; the correct password from it is refused |
| E3 | T1 | `SessionServiceLoginTests.LoginAsync_LockedAccountWrongPassword_Throws401WithoutCountingAttemptOrExtendingLock` | Checks no failure is stored; no counter seeded |
| E4 | T1 | `SessionServiceLoginTests.LoginAsync_CorrectPasswordAfterFailures_ClearsAddressCountAndRecordsLoginTime` (renamed) | Three real wrong passwords replace the seeded counter |
| E5 | T1 | `SessionServiceLoginTests.LoginAsync_LockExpiredCorrectPassword_IssuesTokensAndClearsLock` | Counter no longer seeded; still passes today |
| E6 | T1 | `PasswordResetServiceResetTests.ResetPasswordAsync_ValidCodeOnLockedAccount_ClearsAccountLockAndStoredLoginFailures` (renamed) | Reset deletes stored failures and clears the account lock |
| E7 | T1 | `PasswordChangeServiceTests.ChangePasswordAsync_WrongCurrentPasswordRepeatedly_NeverCountsTowardLoginLock` | Checks no failure is stored |
| E8 | T1 | `RegisterEndpointTests.Register_BodyCarriesProtectedFields_UserIsStillUnverifiedActiveAndWithoutMemberships` | Checks no failure is stored and the account is unlocked |
| E9 | T1 | `ShippedLimitsTests.Configuration_ShippedLockoutAndCodeLimits_MatchTheMatrix` | Also reads the two new keys (20 and 60) straight from `appsettings.json` |
| — | T1 | `IdentityTestDatabase.cs` (`TestSecurityOptions.Create`) | Two new optional parameters for the account limit and window (defaults 20 and 60); no existing caller changes |

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 One address cannot lock others out | 1, 10, 13, E2 |
| #2 Spread-out attacks are stopped | 2, 3, 6, 8, 34 |
| #3 No lock is revealed | 15 (plus the existing L5 tests, which stay green) |
| #4 Lock email sent once | 19–33, 35 |
| #5 Locked attempts are not counted | 4, 21, E3 |
| #6 Address rules are shared | 13, 14 |
| #7 Shipped limits match documentation | E9 (shipped values); the documents themselves at review (T3) |
