# Review: backlog-fixes 2026-10-16 (72-byte password limit at login and change password)

**Verdict:** Ready to merge
**Build:** pass (0 errors, 1 warning: xUnit1026, already on `development`)   **Tests:** 529 passed, 0 failed (Identity 213, Web 316; StartupConfigurationTests passed first time)

No spec or plan folder (small changes), so the Completeness check was skipped. No project or package files changed, so no vulnerability scan was needed. No secrets or personal data in the diff (test data only).

**What the pull request holds:** after `git fetch`, `origin/development..HEAD` is d2b2676 and 07254d2 (code) plus ed4eac6 and dfe3fe1 (docs only). 7abb057, caae3b3 and 05c5b61 are already on `development`; their findings are kept below only where they still apply.

**One commit per change:** d2b2676 (login) and 07254d2 (change password) each make one change, Conventional Commits, scope `identity`, summaries under 72 characters, body explains why. ed4eac6 adds one evidence line to the flaky startup tests item. dfe3fe1 is the previous version of this file. The 5 deleted backlog files are uncommitted on purpose and were not reviewed.

**Does the change close the timing gap from earlier Watch out 1?** Yes.
- **Same 400 for every email:** `AuthController` has `[ApiController]`, so `[MaxPasswordBytes]` on `LoginRequest.Password` answers 400 before `SessionService.LoginAsync` runs: no user lookup, no `FailedAttempts` write, no BCrypt. The response depends only on the password, never on the email. `Login_PasswordOver72Bytes_ReturnsSameLocalizedByteLimitError` proves it for a known and an unknown email, in en and pt.
- **No path reaches `Verify` with over 72 bytes:** `Verify` has two callers, `SessionService:80` (`LoginRequest.Password`) and `PasswordChangeService:46` (`ChangePasswordRequest.CurrentPassword`). Both properties now carry the attribute, and both actions bind `[FromBody]` under `[ApiController]`. `Verify` still returns false above 72 bytes as a second guard.
- **Boundary:** 72 bytes passes validation (login 401, not 400; change password gets no byte error), 73 bytes fails. Unit tests cover 72, 73 and null.
- **No duplicated rule:** the attribute calls the same `BcryptPasswordHasher.ExceedsByteLimit` and `MaxPasswordBytes` as `PasswordRulesAttribute` and the hasher, and reuses the catalog key `PasswordTooManyBytes_ValidationError`. Only the five lines that build the translated message are copied (Watch out 2).
- **Layering:** a `Models/` attribute calling a public static on `Services/BcryptPasswordHasher` is the same pattern `PasswordRulesAttribute` already uses. The architecture skill bans cross-module access and service injection into other modules, not this; it stays inside Identity and uses no instance or DbContext. Acceptable, but now two attributes depend on the BCrypt class (Watch out 1).
- **Security skill section 5** matches the code: it names `PasswordRulesAttribute` for new passwords and `MaxPasswordBytesAttribute` on the login password and the change-password current password, plus the hasher guards.

