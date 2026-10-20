# Review: Invite a member and edit their access grid

**Verdict:** Fix first — matrix row T83 is ✅ and promises "no email sent", but neither of the tests it names checks that
**Build:** pass (0 errors, 4 warnings in test code)   **Tests:** 1161 passed, 0 failed (Shared 191, Organization 124, Menu 68, Identity 345, Web 433)

Second review. Commit `a14ba38` fixes both earlier Fix first items:
- **Email validation.** `CreateInvitationRequest.Email` now has `[Required]`, `[EmailAddress]` and `[MaxLength(320)]`, with the registration error keys.
- **Size limits.** Every collection and both `Token` strings now have a `[MaxLength]`.
- **Repeated location.** `InvitationService.cs:82` answers 400 before any lookup.
- **Coverage.** New rows T83–T86 and scenarios 44–47, with tests in `InvitationEmailValidationEndpointTests` and `InvitationSizeLimitEndpointTests`.

Other checks:
- The only test changes since the baseline commit `108642f` are those two new files. No test was edited, deleted, skipped or weakened.
- `python3 .claude/scripts/check-security-matrices.py` passes.
- No package was added, so the vulnerable-package check was not needed.
- No secrets in the diff, and no new folders.

Terms:
- DoD: Definition of Done, the checks a task must pass to count as finished.
- NRE: NullReferenceException, a crash from using a missing value.

## Fix first
| # | File:line | Problem | Change |
|---|---|---|---|
| 1 | `docs/security/identity-matrix.md:285` (row T83); `tests/RestaurantManagement.Web.Tests/Identity/InvitationEmailValidationEndpointTests.cs:35-36,52-53` | Row T83 is ✅ and its Expected column says "nothing stored, no email sent". The two named tests check only the 400 and that nothing is stored. Matrix rule: every ✅ names a test that checks the row's Expected result. The T56 test for this feature already asserts `SentEmails().ShouldBeEmpty(...)`. | In both tests, assert that `factory.EmailService` received no `SendEmailAsync` call, for example "no email goes out for a refused invitation". This adds an assertion and weakens nothing. Or remove "no email sent" from the row. While there: rows T85 and T86 (lines 286-287) say "before any lookup", which no endpoint test can see. Reword to "400; nothing stored". |

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `src/RestaurantManagement.Modules.Identity/Services/InvitationService.cs:82` | A JSON body with `"locations": [null]` gets past model validation. `l.LocationId` then crashes with an NRE, which returns a 500. Only a signed-in caller can send this, and nothing is stored. | Refuse a null entry with 400 in the same check, or mark the elements as required. |
| 2 | `src/RestaurantManagement.Modules.Identity/Services/MemberAccessService.cs:26,45` | Still open from the first review. `_gridBuilder` is injected but never used, and `SetAccessAsync` repeats the merge that `MemberGridBuilder` does. No backlog item tracks this. | Add a `tech-debt` backlog item: switch to `IMemberGridBuilder` once the T37–T42 tests use the real builder. |
| 3 | `src/RestaurantManagement.Modules.Identity/Controllers/MembersController.cs:36` | `ParseLevels` copies `InvitationGrids.ParseLevels`, and it is validation logic inside a controller. | Parse in the service, so the controller only binds and calls. |
| 4 | `InvitationService.cs:224-227` | Cancel finds the invitation's locations from its stored rows. A location with no roles and no levels stores no rows. If every location is empty, any member can cancel the invitation. | Refuse an empty location grid at create with 400, or store the invitation's location list. |
| 5 | `InvitationService.cs:89-99` | Two invitations to the same email at the same moment: the second hits `ix_organization_invitations_open_email` and returns a 500. | Catch the unique violation and answer 409 or 400, as `InvitationService.Accept.cs:49` does. |
| 6 | `docs/plans/shared/2026-10-20-invite-member-and-edit-access-grid/plan.md:48-55` | The Checklist has no commit hashes. T7 and T8 share commit `121c85c`, and the fix commit `a14ba38` is not recorded. `InvitationTokens.cs`, `InvitationGrids.cs`, `InvitationService.Accept.cs` and the two new test files are in no task's Files row. | Add each task's hash and the fix commit to the Checklist, and list the extra files. |
| 7 | `batches/B3.md` T4 DoD, `batches/B4.md` T5 DoD | Endpoint tests named in the DoD do not exist: one 403 per default role, self-edit, Owner-edit, and missing Members = Manage at a location. Only unit tests for the delegation rules (T45–T49) prove these. | List this in the pull request. Endpoint rows can follow later. |
| 8 | `src/RestaurantManagement.Modules.Identity/Services/IMemberAccessService.cs:16` | The comment repeats "Returns the member's access at that location." twice on one very long line. | Remove the duplicate and wrap the line. |
| 9 | `backlog/features/2026-10-16-member-role-change.md:12` | The evidence line says no endpoint or delegation rules expose `SetAccessAsync`. That is no longer true. | Replace it with evidence for what is still missing: Owner role changes and last-Owner protection. |

