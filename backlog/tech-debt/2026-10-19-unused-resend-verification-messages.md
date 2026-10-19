# Unused resend verification messages left in the shared resources

**Status:** proposed
**Severity:** low
**Origin:** docs/plans/identity/2026-10-19-resend-verification-same-response/plan.md · T3
**Date:** 2026-10-19

## Problem
After resend verification always returns `VerificationResendAcknowledged`, the messages
`VerificationCodeResent` and `EmailSendingFailed` are no longer used by any code, but stay in the
English and Portuguese resource files of the Shared project.

## Evidence
- `src/RestaurantManagement.Shared/Localization/SharedResource.resx:81` and `:120` — `VerificationCodeResent` and `EmailSendingFailed` defined
- `src/RestaurantManagement.Shared/Localization/SharedResource.pt.resx:81` and `:120` — same two keys defined
- `tests/RestaurantManagement.Modules.Identity.Tests/AuthControllerTests.cs:47` — the mock still returns `"VerificationCodeResent"`

## Acceptance criteria
- [ ] Neither key is defined in `SharedResource.resx` or `SharedResource.pt.resx`
- [ ] A search of `src/` and `tests/` finds no use of either key
- [ ] The mock string in `AuthControllerTests.cs:47` changes too; this is a test edit that needs the user's approval
- [ ] Build and full test suite pass

## Related
None
