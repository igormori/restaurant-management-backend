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
- `src/RestaurantManagement.Modules.Identity/Services/VerificationService.cs:139` — only use of `EmailSendingFailed`
- `src/RestaurantManagement.Modules.Identity/Services/VerificationService.cs:142` — only use of `VerificationCodeResent`
- `src/RestaurantManagement.Shared/Localization/SharedResource.resx` and `SharedResource.pt.resx` — both keys defined

## Acceptance criteria
- [ ] Neither key is defined in `SharedResource.resx` or `SharedResource.pt.resx`
- [ ] A search of `src/` and `tests/` finds no use of either key
- [ ] Build and full test suite pass

## Related
None