## Check before merging
- **Public `POST api/auth/accept-invitation`** (`InvitationService.Accept.cs`):
  - It creates a verified account without an email code. The one-time link is what proves the email belongs to the person.
  - It writes the membership under the invitation's tenant scope, not the token's.
  - The link is claimed with a conditional update (T75).
  - Its token is now capped at 200 characters (T85).
  - Confirm that skipping email verification for invitees is intended.
- **Delegation rules** (`MemberDelegationRules.cs`):
  - Edit, invite and cancel carry only a plain `[Authorize]`, so any gap in `RequireAsync` is an authorization gap.
  - The cap applies only to raised cells: a caller with Members = Manage can lower cells above their own level, as decided at Gate 1.
- **Shared contract change** (`LocationSummaryDto.Name`) and the migration `AddOrganizationInvitations`. The migration adds three new tables, and the database holds no data yet.

## Pull request description
**Invite a member and edit their access grid** (slice 2 of 3 of the per-location access grid)

Authorized members can now invite a person by email and edit an existing member's access grid. Authorized means the Owner, or an Admin with Members = Manage at the locations involved. An invitation sets role presets and a grid for each location. Every grant follows the delegation rules:
- Nobody grants a level above their own.
- Nobody edits their own access.
- Nobody can edit the Owner's access.

What changed per module:
- **Identity:**
  - `PUT api/members/{userId}/access/{locationId}` edits a member's presets and cells.
  - `POST api/invitations` creates an invitation and emails a one-time link. Only a SHA-256 fingerprint of the token is stored, and the link expires after 7 days.
  - `POST api/invitations/accept` lets a signed-in account accept.
  - Public `POST api/auth/accept-invitation` (rate-limited) creates a verified account and joins the organization.
  - `DELETE api/invitations/{id}` cancels an invitation.
  - Request checks: a well-formed email of at most 320 characters, at most 50 locations, 20 role names and 20 levels, tokens of at most 200 characters, and no repeated location.
  - New services for the delegation rules and the grid builder.
  - Invitation emails in English and Portuguese.
  - Migration `AddOrganizationInvitations`.
- **Organization:** the location lookup also returns the location name.
- **Shared:** `LocationSummaryDto.Name`, the audit actions `member.invited` and `member.invitation_cancelled`, and 7 new messages in English and Portuguese.
- **Web:** service registrations, and the required setting `Invitation:AcceptPageUrl`, checked at startup.

Spec Outcomes delivered: #1–#12 and #11a. Security matrix rows: T43–T86, OR44, SH43 and SH44.

Risk: **High** (from `plan.md`):
- a new public endpoint that creates accounts and memberships
- new authorization (delegation) rules
- a Shared contract change
- new Identity tables (no data yet, before launch)

Flags:
- The accept page address must be set in every environment (`Invitation:AcceptPageUrl`).
- Some endpoint tests listed in the T4 and T5 DoD are not written. Unit tests on the delegation rules cover those rules.
- The per-role 403, self-edit, Owner-edit and missing Members = Manage cases are proven only by the delegation-rules unit tests (T45–T49); endpoint rows will follow later.
- The deletion of `backlog/features/2026-10-19-invite-member-and-edit-access-grid.md` (fully solved) is left for the user to commit.
- New backlog items: list members and pending invitations, purge old invitations, and store each user's preferred language.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
