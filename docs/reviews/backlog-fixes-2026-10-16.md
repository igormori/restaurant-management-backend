# Review: backlog-fixes 2026-10-16 (selected organization, password hashing, 72-byte password limit)

**Verdict:** Ready to merge
**Build:** pass (0 errors, 1 warning: xUnit1026, already on `development`)   **Tests:** 674 passed, 0 failed (Identity 210, Shared 156, Web 308; StartupConfigurationTests passed first time)

No spec or plan folder (small changes), so the Completeness check was skipped. No project or package files changed, so no vulnerability scan was needed. No secrets or personal data in the diff (test data only).

**What the pull request holds:** after `git fetch`, 7abb057 and caae3b3 are already ancestors of `origin/development` (they went in with the previous review). `origin/development..HEAD` is now only **05c5b61**. All three were reviewed; 7abb057 and caae3b3 are unchanged since the previous review, whose findings stand except where noted below.

**One commit per item:** 7abb057 (Refs the selected-organization item), caae3b3 (Refs the password-hashing item) and 05c5b61 (user request, no backlog item, so no `Refs:`) each cover one item. 05c5b61's summary is 69 characters, Conventional Commits, scope `identity`. It also touches the Shared message catalog and the security skill, which belong to the same change. The item file deletions are uncommitted, as the backlog skill requires.

**Does 05c5b61 close the truncation gap?** Yes.
- **Every path that hashes:** `_passwordHasher.Hash` is called only in `RegistrationService:52`, `PasswordResetService:133` and `PasswordChangeService:73`. All three take a value that `[PasswordRules]` has already checked (`RegisterRequest.Password`, `ResetPasswordRequest.NewPassword`, `ChangePasswordRequest.NewPassword`), and `AuthController` has `[ApiController]`, so a request over 72 bytes gets a 400 before any service runs. `Hash` throws if something ever gets past that.
- **Every path that verifies:** `SessionService:80` (login) and `PasswordChangeService:46` (current password). Both go through `Verify`, which returns false above 72 bytes. No `BCrypt.` call remains outside `BcryptPasswordHasher`.
- **72/73-byte boundary:** BCrypt.Net-Next 4.0.3 was checked in a scratch program. Two 72-byte passwords that differ only in byte 72 do **not** match each other, so byte 72 counts. A 73-byte password **does** match the hash of its first 72 bytes, so byte 73 is the first one ignored. `> 72` is the correct cut-off. Both `PasswordRulesAttribute` and the hasher use the same `ExceedsByteLimit`, so they cannot drift apart.
- **Unpaired UTF-16 (Unicode text encoding) surrogates:** BCrypt throws on them, but System.Text.Json rejects them when it reads the request (checked), so they cannot reach the hasher.
- **Telling a wrong password apart from other failures:** login sends an over-72-byte password through `Verify`, and that returns false. The result is the same generic 401 `InvalidEmailOrPassword` for known and unknown emails, and it counts as a failed attempt like any wrong password. Change password returns the existing `CurrentPasswordIncorrect`. No new message or status code. The one side effect is on timing (Watch out 1).
- **Security skill section 5** now matches the code: 64 characters and 72 UTF-8 bytes, rejected and never cut, enforced in validation and in the hasher.

