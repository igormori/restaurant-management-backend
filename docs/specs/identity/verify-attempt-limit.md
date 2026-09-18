# Verify attempt limit

## Problem
`POST /api/auth/verify` checks an emailed 6-digit code against the account's latest
unused, unexpired verification credential, valid for a 15-minute window
(`SecurityOptions.VerificationCodeExpiryMinutes`, default 15). Nothing currently counts
or throttles repeated wrong guesses, so an attacker who knows (or guesses) a registered,
unverified email can submit up to 1,000,000 possible codes against `/api/auth/verify`
within that 15-minute window with no penalty, and eventually verify the account without
ever receiving the email.

## Scope
- Module affected: Identity. This is an addition to the existing verification flow
  (`VerificationService.VerifyEmailAsync`, and, for lockout enforcement only,
  `VerificationService.ResendVerificationCodeAsync`); it does not change how codes are
  generated or how the resend cooldown works.
- Roles allowed: not role-gated. Like the existing verify/resend endpoints, this applies
  to any caller who supplies an email and (for verify) a code — the account has no
  organization role yet at this stage. Not scoped by organization: verification is
  account-level, not tenant data, consistent with the existing verification flow.

## Behaviour
1. Each account has a running count of failed verification attempts, tracked separately
   from the login failed-attempt/lockout fields (`User.FailedAttempts` /
   `User.LockedUntil`) so that verification brute-force protection cannot accidentally
   lock a user out of logging in once verified **[assumption — see Open questions]**.
2. A verify call that does not result in the account becoming verified — wrong code, no
   active (unused, unexpired) code, or code already used/expired — increments that
   account's failed-attempt count by one **[assumption — see Open questions]**.
3. When the failed-attempt count reaches a configured maximum (default 5, mirroring
   `MaxFailedLoginAttempts`), the account's current verification credential (if any) is
   invalidated and the account enters a verification lockout for a configured duration
   (default 15 minutes, mirroring `LockoutDurationMinutes`).
4. While an account is in verification lockout, both `/api/auth/verify` and
   `/api/auth/resend-verification` reject requests for that account, even with an
   otherwise-correct code, until the lockout expires.
5. A verify call that successfully verifies the account (correct code, before the limit
   is reached) resets the failed-attempt count to zero.
6. A resend that succeeds (i.e. is not itself blocked by lockout or the existing resend
   cooldown) issues a new code and resets the failed-attempt count to zero for that new
   code.
7. The attempt limit is generous enough that a legitimate user who mistypes the code a
   couple of times can still verify successfully on a later correct attempt within the
   same code's lifetime.

## Data
Verification attempt tracking (per account):
- Failed attempt count — integer, starts at 0, increments on each failed verify call,
  resets to 0 on successful verification or on a new code being issued.
- Verification lockout expiry — optional timestamp; set when the failed-attempt count
  reaches the configured maximum; null when the account is not locked out.

Configuration (new, alongside existing `SecurityOptions`):
- Maximum verification attempts — integer, default 5.
- Verification lockout duration — duration in minutes, default 15.

No changes to existing verify/resend request shapes (`VerifyEmailRequest`:
`Email`, `Code`; `ResendVerificationRequest`: `Email`).

## Acceptance criteria
- A verify call with an incorrect code is rejected and increments the account's
  failed-attempt count.
- Once the failed-attempt count reaches the configured maximum, the current code is
  invalidated and the account enters lockout, even if the very next attempt would have
  used the correct code.
- A verify call for an account currently in lockout is rejected without checking the
  code at all, even if the code supplied is correct.
- A resend request for an account currently in lockout is rejected, regardless of the
  normal resend cooldown state.
- A verify call for an account that has never failed, or whose lockout has expired, is
  evaluated normally (not blocked).
- A user who enters the wrong code up to (maximum − 1) times and then the correct code
  within the same code's validity window is successfully verified, and the failed-attempt
  count is reset to zero.
- A successful verification always resets the failed-attempt count to zero, regardless of
  how many prior failures there were (as long as lockout was not yet triggered).
- A resend that succeeds outside of lockout resets the failed-attempt count to zero for
  the newly issued code.
- A valid, correct code submitted on the first attempt verifies the account as before,
  unaffected by this feature.
- After a lockout expires, the account can verify or resend normally again, starting from
  a failed-attempt count of zero.

## Out of scope
- Any lockout or throttling based on IP address, device, or request origin — this spec
  only covers per-account limiting, consistent with the existing login lockout's
  approach.
- Changing the resend cooldown (`ResendCooldownSeconds`) or how it is computed — that
  behavior is unchanged; lockout is an additional, separate restriction.
- Changing code length, generation method, or expiry window.
- Distinguishing, in the error response, between "wrong code" and "lockout" beyond what
  is needed for the caller to know to wait and/or request a new code — exact wording is
  an implementation detail, not a behavioral requirement of this spec.
- Notifying the user (e.g. by email) that their account was locked out due to repeated
  failed attempts.
- Any change to the login lockout flow (`User.FailedAttempts` / `User.LockedUntil`,
  `MaxFailedLoginAttempts`, `LockoutDurationMinutes`) — this spec introduces separate
  tracking for verification, not a shared mechanism.
- The account-existence disclosure gap tracked separately in `docs/follow-ups.md`
  (verify/resend returning distinct "user not found" vs "invalid/expired code" errors).

## Open questions
- **Separate tracking vs. reusing login lockout fields**: this spec assumes verification
  attempts must be tracked and locked out independently of the existing
  `User.FailedAttempts` / `User.LockedUntil` fields used by login, to avoid a burst of
  failed verify guesses (from an attacker who doesn't even know the code) locking a
  legitimate, already-verified-through-another-path user out of logging in. Confirm this
  separation is required, or whether sharing the fields is acceptable because an
  unverified user cannot log in anyway.
- **What counts as a failed attempt**: this spec counts any non-successful verify call
  (wrong code, no active code, expired code) as a failed attempt, to keep the rule simple
  and avoid leaking why a call failed. Confirm this is acceptable, versus only counting
  calls where an active code exists but the submitted code is wrong.
- **Blocking resend during lockout**: this spec blocks resend while an account is locked
  out, closing the loophole of an attacker repeatedly calling resend to get fresh 5-guess
  windows. Confirm this is the intended trade-off versus allowing resend (with its
  existing cooldown) to continue working during lockout, which would let a legitimate
  user recover faster but would weaken the brute-force protection.
- **Default values**: max attempts (5) and lockout duration (15 minutes) are proposed to
  mirror the existing login lockout configuration (`MaxFailedLoginAttempts`,
  `LockoutDurationMinutes`). Confirm these defaults, independent of the login values.
