# Store each user's preferred language for emails

**Status:** proposed
**Severity:** low
**Origin:** docs/plans/shared/2026-10-20-invite-member-and-edit-access-grid/plan.md · Planning
**Date:** 2026-10-20

## Problem
The invitation email should use the invitee's language when known, otherwise the inviter's. No
user's language is stored anywhere, so it is never known: invitations always use the language of
the inviter's request. Verification and reset emails have no language choice at all.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Entities/User.cs` — no language field
- `src/RestaurantManagement.Shared/Utils/Localization.cs` — the language comes only from the request's `Accept-Language` header
- `docs/specs/shared/2026-10-20-invite-member-and-edit-access-grid.md` — "Invitation email", Language row

## Acceptance criteria
- [ ] A user's preferred language (English or Portuguese) is stored and can be changed
- [ ] An invitation to an existing account uses that account's language
- [ ] Verification and password reset emails use the user's language

## Related
None