Test edits: both commits only add tests (one new test class, new cases in two endpoint test classes). Nothing was edited, weakened, skipped or deleted.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | src/RestaurantManagement.Modules.Identity/Models/MaxPasswordBytesAttribute.cs:3,21 (also PasswordRulesAttribute.cs:3,45) | Two request-model attributes now depend on the concrete `BcryptPasswordHasher` in `Services/` for one constant and one byte count. Small coupling of the DTO (Data Transfer Object: request/response shape) layer to the BCrypt implementation. | Leave it, or move `MaxPasswordBytes` / `ExceedsByteLimit` to a small `PasswordLimits` static class used by both attributes and the hasher. |
| 2 | src/RestaurantManagement.Modules.Identity/Models/MaxPasswordBytesAttribute.cs:24-29 | The localizer lookup and `ValidationResult` building are copied from `PasswordRulesAttribute.cs:28-33`. | Next time either file changes, extract a private static helper (for example `LocalizedValidation.Result(context, key, args)`) inside Identity `Models/`. |
| 3 | src/RestaurantManagement.Modules.Identity/Models/MaxPasswordBytesAttribute.cs:8-11 | `///` summary on a class outside Shared (clean-code skill section 5 allows `///` only on Shared public interfaces). | Turn it into a one-line `//` "why" comment. |
| 4 | .claude/skills/security/SKILL.md:47-48 | The two edited lines run to about 110 and 130 characters, while the rest of the section wraps near 95. | Rewrap the bullet. |
| 5 | src/RestaurantManagement.Modules.Identity/Models/PasswordRulesAttribute.cs:42-46 | Carried over: the byte check sits after the three complexity checks it does not use. | Move the `ExceedsByteLimit` guard up, right after the `MaxLength` line (40). |
| 6 | tests/RestaurantManagement.Modules.Identity.Tests/BcryptPasswordHasherTests.cs:71 | Carried over: nothing proves that byte 72 counts (the 72-byte test passes even if BCrypt ignored it). | Add: hash `Password72Bytes`, verify the same text with the last `é` replaced by `è`: must return false. |
| 7 | src/RestaurantManagement.Modules.Identity/Services/IPasswordHasher.cs:3, PasswordRulesAttribute.cs:9 | Carried over: `///` outside Shared, and a 125-character summary line. | Make them `//` "why" comments when the files are next touched. |
| 8 | tests/RestaurantManagement.Modules.Identity.Tests/BcryptPasswordHasherTests.cs:116 | Carried over: request-model validation tests sit in the hasher test class. Now that `MaxPasswordBytesAttributeTests` exists, the split is more visible. | Move them to a request-model validation test class. |

Earlier Watch out 1 (login timing for over-72-byte passwords) and 2 (no login test for 73 bytes) are solved by d2b2676. Earlier findings on the `RequiredIdAttribute` file layout, xUnit1026, the 4f20dc7 scope and `test-summary.sh` concern commits already on `development` and are unchanged.

Note: `dotnet format` keeps reformatting `tests/RestaurantManagement.Web.Tests/RequiredIdFieldEndpointTests.cs`. That file is not in these commits; not a finding here.

## Check before merging
- **Login now answers 400 instead of 401 for a password over 72 bytes (d2b2676).** It is identical for every email and skips the user lookup, which is the point. It also means such attempts no longer count toward the account lockout; the login rate limit still applies, so no new brute-force path (a 73-byte guess can never match anyway).
- **Change password answers 400 for a current password over 72 bytes (07254d2)**, before `Verify`. Users whose stored password was over 72 bytes already had to reset it (accepted pre-launch).
- **Validation is now the main guard on both `Verify` callers.** A future endpoint that verifies a password (for example "confirm password to delete account") must add `[MaxPasswordBytes]` too; the security skill now says so.

## Pull request description
**Identity: reject over-72-byte passwords at login and change password before any lookup**

Summary: BCrypt reads only the first 72 bytes of a password. Since the previous change, a login password over 72 bytes was refused inside `Verify` without running BCrypt, so it answered faster than a normal failed login and the timing could hint whether an email is registered. Such passwords are now rejected in request validation with a translated 400, before the user is looked up, identically for every email. The change-password current password gets the same rule.

Identity
- New `MaxPasswordBytesAttribute` (72 UTF-8 bytes, reuses `BcryptPasswordHasher.ExceedsByteLimit` and the existing `PasswordTooManyBytes_ValidationError` message)
- Applied to `LoginRequest.Password` and `ChangePasswordRequest.CurrentPassword`

Docs
- Security skill section 5 names where each attribute applies
- Flaky startup tests backlog item: one more piece of evidence

Tests
- Attribute: 72 bytes passes, 73 fails, null passes
- Login endpoint: 73 bytes gives the same en/pt 400 for a known and an unknown email; 72 bytes is not a 400
- Change password endpoint: 73-byte current password gives the en/pt-BR 400; 72 bytes gets no byte error

Risk and flags
- Authentication code (request validation only). No new endpoint, package, migration or Shared contract
- Login status for over-72-byte passwords changes from 401 to 400

🤖 Generated with [Claude Code](https://claude.com/claude-code)
