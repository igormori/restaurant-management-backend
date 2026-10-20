# Token role checks count location-only roles for organization-wide actions

**Status:** proposed
**Severity:** high
**Origin:** docs/plans/shared/2026-10-19-access-decisions-use-all-user-roles/plan.md · planning
**Date:** 2026-10-19

## Problem
The access token lists every role the user holds in the organization, with no location. Permission
policies and `[Authorize(Roles = ...)]` read those claims, so a role assigned for one location
counts for organization-wide actions. Example: Admin at location A only passes the `reports.view`
policy and reads the whole organization's audit log. The spec
`docs/specs/shared/2026-10-19-access-decisions-use-all-user-roles.md` (Rule 5) says roles for
specific locations never count for organization-wide actions; that spec fixes only the service
checks in Menu and Organization.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Services/AccessTokenFactory.cs:44` — adds a role claim per assignment, location dropped
- `src/RestaurantManagement.Web/Auth/PermissionAuthorizationHandler.cs:20` — grants when any role claim has the permission
- `src/RestaurantManagement.Shared/Controllers/AuditLogController.cs:11` — organization-wide endpoint behind that policy

## Acceptance criteria
- [ ] A user whose only Admin or Manager assignment is for one location gets 403 from the audit log
- [ ] A user with Admin for all locations still reads the audit log
- [ ] Permission policies decide from all-locations assignments for organization-wide actions

## Related
backlog/improvements/2026-10-15-apply-permission-policies-to-endpoints.md
