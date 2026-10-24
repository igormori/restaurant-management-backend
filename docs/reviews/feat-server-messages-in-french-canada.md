# Review: Server messages in French (fr and fr-CA)

**Verdict:** Ready to merge
**Build:** pass (0 errors, 5 warnings, none in changed files)   **Tests:** full suite runs in CI (Continuous Integration) — merge only when it is green

This is the second review of branch `feat/server-messages-in-french-canada`. It has 3 commits on top of `origin/development`: `b404b45`, `c06bc8a` and `5a562a5`.
It is a small change (`/small`), so there is no spec or plan folder and no Completeness check. The user approved going over the size limit.
No packages were added. `python3 .claude/scripts/check-security-matrices.py` passes.

## Earlier findings: all resolved

| Earlier # | Status | Evidence |
|---|---|---|
| Fix first 1 (SH44) | Resolved | `InvitationMessageKeysTests.cs:15-37`: a `fr` case for each of the 7 keys. The summary says "English, Portuguese and French". The test fails if the key is missing or returns the key itself. |
| Fix first 2 (SH46) | Resolved | `RoleMessageKeysTests.cs:15-22`: `fr` cases for `RoleNameAlreadyUsed` and `RoleNameInvalid`. The summary is updated. |
| Watch out 1 (native review) | Resolved | `backlog/improvements/2026-10-23-review-french-canada-translations.md`: status `proposed`, every field filled, no duplicate in `backlog/`. |
| Watch out 2 (`OrganizationRequired`) | Resolved | `SharedResource.fr.resx:133` now reads "…pour enregistrer cet élément." |
| Watch out 3 ("emplacement") | Resolved | "établissement" in all 8 location messages. No "emplacement" is left. The agreement is correct ("un seul établissement", "L'établissement sélectionné"). |
| Watch out 4 (provider list) | Resolved | `RegionalCultureProvider.cs:10` has `static readonly LanguagesWithCatalog`. Line 21 uses `StringComparison.Ordinal`. |
| Watch out 5 (file name) | Resolved | The file is now `RegionalCultureProvider.cs`, which matches the class name. |
| Watch out 6 (test name) | Resolved | `SharedResourceCatalogTests.cs:83` is `..._ReturnsBaseLanguageOrEnglishFallback`. The SH19 row (`shared-matrix.md:71`) uses the new name. |

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `src/RestaurantManagement.Shared/Utils/Localization/RegionalCultureProvider.cs:10`, `src/RestaurantManagement.Web/Program.cs:52-60` | The languages that have a catalog are still listed in two places. A third language needs both lists changed, and no test checks that they match. This was part of the earlier Watch out 4, and the named field makes it easier to find. | Optional, can be done later: give `Program.cs` the cultures from one shared list. |

## Check before merging
- **Culture provider** (`RegionalCultureProvider.cs`). Every request with an `Accept-Language` header goes through it. Any `fr-*` or `pt-*` value maps to its base language. Only the first entry in the header counts (agreed with the user). `ValidationTitleEndpointTests` checks `fr` and `fr-CA` through the real pipeline.
- **Changed test case** (`SharedResourceCatalogTests.cs:79-81`). The old case was `fr`, which returned English. It is now `fr-CA`, which returns French, plus a new `de` case that returns English. This is an intended change of behaviour. The fallback for unsupported languages is still tested.
- **Commit layout.** The file rename is in the docs commit `c06bc8a`, and the content change is in `5a562a5`. This matters only if someone reverts one commit without the other. Merge or squash the branch as a whole.

## Pull request description

**Server messages in French for fr and fr-CA**

French-speaking users in Canada saw every server message in English. The API now answers in French when the request language (`Accept-Language`) is `fr`, `fr-CA` or any other `fr-*` variant. This works the same way as `pt-*`, which maps to Portuguese.

**Shared**
- New catalog `SharedResource.fr.resx` with all 73 messages in Canadian French ("courriel", "établissement"). A native speaker still has to review them: see `backlog/improvements/2026-10-23-review-french-canada-translations.md`.
- `CustomPortugueseCultureProvider` is renamed to `RegionalCultureProvider` (file `RegionalCultureProvider.cs`). Any `pt-*` or `fr-*` header maps to its base language.
- Tests: the French catalog holds the same keys as English, each with text. The invitation (SH44) and role-name (SH46) messages are tested in French. `fr-CA` falls back to French and `de` falls back to English.
- Security matrix: SH19, SH44 and SH46 now include French.

**Web**
- `fr` and `fr-CA` are added to the supported request cultures.
- The validation title is checked in `fr` and `fr-CA` (W22).

**Risk:** low. No migration, no new package, and no change to endpoints or Shared contracts. Other languages behave as before: `de` and unknown languages still get English. Only the first language in `Accept-Language` counts, as before.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
