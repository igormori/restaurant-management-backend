# Startup configuration tests fail intermittently in full runs

**Status:** proposed
**Severity:** medium
**Origin:** docs/plans/identity/2026-10-16-password-reset/review.md · Process notes 8
**Date:** 2026-10-16

## Problem
Some `StartupConfigurationTests` cases fail on a first full `dotnet test` run and pass on every
rerun, with no code change in between. A red suite that turns green on rerun hides real failures
and costs time on every task. The likely cause is environment or configuration shared between
tests that build the app (for example, settings or static state leaking between
`WebApplicationFactory` instances that run in parallel). Not confirmed yet.

## Evidence
- `tests/RestaurantManagement.Web.Tests/StartupConfigurationTests.cs:37-41` — each test builds an `AuthApiFactory` with its own environment and in-memory settings
- `Startup_JwtKeyMissingOrUnder32Bytes_FailsNamingJwtKey(key: "")` failed in one full run during B5/B6 of the password reset plan, then passed alone three times and in the next full run
- `Startup_VerificationHashKeyEmpty_FailsNamingHashKey` and `Startup_VerificationHashKeyEmptyInProduction_FailsNamingHashKey` failed in the pr-reviewer's first full run, then passed in a Web-only rerun and a second full run
- Branch `backlog-fixes/2026-10-16` (2026-10-16): `Startup_JwtKeyMissingOrUnder32Bytes_FailsNamingJwtKey` failed in one full run while dropping `User.SelectedOrganizationId` (7abb057), and 2 JWT startup cases failed in the first full run for the 72-byte password limit (05c5b61). Both passed on rerun with no change; neither commit touches startup configuration

## Acceptance criteria
- [ ] The cause of the intermittent failures is identified and fixed
- [ ] `StartupConfigurationTests` pass in 10 consecutive full `dotnet test` runs

## Related
None
