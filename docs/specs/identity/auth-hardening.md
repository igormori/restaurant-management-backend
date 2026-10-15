# Auth hardening

## Summary
Seven small fixes that close security gaps in authentication and its supporting configuration. No new endpoints; one response change (locked accounts) and configuration changes.

## Rules
1. **Sentry configuration**
   - `Debug` is on only in Development.
   - `TracesSampleRate` is read from configuration (`Sentry:TracesSampleRate`); default 1.0 in Development, 0.1 elsewhere.
2. **No emails in Sentry**
   - The account-lockout Sentry message includes the user ID, never the email.
3. **MailKit**
   - Upgrade MailKit to the latest version with no known vulnerability advisory; email sending keeps working.
4. **HTTPS between Next.js and the API**
   - Outside Development, the API listens on HTTPS only, on the private network; the certificate is supplied through configuration at deploy time, never committed.
   - The existing `UseHttpsRedirection()` stays.
   - No HSTS in the API: only Next.js calls it, and HSTS only affects browsers. HTTPS and HSTS toward the browser are Next.js's responsibility.
5. **JWT key validation at startup**
   - The app fails to start if `Jwt:Key` is missing or shorter than 32 bytes (UTF-8).
   - Outside Development, the app fails to start if `Jwt:Key` equals the placeholder value in `appsettings.json`.
6. **Same response for locked accounts**
   - Login on a locked account returns the same 401 `InvalidEmailOrPassword` response as any other failed login, instead of 423 with an unlock time.
   - Lockout itself (counting, duration, unlock) is unchanged.
7. **Shorter access tokens**
   - Default `Jwt:ExpireMinutes` changes from 60 to 15. Refresh token lifetime is unchanged.

## Data
Configuration only, no API change except the locked-account response.
| Field | Type | Required | Validation |
|---|---|---|---|
| Sentry:TracesSampleRate | number | No | 0.0 to 1.0; default per Rule 1 |
| Jwt:Key | string | Yes | At least 32 bytes; not the placeholder outside Development |
| Jwt:ExpireMinutes | int | No | Greater than 0; default 15 |

## Acceptance criteria
- Startup fails when `Jwt:Key` is missing or shorter than 32 bytes.
- Startup fails outside Development when `Jwt:Key` is the placeholder.
- Startup fails when `Sentry:TracesSampleRate` is outside 0.0–1.0.
- Login on a locked account returns 401 with the `InvalidEmailOrPassword` message, identical to a wrong password.
- A locked account still cannot log in until `LockedUntil` passes, even with the correct password.
- The lockout Sentry message contains the user ID and no email address.
- Sentry `Debug` is off outside Development.
- Outside Development, the API accepts HTTPS connections and does not serve plain HTTP.
- Startup fails outside Development when no HTTPS certificate is configured.
- Newly issued access tokens expire after 15 minutes by default.
- MailKit has no known vulnerability advisory, and the existing email tests pass.
- Build and all existing tests pass.

## Out of scope
- Logout, session revocation, refresh token redesign.
- Forgot/change password.
- Notifying users by email that their account was locked.
- Rotating the JWT signing key.

## Assumptions
- A locked user is not told their account is locked; they see the generic login error until the lock expires.
- The `AccountLockedUntil` localization entry is removed once nothing uses it.
- The certificate comes from an internal CA (or is self-signed) and is trusted by the Next.js server; issuing and trusting it is deployment work, not code.
- The placeholder check compares against the exact string in `appsettings.json`.
- 15-minute tokens are acceptable for the frontend because it refreshes automatically.

## Deployment
- The API is reachable only from Next.js over a private network, using HTTPS.
- Next.js serves HTTPS (with HSTS) to the browser.
