# Scenarios: Pending-email queue gets neutral names

**Plan:** docs/plans/identity/2026-10-19-pending-email-queue-neutral-names/plan.md
**Coverage:** 12 scenarios — 5 happy · 3 edge · 3 failure · 0 security · 1 guard

This is a rename with no behaviour change, so the scenarios prove the renamed pieces (table, limits, worker, settings) work and the behaviour stays the same. The tests already covering each behaviour are renamed, not duplicated (see "Existing tests updated"). No endpoint or tenant data is touched, so there are no Security scenarios.

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Edge | A clean migration creates the `pending_emails` table with the same columns, key, two indexes and no link to users | `Migrate_CleanDatabase_CreatesPendingEmailTableWithDueTimeIndexAndNoUserLink` | `tests/RestaurantManagement.Web.Tests/Identity/PendingEmailsTableTests.cs` |
| 2 | T1 | Failure | A queued email without a purpose is refused by the database (purpose column required) | `Migrate_CleanDatabase_PurposeColumnIsRequired` | `tests/RestaurantManagement.Web.Tests/Identity/PendingEmailsTableTests.cs` |
| 3 | T1 | Edge | More due emails than the batch size: one batch is handled, the rest wait (limit now on the shared queue holder) | `ProcessDueAsync_MoreDueRequestsThanBatchSize_HandlesOneBatchAndLeavesTheRest` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetEmailProcessorLimitsTests.cs` |
| 4 | T1 | Edge | Failed emails older than the retention are removed, newer ones kept (limit now on the shared queue holder) | `ProcessDueAsync_FailedRequestsOlderAndNewerThanRetention_RemovesOnlyTheOlder` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetEmailProcessorLimitsTests.cs` |
| 5 | T1 | Happy | A forgot-password request queues one pending email with the password reset purpose and creates no code | `RequestResetCodeAsync_AnyEmail_QueuesTheRequestWithThePasswordResetPurpose` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceRequestTests.cs` |
| 6 | T1 | Happy | A resend-verification request queues one pending email for any address and changes nothing else | `ResendVerificationCodeAsync_AnyEmail_QueuesOneVerificationRequestAndChangesNothingElse` | `tests/RestaurantManagement.Modules.Identity.Tests/VerificationServiceResendTests.cs` |
| 7 | T2 | Happy | With `PendingEmails:Enabled` true the host runs the pending email worker | `HostedServices_EmailWorkerEnabled_RegistersWorker` | `tests/RestaurantManagement.Web.Tests/Identity/PendingEmailWorkerWiringTests.cs` |
| 8 | T2 | Guard | The worker stays off in the test host (switched off by `PendingEmails__Enabled`) | `HostedServices_EmailWorkerDisabledInTests_DoesNotRegisterWorker` | `tests/RestaurantManagement.Web.Tests/Identity/PendingEmailWorkerWiringTests.cs` |
| 9 | T2 | Failure | An interval of 0 or below stops startup and the error names `PendingEmails:IntervalSeconds` (parameterized: 0 and -5) | `Startup_EmailWorkerIntervalZeroOrBelow_RefusesToStartNamingTheSetting` | `tests/RestaurantManagement.Web.Tests/Identity/PendingEmailIntervalTests.cs` |
| 10 | T2 | Happy | A valid interval starts the app and registers the pending email worker | `Startup_EmailWorkerIntervalAboveZero_StartsAndRegistersWorker` | `tests/RestaurantManagement.Web.Tests/Identity/PendingEmailIntervalTests.cs` |
| 11 | T2 | Happy | The real worker loop sends the queued password reset email and removes the row on its own | `ForgotPassword_WorkerEnabledWithShortInterval_EmailsMariaAndRemovesRequestWithoutAnyoneCallingTheProcessor` | `tests/RestaurantManagement.Web.Tests/Identity/PendingEmailWorkerEndpointTests.cs` |
| 12 | T2 | Failure | The worker pass logs a failing processor without secrets and the next pass still runs (both processors, each failure isolated) | `RunOnceAsync_VerificationProcessorThrows_ResetProcessorStillRunsAndLogsNoSecrets` | `tests/RestaurantManagement.Modules.Identity.Tests/PendingEmailWorkerBothProcessorsTests.cs` |

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 The queue, sending job and settings have neutral names | 1, 3, 4, 7, 9, 10. Also checked by a search for the old names at review (plan DoD) |
| #2 Password reset and resend verification emails still go out and retry as before | 5, 6, 11, 12 and every test under "Existing tests updated" |
| #3 The security rules list matches the new names | Rows D18–D26 now name the new tests; `python3 .claude/scripts/check-security-matrices.py` (passes now and after) |
| #4 The full build and test suite pass | Final full-suite run at Gate 3 |

## Existing tests updated
Renamed to the new types, settings and table name only; no assertion changed. They fail now (they use the new names, which are stubs) and pass once T1/T2 are done:
- Identity.Tests: all `PasswordResetEmailProcessor*Tests`, `VerificationEmailProcessor*Tests`, `PasswordResetServiceRequestTests`, `VerificationServiceResendTests` (type names `PendingEmail`, `PendingEmailPurpose`, limits from `PendingEmailQueue`); `PasswordResetEmailWorker*Tests` renamed `PendingEmailWorker*Tests` (rows D21, D24, D26).
- Web.Tests/Identity: `PasswordResetRequestsTableTests` renamed `PendingEmailsTableTests`; `PasswordResetEmailProcessorConcurrencyTests`, `PasswordResetEndpointTests`, `VerificationEmailWorkerEndpointTests` (row D27) updated to the new names; `TestHostSettings.cs` sets `PendingEmails__Enabled`.
- `IdentityTestDatabase.cs`: the helper class `QueueAwareIdentityDbContext` (a leftover from before the module mapped the purpose column) is removed and the plain `IdentityDbContext` used, because mapping the stub entity in the test model broke unrelated tests. Behaviour unchanged.
- Test method renamed: `Migrate_CleanDatabase_CreatesResetRequestTable...` to `Migrate_CleanDatabase_CreatesPendingEmailTable...` (row D18).

## Notes for Gate 2
- **Guard #8 is weak until T2.** It passes today only because no worker class is registered yet, and it would also pass if the `PendingEmails__Enabled` key were misspelled. It becomes meaningful once T2 removes the old key from `TestHostSettings.cs`.
- **Test-file edits after Gate 2 (approved with this file):** (a) T2 removes the line marked TEMPORARY (`PasswordResetEmails__Enabled`) from `tests/RestaurantManagement.Web.Tests/TestHostSettings.cs`; (b) the removal of `QueueAwareIdentityDbContext` from `IdentityTestDatabase.cs` is already done in this step.
- **Old settings no longer read:** no scenario proves that `PasswordResetEmails:*` is ignored. The scenario budget (12) is full, so the final search for old names (plan DoD) is the only check. Accepted.
- **Count:** scenario #12 is one of the 12 numbered scenarios and is listed under "Existing tests updated" only as the family of renamed worker tests, not counted twice. Red state: most renamed tests fail with "type not in the model" instead of `NotImplementedException`, because the stub entity is `[NotMapped]`; this is the natural red state for a rename.
- Pre-existing tests in `PasswordResetServiceRequestTests` and `VerificationServiceResendTests` that do not touch the queue type pass now; they cannot prove the rename.
