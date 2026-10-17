# Review: Backlog fixes 2026-10-16 (ASVS #24–#28, Identity missing tests)

**Verdict:** Ready to merge
**Build:** pass   **Tests:** 375 passed, 0 failed (Modules.Identity.Tests 253; Web.Tests, Identity folder classes, 122)

Scope: 5 commits on `backlog-fixes/2026-10-16` since base `4e50ec0` (`security-fixes`). No plan
folder: reviewed against the architecture, testing, security and clean-code skills and `CLAUDE.md`.
ASVS means OWASP Application Security Verification Standard (the checklist the gap report follows).
`python3 .claude/scripts/check-security-matrices.py`: "Security matrices in step with the tests".

| Check | Result |
|---|---|
| One commit per backlog item | Yes: 7fe038a #24, 7794b0e #25, bbcfd2b #26, 2ccab2f #27, a60637c #28. Each adds one matrix row (T21–T25), its test, and removes its backlog line |
| Tests only, no production code | Yes. Only `tests/`, `docs/security/identity-matrix.md` and the backlog item changed |
| Structure | New files in allowed folders (`Modules.Identity.Tests/`, `Web.Tests/Identity/`), Web.Tests namespace as the skill requires; no new folders or projects |
| Security matrix | T21–T25 each name a test that exists, carries `[Trait("Rule", ...)]` and asserts the row's Expected result with a plain-words message |
| Commits | Conventional Commits, scope `identity`, lowercase imperative summaries under 72 characters, `Refs:` to the backlog file in the body |
| Secrets | None. The attacker key in T22 is a test literal, not a real key |
| Packages, migrations, endpoints, Shared contracts | None added |

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | tests/RestaurantManagement.Web.Tests/Identity/AuthEndpointSecretsNotInUrlTests.cs:37 | `EndpointMetadata.OfType<HttpMethodMetadata>().All(...)` is true for an action with no verb metadata, so a future action with only `[Route]` (accepts every verb, including GET) would pass T23 | Require exactly POST: `.SelectMany(m => m.HttpMethods).SequenceEqual(["POST"])`, so an empty list fails |
| 2 | tests/RestaurantManagement.Web.Tests/Identity/AuthEndpointSecretsNotInUrlTests.cs:43 | Only `Query` and `Path` binding sources are refused; a parameter with no `BindingInfo` (possible if `[ApiController]` is ever removed) is silently skipped | Also flag `p.BindingInfo is null`, or assert every parameter binds from `Body`, `Services` or a cookie |
| 3 | tests/RestaurantManagement.Web.Tests/Identity/AccessTokenValidationEndpointTests.cs:154-163 | For `jku` and `x5u` the token is signed by a key that is not configured and the URL is unreachable, so the case is refused for the same reason as the existing wrong-key test; only the `jwk` case truly proves the header is ignored | Acceptable (the JWT library never fetches keys). Optionally note in the T22 row that `jwk` is the decisive case |
| 4 | docs/security/identity-asvs-gap-report.md:75,132,133,155,162 | Gap report rows for #24–#28 still say "Partial"; the backlog acceptance criterion was reworded (backlog/tech-debt/2026-10-16-asvs-small-fixes-and-missing-tests.md:23) to defer this instead of doing it | Mark V6.3.2, V9.1.3, V9.2.1, V11.4.1 and V14.2.1 "Covered" with T24, T22, T21, T25, T23 in a follow-up docs commit |
| 5 | tests/RestaurantManagement.Modules.Identity.Tests/ApprovedHashAlgorithmsTests.cs:65 | One very long line with an inline fully qualified `RestaurantManagement.Shared.Services.Organization.IOrganizationLookup` | Add a `using` and break the constructor call across lines (style only) |

## Check before merging
- **Base branch:** this branch sits on `security-fixes`, which has 79 commits not yet in
  `development`. Merge `security-fixes` first, or this pull request carries all of them.
- **T23 reflection test** (`AuthEndpointSecretsNotInUrlTests`): it is the only guard that no
  Identity endpoint ever takes a secret in the URL; see Watch out 1 and 2 for its blind spots.
- **T24 fresh database test** reads `user_roles` with raw SQL (no tenant filter applies); if the
  table is renamed, the test fails loudly rather than silently, which is the intended behaviour.

## Pull request description

**Title:** test(identity): prove ASVS #24–#28 session and token rules

### Summary
Adds tests and Identity security matrix rows for five rules the code already enforced but no
test proved (OWASP ASVS 5.0 Level 2 gaps #24–#28). No production code changes.

### What changed (Identity)
| Matrix row | Rule | Test |
|---|---|---|
| T21 | An access token used before its `nbf` (not before) time is refused (401) | `AccessTokenValidationEndpointTests.LogoutAll_AccessTokenNotValidForAnotherTenMinutes_ReturnsUnauthorized` |
| T22 | `jku`, `x5u` and `jwk` token headers are ignored; the signing key comes only from configuration | `AccessTokenValidationEndpointTests.LogoutAll_TokenSignedByAttackerNamingItsKeyInTheHeader_ReturnsUnauthorized` |
| T23 | Every Identity endpoint is POST only and reads nothing from the query string or route | `AuthEndpointSecretsNotInUrlTests.AuthEndpoints_AllActions_AcceptOnlyPostWithNoQueryOrRouteParameters` |
| T24 | A database built only from the migrations has no users or role assignments | `FreshDatabaseHasNoUsersTests.Migrate_CleanDatabase_HasNoUsersOrRoles` |
| T25 | Refresh token fingerprints are SHA-256; access tokens are signed with HS256 | `ApprovedHashAlgorithmsTests.IssueAsync_NewSignIn_StoredFingerprintIsSha256OfTheIssuedToken`, `ApprovedHashAlgorithmsTests.LoginAsync_ValidCredentials_AccessTokenIsSignedWithHs256` |

Backlog: `backlog/tech-debt/2026-10-16-asvs-small-fixes-and-missing-tests.md` now lists only
#15 and #18 (code changes still open).

### Risk and flags
Low. Tests and docs only: no migration, package, endpoint or Shared contract change.
Depends on `security-fixes` being merged into `development` first.

### Checks
- `dotnet build RestaurantManagement.sln`: pass
- Identity unit and endpoint tests: 375 passed, 0 failed
- `python3 .claude/scripts/check-security-matrices.py`: in step

### Follow-up
Mark gap report rows V6.3.2, V9.1.3, V9.2.1, V11.4.1 and V14.2.1 as Covered.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
