# Review: Backlog fixes 2026-10-16 (ASVS #15, #18, #24–#28, V11, Identity missing tests)

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 789 passed, 0 failed (Identity 258, Shared 159, Web 372)

Reviewed `git log 4e50ec0..HEAD` (18 commits) against the architecture, testing, security and
clean-code skills and `CLAUDE.md`. No plan folder. ASVS means OWASP Application Security
Verification Standard (the checklist the gap report follows).
`python3 .claude/scripts/check-security-matrices.py`: "Security matrices in step with the tests".
No package, migration, endpoint or Shared contract added; no secrets (the T22 attacker key and
test passwords are test literals).

## Commits: one item each
| Commit | Item |
|---|---|
| 7fe038a, 7794b0e, bbcfd2b, 2ccab2f, a60637c | Tests for gaps #24–#28 (rows T21–T25) |
| 0c1b141 | Previous review |
| 4e07324 / eb17630 / 9b7bcbc | Fixes #18 (T26) / #15 (L20) / V11 bug (V4, V5, RS3, V11) |
| 1f46f71, 50ff3d9 | Gap report: rows marked Covered; V11 no longer listed as open |
| 65aac8e, e72c540, 0d2a3c8, 37262c0, bdb6bc3 | Earlier findings 5, 7, 8, 1+2, 3 |
| 113b5d0 | Deletes the two solved backlog items |
| 3a089b9 | Earlier finding 6 (locked verify timing) |

## Earlier findings
| # | Status |
|---|---|
| 1, 2 (T23 loopholes) | Resolved: verbs must be exactly POST; a parameter with no or unknown binding source counts as a URL read |
| 3 (T22 not decisive) | Resolved: new test sends each header on a server-signed token and expects it accepted; row T22 names it |
| 4 (long line) | Open, style only: `ApprovedHashAlgorithmsTests.cs:63` |
| 5 (controller wiring untested) | Resolved: `= null` default removed, so a caller must pass the cookie; endpoint test on real PostgreSQL proves the old token is refused; named in L20 |
| 6 (locked verify answers faster) | Resolved: the lock check now runs after the code lookup; V11 names the test |
| 7 (unused resx key) | Resolved in both languages |
| 8 (V4/V5 message not asserted) | Resolved |
| 9 (gap report V11 note) | Resolved |
| 10 (login can trigger reuse detection) | Accepted, see Watch out 1 |

## Production changes: full review
- **HS256 allowlist** (`Program.cs:88`): `ValidAlgorithms = [HmacSha256]` is correct; T26
  proves HS384/HS512 with the right key get 401, T25 proves issued tokens are HS256.
- **Login revokes the browser's cookie** (`SessionService.cs:101-103`): runs only after every
  check passed and counters are saved, before the new token is issued. L20 covers success, a
  refused login keeping the token, other devices untouched, and the controller wiring.
- **V11** (`VerificationService.cs:46-47, 89-90, 168`): locked verify, the lock-tripping attempt
  and locked resend all give the unknown-email answer. V4, V5, RS3, V11 rows match tests.
- Test edits in 65aac8e are signature updates only (`null` or `Arg.Any<string?>()` added); no
  assertion dropped or weakened.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | src/RestaurantManagement.Modules.Identity/Services/SessionService.cs:103 | `RevokeAsync` does not check the cookie belongs to the user logging in, and an already rotated cookie revokes its whole family. Harmless (the same browser gets a new family), but undocumented | Say so in row L20 |
| 2 | 65aac8e (commit) | Typed `test` but changes `ISessionService`/`SessionService` signatures in `src/` | Next time use `refactor`; no action needed now |
| 3 | docs/security/identity-asvs-gap-report.md:5,19 | Header says "Nothing changed: no code, test or matrix edits" and the summary is titled "Partial and Missing", but it now holds Covered rows | Update the header, or move Covered rows out of the summary |
| 4 | tests/RestaurantManagement.Modules.Identity.Tests/ApprovedHashAlgorithmsTests.cs:63 | Long line with fully qualified `IOrganizationLookup` (style) | Add a `using` and break the call |

## Check before merging
- **Base branch:** this branch sits on `security-fixes`. Merge `security-fixes` into
  `development` first, or this pull request carries its commits too.
- **HS256 allowlist (Program.cs:88):** any token not signed HS256 is now refused. Today's tokens
  are HS256, so no live session breaks.
- **Backlog deletion committed (113b5d0):** committed at the user's request, as an exception
  to the `CLAUDE.md` rule that the user commits backlog deletions.
- **Verification lockout:** the frontend can no longer show "locked until"; a locked user sees
  "invalid email or code" for 15 minutes.

## Pull request description

**Title:** fix(identity): close ASVS gaps #15, #18, #24–#28 and the verification lockout leak

### Summary
Fixes three Identity security gaps and adds proof for five rules the code already enforced
(OWASP ASVS 5.0 Level 2 gaps #24–#28). A locked verification no longer reveals that an email
has an account.

### What changed
| Module | Change | Matrix row |
|---|---|---|
| Web | Access tokens signed with anything but HS256 are refused (401) | T26 |
| Identity | Logging in on a browser revokes that browser's previous refresh token, only once the login succeeds; other devices stay signed in | L20 |
| Identity | A verification-locked account gets the same 401 (verify) or acknowledgement (resend) as an unknown email, instead of 423, and runs the same code lookup so it is not faster | V4, V5, RS3, V11 |
| Identity | New tests: `nbf` enforced, `jku`/`x5u`/`jwk` headers ignored, POST-only endpoints with no secrets in URLs, no default accounts, SHA-256 fingerprints and HS256 signing | T21–T25 |
| Shared | Unused `VerificationLockedUntil` message removed (English and Portuguese) | — |
| Docs | Gap report rows #15, #18, #24–#28 marked Covered; V11 marked fixed | — |

### Risk and flags
Auth changes (token validation, login, verification responses), approved by the solution
architect. `ISessionService.LoginAsync` now requires the previous refresh token (module
internal). The V4, V5 and RS3 tests changed from expecting 423, as the approved V11 fix requires.
No migration, package, endpoint or Shared contract. Depends on `security-fixes` being merged
into `development` first. Backlog items `bugs/2026-10-16-verification-lockout-reveals-account.md`
and `tech-debt/2026-10-16-asvs-small-fixes-and-missing-tests.md` are deleted (solved).

### Checks
- `dotnet build RestaurantManagement.sln`: pass
- Identity, Shared and Web tests: 789 passed, 0 failed
- `python3 .claude/scripts/check-security-matrices.py`: in step

🤖 Generated with [Claude Code](https://claude.com/claude-code)
