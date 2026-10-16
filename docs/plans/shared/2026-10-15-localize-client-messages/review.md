# Review: Localize All Client-Facing Messages

**Verdict:** Ready to merge
**Build:** pass (0 warnings, 0 errors)   **Tests:** 683 passed, 0 failed

Compared against `development` (as requested), not `main`. Shared.Tests 143, Organization.Tests 102,
Menu.Tests 47, Identity.Tests 148, Web.Tests 243. No packages added, so `dotnet list package --vulnerable`
was not needed. No test file changed after the baseline commit `92bf231`
(`git diff 92bf231..HEAD -- tests/` is empty). Working tree clean, no untracked files.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `src/RestaurantManagement.Shared/Middleware/ExceptionHandlingMiddleware.cs:52` | `GetRequiredService` runs inside the catch-all. If the localizer cannot be resolved (for example, a host or test that does not register localization), the handler itself throws and the client gets the framework's bare 500 instead of the JSON (JavaScript Object Notation) body. No detail leaks, but the last-resort handler should not be able to fail | Use `GetService<IStringLocalizer<SharedResource>>()` and fall back to the English text when it returns null. **Fixed in `2517620`** |
| 2 | Commits `2501457`, `2e332c6` | Commits: "one commit per task" is not met. T3 and T4 share one commit, and T6, T7 and T8 share another. The `plan.md` Checklist also has no commit hashes next to the ticked tasks | For future plans, make one commit per task. For this branch, add the hashes to the Checklist (T1 `b19cfa2`, T2 `f7e5ca3`, T3–T4 `2501457`, T5 `da11a95`, T6–T8 `2e332c6`, T9 `f756efe`) |
| 3 | `src/RestaurantManagement.Modules.Organization/Models/EditOrganizationRequest.cs:80`, `IanaTimeZoneAttribute.cs:22` | The English fallback text is copied from the catalog as an inline string. If the catalog wording changes, the two copies will drift apart (magic strings) | Acceptable for now, because module unit tests run the validator without services. If more custom validators appear, add one small shared helper that formats a catalog key with a fallback |
| 4 | `src/RestaurantManagement.Modules.Organization/Controllers/LocationController.cs:70` | The invalid-user 401 is now localized only on `api/locations`. `OrganizationController`, `MenuController` and `MenuCategoryController` still return an empty `Unauthorized()`, so the API is inconsistent | Already tracked in `backlog/improvements/2026-10-15-empty-401-body-on-invalid-user-id.md`; no action in this branch |

## Check before merging
- **Error handler shared by every endpoint (T2).** Every unexpected 500 now resolves the localizer
  from request services. Exception detail is still shown only in Development (scenario 5 guards
  this), but see Watch out #1 for the failure path.
- **Auth failure status change on `api/locations` (T8).** A token whose user identifier cannot be
  read now gets 401 with "Unauthorized user." / "Usuário não autorizado!" instead of 500. Valid
  tokens are unchanged. The cross-tenant 404 is still covered by `OrganizationTenantEndpointTests`.
- **`IanaTimeZoneAttribute` now overrides `IsValid(object, ValidationContext)` (T7).** This differs
  from the guide (recorded in the B4 Delivered note). The time-zone rule itself is unchanged, and
  the existing `LocationFieldsTests` pass.

## Pull request description
**Localize all client-facing messages (en and pt)**

Every message the API sends to a client now exists in English and Brazilian Portuguese and follows
the request's `Accept-Language` header. Unsupported languages fall back to English. Before this
change, some errors came back as raw codes (`MenuNotFound`), as fixed English, or as the
framework's default English.

What changed per module:
- **Shared:** 13 new messages added to the catalog (`SharedResource.resx` / `.pt.resx`). The
  unexpected-error 500 message is now translated; exception detail is still shown only in Development.
- **Menu:** the "menu not found" and "menu category not found" 404 responses are translated, and
  every menu and menu-category field error now uses catalog messages.
- **Identity:** login and organization-selection field errors and the registration phone error
  are translated. Login credential failures still return the same generic 401.
- **Organization:** organization create/edit field errors (colors, currency, ranges, 2 decimals,
  HH:mm cutoff, logo URL, name, description) and location create/edit field errors (type, code,
  time zone, required, length) are translated. On `api/locations`, an unreadable user identifier
  in the token now returns a localized **401** instead of a 500.
- **Tests:** new endpoint tests in en and pt for every case. A catalog completeness check fails if
  en and pt hold different keys, if a value is empty, if a request-model validation rule does not
  use a catalog key, or if code looks up a key that does not exist.

Spec Outcomes delivered: #1 to #9.

Risk: Medium. Shared code used by every endpoint changes (message catalog, error handler). No data,
schema, role or permission change. No migration, no new package.

Flags from plan.md:
- T2: the error handler is shared by all endpoints; exception detail stays hidden outside Development.
- T8: the auth failure response on `api/locations` changes from 500 to 401; no permission change.

Known follow-ups (backlog): organization selection with an empty ID is not rejected
(`backlog/bugs/2026-10-15-select-organization-missing-id-not-rejected.md`). Other controllers still
return an empty 401 for an unreadable user identifier
(`backlog/improvements/2026-10-15-empty-401-body-on-invalid-user-id.md`).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
