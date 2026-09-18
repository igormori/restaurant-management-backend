# Consistent verify/resend response format

## Problem
`POST /api/auth/verify` and `POST /api/auth/resend-verification` return a bare string with
a `text/plain` content type on success, but return a JSON object on failure (via the shared
exception-handling middleware). API consumers of these two endpoints must therefore branch
on content type/shape depending on whether the call succeeded, instead of always parsing
JSON. This spec defines a single, consistent JSON response contract for both endpoints,
covering success and failure alike.

## Scope
- Module affected: Identity (`AuthController.VerifyEmail`, `AuthController.ResendVerification`,
  and the `IVerificationService` methods they call: `VerifyEmailAsync`,
  `ResendVerificationCodeAsync`).
- Other Identity endpoints (`register`, `login`, `refresh`) already return a JSON body
  (`AuthResponse`) on success and are not affected.
- Roles allowed: unchanged — this is a pre-authentication flow available to any caller
  with a valid request; no Owner/Admin/Staff role check applies. Not organization-scoped
  (see `src/RestaurantManagement.Modules.Identity/CLAUDE.md`: verification is
  account-level, before a user has an `OrganizationId`), so there is no cross-tenant data
  access to guard against for this specific change.

## Behaviour
1. On success, `POST /api/auth/verify` must return a JSON response body instead of a bare
   string, with `Content-Type: application/json`.
2. On success, `POST /api/auth/resend-verification` must return a JSON response body
   instead of a bare string, with `Content-Type: application/json`.
3. The success response body shape must be the same for both endpoints (same field
   names/types), so a consumer can parse both with one code path.
4. The success response must still convey the human-readable confirmation text these
   endpoints currently return as their entire body (the localized "UserVerified" /
   "VerificationCodeResent" strings), as a field in the JSON body rather than as the raw
   body.
5. Failure responses from both endpoints keep their current JSON shape (`{ "error":
   "<message>" }`, produced by the shared exception-handling middleware) and current HTTP
   status codes (400, 401, 404, 429, 500 depending on the case). This spec does not change
   error status codes, error messages, or the error body shape — only the success path is
   brought in line with it.
6. This is a breaking change for any existing consumer that parses the current bare-string
   success body or relies on `Content-Type: text/plain` for these two endpoints. See Open
   questions.

## Data
Success response body (new; applies to both `verify` and `resend-verification`):
- `message` (string, required) — the human-readable confirmation text, equivalent in
  content to what the endpoint currently returns as its raw body (e.g. the localized
  "UserVerified" / "VerificationCodeResent" text).

Failure response body (unchanged, already implemented by
`ExceptionHandlingMiddleware`):
- `error` (string, required) — the localized business-error message.
- `detail` (string, optional) — only present for unhandled (500) errors in a development
  environment; not applicable to the known business-error cases these two endpoints raise.

Request bodies (`VerifyEmailRequest`, `ResendVerificationRequest`) are unchanged by this
spec.

## Acceptance criteria
- Verifying with a code that does not match any unused, unexpired credential still
  returns HTTP 401 with the existing JSON error shape (`{ "error": "..." }`) and
  `Content-Type: application/json` — unchanged by this spec.
- Verifying an email with no matching account still returns HTTP 400 with the existing
  JSON error shape — unchanged by this spec.
- Requesting a resend for an unknown email still returns HTTP 404 with the existing JSON
  error shape — unchanged by this spec.
- Requesting a resend for an already-verified account still returns HTTP 400 with the
  existing JSON error shape — unchanged by this spec.
- Requesting a resend before the cooldown has elapsed still returns HTTP 429 with the
  existing JSON error shape — unchanged by this spec.
- A resend whose email send fails still returns HTTP 500 with the existing JSON error
  shape — unchanged by this spec.
- A successful `POST /api/auth/verify` returns HTTP 200 with `Content-Type:
  application/json` and a JSON body containing a `message` field (not a bare string body).
- A successful `POST /api/auth/resend-verification` returns HTTP 200 with `Content-Type:
  application/json` and a JSON body containing a `message` field (not a bare string body).
- The success response body field names are identical between `verify` and
  `resend-verification` (both expose `message`), so a single deserialization type works
  for both.

## Out of scope
- Changing the error response shape, error messages, or error status codes — these are
  already JSON and already correct per the problem statement.
- Any other Identity endpoint (`register`, `login`, `refresh`) — these already return a
  JSON `AuthResponse` on success.
- Backward-compatibility shims for existing consumers of the current bare-string format
  (e.g. content negotiation that serves `text/plain` to old clients, API versioning, or a
  deprecation period). Whether one is needed is called out below.
- The other known Identity follow-ups (brute-force limiting on `/verify`, distinct
  error-message disclosure on `verify`/`resend`, missing unique index on `email`) — each
  is tracked separately in `docs/follow-ups.md` and is out of scope here.
- Any change to the underlying verification/resend business logic (cooldown, expiry,
  invalidation rules) — this spec only changes response shape/content type.

## Open questions
- **Breaking-change handling**: this changes both the content type and the body shape of a
  currently-200 response for two live endpoints. Assumed acceptable to ship as a breaking
  change with no compatibility shim, consistent with how the codebase treats other
  known-gap fixes (no mention of API versioning elsewhere in the repo) **[assumption]**.
  Confirm whether any existing consumer (mobile app, frontend) needs a migration window,
  a new endpoint/version instead of changing the existing one, or a release-notes call-out.
- **Success body shape beyond `message`**: assumed a single `message` field is sufficient
  since no other data changes are requested and the current behavior only ever returns
  text **[assumption]**. Confirm whether consumers need additional structured fields (e.g.
  `email`, or a `verified`/`success` boolean) — redundant with the 200 status code, so not
  included here unless there's a known consumer need.
- **Message content**: assumed the `message` field carries the exact same localized text
  the endpoints already produce (`UserVerified`, `VerificationCodeResent` resource
  strings), just wrapped in JSON, with no wording changes **[assumption]**.
