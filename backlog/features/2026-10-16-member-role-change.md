# Member role change spec

**Status:** proposed
**Severity:** medium
**Origin:** docs/plans/shared/2026-10-15-audit-log/plan.md · Phase 0 — Task 8
**Date:** 2026-10-16

## Problem
No feature lets a member's role be changed, for example from Waiter to Cashier. The audit log spec (Outcome #1) expects a "member role changed" entry. Its plan defines that action but cannot check the outcome until a role change feature exists. The business rules are undecided: who may change whose role, and how the Owner is protected (for example, nobody can demote or remove the last Owner).

## Evidence
- `src/RestaurantManagement.Shared/Services/Identity/IUserRoleAssigner.cs:5` — only assign and revoke exist, with no change operation
- `src/RestaurantManagement.Modules.Organization/Services/OrganizationService.cs:129` — the only caller, which assigns Owner at organization creation
- `docs/specs/shared/2026-10-15-audit-log.md` — Outcome #1 expects Before = Waiter, After = Cashier

## Acceptance criteria
- [ ] A spec defines who may change whose role (permission, location-level or organization-wide) and how the Owner is protected
- [ ] Changing a member's role from Waiter to Cashier writes one "member role changed" audit entry with Before = Waiter and After = Cashier (audit log Outcome #1)
- [ ] Another organization's member returns not found; roles without members.manage are refused

## Related
backlog/features/audit-log.md, backlog/improvements/session-redesign.md (role change should invalidate older tokens)
