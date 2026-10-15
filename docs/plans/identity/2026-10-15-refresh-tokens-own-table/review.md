# Review: Refresh tokens in their own table

**Verdict:** Ready to merge
**Build:** pass (0 errors, 2 existing warnings in the Organization module)   **Tests:** 384 passed, 0 failed

Reviewed against base branch `development` (7 commits, `3187c25`..`5c7cb72`). No test file changed since the baseline commit `3187c25` (`git diff 3187c25..HEAD -- tests/` is empty). No package added, so `dotnet list package --vulnerable` was not needed. No secrets found in the diff.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | src/RestaurantManagement.Web/Program.cs (whole file) | The file's line endings changed from CRLF (Windows) to LF (Unix), so the diff shows 574 changed lines when only 3 really changed (service registration and `AllowCredentials()`). This breaks the "surgical changes" rule and hides the real change from reviewers | Restore the CRLF line endings, or move the line-ending change into its own `chore` commit |
| 2 | src/RestaurantManagement.Modules.Identity/Services/SessionService.cs:44-50 | `SessionService` creates `AccessTokenFactory` and `OrganizationMembership` with `new` instead of receiving them through the constructor. That goes against the architecture skill rule "services depend on interfaces" (the D in SOLID: Dependency inversion) | Later, register both helpers behind small interfaces and inject them. Acceptable for now because they are internal and covered by the existing tests |
| 3 | src/RestaurantManagement.Modules.Identity/Services/AccessTokenFactory.cs, OrganizationMembership.cs, SessionResults.cs, PasswordVerifier.cs | These 4 new files are not in any task's "Files" row. They were split out of `SessionService` to stay under the size limit. The T3 Delivered note mentions this, but plan.md does not | The architect should accept the change at Gate 3. The folder (`Services/`) is correct |
| 4 | src/RestaurantManagement.Modules.Identity/Entities/RefreshToken.cs:3, Models/ClientInfo.cs:3, Models/IssuedRefreshToken.cs:3, Models/SessionResult.cs:3 | The tester's stub comments were left in and are now wrong. For example, "Not yet mapped in IdentityDbContext (task T1)" is no longer true | Replace them with a one-line "why" comment, or delete them |
| 5 | src/RestaurantManagement.Shared/Localization/SharedResource.resx:72, SharedResource.pt.resx:72 | The `InvalidRefreshToken` key is no longer used. This change made it unused, so the clean-code skill says this change should remove it | Delete the key from both `.resx` files |
| 6 | src/RestaurantManagement.Modules.Identity/Controllers/AuthController.cs:42,52 | `logout` has no authorization attribute and `logout-all` uses a plain `[Authorize]` without roles. The security skill asks for explicit roles on every action. The plan flags both and allows them on purpose: the spec covers every role, and users without an organization have no role at all | No change needed. Consider adding an explicit `[AllowAnonymous]` to `Logout` so the intent is visible |
| 7 | docs/plans/identity/2026-10-15-refresh-tokens-own-table/plan.md:31-35 | The Checklist ticks each task but gives no commit hash | Add the hashes: T1 `d545edf`, T2 `555ac29`, T3 `4e3d423`, T4 `8a02b05`, T5 `f9eccb0` |
| 8 | commit 3187c25 (body) | The baseline commit says `Scenarios: 1–N`, which is a placeholder | Leave it if the history is already shared. Otherwise reword it to `Scenarios: 1–78` |

## Check before merging
- **CORS `AllowCredentials()` with `SameSite=Strict` cookies** (CORS: Cross-Origin Resource Sharing, the browser rules for calls from another website). The cookie is sent only when the frontend and the API are on the same site (same registrable domain, so `app.example.com` and `api.example.com` count). If the frontend runs on a different domain, refresh silently returns 401. Also confirm that `Cors:AllowedOrigins` lists exact origins in every environment.
- **`DropUserRefreshTokenColumns` drops two columns from `users`**, a table that holds data. This is irreversible data loss by design: every user is signed out at release. The frontend must ship at the same time, because the token moves from the response body to the cookie and `/refresh` no longer accepts a body.
- **Rotation and reuse detection in `RefreshTokenService.ReplaceAsync`.** Two refreshes at the same moment from one device (for example two browser tabs) revoke the whole family and sign the user out. This is expected and tracked in `backlog/improvements/2026-10-15-refresh-reuse-grace-window.md`, but support should know about it.

## Pull request description
**Refresh tokens in their own table (Identity)**

Every sign-in now gets its own stored "stay signed in" token (refresh token), so users can stay signed in on several devices at once. Each token is replaced on every use. Reusing an old token is treated as theft and signs out that device. Each device can be signed out alone, or all devices at once.

**What changed**
- Identity
  - New `refresh_tokens` table. Only a SHA-256 (Secure Hash Algorithm, 256-bit) fingerprint of each token is stored, never the token itself. The table also holds the token family, expiry, revocation reason, the replacement link, the device description and the IP (Internet Protocol) address. Deleting a user deletes their tokens (cascade).
  - New `RefreshTokenService`: issue, rotate (a conditional update, so two parallel uses succeed only once), revoke the whole family on reuse, revoke one token, revoke all tokens of a user.
  - Login, refresh and select-organization now set an HttpOnly, Secure, SameSite=Strict `refresh_token` cookie (Path `/api/auth`). The token is no longer in the response body. `/refresh` takes no body.
  - New `POST api/auth/logout` (public, rate-limited) and `POST api/auth/logout-all` (needs sign-in). Both return 204 and clear the cookie.
  - Dropped `users.refresh_token_hash` and `users.refresh_token_expiry`.
  - `SessionService` was split into `AccessTokenFactory`, `OrganizationMembership`, `SessionResults` and `PasswordVerifier` to stay under the size limit.
- Web: registers `IRefreshTokenService`; the CORS policy now allows credentials.

**Spec Outcomes delivered:** #1–#9 (cookie attributes, independent devices, rotation, reuse revokes the family, expiry refused, single-device logout, fingerprint differs from the token, sign out everywhere, old columns removed).

**Risk:** High. Flags from plan.md:
- New table with cascade delete that stores personal data (IP address, device).
- Unkeyed SHA-256 fingerprint.
- Change to the authentication API contract.
- CORS `AllowCredentials`.
- New public `logout` endpoint.
- Migration drops two columns from `users`.

**Release notes:** every user must sign in again once. The frontend must switch to the cookie (`credentials: 'include'`) and stop sending or reading `refreshToken` in request and response bodies.

**Follow-ups (backlog):** refresh reuse grace window, purge old refresh tokens, `User.SelectedOrganizationId` now redundant, access-pass revocation (`session-redesign.md`, updated).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
