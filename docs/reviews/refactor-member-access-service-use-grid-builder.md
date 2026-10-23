# Review: Build member access grid through the grid builder

**Verdict:** Ready to merge
**Build:** pass (0 errors, 5 existing warnings in test projects)   **Tests:** full suite runs in CI (Continuous Integration) — merge only when it is green

This was a small change (`/small`) with no spec or plan folder, so the Completeness check is skipped.
It was reviewed against the architecture, clean-code, security and testing skills, the Identity
`CLAUDE.md`, and the backlog item `backlog/tech-debt/2026-10-20-member-access-service-use-grid-builder.md`.

## Findings
No Fix first and no Watch out findings.

What was checked:
- **Behaviour unchanged:** the removed `RequireRolesAsync` and `MergeLevelsAsync` match
  `MemberGridBuilder.BuildAsync` line for line: the same distinct role lookup filtered by
  organization, the same `RoleNotFound` 404, the same higher-level-per-cell merge with changed
  levels on top, and None cells dropped. The order of checks is the same (member, location, roles,
  delegation rules), so the error a caller gets for a bad request does not change.
- **Tenancy:** the builder filters roles and role levels by the `organizationId` the service gets
  from the token (`RequireOrganizationId`). Nothing is read from the URL or body.
- **Scope:** 2 files changed, both named in the backlog item. No migration, package, endpoint,
  Shared contract or new file. `IMemberGridBuilder` was already registered as scoped
  (`Program.cs:293`), so it shares the request's `IdentityDbContext`.
- **Tests:** only the test helper `MemberAccessTestSupport.CreateService` changed. It now passes a
  real `MemberGridBuilder` built on the same context and localizer, which is what the backlog item
  asked for. This makes the tests stronger: T37 to T42 now run through the real merge. No test
  method was edited, deleted or skipped.
- **Security matrix:** `python3 .claude/scripts/check-security-matrices.py` passes (Identity 474
  named tests). Rows T37 to T42 in `docs/security/identity-matrix.md` describe what happens, not
  how it is built, so they still match the code. No row needed a change.
- **Clean code:** the duplicate merge and the unused field the backlog item mentions are both gone.
  `MemberAccessService.cs` is 239 lines, `SetAccessAsync` is under 30 lines, and no new comments
  were added. The helper signatures moved to `IReadOnlyDictionary` and `IReadOnlyList` to match
  `BuiltGrid`.
- **Commits:** one commit, `refactor(identity): build member access grid through the grid builder`.
  It follows Conventional Commits, its body says why, and it is not amended.
- **Secrets:** none in the diff.

## Check before merging
- **`SetAccessAsync` now depends on `MemberGridBuilder`.** Any later change to the builder
  (for example for invitations) also changes how a member's access is edited. That is the point of
  this change, but the people reviewing a future builder change need to know it affects both.
  T37 to T42 now protect the edit flow.
- **The backlog item deletion is not committed.**
  `backlog/tech-debt/2026-10-20-member-access-service-use-grid-builder.md` is deleted only in the
  working tree. Check the item's three acceptance criteria (all met by this commit), then commit the
  deletion yourself before pushing, so the item does not stay open on `development`.
- **CI must be green.** The tests were not run here. The helper change affects every test in
  `MemberAccessServiceTests` and `RoleServiceEditTests` that calls `CreateService`.

## Pull request description

### Summary
Editing a member's access at a location now builds the final grid through the shared
`MemberGridBuilder`, the same code invitations use. Before, `MemberAccessService` had its own copy
of the role merge (higher level per cell wins, changed levels on top), so the rule lived in two
places. Behaviour does not change.

### Changes
- **Identity:** `MemberAccessService.SetAccessAsync` calls `IMemberGridBuilder.BuildAsync` and
  stores the grid's levels and roles. The private `RequireRolesAsync` and `MergeLevelsAsync` are
  removed.
- **Tests:** `MemberAccessTestSupport.CreateService` uses the real `MemberGridBuilder` instead of a
  substitute, so tests T37 to T42 cover the shared merge.

### Risk
Low. This is a refactor inside the Identity module only. No migration, package, endpoint or Shared
contract changes. The security matrix check passes, and no matrix row changed.

Solves `backlog/tech-debt/2026-10-20-member-access-service-use-grid-builder.md`.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
