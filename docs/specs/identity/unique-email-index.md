# Unique email index

## Problem
`users.email` has no database-level uniqueness constraint. `RegistrationService`
prevents duplicates only with a query-then-insert check (`_db.Users.AnyAsync(u =>
u.Email == request.Email)` before `_db.Users.Add(user)`). Two concurrent registration
requests for the same email can both pass that check before either insert commits,
producing two `User` rows with the same email. This breaks the assumption (used by
login, verification, and resend, which all look up a user by `FirstOrDefaultAsync(u =>
u.Email == ...)`) that email identifies at most one account.

## Scope
- Module affected: Identity (owns the `User` entity and `IdentityDbContext`).
- Roles allowed: not applicable — this is a data-integrity constraint enforced for every
  registration, regardless of who is registering (there is no authenticated caller at
  registration time).

## Behaviour
1. The database enforces that no two rows in `users` share the same email value; this
   must hold even when two insert attempts for the same email execute concurrently.
2. `RegistrationService.RegisterAsync` keeps its existing pre-check (`AnyAsync`) as a
   fast path that returns a normal validation-style error for the common case, but no
   longer relies on it as the only safeguard.
3. When an insert is attempted for an email that already exists (i.e. the pre-check
   raced and missed it), the database rejects the insert. The application catches this
   database-level conflict and translates it into the same rejection the application
   already returns for the pre-check case (see Acceptance criteria) — the caller never
   sees a raw database error or a 500 for a duplicate email.
4. The uniqueness check treats email as case-insensitive: `User@Example.com` and
   `user@example.com` are considered the same email and cannot both exist as separate
   accounts. `RegisterRequest.Email` already normalizes input to trimmed, lowercase
   form before it reaches the service, so all values written through registration are
   already lowercase; the database constraint must still enforce case-insensitive
   uniqueness itself rather than depend solely on that request-level normalization,
   since other write paths (e.g. future admin tooling, data fixes) are not guaranteed to
   go through `RegisterRequest`.
5. Existing rows are checked for pre-existing duplicates (case-insensitive) before the
   constraint is added. If duplicates already exist, adding the constraint must not
   silently corrupt or delete data; see Acceptance criteria and Open questions for how
   this is handled.

## Data
No new fields. This affects the existing `email` column on `users` (backing
`User.Email`, `string`, required, already validated as an email format and length-capped
at 320 characters by `RegisterRequest`):
- Add a database-level uniqueness guarantee on `email`, case-insensitive.

## Acceptance criteria
- Two concurrent registration requests with the same email (or the same email in
  different casing) result in exactly one `User` row being created; the losing request
  receives the same "email already registered" error the pre-check already returns for
  a sequential duplicate, not an unhandled exception or a 500.
- A duplicate-email database conflict is never surfaced to the caller as a raw
  database/constraint-violation error or an unrelated 500; it is translated to the
  existing `BusinessException` used for "email already registered".
- Registering `User@Example.com` when `user@example.com` already exists is rejected as a
  duplicate.
- Applying the new constraint to the current database does not fail or lose data when no
  duplicate emails exist (the expected case, since duplicates are only possible from the
  race this spec closes).
- Registering with a genuinely new, unique email still succeeds and returns the created
  account, unchanged from current behavior.
- Existing flows that look up a user by email (login, verify, resend) are unaffected —
  they continue to find at most one matching row.

## Out of scope
- The other follow-ups listed alongside this one in `docs/follow-ups.md` (login
  revealing account existence, verification brute-force protection, verify/resend
  response content-type) — each is its own spec.
- Any change to `RegisterRequest` validation or normalization beyond what already exists
  (trim + lowercase is already in place and is not being altered here).
- Deduplicating or merging any pre-existing duplicate rows found in current data; this
  spec only requires that their presence be detected and handled deliberately (see Open
  questions), not that they be automatically resolved.
- Adding uniqueness constraints on any other field or table.
- Rate-limiting or otherwise restricting registration attempts (covered by the
  brute-force follow-up, not this one).

## Open questions
- **Pre-existing duplicates**: if the current database already contains duplicate
  emails (possible, since this is exactly the bug being fixed), the migration that adds
  the unique constraint will fail to apply as-is. This spec assumes duplicates should be
  identified and resolved manually (or via a separate data-cleanup step) before the
  constraint migration is deployed, rather than the migration silently deleting or
  merging rows. Confirm this is acceptable, and who verifies production data is clean
  before deploy **[assumption]**.
- **Case-insensitivity mechanism**: this spec requires the constraint itself to be
  case-insensitive rather than relying only on `RegisterRequest`'s normalization. The
  planner should decide the mechanism (e.g. a functional/expression index, a citext-like
  type, or a stored normalized column) — this spec only states the required behavior,
  not the implementation.
- **Error message specificity**: should the translated error for a database-level
  conflict be identical in wording to the existing pre-check error ("email already
  registered"), or is a distinguishable message acceptable for observability? This spec
  assumes the caller-facing message is identical, since the caller should not be able to
  tell which safeguard caught the duplicate **[assumption]**.
