# Review: Server messages in French (fr and fr-CA)

**Verdict:** Ready to merge
**Build:** pass (0 errors, 6 warnings, 1 of them in a changed file)   **Tests:** full suite runs in CI (Continuous Integration) — merge only when it is green

This is the third review of branch `feat/server-messages-in-french-canada`. It covers 5 commits on top of `origin/development`. The new ones are `1ab8571` (one list of supported languages) and `3c0bf1e` (the previous review file only).
It is a small change (`/small`), so there is no spec or plan folder and no Completeness check. The user approved going over the size limit.
No packages were added. `python3 .claude/scripts/check-security-matrices.py` passes (Shared 155 named tests).
The implementer reports a local full-suite run: 124/0, 217/0, 68/0, 419/0, 504/0. This review did not run tests.

## Earlier Watch out 1 (two language lists): resolved
- `RegionalCultureProvider.cs:10-18`: `SupportedCultures` is now the only list. `Program.cs:53` reads it, and the old inline list is gone.
- `RegionalCultureProvider.cs:21-24`: the base languages (`pt`, `fr`) come from that list. Static fields start in the order they are written, so `SupportedCultures` is ready before `LanguagesWithCatalog` reads it.
- `RegionalCultureProviderTests.cs`: 2 new tests, tagged SH19 and named in the SH19 row (`shared-matrix.md:71`). The folder `Shared.Tests/Localization/` is allowed.
- The behaviour is the same as before: the 6 cultures and the `pt`/`fr` mapping did not change.

## Is `SupportedCultures` a "new Shared contract" (high risk)? No
- Architecture skill, section 2: a Shared contract is an interface that the owning module exposes to other modules (for example `IOrganizationLookup`). It returns DTOs (Data Transfer Objects: simple data classes). The clean-code skill, section 5, also calls the public interfaces in Shared "cross-module contracts".
- `SupportedCultures` is a static setting on a class that was already public in Shared (`CustomPortugueseCultureProvider` before this branch). Only the Web host reads it. No module uses it, and it is not an interface.
- The implementer's view is right: the risk is low, and `plan.md` would not need a Flag for it.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `src/RestaurantManagement.Web/Program.cs:7` | `using System.Globalization;` is no longer used. The inline `CultureInfo` list was the last thing that needed it. Clean-code skill, section 6: remove the unused code your change created. | Delete the line. |
| 2 | `tests/RestaurantManagement.Shared.Tests/Localization/RegionalCultureProviderTests.cs:51` | New build warning CS8620: `picked` is a `List<string?>`, but `ShouldBe` expects `IEnumerable<string>`. | Declare `picked` as `List<string>` and add `!` or a null check on the result. Or compare with `picked.ShouldBe(baseLanguages.Cast<string?>(), …)`. |
| 3 | `tests/RestaurantManagement.Shared.Tests/Localization/RegionalCultureProviderTests.cs:33-52` | The name says "EveryCatalogLanguage", but the test never looks at a catalog. It copies the production filter (`!= "en" && !Contains('-')`), so it cannot find a gap between the list and the catalog files. A new `SharedResource.es.resx` without `es` in the list would still pass. | Check the list against the real catalogs. List the `SharedResource.*.resx` cultures, or the satellite assemblies, and assert that each one is in `SupportedCultures`. Or assert a fixed `["pt", "fr"]`. |
| 4 | `src/RestaurantManagement.Shared/Utils/Localization/RegionalCultureProvider.cs:10` | `public static readonly CultureInfo[]` is a public array that any code can change: `SupportedCultures[4] = …` would change both the provider and the middleware. | Make it `IReadOnlyList<CultureInfo>`, and pass `SupportedCultures.ToList()` in `Program.cs:53`. |
| 5 | `RegionalCultureProvider.cs:22`, `Program.cs:57`, `RegionalCultureProviderTests.cs:39` | The default language `"en"` is written in 3 places. Clean-code skill, section 4: no magic strings. | Add `public const string DefaultLanguage = "en";` next to the list, and use it in all 3 places. |

## Check before merging
- **One shared list** (`RegionalCultureProvider.SupportedCultures`). Every request now uses one list for both the middleware's supported cultures and the provider's base languages. A mistake in that list changes the language of every response. The 6 values are the same as before.
- **Culture provider** (`RegionalCultureProvider.cs:26-40`). Every request with an `Accept-Language` header goes through it. Any `pt*` or `fr*` value maps to its base language. Only the first entry counts (agreed with the user). `ValidationTitleEndpointTests` checks `fr` and `fr-CA` through the real pipeline.
- **Commit layout.** The file rename is in docs commit `c06bc8a`, and its content change is in `5a562a5`. Revert or cherry-pick them together, or merge the branch as a whole.

## Pull request description

**Server messages in French for fr and fr-CA**

French-speaking users in Canada saw every server message in English. The API now answers in French when the request language (`Accept-Language`) is `fr`, `fr-CA` or any other `fr-*` variant. This works the same way as `pt-*`, which maps to Portuguese.

**Shared**
- New catalog `SharedResource.fr.resx` with all 73 messages in Canadian French ("courriel", "établissement"). A native speaker still has to review them: see `backlog/improvements/2026-10-23-review-french-canada-translations.md`.
- `CustomPortugueseCultureProvider` is renamed to `RegionalCultureProvider` (file `RegionalCultureProvider.cs`). Any `pt-*` or `fr-*` header maps to its base language.
- `RegionalCultureProvider.SupportedCultures` is now the only list of supported languages. The host and the provider both read it, so adding a language means changing one list.
- Tests: the French catalog holds the same keys as English, each with text. The invitation (SH44) and role-name (SH46) messages are tested in French. `fr-CA` falls back to French and `de` falls back to English. New `RegionalCultureProviderTests` check that each regional variant maps to a supported base language.
- Security matrix: SH19, SH44 and SH46 now include French.

**Web**
- `fr` and `fr-CA` are added to the supported request cultures, read from the shared list.
- The validation title is checked in `fr` and `fr-CA` (W22).

**Risk:** low. No migration, no new package, no endpoint change and no new Shared contract. `SupportedCultures` is a setting that only the host reads, not a cross-module interface. Other languages behave as before: `de` and unknown languages still get English. Only the first language in `Accept-Language` counts, as before.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
