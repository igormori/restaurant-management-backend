# Identity module

Context beyond the root CLAUDE.md and the code itself. See docs/specs/identity/ and
docs/plans/identity/ for the full reasoning.

Every change here updates docs/security/identity-matrix.md in the same commit (rules in the
root CLAUDE.md, "Security matrices"). Endpoint and database tests go in
tests/RestaurantManagement.Web.Tests/Identity/.

Verification is account-level, never organization-scoped: a user has no UserRole (and no
OrganizationId) until after verification, when they create/join an organization.
Login collapses unknown email, wrong password, locked account, and unverified account into
one generic 401; verify collapses unknown email, wrong code and a verification-locked account
into one generic 401; resend only queues the email and always returns the same acknowledgement, whatever the account state.
Login hashes against a fixed dummy password when no user row exists, so an unregistered
email does the same work as a wrong password for a real one. Don't remove it as dead code.
Verification has its own lockout (VerificationFailedAttempts/VerificationLockedUntil,
SecurityOptions.MaxVerificationAttempts/VerificationLockoutDurationMinutes), separate
from login's FailedAttempts/LockedUntil.
Email uniqueness is enforced in the database via a computed, lowercased shadow column
and unique index (IdentityDbContext.UniqueEmailIndexName); RegistrationService catches
the Postgres unique-violation as the real guard, the AnyAsync check is just a fast path.
Resend only adds a verification row to password_reset_requests; VerificationEmailProcessor invalidates
all previously unused codes, applies the 60 s wait (newest code's CreatedAt, used or not) and sends.
IsUsed means "no longer usable" by design, covering both consumed and invalidated by a
newer resend.
Registration swallows email-send failures and returns 200 with an unverified account.
Resend never sends in the request, so a sending failure never reaches the requester either.
Verification codes are generated with RandomNumberGenerator, never new Random().
Codes are stored only as keyed fingerprints (CodeHash); the readable code goes only to the email.
SendVerificationEmailAsync(to, firstName, code, expiryMinutes) takes three adjacent
string parameters with no compiler protection against swapping them.
UniqueEmailIndexTests run against a real Postgres via Testcontainers (IdentityPostgresFixture)
because they prove constraint races and migration-on-existing-data; other tests stay on
the SQLite in-memory database.
Forgot password only queues the email in password_reset_requests; PasswordResetEmailProcessor decides
eligibility, creates the code, sends and retries (1, 2, 4, 8 min; failed after the 5th).
Delivery is at most once after a crash: a request still marked code-issued when claimed again is
dropped without sending (a second code would break the first). Failed rows are purged after 7 days.
Out-of-scope findings and accepted gaps are tracked in backlog/; don't fix them as drive-by changes.
