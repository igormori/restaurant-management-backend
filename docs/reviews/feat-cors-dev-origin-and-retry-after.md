# Review: Allow the frontend dev origin and expose Retry-After through CORS

**Verdict:** Ready to merge
**Build:** pass (0 errors; 6 warnings, all in existing files, none in the new test)   **Tests:** full suite runs in CI — merge only when it is green

Scope: small change (`/small`), one commit `82255c7`, 4 files. CORS (Cross-Origin Resource
Sharing: the browser rules that decide whether a page on another site may read an answer).
Security matrix check (`python3 .claude/scripts/check-security-matrices.py`): passes.
No packages added, so `dotnet list package --vulnerable` was not needed.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `infra/docker/docker-compose.yml:43` | Docker runs the API in Development but never reads `launchSettings.json`. So the API in Docker still allows no origin, and the frontend dev server cannot call it there. This file is outside the diff. | If the frontend is meant to call the Docker API, add `Cors__AllowedOrigins__0: ${CORS_ALLOWED_ORIGIN:-http://localhost:5173}` to the `api` service in a follow-up, or write a backlog item. |
| 2 | `src/RestaurantManagement.Web/Properties/launchSettings.json:11` | An environment variable overrides user-secrets and `appsettings.Development.json`. A developer who sets `Cors:AllowedOrigins:0` there would see it silently replaced by `http://localhost:5173`. | No change needed now. A second dev origin must use index `1` (`Cors__AllowedOrigins__1`). |

## Implementer's open question: does Development pick up the origin?
Yes, from reading the code (not run by hand):
- `Program.cs:301` binds `builder.Configuration.GetSection("Cors").Get<CorsOptions>()`. In `CreateBuilder`,
  environment variables load after `appsettings.json` and `appsettings.Development.json`.
  `Cors__AllowedOrigins__0` maps to `Cors:AllowedOrigins:0`. The `[]` in `appsettings.json` adds no
  keys, so nothing conflicts, and the binder fills `AllowedOrigins[0]`.
- `dotnet run` and `dotnet watch` apply the first profile (`http`) by default. Rider and
  Visual Studio apply the profile you pick. Both profiles set the variable.
- No origin is set by `dotnet run --no-launch-profile`, a published `dotnet <dll>`, or Docker
  (see Watch out 1). Production configuration (`appsettings.json`) still allows no origin, so
  acceptance criterion 1 holds.
- The new test sets the same key (`Cors:AllowedOrigins:0`) through in-memory configuration. This
  proves the key binds to the policy.

## Acceptance criteria
1. Dev origin comes from the tracked `launchSettings.json`, both profiles. Nothing changed in `appsettings.json`. Met.
2. `.WithExposedHeaders("Retry-After")` is on the single `DefaultCorsPolicy` (`Program.cs:309`), so
   every answer to an allowed origin carries it. Met.
3. `CorsRetryAfterTests.Register_RateLimitedFromAllowedOrigin_ExposesRetryAfter` sends 2 registrations
   with a limit of 1 and checks that the 429 carries `Access-Control-Expose-Headers` with `Retry-After`.
   It is tagged `W25`, and W25 in `docs/security/web-matrix.md` names it. Met.

## Checks
- Structure: the new test is in `tests/RestaurantManagement.Web.Tests/Web/`, the matrix's folder.
  Its namespace matches its sibling tests. No new folders.
- Architecture and design: one line of policy setup in `Program.cs`. No module boundary touched.
- Security: the origin list stays explicit (no wildcard with credentials). Only `Retry-After` is
  exposed, and it holds no secret. W24 (unknown origin refused) is unchanged.
- Secrets: none in the diff.
- Commit: Conventional Commits, scope `web`, 71 characters, body says why. The backlog item file is
  not on `development`, so there is nothing to delete.

## Check before merging
- `Program.cs:309`: the exposed header applies to every answer to every allowed origin, in every
  environment. This is intended, and it leaks nothing.
- `launchSettings.json`: the dev origin only works when the API starts through a launch profile.
  Docker and `--no-launch-profile` still allow no origin.

## Pull request description
**Allow the frontend dev origin and expose Retry-After through CORS**

A page on another origin could not read `Retry-After` after a 429 (Too Many Requests), so the
frontend could not tell users how long to wait. Also, the frontend dev server at
`http://localhost:5173` was not an allowed origin.

Web:
- `Program.cs`: the CORS policy now exposes `Retry-After` to allowed origins.
- `Properties/launchSettings.json`: both Development profiles set
  `Cors__AllowedOrigins__0=http://localhost:5173`. Production configuration still allows no origin.
- New test `CorsRetryAfterTests.Register_RateLimitedFromAllowedOrigin_ExposesRetryAfter`. It proves a
  cross-origin 429 from `POST /api/auth/register` exposes `Retry-After`.
- `docs/security/web-matrix.md`: new row W25.

Risk: low. No migration, no package, no new endpoint, no Shared contract change.
Note: the API in Docker (`infra/docker/docker-compose.yml`) still allows no origin, because Docker
does not read `launchSettings.json`.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
