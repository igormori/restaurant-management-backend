# Review: Remove Sentry test exception from organization create

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 600 passed, 0 failed

Scope reviewed: commit 39f9fc0, `git diff 16081d2 39f9fc0` (16081d2 is origin/development).
Small-change path: no spec, plan or scenarios; checked against `CLAUDE.md`, the Organization
module `CLAUDE.md`, and the architecture, security, clean-code and testing skills.

Test totals per project: Shared 106, Organization 102, Menu 47, Identity 148, Web 197.
Build had 0 warnings and 0 errors. No packages were added or updated, so the vulnerable-package
check was not needed.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | tests/RestaurantManagement.Modules.Organization.Tests/OrganizationServiceReservedNameTests.cs:13 | The class name says "ReservedName", but the point of the fix is that there are no reserved names. A later reader may think a reserved-name rule exists. | If you touch this file again, rename it to something like `OrganizationServiceFormerSentryHookTests`. Optional: test files are read-only after they are committed, so leave it as is if you prefer. |
| 2 | src/RestaurantManagement.Modules.Organization/Services/OrganizationService.cs:72 | The team no longer has a way to send a test event and check that Sentry error tracking works end to end. | If you still need one, add it as a development-only endpoint or a script, and log it in `backlog/improvements/`. Do not put it back in a production code path. |

## Checks passed
- **Scope:** only the 6-line hook was removed, plus one new test file and the resolved backlog
  file. No migration, package, endpoint or Shared contract (a type that other modules depend
  on) changed.
- **Structure:** the new test file is in the existing Organization test project, next to the
  other `OrganizationService*Tests`, and no new folders were added. It uses Shouldly,
  NSubstitute and the `Method_Scenario_ExpectedResult` naming, as the testing skill requires.
  Using a separate file is reasonable because `OrganizationServiceCreateTests.cs` still uses
  FluentAssertions.
- **Tests:** no existing test was edited, deleted, skipped or weakened. The new test covers
  both letter cases the old hook matched, and it checks both the response and the stored row.
- **Security:** this change fixes a security problem. Before it, any authenticated user could
  trigger a 500 error and a Sentry event whenever they wanted. The fix adds no new query,
  action or request field. There are no secrets or personal data in the diff, and no
  `TEST_SENTRY` reference is left in `src/`.
- **Comments:** the emoji "what" comment was removed along with the code. No `TODO`s were added.
- **Commit:** a single Conventional Commit (`fix(organization): ...`), lowercase, imperative,
  under 72 characters. The body explains why. A Scenarios line is not needed because this
  change has no scenarios.
- **Backlog:** the resolved bug file was deleted in the same commit.

## Check before merging
- The `OrganizationService.CreateOrganizationAsync` change is a pure deletion. The trial check
  above it and the transaction below it are unchanged, so the risk is very low.
- Make sure nobody depends on the `TEST_SENTRY` name to test Sentry alerting, for example in a
  runbook or a staging smoke test. After this merge, that name creates a real organization
  instead of throwing an error.

## Pull request description

**fix(organization): remove Sentry test exception from organization create**

### Summary
Creating an organization named `TEST_SENTRY` (in any letter case) used to throw a deliberate
exception. It was left over from setting up Sentry error tracking. Any user could trigger a
500 error and a Sentry event whenever they wanted, and a restaurant with that name could not be
created. This PR removes the hook.

### Changes
- **Organization:** removed the test exception from `OrganizationService.CreateOrganizationAsync`.
- **Tests:** added `OrganizationServiceReservedNameTests`. It checks that `TEST_SENTRY` and
  `test_sentry` now create an organization normally.
- **Backlog:** removed the resolved bug
  `backlog/bugs/2026-10-15-sentry-test-exception-in-organization-create.md`.

### Risk and flags
Low risk. This is a deletion only. There is no migration, no new package, no endpoint or role
change, and no Shared contract change.

### Verification
`dotnet build RestaurantManagement.sln` passes. `dotnet test`: 600 passed, 0 failed.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