Test edits: 05c5b61 only adds tests (hasher 73 bytes throws, `Verify` false even when the first 72 bytes match, exactly 72 bytes works; attribute 72/73 bytes; endpoint en/pt message). Nothing was edited, weakened, skipped or deleted.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | src/RestaurantManagement.Modules.Identity/Services/BcryptPasswordHasher.cs:27, SessionService.cs:80 | For a password over 72 bytes, `Verify` returns before BCrypt runs, so login skips the roughly 250 ms hash for known **and** unknown emails. The email cannot be told from the response. But the existing timing difference between the two (a known email also writes `FailedAttempts`) is then no longer hidden behind the BCrypt time, so it becomes easier to measure. The dummy-hash rule in the Identity `CLAUDE.md` exists to prevent exactly that kind of oracle (a timing difference that reveals whether an email is registered). | Reject over-72-byte login passwords in `LoginRequest` validation, the same way `[MaxLength(64)]` already does: a 400 before any user lookup, identical for every email. Or let `Verify` still run BCrypt against the hash before returning false. |
| 2 | tests/RestaurantManagement.Web.Tests (no file) | No test proves that a login with a 73-byte, 43-character password returns the same generic 401 as a wrong password (and not a 500 or a field error). That is the main guarantee of this change on the login path. | Add a login endpoint test: a known user with a 73-byte password gets 401 `InvalidEmailOrPassword`, the same as an unknown email. |
| 3 | tests/RestaurantManagement.Modules.Identity.Tests/BcryptPasswordHasherTests.cs:71 | `Hash_PasswordOfExactly72Bytes_HashesAndVerifies` would still pass if BCrypt ignored byte 72. Nothing proves that the 72nd byte counts. | Add: hash `Password72Bytes`, then verify the same text with the last `é` replaced by `è` (differs only in byte 72): it must return false. |
| 4 | src/RestaurantManagement.Modules.Identity/Models/PasswordRulesAttribute.cs:42-46 | The byte check sits after the three complexity checks it does not use, so they run for nothing when the byte limit fails. It reads like part of the complexity rule. | Move the `ExceedsByteLimit` guard up, right after the `MaxLength` line (40), as one more guard clause. |
| 5 | src/RestaurantManagement.Modules.Identity/Models/PasswordRulesAttribute.cs:3,45 | A model in `Models/` now depends on the concrete `BcryptPasswordHasher` in `Services/` through a public static method. That is a small coupling to the BCrypt implementation, not to `IPasswordHasher`. Acceptable today, since one constant is shared by both sides. | Leave it, or move `MaxPasswordBytes` / `ExceedsByteLimit` to a small `PasswordLimits` static class used by both the attribute and the hasher. |
| 6 | src/RestaurantManagement.Modules.Identity/Models/PasswordRulesAttribute.cs:9 | The `///` summary line was extended to about 125 characters, and `///` is outside Shared (clean-code skill section 5). | Rewrap it, or turn it into a one-line `//` "why" comment next time this file is touched. |
| 7 | src/RestaurantManagement.Modules.Identity/Services/IPasswordHasher.cs:3 | Carried over: `///` on an interface outside Shared. | Make it a `//` "why" comment. |
| 8 | tests/RestaurantManagement.Modules.Identity.Tests/BcryptPasswordHasherTests.cs:116 | Carried over: request-model validation tests (login, change password) sit in the hasher test class. | Move them to a request-model validation test class. |

Earlier findings 2–4, 7 and 8 (the `RequiredIdAttribute` file layout, the bad-ID wording, xUnit1026, the 4f20dc7 scope, `test-summary.sh`) concern commits already on `development` and are unchanged. The bad-ID wording is accepted. Earlier Watch out 1 (the byte limit) is solved by 05c5b61.

## Check before merging
- **Login with an over-72-byte password (05c5b61):** returns the generic 401, but now without BCrypt work. See Watch out 1. It is the only part of this change that touches the account-enumeration defence (stopping callers from learning which emails are registered).
- **Existing hashes of over-72-byte passwords stop matching (05c5b61).** Any account whose password was saved at more than 72 bytes (possible before this commit) can no longer log in or change its password, and must reset it. Fine before launch (no real data); dev and seed users with plain ASCII (basic Latin characters, 1 byte each) passwords are not affected.
- **`Hash` now throws `ArgumentException` (05c5b61).** Today only validated values reach it. A future caller that skips `[PasswordRules]` (for example a seed or an admin "set password" path) would get a 500, not a 400. The interface comment says so.

## Pull request description
**Identity: reject passwords over 72 bytes instead of letting BCrypt cut them**

Summary: BCrypt only reads the first 72 bytes of a password. Accented letters (common in Portuguese) take 2 bytes and emoji take 4, so a password within the 64-character limit could still be silently shortened, and anything after byte 72 would be ignored at login. Passwords over 72 UTF-8 bytes are now rejected with a translated message, and the hasher refuses to hash or match them.

Identity
- `PasswordRulesAttribute` (registration, reset, change) rejects passwords over 72 UTF-8 bytes with a 400 and the new message `PasswordTooManyBytes_ValidationError`
- `BcryptPasswordHasher.Hash` throws above 72 bytes; `Verify` returns false, so login answers with the usual generic 401

Shared
- New catalog key `PasswordTooManyBytes_ValidationError` in English and Portuguese

Docs
- Security skill section 5: 64 characters **and** 72 UTF-8 bytes, rejected and never cut, enforced in validation and the hasher

Tests
- Hasher: 73 bytes throws, `Verify` false even when the first 72 bytes match, exactly 72 bytes works
- Validation: 72 bytes passes, 73 bytes (43 characters) fails; registration endpoint returns the en/pt message

Risk and flags
- Authentication code: password validation and hashing. No new endpoint, package, migration or Shared contract
- Passwords already stored at over 72 bytes no longer match (no real data before launch)

Already on `development` from this batch: drop `User.SelectedOrganizationId` (7abb057), password hashing through one interface with work factor 12 and a 64-character limit (caae3b3).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
