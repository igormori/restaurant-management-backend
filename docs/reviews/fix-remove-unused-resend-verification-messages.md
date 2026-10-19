# Review: Remove unused resend verification messages

**Verdict:** Ready to merge
**Build:** pass (0 warnings, 0 errors)   **Tests:** 1041 passed, 0 failed

Small change (`/small`): no spec, plan or scenarios. Reviewed against the project skills and
backlog item `backlog/tech-debt/2026-10-19-unused-resend-verification-messages.md`.

## Checks
- **Backlog acceptance criteria:** all four met. Neither `VerificationCodeResent` nor
  `EmailSendingFailed` is defined in `SharedResource.resx` or `SharedResource.pt.resx`; a search
  of `src/`, `tests/` and `docs/security/` finds no use of either key; the mock in
  `AuthControllerTests.cs:47` now returns `VerificationResendAcknowledged`, the key
  `VerificationService.cs:85` really returns; build and suite pass.
- **Tests:** the only test edit is the approved mock string at `AuthControllerTests.cs:47`. Its
  assertions (200, non-empty message) are unchanged, so the test is not weakened. The message
  catalog tests (`SharedResourceCatalogTests`, `MessageCatalogCompletenessTests`) still pass, so
  the English and Portuguese files stay in step.
- **Scope and structure:** three files modified, no new files or folders, no migration, package,
  endpoint or Shared contract change. Removing two unused resource keys is not a contract
  change: no code reads them.
- **Security matrices:** `python3 .claude/scripts/check-security-matrices.py` passes. No
  behaviour changed, so no matrix row needs updating; row RS1 still names the edited test.
- **Secrets:** none in the diff.
- **Commits:** one commit, `chore(shared): ...`, Conventional Commits format, body explains why.
- **Backlog:** the solved item is deleted in the working tree and left uncommitted, as the
  backlog rules require.

## Check before merging
- Commit the deletion of `backlog/tech-debt/2026-10-19-unused-resend-verification-messages.md`
  yourself after checking it; it is not part of the branch commit.
- Low risk otherwise: the two keys had no readers, and the catalog tests prove both language
  files still match.

## Pull request description

Removes two shared messages left without readers after resend verification was changed to
always return the same acknowledgement.

**Shared**
- Removed `VerificationCodeResent` and `EmailSendingFailed` from `SharedResource.resx` (English)
  and `SharedResource.pt.resx` (Portuguese).

**Identity (tests)**
- `AuthControllerTests.ResendVerification_ValidRequest_ReturnsOkWithMessageBody`: the mocked
  service now returns `VerificationResendAcknowledged`, the message the real service sends.
  Assertions unchanged.

**Risk:** low. No behaviour, endpoint, migration or package change. Build and all 1041 tests
pass; security matrices check passes.

Solves backlog item `backlog/tech-debt/2026-10-19-unused-resend-verification-messages.md`.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
