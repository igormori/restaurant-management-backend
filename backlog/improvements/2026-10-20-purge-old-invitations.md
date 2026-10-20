# Purge expired, used and cancelled invitations

**Status:** proposed
**Severity:** low
**Origin:** docs/plans/shared/2026-10-20-invite-member-and-edit-access-grid/plan.md · Planning (T5)
**Date:** 2026-10-20

## Problem
Invitations keep the invited email address forever, even after they expire, are used, replaced or
cancelled. The pending email queue already removes failed rows after 7 days so raw email addresses
are not kept; invitations have no such cleanup.

## Evidence
- `docs/plans/shared/2026-10-20-invite-member-and-edit-access-grid/batches/B4.md` — T5 stores the invitation with its email and adds no cleanup
- `src/RestaurantManagement.Modules.Identity/Services/PendingEmailQueue.cs` — `FailedRetention` of 7 days for the queue

## Acceptance criteria
- [ ] Invitations that expired, were used, replaced or cancelled more than a set time ago are removed by a background pass
- [ ] Pending, unexpired invitations are never removed
- [ ] The audit log entries for those invitations stay

## Related
None
