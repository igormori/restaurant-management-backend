# Review: Rename the PostgreSQL test fixture to PostgresDatabaseFixture

**Verdict:** Ready to merge
**Build:** pass (0 warnings, 0 errors)   **Tests:** 800 passed, 0 failed
(Shared 152, Organization 123, Menu 47, Identity 187, Web 291)

Scope: branch `feat/generic-postgres-test-fixture-name` against `development` (merge base 0a916a1),
one commit 7c4f8db, 24 files. Small-change path, so there is no spec or plan and the Completeness
check does not apply. The user approved going over the 5-file small-change limit for this
mechanical rename, and approved keeping the `dotnet format` change in `OrganizationServiceAuditTests.cs`.

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `.claude/skills/testing/SKILL.md:44` | The backlog item's reason was "harder to find". The testing skill's "Existing fixtures" table still has placeholders (`<fixture class>`, `<path>`), so agents still won't find the new fixture there. | In a later docs change, fill the integration-test row with `PostgresDatabaseFixture` / `tests/RestaurantManagement.Web.Tests/PostgresDatabaseFixture.cs` (and `TenantApiFactory.CreateAsync` for endpoint tests). |
| 2 | `docs/plans/shared/2026-10-15-restrict-cascade-deletes/batches/B1.md`, `B2.md`; `docs/plans/identity/2026-10-15-hash-verification-codes/batches/B3.md`; `docs/plans/shared/2026-10-15-tenant-query-filters-and-location-access/scenarios.md` | These still use the old name `AuditFieldsPostgresFixture`. They are historical plan records, so leaving them is fine. A future planner copying from them would only get a compile error. | No change needed. If you like, mention the rename in the pull request so readers can map the old name. |

## Checks
- **Tests (no weakening):** each of the 22 test-file diffs only swaps the type name in
  `IClassFixture<...>`, the field type and the constructor parameter. No assertion, attribute
  or test method was changed, skipped or removed. `OrganizationServiceAuditTests.cs:33-39` is
  only re-indented (approved).
- **Structure:** `PostgresDatabaseFixture.cs` is new and sits in the existing
  `tests/RestaurantManagement.Web.Tests/` folder, where the testing skill puts Testcontainers
  PostgreSQL fixtures. No new folders, no untracked files.
- **Design:** the fixture has one job (one container, one fresh database per call) and is 28
  lines. The class body is the same as before. The only behaviour change is the database
  name prefix, `audit_` to `test_` (`PostgresDatabaseFixture.cs:20`).
- **Security:** `CREATE DATABASE {databaseName}` builds SQL from a GUID (globally unique
  identifier) the fixture creates itself, never from user input. No secrets: the connection
  string comes from the container at runtime.
- **Scope:** no `src/` changes, no migration, no package, no endpoint, no Shared contract.
  No references to `AuditFieldsPostgresFixture` remain in `src/` or `tests/`.
- **Backlog:** the item `backlog/tech-debt/2026-10-15-generic-postgres-test-fixture-name.md`
  is deleted because this change resolves it (fixture renamed and moved to its own file).
- **Commit:** `refactor(web): ...`, 70 characters, follows Conventional Commits. The body
  explains why and mentions the format change. No scenario numbers, which is expected
  without a spec.

## Check before merging
- `PostgresDatabaseFixture.cs:20`: the per-test database prefix changed from `audit_` to
  `test_`. Nothing in the tests depends on the prefix (checked by search), but it is the only
  change that is not a pure rename.
- `OrganizationServiceAuditTests.cs:33-39`: unrelated whitespace change from `dotnet format`
  in a different test project. You approved it; it adds noise to the diff of a rename pull request.

## Pull request description
**Rename the shared PostgreSQL test fixture to `PostgresDatabaseFixture`**

Every migration, constraint and endpoint test in Web.Tests uses the same Testcontainers
PostgreSQL fixture. It was called `AuditFieldsPostgresFixture` and lived inside
`AuditFieldsMigrationTests.cs`, although it has nothing to do with audit fields. This change
renames it and moves it to its own file.

What changed
- **Web.Tests:** new `PostgresDatabaseFixture.cs` (same behaviour; per-test databases are
  now named `test_<guid>` instead of `audit_<guid>`). 21 test classes and
  `TenantApiFactory.CreateAsync` now use the new name. No test logic or assertions changed.
- **Organization.Tests:** whitespace-only `dotnet format` fix in `OrganizationServiceAuditTests.cs`.
- **Backlog:** resolves and removes `backlog/tech-debt/2026-10-15-generic-postgres-test-fixture-name.md`.

Risk: low. Test code only; no production code, migrations, packages or contracts.
Build clean; 800 tests passed, 0 failed.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
