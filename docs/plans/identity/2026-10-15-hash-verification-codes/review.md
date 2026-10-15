# Review: Hash verification codes

**Verdict:** Ready to merge
**Build:** pass (0 errors, 9 existing warnings)   **Tests:** 311 passed, 0 failed (Shared 77 · Organization 32 · Menu 23 · Web 82 · Identity 97)

Reviewed against `development` (where PRs #3 to #5 were merged), not `main`. See "Check before merging".
No packages were added, so `dotnet list package --vulnerable` was not needed. No test file changed after the baseline commit `ba1f4c3` (`git diff ba1f4c3..HEAD -- tests/` is empty).

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | docs/plans/identity/2026-10-15-hash-verification-codes/plan.md:32-35 | Every Checklist task is ticked, but none shows its commit hash | Add the hashes: T1 `6a72083` + `4bc2bca`, T2 `55c5519`, T3 `f735c49`, T4 `d93381e` |
| 2 | Commits `6a72083`, `4bc2bca` | T1 is split over two commits, which breaks the "one commit per task" rule. The follow-up only adds a comment to `.env.example` | Squash `4bc2bca` into `6a72083` before merging, or accept as is |
| 3 | src/RestaurantManagement.Web/Program.cs:229 | Change outside the task: the `IVerificationService` line now ends in LF (line feed) instead of CRLF (carriage return plus line feed), so the file mixes line endings | Restore CRLF on that line, or leave it as harmless noise |
| 4 | src/RestaurantManagement.Shared/Options/VerificationCodeOptionsValidator.cs:12 | A key of 32 spaces passes validation. That is a weak secret | Use `string.IsNullOrWhiteSpace` (needs a new test, so put it in the backlog rather than this branch) |

## Check before merging
1. **The new required secret.** The app and the Docker `migrate` step now refuse to start without `VerificationCode:HashKey` (at least 32 bytes) in every environment. The Key Vault secret `VerificationCode--HashKey` must exist in every deployed environment before this ships. Otherwise the deploy fails at startup. Changing the key later cancels every code that has not been used yet.
2. **Migrations `HashVerificationCodes` and `RetireUnhashedVerificationCodes`.** These rename `code` to `code_hash` and run a backfill that marks every existing code used. This is a breaking schema change: an old app instance still running during the rollout fails on the old column name. Deploy with a short stop, not a rolling deploy. Users holding a code at release must ask for a new one (spec Rule 6). `Down` cannot bring the readable codes back.
3. **Pull request target.** `main...HEAD` also contains all of `development` (PRs #3 to #5, other migrations, the new `.claude/` workflow), because those were never merged into `main`. Open this pull request against `development`. If it targets `main`, those changes go out unreviewed.

## Pull request description
**fix(identity): store email verification codes as keyed fingerprints**

### Summary
Email verification codes were saved in the database as readable text, so anyone with database access could use them. Codes are now stored only as an HMAC-SHA256 fingerprint. HMAC (Hash-based Message Authentication Code) is a one-way fingerprint made with a server secret that is kept outside the database. Without that secret, a leaked fingerprint cannot be matched by trying all one million 6-digit codes. The submitted code is checked with a constant-time comparison. Users do not see any change in the verification flow.

### Changes per module
- **Shared:** `VerificationCodeOptions` plus a validator. `VerificationCode:HashKey` is required and must be at least 32 bytes (UTF-8).
- **Identity:** `IVerificationCodeHasher` and `VerificationCodeHasher`. The fingerprint covers the user ID and the code, and matching uses `FixedTimeEquals`. Registration and resend save only the fingerprint, and verify uses `IsMatch`. Migration `HashVerificationCodes` renames `code` to `code_hash`. Migration `RetireUnhashedVerificationCodes` marks all existing codes used and blanks them.
- **Web:** startup validation (`ValidateOnStart`) and hasher registration. `appsettings.json` has an empty key with a note on how to supply it. `UserSecretsId` added.
- **Infra:** `docker-compose.yml` passes `VERIFICATION_CODE_HASH_KEY` to `migrate` and `api`. `.env.example` and `infra/CLAUDE.md` updated.
- **Docs:** old-workflow specs and plans removed, as the user asked.

### Spec Outcomes delivered
1 Stored code is not readable · 2 Valid code verifies · 3 Wrong code is counted · 4 Lock still works · 5 Expired or used code rejected · 6 Only newest code works · 7 Old codes retired · 8 Missing secret is caught. The tests cover all 39 scenarios.

### Risk and flags (from plan.md)
**Risk: High.** A column on a table with data is renamed, a backfill runs, and a security check changes.
- New required secret `VerificationCode:HashKey`. It is never committed. Sources: `infra/docker/.env`, user-secrets, or Key Vault `VerificationCode--HashKey`.
- Cryptography from the .NET base library only. No new package.
- Hand-written `migrationBuilder.Sql` backfill. Codes that exist at release stop working, and those users must request a new code.

### Before deploying
- Create the Key Vault secret `VerificationCode--HashKey` (for example `openssl rand -base64 32`) in every environment.
- Local: `dotnet user-secrets set VerificationCode:HashKey <value> --project src/RestaurantManagement.Web`, or set `VERIFICATION_CODE_HASH_KEY` in `infra/docker/.env`.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
