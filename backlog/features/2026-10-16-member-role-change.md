# Member role change spec

**Status:** proposed
**Severity:** medium
**Origin:** docs/plans/shared/2026-10-15-audit-log/plan.md · Phase 0 — Task 8
**Date:** 2026-10-16

## Problem
Changing a member's presets and grid is decided in docs/specs/shared/2026-10-20-invite-member-and-edit-access-grid.md. Still missing: making someone an Owner or removing that role (Owner only), and protecting the last Owner so an organization always keeps one.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Services/Members/MemberDelegationRules.cs` — refuses any edit of an Owner's access and has no rule for making someone an Owner or for the last Owner
- `src/RestaurantManagement.Modules.Identity/Controllers/MembersController.cs` — `PUT api/members/{userId}/access/{locationId}` edits presets and cells but cannot grant or remove the Owner role
- `src/RestaurantManagement.Shared/Services/Identity/IOrganizationAccessSetup.cs` — the only other writer, which sets up the Owner and default roles at organization creation
- `docs/specs/shared/2026-10-15-audit-log.md` — Outcome #1 expects Before = Waiter, After = Cashier

## Acceptance criteria
- [ ] Only an Owner can make another member an Owner or remove that role
- [ ] Removing the Owner role from the last Owner is refused
- [ ] Each Owner change writes an audit entry; another organization's member returns not found

## Related
backlog/features/audit-log.md, backlog/improvements/session-redesign.md (role change should invalidate older tokens)
