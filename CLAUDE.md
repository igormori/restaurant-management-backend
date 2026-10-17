# RestaurantManagement

.NET 9 modular monolith (Identity, Menu, Organization, Shared, Web) on PostgreSQL. Multi-tenant:
data belongs to an organization. Roles: Owner, Admin, Manager, Cashier, Waiter, Kitchen (access is checked by permission). Launch markets: Brazil, Canada and
the USA. Each organization chooses its currency (BRL: Brazilian real, USD: US dollar, CAD:
Canadian dollar) and each location its time zone; never assume a country default.

**Not launched yet: the database holds no real data.** Tables, columns and migrations may be
changed freely, with no backfill or data preservation. Remove this paragraph at launch, once
`backlog/pre-production/` is empty.

## Skills
Detailed rules live in skills. Read the ones your role needs before starting:

| Skill | Rules | Read by |
|---|---|---|
| `.claude/skills/architecture/SKILL.md` | Modules, cross-module access, folders, layering | Planner, implementer, reviewers |
| `.claude/skills/testing/SKILL.md` | Test tools, test projects, naming, structure | Planner, tester, test-reviewer |
| `.claude/skills/security/SKILL.md` | Tenancy, authorization, input, secrets | Tester, implementer, reviewers |
| `.claude/skills/clean-code/SKILL.md` | Code style and change discipline | Implementer, reviewers |
| `.claude/skills/ef-migration/SKILL.md` | Creating migrations | Implementer |
| `.claude/skills/domain/SKILL.md` | Product, glossary, modules, roles, business rules | Spec writer |

Also read the `CLAUDE.md` inside every module you touch.
The spec writer reads only the domain skill: specs state business rules only (who may do what,
which organization's data), never technical rules.

## Commands
- Build: `dotnet build RestaurantManagement.sln`
- Test: `dotnet test`
- Format: `dotnet format`
- Run: `dotnet run --project src/RestaurantManagement.Web` (Swagger at /swagger)
- Vulnerable packages: `dotnet list package --vulnerable`
- Build and tests must pass before a task is done
- Never commit secrets (connection strings, JWT (JSON Web Token) key)

## Commits
Conventional Commits, enforced by `.githooks/commit-msg`
(enable once per clone: `git config core.hooksPath .githooks`):

```
<type>(<scope>): <summary>

<body: why, not what>

Scenarios: 1–3
```

- Types: `feat`, `fix`, `test`, `refactor`, `perf`, `docs`, `ci`, `build`, `chore`, `revert`
- Scope: the module or area, lowercase: `identity`, `menu`, `organization`, `shared`, `web`,
  `deps`, `infra`, `claude`. A new module adds its scope here and to the hook
- Summary: lowercase, imperative, no trailing period, 72 characters max for the whole line;
  says what changed and its effect, e.g.
  `fix(identity): return a generic 401 for locked accounts`
- Branches start from `development`, and pull requests always target `development`, never `main`
- Never push to `development` or `main`: `.githooks/pre-push` rejects it. A branch created from
  `origin/development` must not track it (`git switch --no-track -c ...`)
- Body only when the why isn't obvious. Scenario numbers and ticket IDs (`Refs: ABC-123`) go
  in the body, never in the summary

## How to work
- Assume, don't ask, unless a wrong guess is expensive. Record every assumption in your
  output. Ask (or mark `[Blocking]` in a subagent) only for roles, tenancy, money, data loss,
  or a requirement that could mean two different features
- Small slices: one spec is one behaviour, reviewable in about 5 minutes. Split when a plan has
  more than 10 files or more than 3 open decisions. Ship shared infrastructure and data
  backfills as their own slices, before the features that use them

## Security matrices
Each module's rules (who may do what, with which organization's data, limits, refusals) and the
tests that prove them live in `docs/security/<module>-matrix.md`. Each matrix's "Rules for this
file" section is the full rule set. Row IDs are unique across matrices.

| Module | Matrix | Row IDs | Unit tests | Endpoint and database tests |
|---|---|---|---|---|
| Identity | `identity-matrix.md` | R V RS L F S O P C T D | `Modules.Identity.Tests/` | `Web.Tests/Identity/` |
| Web | `web-matrix.md` | W, WD | — | `Web.Tests/Web/` |
| Menu | `menu-matrix.md` (not yet) | M, MD | `Modules.Menu.Tests/` | `Web.Tests/Menu/` |
| Organization | `organization-matrix.md` (not yet) | OR, ORD | `Modules.Organization.Tests/` | `Web.Tests/Organization/` |
| Shared | `shared-matrix.md` (not yet) | SH, SHD | `Shared.Tests/` | `Web.Tests/Shared/` |

