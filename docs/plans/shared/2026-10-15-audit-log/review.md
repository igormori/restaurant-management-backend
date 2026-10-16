# Review: Audit Log

**Verdict:** Ready — the only Fix first item is resolved: the user removed the `Program.cs` line limit from the structure hook and the clean-code skill.
**Build:** pass   **Tests:** 733 passed, 0 failed

Terms: DoD (Definition of Done: the checks a task must pass to count as finished). DTO (Data Transfer Object: a plain request or response shape). EF Core (Entity Framework Core: the database library). IP (Internet Protocol) address: the caller's network address.

Second review. Since the first review, there is one new commit, `bc7716c`. It adds `AuditLogServiceCollectionExtensions.AddAuditLog` in `Shared/Services/AuditLog/` and records it in the B1 Delivered note.

Checked and clean:
- Tests: none edited since baseline `6d076c7` (`git diff 6d076c7..HEAD -- tests/` is empty). 733 tests passed: Shared 143, Organization 123, Menu 47, Identity 148, Web 272.
- Completeness: all 4 tasks are ticked, every Delivered note is written and the Overall DoD is ticked. Outcomes #2, #3 and #4 are delivered. #1 is deferred by a user decision (`backlog/features/2026-10-16-member-role-change.md`).
- Structure: the new extension file is in an existing `Services/AuditLog/` folder. There are no new folders. The file is recorded in the B1 Delivered note, as the first review asked.
- Security: the organization comes only from the token (tenant filter plus interceptor stamping). `AuditLogQuery` has no organization field. There is a cross-tenant test, and every disallowed role has a 403 test. Page size is capped at 100. No raw SQL and no `IgnoreQueryFilters`.
- Secrets: none in the diff. Packages: no new package since the first review. The first review ran `dotnet list package --vulnerable` and found nothing.

## Fix first
| # | File:line | Problem | Change |
|---|---|---|---|
| 1 | `src/RestaurantManagement.Web/Program.cs:182-183` | `wc -l` reports 300 lines, but `.claude/hooks/enforce-structure.py:104` counts `content.count("\n") + 1`. The file ends with a newline, so the hook counts 301, which is over `PROGRAM_MAX_LINES = 300`. The next agent edit to `Program.cs` is blocked, even an edit that adds no lines. This is the problem the first review raised, and it is still there. | Add a local function, for example `void UseAppDatabase(IServiceProvider sp, DbContextOptionsBuilder o) => o.UseNpgsql(connectionString).UseSnakeCaseNamingConvention().AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());`. Then pass it to the four identical setups at lines 170-183 (`AddDbContext<IdentityDbContext>(UseAppDatabase)`, and the same for Menu, Organization and `AddAuditLog(UseAppDatabase)`). That saves about 8 lines. Check with `python3 -c "print(open('src/RestaurantManagement.Web/Program.cs').read().count('\n')+1)"`, which must print 300 or less. **Resolved:** the user decided `Program.cs` has no line limit; the hook and clean-code skill now exempt it, so no code change is needed. |

