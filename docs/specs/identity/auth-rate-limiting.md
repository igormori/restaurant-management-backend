# Auth rate limiting

## Summary
Anonymous callers of the Identity auth endpoints (login, register, refresh, verify, resend-verification) are limited per client IP. This blunts brute force, credential stuffing, and email abuse, and complements the existing per-account lockout, which stays unchanged.

## Rules
- Roles: applies to every caller of these five endpoints, authenticated or not.
- Tenancy: not organization-scoped; limits are per client IP and per endpoint, independent of each other.
- Exceeding a limit returns 429 Too Many Requests with a Retry-After header (seconds until allowed).
- The 429 body is `{ "message": "..." }`, localized like other errors, in every supported language.
- Fixed window per endpoint; chosen for simple, predictable Retry-After values.
- Limits (requests per window) are configurable in appsettings; defaults below; invalid values fail startup.
- Rate limiting is always on in every environment, including local development; there is no setting to disable it.
- Client IP is the connection's remote IP, or X-Forwarded-For only when the sender is a configured trusted proxy/network.
- Requests rejected by rate limiting never reach login logic, so they do not count toward account lockout.
- Rejections are reported to Sentry as a warning, once per client IP per endpoint per window (the first rejection), so an attack doesn't flood Sentry. The event includes the endpoint and client IP, never request bodies, emails or tokens.

## Data
Configuration only, no API change. Each endpoint has a permit limit and a window.
| Field | Type | Required | Validation |
|---|---|---|---|
| Login | limit + window | Yes | Default 10 per 1 minute |
| Register | limit + window | Yes | Default 5 per 1 hour |
| ResendVerification | limit + window | Yes | Default 3 per 10 minutes |
| Verify | limit + window | Yes | Default 10 per 10 minutes |
| Refresh | limit + window | Yes | Default 30 per 1 minute |
| TrustedProxies | list of IPs/networks | No | Valid IP or CIDR; empty means trust none |

## Acceptance criteria
- The request after the limit is reached on an endpoint is rejected with 429 and a Retry-After header.
- The 429 body has a localized `message` matching the request language.
- A limited IP on one endpoint can still call the other endpoints.
- A different client IP is unaffected by another IP's exhaustion.
- X-Forwarded-For from an untrusted sender is ignored; the remote IP is used.
- X-Forwarded-For from a trusted proxy partitions by the forwarded client IP.
- Non-positive limits, windows, or malformed proxy entries fail startup validation.
- Rate-limited requests do not increment failed login attempts or change lockout state.
- The first rejection for an IP on an endpoint within a window sends one Sentry warning with the endpoint and client IP; further rejections in the same window send none.
- The Sentry event contains no request body, email or token.
- Requests within the limit behave exactly as before.
- Changing a limit in configuration takes effect without code changes.

## Out of scope
- Distributed or Redis-backed limits across multiple instances.
- Per-account or per-email limits.
- Rate limiting non-auth endpoints.
- CAPTCHA or other challenge mechanisms.
- Any change to lockout thresholds or duration.

## Assumptions
- Fixed window, in-memory, per instance; limits reset on restart.
- Retry-After is in seconds, not an HTTP date.
- Default trusted proxies is empty, so forwarded headers are ignored until configured.
- Without configured proxies, all traffic behind the planned Next.js BFF shares one bucket per endpoint.
- The 429 message is added to every existing localization file.

## Deployment
- The API is not exposed to the internet; only the Next.js BFF can reach it.
- Next.js has a fixed IP address, which is set in TrustedProxies at deploy time.
- Next.js sets X-Forwarded-For to the browser's IP on every request to the API.