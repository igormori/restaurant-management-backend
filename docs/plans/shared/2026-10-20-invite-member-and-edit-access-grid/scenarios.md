# Scenarios: Invite a member and edit their access grid

**Plan:** docs/plans/shared/2026-10-20-invite-member-and-edit-access-grid/plan.md
**Coverage:** 47 scenarios — 12 happy · 5 edge · 7 failure · 22 security · 1 guard

Plan size is L (budget 40); scenarios 41–43 were added after the test-reviewer found the plan's DoD asked for them (3 over budget, flagged for Gate 2). Variants of one rule are parameterized tests, so a scenario can run several cases. Security matrix rows for every scenario are written in `docs/security/identity-matrix.md` (T43–T79), `shared-matrix.md` (SH43, SH44) and `organization-matrix.md` (OR44), all ❌ until the tests pass.

| # | Task | Type | Scenario (plain words) | Test name | Test file |
|---|---|---|---|---|---|
| 1 | T1 | Happy | Asking for a location of the token organization also returns its name | `GetLocationAsync_LocationOfTheTokenOrganization_ReturnsItsName` | `tests/RestaurantManagement.Modules.Organization.Tests/OrganizationLookupLocationTests.cs` |
| 2 | T2 | Happy | The invitation and invitation-cancelled actions are in the audit log's fixed list (2 cases) | `All_FixedListOfAuditedActions_ContainsTheInvitationActions` | `tests/RestaurantManagement.Shared.Tests/Auth/InvitationAuditActionsTests.cs` |
| 3 | T2 | Happy | Every new refusal message exists in English and Portuguese (7 keys, 2 languages) | `Localizer_InvitationMessageKey_ReturnsRealTextInEachLanguage` | `tests/RestaurantManagement.Shared.Tests/Localization/InvitationMessageKeysTests.cs` |
| 4 | T3 | Security | An Admin without Cashier at Batel cannot give the Cashier preset there | `RequireAsync_AdminWithoutCashierAtBatelGivesTheCashierPresetThere_Refuses403` | `tests/RestaurantManagement.Modules.Identity.Tests/MemberDelegationRulesTests.cs` |
| 5 | T3 | Security | An Admin with Reports Read cannot raise Reports to Manage | `RequireAsync_AdminWithReportsReadRaisesReportsToManage_Refuses403` | `tests/RestaurantManagement.Modules.Identity.Tests/MemberDelegationRulesTests.cs` |
| 6 | T3 | Security | Members Manage at Batel only: a change that also touches Mercado is refused | `RequireAsync_MembersManageAtBatelOnlyChangesMercadoToo_Refuses403` | `tests/RestaurantManagement.Modules.Identity.Tests/MemberDelegationRulesTests.cs` |
| 7 | T3 | Security | Nobody can change their own grid, even with Manage everywhere | `RequireAsync_CallerChangesHerOwnAccessWithManageEverywhere_Refuses403` | `tests/RestaurantManagement.Modules.Identity.Tests/MemberDelegationRulesTests.cs` |
| 8 | T3 | Security | Nobody can edit the Owner's access | `RequireAsync_TargetIsTheOwner_Refuses403` | `tests/RestaurantManagement.Modules.Identity.Tests/MemberDelegationRulesTests.cs` |
| 9 | T3 | Edge | The Owner can grant any level (no cap) | `RequireAsync_OwnerGivesFullAdminLevelsAtBatel_Passes` | `tests/RestaurantManagement.Modules.Identity.Tests/MemberDelegationRulesTests.cs` |
| 10 | T3 | Security | A Manager, Cashier, Waiter or Kitchen member (no Members Manage) is refused (4 cases) | `RequireAsync_CallerWithADefaultRoleThatLacksMembersManage_Refuses403` | `tests/RestaurantManagement.Modules.Identity.Tests/MemberDelegationRulesTests.cs` |
| 11 | T3 | Happy | An Admin may grant levels within her own at a location where she manages members | `RequireAsync_AdminGivesLevelsWithinHerOwnAtBatel_Passes` | `tests/RestaurantManagement.Modules.Identity.Tests/MemberDelegationRulesTests.cs` |
| 12 | T4 | Happy | Changing a Waiter to Cashier at Batel updates the grid and label and writes one audit entry (Before Waiter, After Cashier) | `SetAccess_OwnerChangesAWaiterToCashierAtBatel_UpdatesGridAndLabelAndWritesOneAuditEntry` | `tests/RestaurantManagement.Web.Tests/Identity/MemberAccessEndpointTests.cs` |
| 13 | T4 | Happy | After a level is lowered, the member's next request with the same token is refused | `SetAccess_MenusLoweredToNone_TheMembersNextMenuReadWithTheSameTokenIsForbidden` | `tests/RestaurantManagement.Web.Tests/Identity/MemberAccessEndpointTests.cs` |
| 14 | T4 | Security | An Admin giving a preset above her own level gets 403 and nothing is saved | `SetAccess_AdminWithoutCashierGivesBiaTheCashierPreset_ReturnsForbiddenAndStoresNothing` | `tests/RestaurantManagement.Web.Tests/Identity/MemberAccessEndpointTests.cs` |
| 15 | T4 | Security | Editing a member of another organization returns not found and changes nothing | `SetAccess_MemberOfAnotherOrganization_ReturnsNotFoundAndLeavesTheirGridUnchanged` | `tests/RestaurantManagement.Web.Tests/Identity/MemberAccessEndpointTests.cs` |
| 16 | T4 | Failure | An unknown module name or level name is a 400 and nothing is saved (2 cases) | `SetAccess_UnknownModuleOrLevelName_ReturnsBadRequestAndStoresNothing` | `tests/RestaurantManagement.Web.Tests/Identity/MemberAccessEndpointTests.cs` |
| 17 | T5 | Failure | Inviting a current member (or the Owner) in another letter casing is refused and nothing is stored (2 cases) | `CreateAsync_EmailOfACurrentMemberInOtherCasing_Refuses400AndStoresNothing` | `tests/RestaurantManagement.Modules.Identity.Tests/InvitationServiceCreateTests.cs` |
| 18 | T5 | Edge | Inviting a pending email again leaves one valid invitation and the first is replaced | `CreateAsync_SamePendingEmailInvitedAgain_LeavesOneOpenInvitationAndReplacesTheFirst` | `tests/RestaurantManagement.Modules.Identity.Tests/InvitationServiceCreateTests.cs` |
| 19 | T5 | Security | Inviting with a level above the inviter's own is refused, nothing stored, no email | `CreateAsync_AdminWithoutCashierAtBatelGivesTheCashierPreset_Refuses403AndStoresNothing` | `tests/RestaurantManagement.Modules.Identity.Tests/InvitationServiceCreateTests.cs` |
| 20 | T5 | Security | Inviting with a preset named Owner is refused and nothing is stored | `CreateAsync_PresetNamedOwner_IsRefusedAndStoresNothing` | `tests/RestaurantManagement.Modules.Identity.Tests/InvitationServiceCreateTests.cs` |
| 21 | T5 | Happy | The Owner invites a Waiter at Batel: stored for 7 days, no membership, audited | `CreateInvitation_OwnerInvitesAWaiterAtBatel_StoresItForSevenDaysWithNoMembershipAndAuditsIt` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationEndpointTests.cs` |
| 22 | T5 | Security | The one-time token is never in the response, audit entry or database in readable form | `CreateInvitation_Created_TokenIsNeverInTheResponseTheAuditEntryOrTheDatabaseInReadableForm` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationEndpointTests.cs` |
| 23 | T5 | Edge | The database refuses a second open invitation for one email in one organization; another organization may invite the same email | `SaveInvitation_SecondOpenInvitationForOneEmailInOneOrganization_IsRefusedByTheDatabaseButAnotherOrganizationMayInvite` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationEndpointTests.cs` |
| 24 | T5 | Security | Inviting with a location or role of another organization returns not found and stores nothing (2 cases) | `CreateInvitation_LocationOrRoleOfAnotherOrganization_ReturnsNotFoundAndStoresNothing` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationEndpointTests.cs` |
| 25 | T6 | Happy | The English email has inviter, organization, "role at location" lines, accept button, expiry date and no level | `CreateAsync_EnglishRequest_EmailHasInviterOrganizationRolesPerLocationButtonAndExpiryAndNoLevel` | `tests/RestaurantManagement.Modules.Identity.Tests/InvitationEmailTests.cs` |
| 26 | T6 | Edge | A Portuguese request gets a Portuguese email with the same button | `CreateAsync_PortugueseRequest_EmailIsInPortugueseAndStillNamesOrganizationAndRoles` | `tests/RestaurantManagement.Modules.Identity.Tests/InvitationEmailTests.cs` |
| 27 | T6 | Security | Names containing HTML are shown as text in the email | `CreateAsync_InviterNameAndOrganizationNameWithHtml_AreShownAsText` | `tests/RestaurantManagement.Modules.Identity.Tests/InvitationEmailTests.cs` |
| 28 | T6 | Failure | If the email cannot be sent the invitation is kept, the request succeeds and the log has no address or token | `CreateAsync_EmailSendingFails_KeepsTheInvitationAnswersSuccessAndLogsNoAddressOrToken` | `tests/RestaurantManagement.Modules.Identity.Tests/InvitationEmailTests.cs` |
| 29 | T6 | Failure | A missing or relative accept page address refuses startup, naming the setting (3 cases) | `Validate_AcceptPageAddressMissingOrNotAbsolute_FailsNamingTheSetting` | `tests/RestaurantManagement.Modules.Identity.Tests/InvitationOptionsValidatorTests.cs` |
| 30 | T7 | Failure | An expired (7 days and 1 second), used, replaced or cancelled link gets the same generic 400 (4 cases) | `AcceptAsync_LinkExpiredUsedReplacedOrCancelled_RefusesWithTheSameGeneric400` | `tests/RestaurantManagement.Modules.Identity.Tests/InvitationAcceptTests.cs` |
| 31 | T7 | Security | A signed-in account with another email cannot use the link and joins nothing | `AcceptAsync_SignedInAccountHasAnotherEmail_RefusesAndJoinsNothing` | `tests/RestaurantManagement.Modules.Identity.Tests/InvitationAcceptTests.cs` |
| 32 | T7 | Failure | Accepting with a new account when the email already has an account asks to sign in | `AcceptWithNewAccountAsync_InvitedEmailAlreadyHasAnAccount_Refuses400AskingToSignIn` | `tests/RestaurantManagement.Modules.Identity.Tests/InvitationAcceptTests.cs` |
| 33 | T7 | Happy | The invited account accepts and has exactly the invited grid, never Owner, other memberships kept | `Accept_SignedInAccountWithTheInvitedEmail_JoinsWithExactlyTheInvitedGridAndIsNeverOwner` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationAcceptEndpointTests.cs` |
| 34 | T7 | Security | An invitee who has not accepted is refused everywhere and has no membership | `GetMenus_InviteeWhoHasNotAcceptedYet_IsForbidden` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationAcceptEndpointTests.cs` |
| 35 | T7 | Happy | A person without an account creates a verified account while accepting and gets the invited grid | `AcceptWithNewAccount_PersonWithoutAccount_CreatesAVerifiedAccountAndJoinsWithTheInvitedGrid` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationAcceptEndpointTests.cs` |
| 36 | T7 | Edge | The same link used twice at the same moment joins once | `Accept_SameLinkTwiceAtTheSameMoment_JoinsOnceAndRefusesTheOther` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationAcceptEndpointTests.cs` |
| 37 | T8 | Happy | Cancelling a pending invitation succeeds and its link no longer works | `CancelInvitation_PendingInvitation_StopsTheLinkFromWorking` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationCancelEndpointTests.cs` |
| 38 | T8 | Happy | A cancellation writes one audit entry with before (email, roles) and after (cancelled) | `CancelInvitation_PendingInvitation_WritesOneAuditEntryWithBeforeAndAfter` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationCancelEndpointTests.cs` |
| 39 | T8 | Security | A Manager, Cashier, Waiter or Kitchen member cannot cancel; the invitation stays pending (4 cases) | `CancelInvitation_CallerWithADefaultRoleThatLacksMembersManage_ReturnsForbiddenAndKeepsItPending` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationCancelEndpointTests.cs` |
| 40 | T8 | Security | Cancelling another organization's invitation returns not found and it stays pending | `CancelInvitation_InvitationOfAnotherOrganization_ReturnsNotFoundAndStaysPending` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationCancelEndpointTests.cs` |
| 41 | T4, T5, T7, T8 | Guard | The four signed-in invitation and access routes refuse a request with no token (4 cases) | `SignedInInvitationEndpoints_NoAccessToken_ReturnUnauthorized` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationAuthEndpointTests.cs` |
| 42 | T7 | Security | Too many requests to the public accept endpoint get 429 | `AcceptWithNewAccount_MoreRequestsThanTheVerifyLimit_ReturnsTooManyRequests` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationAuthEndpointTests.cs` |
| 43 | T7 | Failure | A weak password when accepting with a new account is a 400 (2 cases) | `AcceptWithNewAccount_WeakPassword_ReturnsBadRequest` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationAuthEndpointTests.cs` |
| 44 | T5 | Security | An invitation with a missing, malformed or too long email is a 400, nothing is stored and no email is sent (4 cases plus 1) | `CreateInvitation_MissingOrMalformedEmail_ReturnsBadRequestAndStoresNothing`, `CreateInvitation_EmailLongerThan320Characters_ReturnsBadRequestAndStoresNothing` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationEmailValidationEndpointTests.cs` |
| 45 | T4, T5 | Security | An invitation or access edit body with an oversized locations, role names or levels list is a 400 and nothing changes (5 cases) | `CreateInvitation_OversizedCollection_ReturnsBadRequestAndStoresNothing`, `SetAccess_OversizedCollection_ReturnsBadRequestAndChangesNothing` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationSizeLimitEndpointTests.cs` |
| 46 | T7 | Security | A token longer than 200 characters is a 400 on both accept routes (2 cases) | `Accept_TokenLongerThanTheLimit_ReturnsBadRequest` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationSizeLimitEndpointTests.cs` |
| 47 | T5 | Security | An invitation listing the same location twice is a 400 and nothing is stored | `CreateInvitation_SameLocationTwice_ReturnsBadRequestAndStoresNothing` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationSizeLimitEndpointTests.cs` |
| 48 | T5 | Security | An invitation location with no roles and no levels is a 400 and nothing is stored | `CreateInvitation_LocationWithNoRolesAndNoLevels_ReturnsBadRequestAndStoresNothing` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationCreateRefusalEndpointTests.cs` |
| 49 | T5 | Security | A null entry in the locations list is a 400, not a server error, and nothing is stored | `CreateInvitation_NullLocationEntry_ReturnsBadRequestAndStoresNothing` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationCreateRefusalEndpointTests.cs` |
| 50 | T5 | Edge | Eight requests for the same email at the same moment: none is a server error and one open invitation remains (the race is not forced, so the test cannot fail every run without the fix) | `CreateInvitation_SameEmailAtTheSameMoment_NeverFailsWithAServerErrorAndLeavesOneOpenInvitation` | `tests/RestaurantManagement.Web.Tests/Identity/InvitationCreateRefusalEndpointTests.cs` |

## Spec Outcome coverage
| Outcome | Scenarios |
|---|---|
| #1 Invite works: invitee gets a link, accepts and has exactly the chosen grid | 21, 25, 33, 35 |
| #2 No access before acceptance | 34 |
| #3 Link is limited: expired or used link refused | 30, 37 |
| #4 Duplicates handled | 17, 18, 23 |
| #5 Grid edit applies at once | 13 |
| #6 Role change possible, one audit entry Waiter to Cashier | 12 |
| #7 Cap enforced | 4, 5, 14, 19 |
| #8 No self-edit | 7 |
| #9 Owner protected (edit refused, invite as Owner refused, accepted member never Owner) | 8, 20, 33 |
| #10 Members level required | 6, 10, 39 |
| #11 Tenant isolation holds | 15, 24, 40 |
| #11a Email is complete | 1, 25, 26, 27 |
| #12 Changes are audited | 2, 3 (action names), 12, 21, 38 |
| Spec rule "Cancel" | 37, 38, 39, 40 |

Accepted at unit level only: self, Owner-target and missing-Members-level refusals (scenarios 7, 8, 10) are proved on the rules; endpoints prove the wiring with the cap case (14, 19) and the per-role 403 on cancel (39). The Owner field in an invitation body is ignored: scenario 33 sends `isOwner: true` and asserts the member is not Owner. Login after accepting with a new account is not tested (the session service is substituted in the endpoint host); scenario 35 checks the account is verified.

Not covered by tests, checked at review: the email text in Portuguese (the spec gives no Portuguese wording; scenario 26 checks only that it is Portuguese and complete); "A pending invitee is refused at organization choice" (scenario 34 proves the menu read; organization choice is a substituted service in the endpoint test host and is already covered by identity matrix S3).

## Existing tests updated
Tests: none rewritten. Three shared test helpers changed because the plan's changes force it:
- `tests/RestaurantManagement.Modules.Identity.Tests/MemberAccessTestSupport.cs` (`CreateService`): `MemberAccessService` gained two constructor parameters (the delegation rules and the grid builder); the helper passes substitutes, so rows T37–T42 still pass unchanged.
- `tests/RestaurantManagement.Modules.Identity.Tests/AuthControllerTests.cs` (`CreateSut`): `AuthController` gained the `IInvitationService` constructor parameter for the public accept route; the helper passes a substitute, no assertion changes.
- `tests/RestaurantManagement.Web.Tests/TenantApiFactory.cs` (a captured `EmailService` substitute, so tests read invitation links) and `AuthApiFactory.cs` (the new `Invitation:AcceptPageUrl` setting). Helpers only; no existing assertion changes.
