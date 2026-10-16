# Spec: Audit log

**Type:** Feature   **Date:** 2026-10-15   **Requested by:** igormori
**Source:** Phase 0 — Task 8   **Size:** M   **Depends on:** Task 1 (base entity)

> **No live data:** the app is not launched and the database holds no real data. Fields and
> tables may be changed freely; no data migration or backfill is needed.

## Business Goals
- Keep a permanent record of who changed roles, settings and location status, and when.
- Prepare one shared log that Phase 1 extends to cancellations, discounts and cash closings.

## Objective
Record selected actions in a permanent, append-only log that authorized users can read for their own organization.

## Current State
No record exists of who changed a role, a setting or a location's status.

## Future State
| Question | Answer |
|---|---|
| **What** will change? | A permanent log of selected actions, written when they happen, and a way to read it. |
| **How** should it behave? | See Rules. |
| **Who** is impacted? | Owners, Admins and anyone with permission to view reports. |
| **Where** is the impact? | Shared module (the log); the role, settings and location status changes that write to it. |

### Rules
| # | Rule |
|---|---|
| 1 | Each entry records: organization, location (optional), user, action, what was changed, the values before and after, an optional reason, the network address and the time. |
| 2 | These actions are logged: member role changed, settings changed, location status changed. |
| 3 | The log is append-only: entries can never be changed or deleted, and no feature offers that. |
| 4 | Users read only their own organization's entries, filtered by date and action. |
| 5 | Reading the log requires the reports.view permission. |

### Decisions from the ticket
- Handlers write to the log explicitly when an action happens (no automatic capture).
- Entries are indexed by organization and time, so reading by date stays fast.

## Outcomes
| # | Outcome | How we know it is achieved |
|---|---|---|
| 1 | Role change is recorded | Changing a member from Waiter to Cashier creates one entry with Before = Waiter and After = Cashier |
| 2 | Only own organization is visible | With entries in Org A and Org B, a user in Org A reads only Org A's entries |
| 3 | Permission required | A user without reports.view is refused (403) |
| 4 | Entries cannot be changed | No feature exists to update or delete an entry |

## Out of Scope
| Item | Backlog file |
|---|---|
| Cancellations, discounts and cash closings in the log | Planned for Phase 1 (no backlog file) |
