# Verify email on registration

## Problem
A new user must prove they own the email address they registered with before they can
use their account, so the platform does not create sessions for unreachable or
misspelled/fraudulent addresses. This spec documents the requirements for that flow.

**Note:** this feature already has a working implementation in the codebase
(`RegistrationService`, `VerificationService`, `EmailService`, and the `/api/auth/register`,
`/api/auth/verify`, `/api/auth/resend-verification` endpoints). This spec formalizes the
requirements the existing behavior should meet and calls out where the current
implementation diverges or is incomplete, so it can be used as the reference for review or
follow-up fixes rather than a from-scratch build.

## Scope
- Module affected: Identity (owns `User` and verification records; no other module is
  touched).
- Roles allowed: this flow applies to any newly registered user, before any
  organization-level role (Owner/Admin/Staff) is meaningful for them. Verification is not
  scoped by organization — a user proves ownership of their email address, not membership
  in a tenant.

## Behaviour
1. When a user registers, the system creates the account in an unverified state and
   generates a single-use verification credential tied to that user.
2. The system sends an email to the address supplied at registration containing that
   verification credential. Registration succeeds (the account is created) even though
   verification is still pending.
3. The verification credential expires after a configured time window. Once expired, it
   can no longer be used to verify the account.
4. Submitting a valid, non-expired credential for the matching account marks the account
   as verified and consumes the credential (it cannot be reused).
5. A user can request a new verification credential to be sent (resend), which invalidates
   any previously issued, unused credential for that account.
6. Resend requests are rate-limited per account to prevent abuse (a cooldown period must
   pass between two resend requests for the same account).
7. Resending is rejected if the account is already verified.
8. An unverified user cannot log in (cannot obtain an access/refresh token) until they
   complete verification.
9. Registration, verification, and resend responses do not reveal whether a given email
   address is already registered beyond what is necessary for the caller to proceed (see
   Acceptance criteria for exact failure behaviour), to avoid leaking account existence to
   unrelated callers **[assumption — see Open questions]**.

## Data
Verification credential:
- `UserId` (required) — the account this credential belongs to.
- `Code`/token value (required) — single-use, unique per issuance.
- `ExpiresAt` (required) — set at issuance from a configured expiry duration.
- `IsUsed` (required, default false) — set true once successfully consumed; a used or
  expired credential is never valid again.

Registration request (already defined): `Email`, `Password`, `FirstName`, `LastName`,
`PhoneNumber` (optional) — unchanged by this feature.

Verification request:
- `Email` (required, valid email format) — identifies the account.
- `Code` (required) — the credential to check against the account's latest unused,
  unexpired credential.

Resend request:
- `Email` (required, valid email format) — identifies the account to resend to.

## Acceptance criteria
- Registering with an email that is already registered is rejected and no verification
  email is sent.
- Verifying with a code that does not match any unused, unexpired credential for the
  given email is rejected.
- Verifying with a code that has expired is rejected, even if it was previously valid.
- Verifying with a code that was already used is rejected.
- Verifying an email that does not correspond to any account is rejected.
- Requesting a resend for an email that does not correspond to any account is rejected.
- Requesting a resend for an already-verified account is rejected.
- Requesting a resend before the cooldown period has elapsed since the last request is
  rejected.
- An unverified user who supplies correct login credentials is rejected and does not
  receive an access or refresh token.
- Registering with a valid, unique email creates the account in an unverified state and
  triggers a verification email to be sent to that address.
- Verifying with a correct, unused, unexpired code for the matching email marks the
  account as verified and the code as used, and subsequent login attempts with correct
  credentials succeed.
- Requesting a resend for an existing, unverified account outside the cooldown window
  invalidates the previous unused code, issues a new one, and sends it by email.
- If the verification email fails to send during registration, the account still exists
  in an unverified state **[assumption — see Open questions on whether registration
  should roll back or fail]**.

## Out of scope
- Password reset / forgot-password flow.
- Verifying phone numbers.
- Restricting anything beyond login for unverified users (e.g. this spec does not define
  whether unverified users can be looked up, invited to an organization, etc. — only that
  they cannot log in).
- Choice of email provider/transport (SMTP vs a transactional email API) — this is an
  infrastructure/configuration concern, not a behavioural requirement.
- Changing the verification credential from a numeric code to a clickable link/token.
  The current implementation uses a short numeric code entered by the user, not a link.
  This spec describes the code-based flow as implemented; switching to a link-based
  flow (as the original feature phrasing suggested) is a separate decision — see Open
  questions.
- Notifying the user by any channel other than email (e.g. SMS).

## Open questions
- **Link vs. code**: the feature was requested as an email containing a "verification
  link/token", but the existing implementation sends a 6-digit numeric code the user
  enters manually via `/api/auth/verify`. This spec documents the code-based behavior
  that already exists. Confirm whether the product intent is to keep the code-based flow
  or migrate to a clickable-link flow (which would change the verification endpoint from
  accepting `Email` + `Code` to accepting a single opaque token, typically via a GET link).
- **What unverified restricts**: the existing implementation blocks login only. Confirm
  this is the full intended restriction — e.g. should an unverified user's registration
  response omit tokens (it currently does, since no token is issued until login), and
  should there be any other restriction (API access, organization invitations, etc.)?
- **Registration failure on email-send failure**: today, if sending the verification
  email fails during registration, the failure is not visibly handled the same way as it
  is on resend (resend explicitly fails with an error if sending fails; registration does
  not). Decide whether registration should fail/roll back if the verification email
  cannot be sent, or succeed regardless (current behavior) with the user relying on
  resend.
- **Account existence disclosure**: resend and verification failures currently return
  distinct "user not found" vs "invalid/expired code" errors, which lets a caller probe
  whether an email is registered. Confirm whether this is acceptable or whether these
  responses should be indistinguishable.
- **Credential expiry and cooldown durations**: current defaults are a 15-minute
  expiry and a 60-second resend cooldown. Confirm these are the intended values (they are
  configurable, not hardcoded, so this is a default-value question, not a behavioural
  gap).
- **Multi-tenancy**: verification is account-level, not organization-scoped, since a user
  is not yet attached to an organization at this stage of registration. Confirm no
  organization context is required at verification time.
