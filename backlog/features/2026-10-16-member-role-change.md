# Member role change spec

**Status:** proposed
**Severity:** medium
**Origin:** docs/plans/shared/2026-10-15-audit-log/plan.md · Phase 0 — Task 8
**Date:** 2026-10-16

## Problem
No feature lets a member's role be changed, for example from Waiter to Cashier. The audit log spec (Outcome #1) expects a "member role changed" entry. Its plan defines that action but cannot check the outcome until a role change feature exists. Decided in docs/specs/shared/2026-10-19-per-location-access-grid.md: roles are presets that fill a stored grid, and delegation rules (no self-edit, grant at most own level, Owner never edited). Still missing: the feature itself (slice 2) and last-Owner protection.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Services/MemberAccessService.cs` — `SetAccessAsync` stores a member's grid at a location, but no endpoint or delegation rules expose it
- `src/RestaurantManagement.Shared/Services/Identity/IOrganizationAccessSetup.cs` — the only other writer, which sets up the Owner and default roles at organization creation
- `docs/specs/shared/2026-10-15-audit-log.md` — Outcome #1 expects Before = Waiter, After = Cashier

## Acceptance criteria
- [ ] Slice 2 spec covers changing a member's preset or grid and protecting the last Owner
- [ ] Changing a member's role from Waiter to Cashier writes one "member role changed" audit entry with Before = Waiter and After = Cashier (audit log Outcome #1)
- [ ] Another organization's member returns not found; roles without members.manage are refused

## Related
backlog/features/audit-log.md, backlog/improvements/session-redesign.md (role change should invalidate older tokens)
