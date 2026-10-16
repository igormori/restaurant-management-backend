# Review: Return 404 or 401 instead of 500 for missing organization records

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 687 passed, 0 failed

Scope reviewed: commit 154fc3a, `git diff development...HEAD`. The branch is one commit on top of
`development`. This is a small change, so there is no spec, plan or scenarios file. It was checked
against `CLAUDE.md`, the Organization module `CLAUDE.md`, and the architecture, security,
clean-code and testing skills.

Test totals per project: Shared 143, Organization 106, Menu 47, Identity 148, Web 243.
The build had 0 warnings and 0 errors. No packages were added or updated, so the
vulnerable-package check was not needed.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | src/RestaurantManagement.Modules.Organization/Services/OrganizationService.cs:190 | The architecture skill (section 5) says "Not found: return `null`; the controller turns that into 404". The fix throws `BusinessException(..., 404)` instead. It matches what `LocationService` and `MenuService` already do, so it is consistent with the code, but not with the written rule. | Keep the fix. Later, either update the architecture skill so it allows `BusinessException` with 404, or add a `backlog/tech-debt/` item to move services to the `null` pattern. The skill and the code should not disagree. |
| 2 | tests/RestaurantManagement.Modules.Organization.Tests/OrganizationServiceNotFoundTests.cs:31 | The new tests only check the exception at the service level. No endpoint test checks that the client really gets 404 or 401 with the translated message. The generic mapping is already covered by `ExceptionHandlingMiddlewareTests`, so the risk is low. | Optional: add one Web.Tests case (for example in `OrganizationMessagesEndpointTests`) that calls `PUT api/organizations/edit` for an organization with no settings and expects 404 with the Portuguese message. |

## Checks passed
- **Scope:** 4 lines changed in `OrganizationService` (three call sites plus the matching one in
  `GetOrganizationsAsync`), one new test file, and the deleted backlog bug. No migration, package,
  endpoint, role or Shared contract (a type that other modules depend on) changed.
  `InvalidOperationException` no longer appears in any module service. It remains only for
  startup and configuration errors, where a 500 is correct.
- **Structure:** the new test file is in the existing Organization test project, next to the
  other `OrganizationService*Tests`. No new folders.
- **Tests:** no existing test was edited, deleted, skipped or weakened. The 4 new tests use
  xUnit, Shouldly and NSubstitute, follow `Method_Scenario_ExpectedResult` naming, have
  Arrange/Act/Assert comments, use realistic values, and check both the status code and the
  message key. Against the old code they would fail, because `InvalidOperationException` is not
  a `BusinessException`. That satisfies the rule that bug fixes start with a failing test.
- **Security:** nothing changed in tenancy. `EditOrganizationAsync` still takes the
  organization only from the token (`_tenantContext.OrganizationId`), and the global query filter
  still applies, so a 404 cannot reveal another organization. The 401 "user not found" is only
  reached with a validly signed token whose user no longer exists, so it does not help anyone
  guess emails. Business errors skip Sentry, so normal not-found cases stop being reported as
  crashes. No secrets or personal data in the diff.
- **Design and comments:** the existing localized keys are reused (`UserNotFound`,
  `OrganizationNotFound`, `OrganizationSettingsNotFound`), and all three exist in English and
  Portuguese. No new comments, no `TODO`s.
- **Commit:** a single Conventional Commit, `fix(organization): ...`, lowercase, imperative,
  under 72 characters, with a body that explains why. No Scenarios line is needed for a small
  change.
- **Backlog:** the resolved bug `backlog/bugs/2026-10-15-organization-not-found-returns-500.md`
  was deleted in the same commit, and no other file refers to it.

## Check before merging
- **401 for a deleted user** (`OrganizationService.cs:50`, `:224`): this changes how clients see
  that case. Before, they got a 500. Now they get a 401 with "User not found", which a frontend
  may treat as "sign out". That seems right, but confirm it is what the client expects. Note that
  a token with an unreadable user ID still returns an empty 401 body. That is already tracked in
  `backlog/improvements/2026-10-15-empty-401-body-on-invalid-user-id.md`.
- **404 for missing settings** (`OrganizationService.cs:195`): every organization is created
  with settings in the same transaction, so this case should only appear if the data is broken.
  Before, broken data showed up in Sentry. Now it is only a warning log. Accept this, or watch
  the logs for it.

## Pull request description

**fix(organization): return 404 or 401 instead of 500 for missing records**

### Summary
When a user, an organization or its settings could not be found, `OrganizationService` threw a
generic error. The client got a 500 "unexpected error" without the translated message, and Sentry
logged it as a crash. These cases now return a normal business error: 404 for a missing
organization or settings, and 401 for a signed-in user who no longer exists. The client gets the
translated message.

### Changes
- **Organization:** `CreateOrganizationAsync` and `GetOrganizationsAsync` return 401 when the
  user does not exist. `EditOrganizationAsync` returns 404 when the organization or its settings
  do not exist.
- **Tests:** added `OrganizationServiceNotFoundTests` (4 tests: two for a missing user, one for a
  missing organization, one for missing settings).
- **Backlog:** removed the resolved bug
  `backlog/bugs/2026-10-15-organization-not-found-returns-500.md`.

### Risk and flags
Low risk. No migration, no new package, no endpoint or role change, and no Shared contract change.
Client-visible change: these cases now return 404 or 401 instead of 500.

### Verification
`dotnet build RestaurantManagement.sln` passes. `dotnet test`: 687 passed, 0 failed.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
