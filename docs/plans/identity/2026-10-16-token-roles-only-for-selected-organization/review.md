# Review: Sign-in token carries roles only for the organization in use

**Verdict:** Fix first — the code is correct and fully tested; two one-line documentation gaps (a matrix row whose test does not check all of its Expected result, and a task tick without its commit hash).
**Build:** pass   **Tests:** 986 passed, 0 failed (Shared 159, Organization 130, Menu 63, Identity 261, Web 373)

Security matrix check (`python3 .claude/scripts/check-security-matrices.py`): passes.
No packages added, so the vulnerable-package check was not needed. Tests are unchanged since the baseline commit `0b7e36e`.

## Fix first
| # | File:line | Problem | Change |
|---|---|---|---|
| 1 | docs/security/identity-matrix.md:113 | Row L21 says "No roles in the token; refused with 403", but the test it names checks only the 403. That 403 also happened before the fix (scenario 3 says so), so this row's ✅ does not prove the "no roles" half. | Either reword L21 Expected to "Refused with 403 (no roles in the token: see L14)", or add `SessionServiceOrganizationClaimTests.LoginAsync_UserWithRolesInTwoOrganizations_TokenHasNoRoleClaims` to its Test cell and run `--fix-tags`. Rewording is better because it changes only the matrix and leaves the read-only test files alone. |
| 2 | docs/plans/identity/2026-10-16-token-roles-only-for-selected-organization/plan.md:29 | T1 is ticked without its commit hash. Other plans use the form `(`hash`)`. | Change it to `- [x] T1 — Token roles only for the organization in the token (`58fd04f`)`. |

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | docs/plans/identity/2026-10-16-token-roles-only-for-selected-organization/scenarios.md:8 | Scenarios 1 and 5 say "Waiter", but the unit tests they name seed the role `Staff`. Only the endpoint test uses `Waiter`. | Next time, write the role the test actually uses. It has no effect on behaviour. |
| 2 | backlog/bugs/2026-10-15-token-roles-from-all-organizations.md | The item is deleted in the working tree but the deletion is not committed. The backlog rules expect this: the user checks and commits it. | Commit the deletion yourself before pushing. The fix fully solves the item. |

## Check before merging
- `AccessTokenFactory.Create` (src/RestaurantManagement.Modules.Identity/Services/AccessTokenFactory.cs:35-55) is an auth change. Every access token (the signed proof of identity sent with each request) now gets its organization and its roles from a single computed value. A user with 2 or more organizations now gets **no roles** until they choose one. Check that the frontend calls select-organization before any role-gated screen.
- `MenuController` still uses `[Authorize(Roles = "Owner,Admin")]`. It now refuses multi-organization users who have not chosen an organization at the filter stage, where before the service refused them. The response is still 403, so nothing changes for API clients.
- Single-organization users and users with no roles are unchanged. The existing tests and the new `LoginAsync_UserWithTwoRolesInOneOrganization_TokenCarriesBothRoles` test cover this.

## Pull request description
**fix(identity): token carries roles only of its own organization**

A user with roles in two or more organizations (for example Owner of one restaurant and Waiter in another) used to get an access token, before choosing an organization, that listed the roles of all their organizations without being tied to any of them. Any check that trusted the role name alone would have accepted that token for either restaurant. The token now carries roles only for the organization it names. With no organization chosen, a multi-organization user's token has no organization and no roles.

**What changed**
- Identity: `AccessTokenFactory.Create` works out the token's organization once (the chosen one, else the user's only one, else none) and uses it for both the organization claim and the roles. `SessionService`, the response models and the list of organizations returned at sign-in are unchanged.
- Security matrix (`docs/security/identity-matrix.md`): L14 and F6 changed; L21 and F10 added, all ✅.
- Tests: 3 new unit tests in `SessionServiceOrganizationClaimTests`, and 1 new end-to-end test `TokenRolesEndpointTests` (real sign-in, then the Owner/Admin menu create action returns 403).

**Spec Outcomes delivered**
1. No roles from other organizations in the token: the 2-organization sign-in gives no role and no organization.
2. Role-only checks refuse that token: 403 on menu create.
3. Choosing an organization gives back only that organization's roles (existing select-organization tests).
4. Single-organization users are unaffected.
5. Token refresh follows the same rules: refreshing without a chosen organization gives no roles.

**Risk and flags (from plan.md)**
- Risk: High. This is an auth change to the role claims in the JWT (JSON Web Token). There is no schema, data, package or Shared contract change.
- Fixes and removes backlog item `backlog/bugs/2026-10-15-token-roles-from-all-organizations.md`.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
