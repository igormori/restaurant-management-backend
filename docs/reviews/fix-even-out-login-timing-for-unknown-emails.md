# Review: Even out login timing for unknown emails

**Verdict:** Ready to merge
**Build:** pass   **Tests:** full suite runs in CI (Continuous Integration) — merge only when it is green

Branch `fix/even-out-login-timing-for-unknown-emails`, commit `3eede73`, against `origin/development`.
Small change: no spec or plan folder, so there is no Completeness check. Source requirement: backlog item
`backlog/improvements/2026-10-23-even-out-login-timing-for-unknown-emails.md`.

Checked:
- Build: 0 errors. The 5 warnings are in Organization endpoint tests that this branch does not touch.
- Security matrices: `python3 .claude/scripts/check-security-matrices.py` passes. Row L6 names both new tests,
  and both carry `[Trait("Rule", "L6")]`.
- Scope: 5 files, all in Identity. No migration, no package, no endpoint, no Shared contract.
- Structure: no new files or folders.
- Tests: only additions. No existing test was edited, deleted or skipped.
- Secrets: none in the diff.
- Commit: one Conventional Commit, `fix(identity)`, and the body explains why.

Round trips (one database command each) per login attempt, counted from the code:

| Case | Commands |
|---|---|
| Wrong password, real account, not locked | 6 (user lookup, address-lock check, cleanup, insert, address count, account count) |
| Unknown email (after this change) | 6 (user lookup plus the same five, with an empty update instead of the insert) |
| Real account, address locked | 2 |
| Real account, account locked | 1 |

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `src/RestaurantManagement.Modules.Identity/Services/Sessions/SessionService.cs:85-93` | A locked real account now runs 1 to 2 commands, but an unknown email runs 6. Before this change the gap was 1 command (2 against 1); now it is 4 to 5. After 5 wrong passwords from one address, a faster 6th reply shows that the email has an account. The 5th wrong password also runs one more save (the address lock) than an unknown email. Row L6 says "the timing matches a real account", which is not true for locked accounts. | Also call `_lockout.SimulateFailureAsync(client.IpAddress)` when `isLocked` is true. That gives 6 to 7 commands. If you leave it for later, write a backlog item for the locked case and change L6 to "matches a wrong password for an account that is not locked". |
| 2 | `src/RestaurantManagement.Modules.Identity/Services/Lockout/LoginLockout.cs:44-45` | The comment says the simulation "changes nothing". `DeleteExpiredAsync` still deletes every user's expired rows. That cleanup is harmless, but the comment is wrong. | Reword it: "changes nothing except the shared cleanup of expired rows". |
| 3 | `tests/RestaurantManagement.Modules.Identity.Tests/SessionServiceLoginTests.cs:377-404` | The backlog item asked for a test or measurement showing the response times differ by no more than normal variation. The new test only counts commands, and only for the first wrong password, on SQLite. An empty UPDATE also costs less than an INSERT that checks a foreign key on PostgreSQL. | Accept the command count as the stand-in for timing, and say so in the pull request. If you want proof, add a timing check against PostgreSQL (Testcontainers) as a follow-up. |

## Check before merging
- The locked-account gap (Watch out #1). This change closes the leak on every wrong password. It also widens
  the gap for locked accounts, which an attacker reaches after 5 attempts from one address (rate limits still apply).
- `SimulateFailureAsync` sends real writes to PostgreSQL: an `UPDATE login_failures ... WHERE "UserId" = Guid.Empty`
  and the cleanup `DELETE`. The foreign key to `users` means no row can have `UserId = Guid.Empty`, so the update
  never matches anything. Each unknown-email login now adds 5 commands, and the cleanup delete runs on every one.
- The backlog file's deletion is not committed. The user checks and commits it.

## Pull request description
**fix(identity): run the same database work for unknown login emails**

Before this change, a login with an unknown email only checked the dummy password hash. A wrong password for a real
account also read and wrote login failures (about 5 more database round trips), so the response time could show
which emails have an account.

What changed (Identity):
- `LoginLockout.SimulateFailureAsync` runs the same database commands as a wrong password, on a user ID that matches
  no row, so it stores nothing. `SessionService.LoginAsync` calls it when the email is unknown.
- `LockAccountWhenLimitReachedAsync` now takes a user ID instead of the user, so the simulation can use it.
- The Identity `CLAUDE.md` says not to remove the simulation as dead code.
- Security matrix row L6 now promises the same command count and names two new tests:
  `LoginAsync_UnknownEmail_RunsAsManyDatabaseCommandsAsAWrongPasswordForARealAccount` and
  `LoginAsync_UnknownEmail_StoresNoLoginFailure`.

Backlog item `2026-10-23-even-out-login-timing-for-unknown-emails`:
- Same number of round trips for an unknown email as for a wrong password: done.
- Every refusal still returns the same 401: done (L5 tests unchanged).
- Timing measurement: covered by the command count test only, not a timing measurement.

Risk: low. No migration, no package, no endpoint, no Shared contract. Known gap: a locked real account still runs
fewer commands than an unknown email (see review Watch out #1).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
