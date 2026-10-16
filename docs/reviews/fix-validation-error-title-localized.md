# Review: Localize the validation error response title

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 690 passed, 0 failed

Scope reviewed: commit 602664f, `git diff development...HEAD`. The branch is one commit on top of
`development`. This is a small change, so there is no spec, plan or scenarios file. It was checked
against `CLAUDE.md` and the architecture, security, clean-code, testing and backlog skills. The Web
host has no module `CLAUDE.md`.

Test totals per project: Shared 143, Organization 106, Menu 47, Identity 148, Web 246 (3 new).
The build had 0 warnings and 0 errors. No packages were added or updated, so the
vulnerable-package check was not needed.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | backlog/bugs/2026-10-15-validation-error-title-english-only.md (deleted) | The backlog item said the title change "is a response-contract change for all endpoints, so it needs its own decision". The branch made that decision without an explicit sign-off. It is not on the `CLAUDE.md` high-risk list (no Shared contract, auth, role, module, migration or package change), so the small-change path is allowed. The impact is also small: only the text of `title` changes, `type`, `status`, `errors` and `traceId` stay the same, the product is not launched, and no test or doc in the repository depends on the old English text. | Treat Gate 3 as that decision: confirm the title text change on purpose before merging. If any frontend shows or matches `title`, tell that team. |
| 2 | src/RestaurantManagement.Web/Hosting/LocalizedValidationTitleExtensions.cs:9 | The `///` summary is on a Web hosting extension. The clean-code skill allows `///` only on public interfaces in `Shared/`, and none of the other `Hosting/*Extensions.cs` files use it. Most of the text says what the code does. | Replace it with a one-line `//` "why" comment, for example: `// The platform title is always English; clients get the catalog message in their language.` Or remove it. |
| 3 | src/RestaurantManagement.Web/Program.cs:120 | `Program.cs` is now 295 lines. The hook limit is 300. | No change needed now. The next host registration should move existing setup (for example the JWT events block) into a `Hosting/` extension method instead of adding lines. |

## Checks passed
- **Scope:** a 32-line extension in `Web/Hosting/` (an existing folder, next to the other hosting
  extensions), one registration line in `Program.cs`, one new test file, the resolved backlog
  item deleted, and the "Related" link to it removed from
  `2026-10-15-select-organization-missing-id-not-rejected.md` (which is still valid: its field
  messages and required-ID problem are not fixed by this change). No migration, package, endpoint,
  role or Shared contract (a type that other modules depend on) changed.
- **Design:** `PostConfigure<ApiBehaviorOptions>` runs after the platform's own setup, so it wraps
  the default factory and changes only `Title`. Status, field errors and `traceId` are still built
  by the platform. No other code in `src/` sets `InvalidModelStateResponseFactory` or builds
  `ModelState` responses by hand, so every automatic 400 from model validation (including bodies
  that cannot be read) gets the new title. `UseRequestLocalization` runs before `MapControllers`,
  so the localizer uses the request's language. The localizer is a local variable named
  `localizer` and uses a quoted key (`localizer["ValidationFailed"]`), as the clean-code skill
  requires. The key already existed in `SharedResource.resx` and `SharedResource.pt.resx`.
  The method is under 30 lines, nesting is within 2 levels, one responsibility.
- **Tests:** no existing test was edited, deleted, skipped or weakened (`tests/` diff is one
  added file). The new tests are endpoint tests in Web.Tests, use xUnit and Shouldly, follow
  `Method_Scenario_ExpectedResult`, have Arrange/Act/Assert comments and realistic Brazilian
  values. The title theory (en and pt) would fail against the old code, which returned
  "One or more validation errors occurred.", so the bug fix starts with a failing test. The
  second test guards that `status` and `errors` keep their shape.
- **Security:** no change to tenancy, authorization, input binding, data access or logging.
  The title is a fixed catalog string, so it reveals nothing about the request. No secrets,
  tokens or personal data in the diff.
- **Commits:** one commit, Conventional Commits format, scope `web`, lowercase imperative summary
  under 72 characters, body explains why.

## Check before merging
1. **Response title change for every endpoint.** Every automatic validation 400 now returns
   `"Validation failed. Please check the input fields."` (or the Portuguese text) instead of
   `"One or more validation errors occurred."`. This was the decision the backlog item asked for;
   confirm it is wanted.
2. **Wrapping the platform factory.** If someone later replaces `InvalidModelStateResponseFactory`
   in another place (for example with `ConfigureApiBehaviorOptions`), the order of registration
   decides which one wins. The new tests will catch it if the title stops being translated.

## Pull request description
**Localize the validation error response title**

Invalid requests returned the platform's default validation response, whose title was always the
English sentence "One or more validation errors occurred.", even when the client asked for
Portuguese. The title now uses the existing `ValidationFailed` catalog message in the requested
language. The status, the field errors and the rest of the response body are unchanged.

What changed
- Web: new `AddLocalizedValidationTitle()` hosting extension that wraps the platform's
  validation response factory and replaces only the title; registered in `Program.cs`.
- Tests: `ValidationTitleEndpointTests` checks the title in English and Portuguese, and checks that
  `status` and `errors` keep their shape.
- Backlog: removed the resolved item `backlog/bugs/2026-10-15-validation-error-title-english-only.md`.

Risk and flags
- Response contract: the `title` text of every automatic validation (400) response changes. No
  field is added or removed. Not launched yet, so no existing clients are affected.
- No migration, no new package, no new endpoint, no auth or role change, no Shared contract change.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
