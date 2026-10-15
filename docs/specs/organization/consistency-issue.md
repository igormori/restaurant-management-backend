# Owner role must never be missing after organization creation

## Problem
When a user creates an organization, the Organization module commits the organization
and its settings in one database transaction, then — after that transaction has already
committed — makes a separate call into the Identity module to assign the creating user
the Owner role for that organization. That call is not covered by any transaction or
retry, and nothing compensates if it fails.

If the role assignment call throws (network blip, Identity database unavailable,
validation error, timeout, etc.), the organization and its settings already exist and
are visible to any query, but no one holds the Owner role on it. The caller receives an
error ("Organization created but failed to assign owner role..."), but the organization
is not rolled back and not deleted. The result is an orphaned organization: it exists,
consumes the user's one-trial-organization allowance, is not owned by anyone, and cannot
be managed, edited, or deleted through any endpoint that requires an Owner or Admin role,
because no such role exists for it.

This is a data-integrity risk specific to the module boundary: Organization and Identity
each own their own DbContext and never reference the other's directly, so a single local
database transaction cannot span both writes without violating that isolation. (Both
DbContexts currently point at the same physical database, but the isolation rule exists
to allow each module to become its own service with its own database later — so the fix
should not rely on today's shared physical database.)

## Scope
- Module(s) affected: Organization (creation flow), Identity (role assignment,
  consumed via the existing `IUserRoleAssigner` contract in Shared)
- Roles allowed: N/A — this is an integrity/consistency requirement for the creation
  flow itself, not a new permission

## Behaviour
1. An organization must never be left in a state where it exists but has no Owner.
2. If owner-role assignment cannot be completed after the organization and its settings
   have been created, the system must not leave that organization permanently visible
   or usable without an owner. Either:
   - the organization creation is undone (the organization and its settings are removed
     or otherwise made inaccessible), or
   - the role assignment is retried automatically until it succeeds, with the
     organization not considered "created" from the caller's perspective until it does.
3. The creating user must never receive a successful organization-creation response
   without also holding the Owner role on that organization.
4. If the creating user receives an error response, the organization must not be
   independently retrievable by that user (e.g. via a subsequent "list my organizations"
   call) unless the Owner role was in fact assigned.
5. Whatever compensating action is taken must not silently fail: if the compensating
   action itself fails, the failure must be surfaced (e.g. logged/reported) so it can be
   found and corrected, rather than left as an invisible orphan.
6. The one-trial-organization-per-user restriction must not count an orphaned or
   rolled-back organization against the user.

## Data
No new persisted fields are introduced by this requirement. Whatever mechanism satisfies
"Behaviour" above may need to track, at minimum, whether an organization has a confirmed
owner (this could be derived from the existence of an Owner UserRole rather than a new
field — an implementation decision, not part of this spec).

## Acceptance criteria
- If owner-role assignment fails after organization/settings creation, the organization
  is not left permanently orphaned: it is either removed/rolled back, or the assignment
  is retried until it succeeds before the request is considered complete.
- If owner-role assignment fails and the organization is rolled back, a subsequent
  "list my organizations" call for that user does not include the failed organization.
- If owner-role assignment fails and the organization is rolled back, the user's
  one-trial-organization limit is not consumed by the failed attempt (they can try again).
- If the compensating rollback/cleanup action itself fails, the failure is observable
  (e.g. surfaced to error tracking) rather than silently discarded.
- A successful organization-creation response is only ever returned once the creating
  user holds the Owner role on that organization.
- Creating an organization when owner-role assignment succeeds on the first attempt
  behaves exactly as it does today: organization, settings, and Owner role all exist,
  and the response is returned.

## Out of scope
- Changing who can create organizations, the trial-plan defaults, or the
  one-trial-organization rule itself (only its interaction with a failed/rolled-back
  creation attempt)
- Fixing other known Organization module inconsistencies (e.g. Owner-vs-Admin
  requirements differing between location and organization edits, or the stray leading
  space in the `Organization` entity filename) — tracked separately in
  `docs/follow-ups.md`
- Retroactively detecting or repairing any organizations that may already exist today
  with no Owner role, if any exist (see open questions)
- Introducing a general-purpose cross-module transaction/saga framework beyond what this
  one flow needs
- Changes to the `IUserRoleAssigner` contract's method signature or to any other
  consumer of it

## Open questions
- Organization and Identity are separate modules with separate DbContexts (per this
  repo's module-isolation rules). Both DbContexts happen to point at the same physical
  database today, but that isolation is deliberate: modules are meant to be extractable
  into separate services with separate databases later, so a solution must not depend on
  sharing a database connection/transaction across the two DbContexts even though it
  would work today. A human decision is needed on the accepted approach: compensating
  delete/rollback of the organization if role assignment fails, retry of the role
  assignment until it succeeds, an outbox/saga pattern, or some other eventual-consistency
  mechanism. This spec states the required outcome, not which of these mechanisms to use.
- Should the organization ever be visible to the creating user (e.g. via "list my
  organizations") in a transient state before the Owner role is confirmed, or must it be
  completely hidden/absent until ownership is confirmed?
- Are there already existing organizations in production data with no Owner role due to
  this bug? If so, does someone need to run a one-off audit/repair, and who decides the
  outcome for each one (assign an owner, or delete the organization)? This spec does not
  assume an answer.
- If retry is the chosen mechanism, is a bounded number of retries with eventual failure
  acceptable, or must it retry indefinitely until success?
