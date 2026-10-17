# Review: Backlog fixes 2026-10-16 (ASVS #17, #19, #21, #22, #23)

**Verdict:** Fix first — ASVS #19 is marked done, but when a verification email fails the
address still reaches the logs through the Shared `EmailService`.
**Build:** pass   **Tests:** 367 passed, 0 failed (Identity unit tests 251, Web.Tests Identity
classes 116; the caller reports the full suite at 961 green)
**Security matrices check:** pass (`check-security-matrices.py`: Identity 315 named tests)

ASVS (Application Security Verification Standard, the OWASP security checklist the backlog
item is measured against). This branch has no plan folder, so it was reviewed against the
skills, the Identity `CLAUDE.md` and the matrix rules only.

Each commit is exactly one backlog item: it touches only that item's code, its tests and its
matrix rows. Every summary follows Conventional Commits and has a `Refs:` line pointing to the
backlog file. There is no `Scenarios:` line, which is expected because there is no plan.
No package, migration, endpoint or Shared contract was added. No secrets are in the diff. The
backlog edit is uncommitted, as intended.

## Fix first
| # | File:line | Problem | Change |
|---|---|---|---|
| 1 | `src/RestaurantManagement.Shared/Services/Email/EmailService.cs:66`, `:73` | ASVS #19 ("a failed verification email logs the user ID, never the email address") is not met in production. `EmailService` logs `Sending email to {To}` before every send and `Error sending email to {to}` when a send fails, so a failed verification email still writes the address to the logs. The new tests replace `IEmailService` with a substitute, so they can't see this. The backlog file now says #19 is done, and R12 and RS6 say "never the email address" | Pick one. (a) Add a separate commit with scope `shared` that drops `to` from both log lines (log no recipient, or a user ID passed in) and adds a row to `shared-matrix.md` with a test. (b) Keep #19 open in the backlog file and add `EmailService.cs:66,73` as evidence. With (b), also reword R12 and RS6 to "the Identity log line names the user ID", so the rows only claim what the tests prove |

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `tests/RestaurantManagement.Modules.Identity.Tests/SecureRandomSourceTests.cs:46` | The source scan only searches for `new Random(` and `Random.Shared`, and only in `Services/`. It misses `new System.Random(`, `Random rng = new()` and code in other folders. It also only proves that `System.Random` is absent; it never proves that codes use `RandomNumberGenerator`, which T19's Expected implies | Search for the word `Random` without a `RandomNumberGenerator` match across the whole Identity project. Or add a check that each code generator (`RegistrationService`, `VerificationService`, `PasswordResetService`) contains `RandomNumberGenerator` |
| 2 | `tests/RestaurantManagement.Web.Tests/Identity/RegisterEndpointTests.cs:88`, `:100` | R15 says "counters at 0", but the forged body sets and checks only `failedAttempts`. `VerificationFailedAttempts`, `LockedUntil` and `VerificationLockedUntil` go untested | Add these fields to the forged body and assert they stay 0 or null, or narrow R15's wording |
| 3 | `tests/RestaurantManagement.Modules.Identity.Tests/PasswordExactMatchTests.cs:22` | T20 is proven only at the hasher. A future `Trim()` in login, change-password or reset would not fail this test. Today no service trims or changes the case of passwords (checked with grep) | Later, add one endpoint-level login test where a trailing space gives the generic 401 |

## Check before merging
- **Cookie rename (76e7528).** The old `refresh_token` cookie is no longer read, so every
  browser session signed in before the deploy is logged out once. That is harmless before
  launch. No other code, config or docs in `src/` or `docs/` still use the old name. Confirm
  the frontend and any proxy do not depend on it. The cookie is HttpOnly (JavaScript cannot
  read it), so the frontend should not.
- **Log privacy (047433f).** See Fix first #1. The Identity change is correct, but the item is
  only half done until `EmailService` stops logging the recipient.
- **Matrix wording.** R12, RS6, L2 and F2 were reworded and R15, T19 and T20 were added. Read
  the reworded rows once: they are now the contract the CI (continuous integration) check
  enforces.

## Pull request description
**Identity: ASVS small fixes and missing tests (#17, #19, #21, #22, #23)**

These five items from `backlog/tech-debt/2026-10-16-asvs-small-fixes-and-missing-tests.md`
each close one OWASP ASVS 5.0 Level 2 gap in Identity, with one commit per item.

What changed (Identity only):
- **#19** `fix(identity)`: when the verification email fails to send, registration and resend
  log the user ID instead of the email address. R12 and RS6 were updated, and
  `VerificationEmailFailureLogPrivacyTests` was added.
- **#17** `fix(identity)`: the refresh cookie is now `__Secure-refresh_token`. The old
  `refresh_token` name is never set or read. L2 and F2 were updated, and two endpoint tests
  were added.
- **#21** `test(identity)`: new row R15 with a test. The register body cannot set the ID,
  verified, active, the failed-attempt counter or roles.
- **#22** `test(identity)`: new row T19 with a test. Refresh tokens are 64 random bytes and
  never repeat, and no Identity service uses `System.Random`.
- **#23** `test(identity)`: new row T20 with a test. Passwords are checked exactly as typed,
  with no trimming and no case folding.

Risk: low. There is no migration, no new package, no new endpoint and no Shared contract.
Deploying logs out existing browser sessions once, because the old cookie name is ignored
(pre-launch, so there are no real users).

Still open in the backlog item: #15, #18, #24–#28.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
