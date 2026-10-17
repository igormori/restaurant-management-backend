# Owner or Admin removes a member and ends their sessions

**Status:** proposed
**Severity:** high
**Origin:** docs/security/identity-asvs-gap-report.md · Summary #10 (ASVS V7.4.2, V7.4.5)
**Date:** 2026-10-16

## Problem
When an employee is fired or a tablet is lost, an Owner or Admin cannot remove that person from
the organization or sign them out. Their sessions keep working until they expire. Agreed with the
user on 2026-10-16:
- **Remove member:** deletes every role the person holds in that organization (one or several,
  built-in or custom) and ends every session of theirs for that organization at once.
- **Sign out member:** ends their sessions for that organization without removing them (lost or
  stolen device).
- **Only this organization:** the person's account stays, and so do their roles and sessions in
  other organizations (one person can work at two restaurants).
- **Who:** Owner and Admin (members.manage). Nobody can remove the last Owner; an Admin cannot
  remove or sign out an Owner.

## Evidence
- `src/RestaurantManagement.Shared/Services/Identity/IUserRoleAssigner.cs` — assign and revoke exist; no "remove member" operation or endpoint
- `src/RestaurantManagement.Modules.Identity/Services/RefreshTokenService.cs:83` — `RevokeAllForUserAsync` ends every session of a user, in every organization; nothing ends only one organization's
- `docs/security/identity-matrix.md` — O5 (logout-all) covers only the caller's own sessions

## Acceptance criteria
- [ ] Owner or Admin removes a member: all their roles in that organization are gone and their refresh tokens for that organization are refused at once
- [ ] Their roles and sessions in another organization keep working
- [ ] Sign out member: sessions for that organization end; roles stay
- [ ] Removing the last Owner is refused; an Admin removing or signing out an Owner is refused
- [ ] Roles without members.manage get 403; a member of another organization gets not found
- [ ] Each removal and sign-out writes an audit entry
- [ ] Identity and Organization matrices get rows with tests

## Related
- backlog/improvements/session-redesign.md (the access token still works up to 15 min; ship close together so removal is immediate)
- backlog/features/2026-10-16-member-role-change.md (same permission and Owner protection)
- backlog/features/2026-10-15-custom-roles.md
