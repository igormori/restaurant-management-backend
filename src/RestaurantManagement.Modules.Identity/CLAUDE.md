# Identity module

Context beyond the root CLAUDE.md and the code itself. See docs/specs/identity/ and
docs/plans/identity/ for the full reasoning.

Verification is account-level, never organization-scoped: a user has no UserRole (and no
OrganizationId) until after verification, when they create/join an organization.
Login collapses unknown email, wrong password, and unverified account into one generic
401; verify collapses unknown email and wrong code into one generic 401; resend always
returns the same acknowledgement. A locked account still gets its own distinct 423.
Login hashes against a fixed dummy password when no user row exists, so an unregistered
email does the same work as a wrong password for a real one. Don't remove it as dead code.
Verification has its own lockout (VerificationFailedAttempts/VerificationLockedUntil,
SecurityOptions.MaxVerificationAttempts/VerificationLockoutDurationMinutes), separate
from login's FailedAttempts/LockedUntil.
Email uniqueness is enforced in the database via a computed, lowercased shadow column
and unique index (IdentityDbContext.UniqueEmailIndexName); RegistrationService catches
the Postgres unique-violation as the real guard, the AnyAsync check is just a fast path.
Resend invalidates all previously unused codes, not just the latest. The cooldown check
still reads only the newest code's CreatedAt, used or not.
IsUsed means "no longer usable" by design, covering both consumed and invalidated by a
newer resend.
Registration swallows email-send failures and returns 200 with an unverified account.
Resend throws BusinessException(500) on the same failure. The asymmetry is deliberate.
Verification codes are generated with RandomNumberGenerator, never new Random().
SendVerificationEmailAsync(to, firstName, code, expiryMinutes) takes three adjacent
string parameters with no compiler protection against swapping them.
UniqueEmailIndexTests run against a real Postgres via Testcontainers (IdentityPostgresFixture)
because they prove constraint races and migration-on-existing-data; other tests stay on
the SQLite in-memory database.
Accepted gaps are tracked in docs/follow-ups.md; don't fix them as drive-by changes.
