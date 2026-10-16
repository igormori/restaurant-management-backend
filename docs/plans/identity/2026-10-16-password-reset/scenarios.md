# Scenarios: Forgot and change password

**Plan:** docs/plans/identity/2026-10-16-password-reset/plan.md
**Coverage:** 43 scenarios — 16 happy · 3 edge · 18 failure · 4 security · 2 guard

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Happy | The reset email shows the user's first name, the code, how many minutes it lasts, and says it is about a password and can be ignored | `Build_ValidInput_ContainsNameCodeExpiryAndIgnoreNotice` | `tests/RestaurantManagement.Shared.Tests/PasswordResetEmailTemplateTests.cs` |
| 2 | T1 | Edge | A first name containing HTML is shown as plain text in the email, never as HTML | `Build_FirstNameWithHtmlCharacters_IsHtmlEncoded` | `tests/RestaurantManagement.Shared.Tests/PasswordResetEmailTemplateTests.cs` |
| 3 | T1 | Security | In development without a mail server, the log shows who the email is for and its subject, but never the body that holds the code | `SendEmailAsync_DevelopmentWithoutSmtpHost_LogsRecipientAndSubjectButNotBody` | `tests/RestaurantManagement.Shared.Tests/EmailServiceDevelopmentLogTests.cs` |
| 4 | T2 | Failure | A zero or negative limit or time window for the forgot, reset or change password request limits stops the app at startup, naming the bad setting | `Validate_PasswordPolicyLimitZeroOrNegative_FailsNamingTheSetting` | `tests/RestaurantManagement.Shared.Tests/AuthRateLimitOptionsValidatorTests.cs` |
| 5 | T3 | Happy | After the database update, the reset code table exists with its columns and a user index, and users have their own reset attempt counter and lock time | `Migrate_CleanDatabase_CreatesResetCodeTableIndexAndUserCounters` | `tests/RestaurantManagement.Web.Tests/PasswordResetCodesTableTests.cs` |
| 6 | T3 | Happy | Deleting a user deletes their reset codes too (the database rule is cascade) | `Migrate_IdentityForeignKeys_RolesRestrictCodesResetCodesAndRefreshTokensCascade` | `tests/RestaurantManagement.Web.Tests/IdentityDeleteRulesTests.cs` |
| 7 | T4 | Failure | A password that is too short, too long, or lacks an uppercase letter, a digit or a symbol is refused | `GetValidationResult_PasswordBreaksARule_ReturnsError` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordRulesAttributeTests.cs` |
| 8 | T4 | Edge | A password of exactly 8 or exactly 128 characters that meets the other rules is accepted | `GetValidationResult_PasswordAtLengthLimits_ReturnsSuccess` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordRulesAttributeTests.cs` |
| 9 | T4 | Guard | Registration keeps returning the same translated password errors (too short, too long, missing character types, missing) after the rules move | `Register_PasswordBreaksRule_ReturnsUnchangedLocalizedError` | `tests/RestaurantManagement.Web.Tests/IdentityMessagesEndpointTests.cs` |
| 10 | T5 | Happy | A reset or change for a user in two organizations writes one audit entry in each, naming the user, with no before, after or reason values | `RecordAsync_UserInTwoOrganizations_OneEntryPerOrganizationWithoutValues` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordAuditRecorderTests.cs` |
| 11 | T5 | Security | No audit entry is written to an organization where the user holds no role, even if another user belongs to it | `RecordAsync_AnotherUserInAnotherOrganization_NoEntryInThatOrganization` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordAuditRecorderTests.cs` |
| 12 | T5 | Edge | A user who belongs to no organization gets no audit entry | `RecordAsync_UserWithoutOrganization_RecordsNothing` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordAuditRecorderTests.cs` |
| 13 | T6 | Failure | Asking for a code with an unknown email gets the same answer as a known one, and no email is sent | `RequestResetCodeAsync_UnknownEmail_SameAcknowledgementAsKnownEmailAndNoEmail` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceRequestTests.cs` |
| 14 | T6 | Failure | An inactive user, or one who never verified their email, gets the unknown-email answer, no email and no stored code | `RequestResetCodeAsync_InactiveOrUnverifiedUser_NoEmailAndUnknownEmailAcknowledgement` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceRequestTests.cs` |
| 15 | T6 | Failure | When the email cannot be sent, the user still gets the same answer and the log never contains the code | `RequestResetCodeAsync_EmailSendFails_SameAcknowledgementAndCodeNotLogged` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceRequestTests.cs` |
| 16 | T6 | Happy | An active, verified user is emailed a 6-digit code with their first name and the expiry minutes | `RequestResetCodeAsync_ActiveVerifiedUser_EmailsSixDigitCodeWithNameAndExpiry` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceRequestTests.cs` |
| 17 | T6 | Happy | The stored code is a protected form that differs from the emailed code | `RequestResetCodeAsync_ActiveVerifiedUser_StoresOnlyProtectedFormOfCode` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceRequestTests.cs` |
| 18 | T7 | Failure | Resetting with an unknown email is refused with exactly the same error as a wrong code | `ResetPasswordAsync_UnknownEmail_SameRejectionAsWrongCode` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceResetTests.cs` |
| 19 | T7 | Failure | A wrong code is refused and nothing changes: same password, every login still active, no audit entry | `ResetPasswordAsync_WrongCode_RejectedAndNothingChanges` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceResetTests.cs` |
| 20 | T7 | Failure | An expired code is refused and the password stays the same | `ResetPasswordAsync_ExpiredCode_RejectedAndPasswordUnchanged` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceResetTests.cs` |
| 21 | T7 | Failure | Using the same code a second time is refused | `ResetPasswordAsync_CodeAlreadyUsed_SecondResetRejected` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceResetTests.cs` |
| 22 | T7 | Failure | After too many wrong codes, even the correct code is refused and the password stays the same | `ResetPasswordAsync_TooManyWrongCodes_CorrectCodeRejectedAfterwards` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceResetTests.cs` |
| 23 | T7 | Failure | After asking for a second code, the first code no longer works | `ResetPasswordAsync_FirstCodeAfterSecondRequest_Rejected` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceResetTests.cs` |
| 24 | T7, T8 | Failure | A weak new password is refused on both reset and change | `Validate_WeakNewPassword_ReturnsNewPasswordError` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordRulesAttributeTests.cs` |
| 25 | T7 | Happy | With a valid code, the new password works and the old one no longer does | `ResetPasswordAsync_ValidCode_NewPasswordVerifiesAndOldDoesNot` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceResetTests.cs` |
| 26 | T7 | Happy | A successful reset ends the user's logins on every device, marked "password changed" | `ResetPasswordAsync_ValidCode_RevokesEveryRefreshTokenAsPasswordChanged` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceResetTests.cs` |
| 27 | T7 | Happy | A successful reset is audited exactly once as a password reset of that user | `ResetPasswordAsync_ValidCode_RecordsPasswordResetAuditOnce` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceResetTests.cs` |
| 28 | T8 | Failure | A change for a user who does not exist, is inactive or is unverified is refused as an invalid session | `ChangePasswordAsync_UnknownInactiveOrUnverifiedUser_RejectedAsInvalidSession` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordChangeServiceTests.cs` |
| 29 | T8 | Failure | A wrong current password is refused and nothing changes: same password, logins active, no audit entry | `ChangePasswordAsync_WrongCurrentPassword_RejectedAndNothingChanges` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordChangeServiceTests.cs` |
| 30 | T8 | Failure | A new password equal to the current one is refused and the logins stay active | `ChangePasswordAsync_NewPasswordSameAsCurrent_RejectedAndSessionsKept` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordChangeServiceTests.cs` |
| 31 | T8 | Happy | With the correct current password, the new password works and the old one no longer does | `ChangePasswordAsync_CorrectCurrentPassword_NewPasswordVerifiesAndOldDoesNot` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordChangeServiceTests.cs` |
| 32 | T8 | Happy | A successful change ends the user's logins on every device, marked "password changed" | `ChangePasswordAsync_CorrectCurrentPassword_RevokesEveryRefreshTokenAsPasswordChanged` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordChangeServiceTests.cs` |
| 33 | T8 | Happy | A successful change is audited exactly once as a password change of that user | `ChangePasswordAsync_CorrectCurrentPassword_RecordsPasswordChangedAuditOnce` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordChangeServiceTests.cs` |
| 34 | T9 | Guard | Changing a password without being signed in is refused with 401 (already true: the app is private by default) | `ChangePassword_NoAccessToken_ReturnsUnauthorized` | `tests/RestaurantManagement.Web.Tests/PasswordChangeEndpointTests.cs` |
| 35 | T9 | Security | Maria's sign-in cannot change João's password by naming him in the request: only Maria's password changes and João stays logged in | `ChangePassword_BodyNamesAnotherUser_ChangesOnlyCallerAndOtherSessionsStayActive` | `tests/RestaurantManagement.Web.Tests/PasswordChangeEndpointTests.cs` |
| 36 | T9 | Happy | Through the API, "forgot password" answers a known and an unknown email with the same status and the same body | `ForgotPassword_KnownAndUnknownEmail_IdenticalStatusAndBody` | `tests/RestaurantManagement.Web.Tests/PasswordResetEndpointTests.cs` |
| 37 | T9 | Happy | Through the API, a reset with the emailed code succeeds, the answer contains neither the code nor the password, and every earlier login is refused | `ResetPassword_ValidCode_ReturnsOkWithoutSecretsAndEarlierRefreshTokensRefused` | `tests/RestaurantManagement.Web.Tests/PasswordResetEndpointTests.cs` |
| 38 | T9 | Happy | Through the API, a reset shows up in the organization's audit log as a password reset of the user, with no password data | `ResetPassword_ValidCode_AuditEntryInUsersOrganizationWithoutPasswordData` | `tests/RestaurantManagement.Web.Tests/PasswordResetEndpointTests.cs` |
| 39 | T9 | Happy | Through the API, a successful change clears the login cookie and the earlier login is refused | `ChangePassword_CorrectCurrentPassword_ClearsRefreshCookieAndEarlierTokenRefused` | `tests/RestaurantManagement.Web.Tests/PasswordChangeEndpointTests.cs` |
| 40 | T9 | Failure | Going over the request limit on forgot, reset or change password gets "too many requests" (429) | `PasswordEndpoint_LimitExceeded_ReturnsTooManyRequests` | `tests/RestaurantManagement.Web.Tests/AuthRateLimitingTests.cs` |
| 41 | T8 | Failure | Fix round: if writing the audit entry fails after a change is saved, the change still succeeds and the error is logged | `ChangePasswordAsync_AuditWriteFails_SucceedsAndLogsError` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordAuditFailureTests.cs` |
| 42 | T7 | Failure | Fix round: if writing the audit entry fails after a reset is saved, the reset still succeeds and the error is logged | `ResetPasswordAsync_AuditWriteFails_SucceedsAndLogsError` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordAuditFailureTests.cs` |
| 43 | T7 | Security | Fix round: a user deactivated after asking for a code gets the same answer as a wrong code, and nothing changes (password, sessions, audit) | `ResetPasswordAsync_DeactivatedAfterRequestingCode_RejectedLikeWrongCodeAndNothingChanges` | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetInactiveUserTests.cs` |

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 Unknown email is not revealed | 13, 14, 18, 36 |
| #2 Valid code resets the password | 1, 25, 37 |
| #3 Code is single use | 21 |
| #4 Expired, wrong or over-attempted code is rejected | 19, 20, 22 |
| #5 Only newest code works | 23 |
| #6 Stored code is not readable | 17 |
| #7 Weak new password is rejected | 7, 8, 24 |
| #8 Change needs current password | 29, 31 |
| #9 All sessions end | 26, 32, 37, 39 |
| #10 Rate limit applies (and codes and passwords never in logs or responses) | 3, 4, 15, 37, 40 |
| #11 Actions are audited | 10, 11, 12, 27, 33, 38, 41, 42 |
| #12 Inactive or unverified user gets no code | 14, 43 |

Spec rule 8 (same password refused on change) is scenario 30. Spec rule 2's lockout uses the
existing verification limits (scenario 22).

## Existing tests updated
- `IdentityDeleteRulesTests.Migrate_IdentityForeignKeys_RolesRestrictCodesAndRefreshTokensCascade`
  renamed to `..._RolesRestrictCodesResetCodesAndRefreshTokensCascade` and now also expects
  `fk_password_reset_codes_users_user_id:CASCADE` (scenario 6). It lists every Identity foreign key,
  so the new T3 table forces the change; it fails until T3 is done.
- New tests were added to existing classes without changing their other tests:
  `AuthRateLimitOptionsValidatorTests` (4), `IdentityMessagesEndpointTests` (9), `AuthRateLimitingTests` (40).
