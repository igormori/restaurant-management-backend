# Review: Emailed codes expire after 10 minutes

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 948 passed, 0 failed

Branch `fix/email-codes-expire-in-10-minutes` against `security-fixes`, one commit (5f33952).
This is a small change (`/small` fast path), so there is no spec or plan folder and the Completeness check is skipped.
Security matrix check (`python3 .claude/scripts/check-security-matrices.py`): passes.
No packages were added, so the vulnerable-package check was not needed.

## Acceptance criteria (backlog item `2026-10-16-email-codes-expire-in-10-minutes.md`)
| Criterion | Met | Evidence |
|---|---|---|
| Codes are refused 10 min after sending, and the emails say 10 min | Yes | `RegistrationService.cs:78`, `VerificationService.cs:123` and `PasswordResetService.cs:75` take the expiry from `VerificationCodeExpiryMinutes`. The same value goes to the email templates (`{{EXPIRY_MINUTES}}`), so the expiry and the email text cannot drift apart |
| The default and the shipped setting are both 10 | Yes | `SecurityOptions.cs:7`, `appsettings.json:48`. `appsettings.Development.json` does not override it |
| Matrix header and T16 say 10 min, and the T16 test proves it | Yes | `identity-matrix.md:35` and `:199`. `ShippedLimitsTests.cs:46` asserts 10 |
| Lockout (15 min) and resend wait (60 s) are unchanged | Yes | Not touched in the diff. T16 still asserts 15, 15 and 60 |

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | docs/security/identity-asvs-gap-report.md:29 | Summary row 8 (ASVS V6.5.5; ASVS is the OWASP Application Security Verification Standard) still says "Partial: codes live 15 min", but this branch fixes that gap | When you commit the backlog item deletion, mark row 8 as Covered (T16), or note that it was fixed on 2026-10-16. The report is a dated snapshot, so this is optional |
| 2 | tests/RestaurantManagement.Modules.Identity.Tests/IdentityTestDatabase.cs:50 | The test helper's default `verificationCodeExpiryMinutes = 15` no longer matches the shipped value (`RetireVerificationCodesMigrationTests.cs:98` also passes 15). Tests are not affected, since none depends on the shipped value | Leave it as is. Change it to 10 only if those files are edited for another reason |

## Check before merging
- `ShippedLimitsTests.cs:46`: an existing test was changed (15 to 10). The backlog item asks for this change, and the test is just as strict as before, so it is not weakened. It is still the only change to a test.
- Environment variables set at deployment (`Security__VerificationCodeExpiryMinutes`) can still override the 10. Check that no deployment sets them (see the backlog item `deployment-overrides-of-security-limits-untested`).
- The deleted backlog item file is not committed yet. Commit the deletion yourself, as the backlog rules require.

## Pull request description
**Emailed codes now expire after 10 minutes instead of 15**

Email verification codes and password reset codes now stop working 10 minutes after they are sent. OWASP ASVS 5.0 Level 2 (V6.5.5) allows at most 10 minutes for codes sent by email. The emails show the new lifetime automatically, because they read the same setting.

Changes by area:
- **Shared:** the default for `SecurityOptions.VerificationCodeExpiryMinutes` is now 10.
- **Web:** the shipped `appsettings.json` value is now 10.
- **Identity (docs and tests):** the matrix limits header and row T16 say 10 minutes. `ShippedLimitsTests` checks that the shipped value is 10.

Unchanged: 5 wrong codes still lock verification or reset for 15 minutes, and a new code still needs a 60-second wait.

Risk: low. One configuration value changes. There is no migration, no new package, no new endpoint and no new Shared contract.

Closes backlog item `backlog/improvements/2026-10-16-email-codes-expire-in-10-minutes.md` (ASVS gap report, Summary #8).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
