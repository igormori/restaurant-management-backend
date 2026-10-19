# Remove the unused IEmailService parameter from PasswordResetService

**Status:** proposed
**Severity:** low
**Origin:** docs/plans/identity/2026-10-17-forgot-password-same-response-time/review.md · second review, Watch out 6
**Date:** 2026-10-19

## Problem
"Forgot password" now only queues a request, and `PasswordResetEmailProcessor` sends the email.
`PasswordResetService` no longer uses its `IEmailService emailService` constructor parameter. It was
kept because tests approved at Gate 2 still construct the service with it, and tests are read-only.

## Evidence
- `src/RestaurantManagement.Modules.Identity/Services/PasswordResetService.cs:31` — unused `IEmailService emailService` parameter
- `tests/RestaurantManagement.Modules.Identity.Tests/PasswordResetServiceRequestTests.cs`, `PasswordResetServiceResetTests.cs` — build the service with the parameter

## Acceptance criteria
- [ ] `PasswordResetService` has no `IEmailService` parameter
- [ ] The tests that construct it are updated in the same change and pass

## Related
None
