# Review: Wire HTTPS into the running app (feat/wire-https)

**Verdict:** Fix first — the new process-wide `Https__*` environment variables leak into `HttpsHostingExtensionsTests` running in parallel; reproduced 1 failure in 15 runs.
**Build:** pass (0 warnings, 0 errors)   **Tests:** 800 passed, 0 failed (full `dotnet test`)

Small change (fast path): no spec, plan or scenarios folder. Reviewed against CLAUDE.md and the
architecture, security, clean-code and testing skills. Resolves `backlog/tech-debt/https-wiring.md` (code part only).

## Fix first
| # | File:line | Problem | Change |
|---|---|---|---|
| 1 | tests/RestaurantManagement.Web.Tests/StartupConfigurationTests.cs:40-46 | `Environment.SetEnvironmentVariable` is process-wide. xUnit runs test classes in parallel by default, and `HttpsHostingExtensionsTests.CreateBuilder` (line 45) calls `WebApplication.CreateBuilder`, which reads environment variables. Scenario 24 (`ProductionWithoutCertificate_ThrowsNamingCertificatePath`, line 69) passes no `Https:CertificatePath`, so it picks up the other class's valid certificate path and does not throw. Reproduced: running only these two classes 15 times gave 1 failure of Scenario 24. A flaky suite in CI (Continuous Integration: the automatic build on every push) blocks every later merge. | Put both classes in one xUnit collection that does not run in parallel: add `[CollectionDefinition("Process environment", DisableParallelization = true)]` on an empty class in the test project and `[Collection("Process environment")]` on `StartupConfigurationTests` and `HttpsHostingExtensionsTests`. This is an attribute-only test edit with no expectation change; get the user's OK first (see Watch out 1). Then rerun the two classes 20+ times. |

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | tests/RestaurantManagement.Web.Tests/StartupConfigurationTests.cs:20-55 | CLAUDE.md says test files are read-only after Gate 2. This branch edits the `CreateFactory` helper and `Dispose` of an approved test file. Checked: no assertion, input or test was changed, deleted or skipped. Scenarios 15, 18, 19 and 20 still assert the same thing, and the backlog item prescribed this exact approach ("No test expectations change"). I judge it acceptable, but it is still an exception to the rule. | The user approves it explicitly at Gate 3. The PR description below lists it. |
| 2 | tests/RestaurantManagement.Web.Tests/StartupConfigurationTests.cs:100-113 | The implementer did not show that the new test fails first. | Verified in a throwaway worktree: with the old `Program.cs`, `Startup_NoCertificateInProduction_FailsNamingHttpsCertificatePath` fails (1 failed, 18 passed). It is a real regression guard. Nothing to change. |
| 3 | infra/docker/Dockerfile:30-31 | The image sets `ASPNETCORE_URLS=http://+:8080` / `EXPOSE 8080` and no environment, so it runs as Production. After this merge, the container fails at startup without a certificate. With a certificate, it listens on 8443, and the explicit `Listen` ignores the URLs setting. Out of scope by design, but anything deployed from this image breaks. | Do not deploy outside Development until the deployment follow-up lands: set `EXPOSE 8443`, drop `ASPNETCORE_URLS`, mount the certificate and set `Https__CertificatePath`/`Https__CertificatePassword`/`Https__Port`, and make Next.js trust the certificate. |
| 4 | backlog/tech-debt/https-wiring.md | The code part is solved and the deployment part is not. The backlog skill (section 5) says a partially solved item has its Problem and Done-when updated to what is left. The branch does not touch it. | Change the item to cover only the deployment steps (Dockerfile port, certificate mount, Next.js trust). The user keeps it or deletes it. |

## Check before merging
- **Program.cs:42 `UseHttpsOnlyOutsideDevelopment()`**: every non-Development environment (Staging, Production) now refuses to start without `Https:CertificatePath` pointing to a real file. That is intended, but any current non-Development deployment breaks until the deployment items are done.
- **Test isolation (Fix first 1)**: the suite is green on most runs but not every run. A green run alone does not show it is fixed.
- **Ordering**: the call sits after `AddKeyVaultSecrets()`, so a certificate password stored in Key Vault is available. Keep it there if `Program.cs` is reorganized.

## Pull request description

**feat(web): serve HTTPS only outside development**

Outside Development, the API now serves HTTPS only and refuses to start without a certificate.
`Program.cs` now calls the existing `UseHttpsOnlyOutsideDevelopment()` extension, which was
already built and tested but not wired in. This completes rule 4 of the auth-hardening spec
(`docs/specs/identity/auth-hardening.md`) and the code part of `backlog/tech-debt/https-wiring.md`.

**What changed**
- Web: `Program.cs` calls `builder.UseHttpsOnlyOutsideDevelopment()` right after Key Vault secrets load.
- Tests: `StartupConfigurationTests.CreateFactory` now passes `Https:*` settings as `Https__*`
  environment variables, cleared in `Dispose`. `Program.cs` reads them before the test factory's
  configuration applies. **Existing test file edited (helper only); no expectation changed.**
  Needs explicit approval under the "tests are read-only" rule.
- New test: Production startup without a certificate fails, naming `Https:CertificatePath`.
  Confirmed failing before the wiring.

**Risk and flags**
- Deployment is not included. The Dockerfile still exposes HTTP 8080, and the app now listens on
  8443 with a mounted certificate. Do not deploy outside Development until the certificate mount,
  `Https__*` variables and Next.js trust are set up.
- No migration, no new package, no new endpoint, no Shared contract change.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
