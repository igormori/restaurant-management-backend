# Uniform auth responses

## Problem
Attackers can determine whether an email address is registered — and, for verify/resend,
its verification state — by observing differences in the responses from
`/api/auth/login`, `/api/auth/verify`, and `/api/auth/resend-verification`. This spec
defines what these three endpoints must return so failure responses no longer disclose
account existence or state, while remaining usable for the legitimate account owner.

## Scope
- Module(s) affected: Identity (`SessionService.LoginAsync`, `VerificationService.VerifyEmailAsync`,
  `VerificationService.ResendVerificationCodeAsync`, and their actions on `AuthController`).
- Roles allowed: unauthenticated callers. These are pre-auth endpoints; no role is
  required and no role changes the behaviour described here.

## Behaviour
1. Login (`POST /api/auth/login`) returns the same status code and error message whether
   the email is not registered, the account exists but is not verified, or the account
   exists, is verified, and the password is wrong. (Account lockout remains a distinct
   response — see Open questions.)
2. Verify (`POST /api/auth/verify`) returns the same status code and error message
   whether the email is not registered or the email is registered but the submitted code
   does not match a currently valid (unused, unexpired) code for that account.
3. Resend (`POST /api/auth/resend-verification`) returns the same status code and
   message for: email not registered, email registered but already verified, and a
   request that hits the resend cooldown for a real account (see Open questions). In all
   these cases no email is sent, and nothing in the response lets the caller tell which
   case occurred.
4. The underlying checks are still enforced in every case — wrong passwords still fail,
   invalid codes still fail, already-verified accounts still can't re-verify — only the
   response the caller receives is unified. The system must not skip a check just because
   an earlier one would have produced the same outward response.
5. Server-side processing must not create an observable timing difference between
   "account does not exist" and "account exists but the credential/code was wrong" for
   the same failure response (e.g. the server must not skip an equivalent-cost operation,
   such as password verification, purely because no account was found).
6. Failed-login counting and account lockout continue to apply only to real accounts
   (existing, separate behaviour). The generic "wrong password" message from #1 is reused
   while an account is being counted toward lockout, so a caller cannot distinguish
   "counted toward lockout" from "email not registered."
7. Successful requests are unchanged: a valid login still returns tokens, a valid code
   still verifies the account, and a valid resend for an eligible account still issues
   and emails a new code.
8. The legitimate account owner is not harmed by unified responses: they already know
   whether they registered with a given email, so a generic "invalid email or password"
   (login) or "invalid email or code" (verify) response is still actionable for them —
   they retry the code or password — even though it no longer confirms which part failed.

## Data
No new fields. This changes response content for existing failure cases only:
- Login failure: one generic message + one status code, replacing today's three distinct
  outcomes — "user not found" (400), "not verified" (401), "wrong password" (401).
- Verify failure: one generic message + one status code, replacing "user not found" (400)
  and "invalid or expired code" (401).
- Resend non-actionable response: one generic message + one status code, replacing "user
  not found" (404) and "already verified" (400), and — per the assumption below —
  cooldown (429).

## Acceptance criteria
- Logging in with an unregistered email returns the same status code and message as
  logging in with a registered, verified account and the wrong password.
- Logging in with a registered, unverified account and the correct password returns the
  same status code and message as the two cases above.
- Verifying with an unregistered email returns the same status code and message as
  verifying a registered account with an invalid or expired code.
- Requesting a resend for an unregistered email returns the same status code and message
  as requesting a resend for an already-verified account.
- None of the three endpoints' failure responses contain wording that lets a caller infer
  which specific case occurred (e.g. no message distinguishes "not found" from "not
  verified" from "wrong password/code").
- A locked-out account still returns its existing, distinct lockout response (unchanged
  by this spec — see Open questions).
- A valid login with correct credentials for a verified account still succeeds and
  returns tokens.
- A valid, unused, unexpired code for a registered account still marks the account
  verified and returns a success response.
- A resend request for a registered, unverified account outside the cooldown window
  still issues a new code and sends the email.

## Out of scope
- Brute-force protection on `/api/auth/verify` (no attempt limit on code guesses) —
  tracked separately in `docs/follow-ups.md`.
- The missing unique-email database constraint / registration race condition — tracked
  separately in `docs/follow-ups.md`.
- Verify/resend returning bare strings instead of JSON — tracked separately in
  `docs/follow-ups.md`.
- Rate limiting or CAPTCHA on these endpoints as an anti-enumeration measure — this spec
  covers response content and timing only, not request throttling.
- Resend's email-send-failure response (500 `EmailSendingFailed`) — an infrastructure
  error unrelated to account existence; unchanged by this spec.
- `/api/auth/register`'s duplicate-email response — already covered by the existing
  `verify-email-on-registration` spec.
- Exact copy/wording of the new generic messages and any new localization resource keys —
  this spec requires the messages to be identical across the folded cases, not what they
  say (see Open questions).

## Open questions
- **Cooldown on resend**: a 429 from resend today reliably confirms the account exists
  and is unverified (an unregistered email can't be "in cooldown"). **Assumption: fold
  the cooldown case into the same generic response as the other resend failures**, so
  resend never reveals more than "if applicable, something may have been sent." Confirm
  this is the intended tradeoff — it removes today's explicit "please wait" signal for
  the legitimate user.
- **Account lockout (423)**: a locked account still returns a distinct status code and
  unlock time, which also confirms the account exists. This spec leaves that response
  untouched as a separate, pre-existing control, even though it's the same class of leak.
  Confirm whether lockout messaging should be folded into this fix or handled separately.
- **Verifying timing-safety**: requirement #5 says processing time must not distinguish
  the folded cases, but this spec doesn't mandate a specific technique or an automated
  timing test (network-level timing assertions are typically noisy in CI). Confirm
  whether a timing test is expected or whether "always do equivalent work" is sufficient
  without a measured assertion.
- **Status codes for folded responses**: this spec requires one status code per endpoint
  for the folded failure cases but doesn't mandate which (e.g. login could standardize on
  401; resend could return 200 with a generic informational message, which is the common
  pattern for enumeration-safe "forgot password"-style flows, or a 4xx). Confirm the
  intended codes per endpoint.
