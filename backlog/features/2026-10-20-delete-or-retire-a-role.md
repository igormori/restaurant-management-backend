# Delete or retire a role

**Status:** proposed
**Severity:** low
**Origin:** docs/specs/identity/2026-10-20-custom-roles.md · Out of Scope
**Date:** 2026-10-20

## Problem
Once roles can be created, an organization may want to remove one it no longer uses. Nothing in use is deleted, so a role held by members needs a rule: refuse, or retire it (hidden from new invitations, kept as a label for current holders).

## Evidence
- `docs/specs/identity/2026-10-20-custom-roles.md` — Out of Scope
- `docs/specs/shared/2026-10-19-per-location-access-grid.md` — Role labels are stored per member and location

## Acceptance criteria
- [ ] The Owner can remove or retire a role of their organization
- [ ] A role held by members is never deleted; its members' labels and access are unchanged
- [ ] The change is written to the audit log
- [ ] Another organization's role returns not found

## Related
backlog/features/2026-10-16-remove-member-and-end-sessions.md
