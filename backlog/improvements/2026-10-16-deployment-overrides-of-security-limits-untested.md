# Deployment settings can change security limits without any test noticing

**Status:** proposed
**Severity:** medium
**Origin:** docs/security/identity-matrix.md · rows T16–T18
**Date:** 2026-10-16

## Problem
The shipped security limits (login and code lockouts, code expiry, the wait between emails, token
lifetimes, rate limits) are guarded by tests that read `appsettings.json`. In deployment, any
environment variable or Key Vault secret with the same name (for example
`Security__MaxFailedLoginAttempts=1000` or `AuthRateLimit__Login__PermitLimit=100000`) replaces
the value silently. Nothing checks the values the running app actually uses, so a typo or a
leftover test setting in a deployment could switch off a lockout or a rate limit.

## Evidence
- `tests/RestaurantManagement.Web.Tests/Identity/ShippedLimitsTests.cs` — reads only `src/RestaurantManagement.Web/appsettings.json`
- `src/RestaurantManagement.Web/Program.cs` — binds `Security`, `Jwt` and `AuthRateLimit` from the full configuration, which includes environment variables and Key Vault (`Hosting/KeyVaultHostingExtensions.cs`)
- `src/RestaurantManagement.Shared/Options/AuthRateLimitOptionsValidator.cs` — rejects only zero or negative limits, not unsafe ones; `SecurityOptions` has no validator

## Acceptance criteria
- [ ] The app refuses to start outside Development when a security limit is outside a safe range (ranges agreed with the user), naming the setting, for `Security`, `Jwt` lifetimes and `AuthRateLimit`
- [ ] Or, if ranges are not wanted: the app logs the effective security limits at startup (no secrets), so a deployment's values are visible
- [ ] Matrix rows cover the chosen behaviour, with tests

## Related
- `backlog/improvements/2026-10-16-mask-secrets-in-ci-test-reports.md`