For a module with a matrix:
- Any change to the module updates its matrix in the same commit: new rows for new behaviour,
  changed rows for changed behaviour, and the test that covers each
- A new feature adds its security cases as ❌ rows before it is built (tester, step 3). They
  turn ✅ only when the named test passes
- No row is ✅ without a named test, and every test in the module's folders is named in its
  matrix. A test may serve rows in several matrices
- New tests go in the module's folders in the table above
- Every named test carries `[Trait("Rule", "<row ID>")]` for each row naming it, and its key
  assertions say the rule in plain words (testing skill, "Writing tests")
- `python3 .claude/scripts/check-security-matrices.py` checks all of this, in CI too. Run it
  before committing a change to a module; `--fix-tags` writes each test's Rule tags from the
  matrices
- In CI, `.claude/scripts/report-test-results.py` shows every failed test with its rule ID, case
  and expected result on the run page and the pull request, and
  `.claude/scripts/report-coverage.py` shows which code of each module no test runs
- The pr-reviewer flags a change that leaves its module's matrix behind as Fix first

## Tenant data filtering
- Tenant data is filtered by organization automatically (EF Core global query filters). The
  organization comes only from the token's `org_id` claim, never from the URL or request body.
- `IgnoreQueryFilters` is allowed in `src/` only in the places below. Adding a place requires
  updating this list in the same change. (Test files may use it to read back stored rows.)
  - `OrganizationService.CreateOrganizationAsync`: trial check across all of the user's
    memberships, which may have no token organization yet
  - `OrganizationService.GetOrganizationsAsync`: plan details for every one of the user's
    memberships

## Workflow
Test-first, with three human gates (⏸). Reviewer agents run automatically between steps and only
stop the flow when something is wrong.

- `/spec <requirement>` runs step 1. `/plan <module>/<spec-name>` runs step 2 and stops at Gate 1.
- `/test <module>/<spec-name>` runs step 3 and stops at Gate 2.
- `/implement <module>/<spec-name> [branch or ticket]` runs steps 4–5 and stops at Gate 3.
- Other commands: `/review`, `/fixbug`.

1. Spec: spec-writer writes `docs/specs/<module>/<YYYY-MM-DD>-<feature>.md` (`shared/` when it
   spans modules). Stops only if it has `[Blocking]` questions.
2. Plan: planner writes the folder `docs/plans/<module>/<spec-name>/`:
   - `plan.md`: Risk, tasks, batches, Flags, overall Definition of Done and the task Checklist.
     The only file with checkboxes and Status
   - `batches/B<n>.md`: each task's details (context, files, DoD, dependencies), read only by
     the tester and implementer

   ⏸ Gate 1: the solution architect approves `plan.md`, including Risk and Flags.
3. Tests: tester writes `scenarios.md`, stubs and failing tests for **all** batches, plus an
   Implementation guide per task in the batch files; test-reviewer checks them, and the tester
   fixes its findings once.
   ⏸ Gate 2: the user approves `scenarios.md` (test coverage).
4. Implement: implementer commits the baseline (spec, plan, stubs, tests), then one task per
   commit with the full suite green, following each task's Implementation guide. After each
   task it writes the task's Delivered note in its batch file and ticks the task in the
   `plan.md` Checklist. Batches run in order; a batch starts only when every task it depends
   on is ticked.
5. Review: pr-reviewer reviews the branch.
   ⏸ Gate 3: the user reads the summary and review, then pushes and merges.

High risk (flagged in `plan.md`; the solution architect decides what to do at Gate 1):
new Shared contract, auth or role changes, new module, migration on a table that has data or
with a backfill. Adding a package not already in the dependency graph is high risk. Pinning an
existing transitive package to a patched version is low risk.

After Gate 2, test files are read-only: never edit, delete, skip or weaken a test. If a test
looks wrong, stop and say why.

### Backlog
Rules in `.claude/skills/backlog/SKILL.md`, read only when about to write an item. In short: a
finding that breaks the current task blocks it; a tiny fix in a file already being changed is made
inline; anything else becomes a backlog item written by the agent that found it, after searching
for duplicates. Agents write items as `proposed`; only the user accepts or merges them.
When work fully solves an item, agents commit the fix and delete the item file, but never commit
the deletion: the user checks and commits it. Agents never push.

### Terminal output
The reviewer reads the files in `docs/`, not the terminal. When a step finishes, show the
agent's final message exactly as returned. Never summarize, rebuild or add sections from the
spec, plan, batch or scenarios files.