## Watch out
| # | File:line | Problem | Suggestion |
|---|---|---|---|
| 1 | `.claude/hooks/enforce-structure.py:104` | The hook counts one more line than `wc -l` and editors for any file that ends with a newline. Every limit (200 and 300) is effectively one line lower than the skill says. | Write a `proposed` tech-debt backlog item: count with `len(new_content.splitlines())`, or document the rule in the clean-code skill. |
| 2 | `src/RestaurantManagement.Modules.Organization/Services/LocationService.cs:139` | This branch takes the file from 195 to 231 lines, over the 200-line limit. `OrganizationService.cs` goes from 258 to 297. There is still no backlog item for this. | Write a `proposed` tech-debt backlog item to split both services by responsibility (for example, move status change recording into its own class). |
| 3 | `LocationService.cs:149`, `OrganizationService.cs:240` | The magic strings `"Location"` and `"OrganizationSettings"` are used as the changed item type. | Use named constants, for example in `AuditActions` or as a private `const` in each service. |
| 4 | `src/RestaurantManagement.Shared/Services/AuditLog/AuditLogReader.cs:43` | `(page - 1) * pageSize` overflows `int` when `page` is very large. The skip becomes negative and the request returns a 500, not a 400. | Cap `page` in `AuditLogQuery` validation, or compute the skip as `long` and reject values out of range. |
| 5 | `src/RestaurantManagement.Shared/Data/AuditLogDbContext.cs:37` | Append-only is enforced only inside `SaveChanges`. `ExecuteUpdate`, `ExecuteDelete` or raw SQL would bypass it. This is acceptable for Outcome #4 ("no feature exists"). | Add a database-level guard (a trigger, or revoking UPDATE and DELETE) before Phase 1 adds money actions. Record this in the outbox backlog item. |
| 6 | commit `6d076c7` | The baseline commit body says `Scenarios: 1–N`, not `1–25`. The B1 Delivered note records this. | Fix it if you squash or rebase. Otherwise leave it. |
| 7 | `backlog/features/audit-log.md` | This older item lists "role change", which overlaps with this feature and with the new member role change item. | Next time you sort the backlog, cut it down to auth events only. |

## Check before merging
- **New Shared DbContext and migration (`AuditLogDbContext`, `20261016054916_AddAuditLogEntries`):** these are the first ones in Shared. The deploy steps that apply migrations must include this context. If they don't, every settings or location edit logs an error and records nothing, because recording failures are swallowed.
- **Recording errors are swallowed (`OrganizationService.cs:230`, `LocationService.cs:139`):** the user chose this, and the outbox item tracks it. Make sure those error logs reach Sentry, so gaps in the audit log get noticed.
- **First endpoint behind a permission policy (`AuditLogController.cs`, `reports.view`):** it uses `[Authorize(Policy = ...)]`, but the security skill shows `Roles = ...`. The plan and the architecture skill approve policies. Confirm this is the direction you want.

## Pull request description
**Audit log (Phase 0, Task 8)**

Adds a permanent, append-only audit log that authorized users can read for their own organization. The first actions recorded are settings changes and location status changes.

**Shared**
- New `audit_log_entries` table with its own `AuditLogDbContext` and migration, indexed on (organization, time). The DbContext refuses to save a changed or removed entry.
- New cross-module contract `IAuditLog` (record only). The organization, user and time are stamped from the token. The IP address comes from the request.
- New `GET /api/audit-log` endpoint behind the `reports.view` permission. It filters by date (UTC, inclusive) and by action, and returns the newest entries first. The page size defaults to 50, with a maximum of 100. Two new localized messages (English and Portuguese).
- New `AddAuditLog` extension method that registers the context, the writer and the reader.
- Shared now references the Npgsql EF Core provider 9.0.3, the same version the modules use.

**Organization**
- Editing any of the three organization settings records one `settings.changed` entry, with the before and after values as JSON.
- Changing a location's status (by editing or by closing it) records one `location.status_changed` entry.
- Each entry is written in its own save after the change. If that save fails, the change stays and an error is logged.

**Spec outcomes**
- #2 Each organization sees only its own entries: delivered.
- #3 Users without reports.view get a 403: delivered.
- #4 Entries cannot be changed or deleted: delivered.
- #1 A role change is recorded: can't be checked yet, because no role change feature exists (`backlog/features/2026-10-16-member-role-change.md`).

**Risk and flags (from plan.md):** High. A new table, the first DbContext and migration in Shared, a new Shared contract, a new endpoint behind a permission policy, and the provider package added to Shared. No public endpoint, no role or auth change, no raw SQL.

**Follow-ups:** `backlog/improvements/2026-10-16-audit-entries-through-outbox.md` must be done before Phase 1 logs cancellations, discounts or cash closings. This branch also adds a domain rule: changes to money, roles, settings or status are audited.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
