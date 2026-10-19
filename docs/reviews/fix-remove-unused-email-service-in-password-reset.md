# Review: Remove the unused email service from password reset

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 1,025 passed, 0 failed (verified by the caller; matrix check passes)

Scope: `fix/2026-10-17-forgot-password-same-response-time..HEAD`, one commit (9120f2e), 5 files,
8 lines removed. Solves `backlog/tech-debt/2026-10-19-unused-email-service-in-password-reset-service.md`
(file deletion left uncommitted for the user, as the backlog rules require).

## Checks
- **Acceptance criteria:** both met. `PasswordResetService` has no `IEmailService` parameter or
  `using`; the four tests that build it drop the argument and pass.
- **Tests:** only constructor arguments and one unused local and `using` removed. No assertion,
  `[Fact]`, `[Trait]` or test name changed, nothing skipped.
- **Scope and structure:** no new files, migrations, packages, endpoints or Shared contracts.
  Dependency injection (`Program.cs:253`) resolves the constructor automatically, so nothing to change there.
  `IEmailService` is still used by `PasswordResetEmailProcessor`, `RegistrationService` and `VerificationService`.
- **Architecture, design, security, secrets:** nothing new. Fewer dependencies, no behaviour change.
- **Commit:** Conventional Commits, scope `identity`, explains why. There is no `Scenarios:` line,
  which is fine because a backlog fix has no scenarios.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceRequestTests.cs:117-148`, `docs/security/identity-matrix.md:160` | The two P5 tests still set up `_emailService` to throw or hang, but that substitute is no longer passed to the service. Their email-service setup can never affect the result. The service never called it before this commit either, so no test got weaker. P5's "never calls the email service" is now guaranteed by the constructor itself, not by the tests. | Reword P5's Expected result to say the request service has no email dependency (sending happens only in `PasswordResetEmailProcessor`). Add a `proposed` backlog item to simplify or rename these tests, because tests are read-only. |
| 2 | `PasswordAuditFailureTests.cs:9,28`; `PasswordResetServiceResetTests.cs:9,31,36-43`; `PasswordResetServiceRequestTests.cs:8,27` | The `_emailService` fields, their `using`s, and the `ResetTests` constructor stub that sets `_lastEmailedCode` are now dead code. `_lastEmailedCode` is always set directly at line 91. | Fold this into the same backlog item: remove the dead fields, stubs and `using`s. Leaving them out of this change was correct, because the acceptance criteria cover only construction code. |

## Check before merging
- P5 (identity matrix): its two named tests no longer connect the email substitute to the service,
  so the row now depends on the constructor having no email dependency. Make sure you are
  comfortable with that, or reword the row (Watch out 1).
- Commit the backlog file deletion yourself after checking the fix (agents never commit it).

## Update after review
- Watch out 1 and 2 fixed in 5048a91, with the user's approval to edit the tests: the two P5
  tests are replaced by one that checks `PasswordResetService` has no `IEmailService`, the P5 row
  is reworded, and the unused email substitutes, `using`s and the constructor stub are removed.
  Build, all 1,024 tests, format and matrix checks pass.
- Backlog file deletion committed in 52919dc, at the user's request.

## Pull request description
**Remove the unused email service from password reset**

"Forgot password" only queues a request now, and `PasswordResetEmailProcessor` sends the email.
`PasswordResetService` still asked for an `IEmailService` it never used. This change removes it.

- **Identity:** `PasswordResetService` no longer takes `IEmailService`. No behaviour change.
- **Tests:** the four test classes that build the service drop the argument and their unused email
  substitutes. The two P5 tests that made an unconnected email substitute fail are replaced by one
  that checks the service has no email service; the P5 matrix row says so.

Risk: low. No migration, package, endpoint, Shared contract, or auth change.
Solves backlog item `backlog/tech-debt/2026-10-19-unused-email-service-in-password-reset-service.md`.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
