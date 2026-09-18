# Follow-ups

Known gaps, deliberately not fixed. Don't fix these as drive-by changes: each needs
its own spec.


## Organization
- Consistency: owner-role assignment happens after the organization+settings transaction
  commits, as a separate unguarded call; if it fails the organization exists with no
  owner and nothing compensates
- Consistency: location writes (create/edit/delete) require the Owner role, but editing
  the parent organization itself allows Owner or Admin
- Cleanup: Entities/ Organization.cs has a stray leading space in its filename

## Menu
- Security: MenuCategoryController has no role or organization/location ownership check
  beyond [Authorize]; any authenticated user can create, edit, delete, or view any
  organization's menu categories
- Bug: MenuController's read endpoints authorize "Owner,Admin,Manager,Employee", but
  Shared.Roles only defines Owner, Admin, Staff; Manager/Employee are never assigned and
  Staff is never included, so Staff users can never read menus
