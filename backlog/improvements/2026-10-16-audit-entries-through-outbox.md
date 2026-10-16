# Write audit entries through the outbox

**Status:** proposed
**Severity:** medium
**Origin:** docs/plans/shared/2026-10-15-audit-log/plan.md · Phase 0 — Task 8
**Date:** 2026-10-16

## Problem
Audit entries are written in their own save, right after the change is saved. This avoids sharing a database transaction across modules. If that second write fails, the change stays and only an error is logged, so the audit log has no record of it. That is acceptable for settings and location status changes. It is not acceptable for Phase 1 cancellations, discounts and cash closings, where every entry must exist.

## Evidence
- `docs/plans/shared/2026-10-15-audit-log/batches/B3.md` — entries are recorded after the service's save; a failure is logged, and the request still succeeds
- `docs/specs/shared/2026-10-15-audit-log.md` — Business Goals: a permanent record that Phase 1 extends to cancellations, discounts and cash closings

## Acceptance criteria
- [ ] Once the outbox (Phase 0 — Task 11) exists, audit entries are saved to the outbox in the same save as the change and delivered to the audit log from there
- [ ] When the audit log write fails, the entry is retried later and not lost
- [ ] Done before Phase 1 logs cancellations, discounts or cash closings

## Related
backlog/features/audit-log.md
