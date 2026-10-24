# Native review of the French (Canada) server messages

**Status:** proposed
**Severity:** medium
**Origin:** docs/reviews/feat-server-messages-in-french-canada.md · French catalog review
**Date:** 2026-10-23

## Problem
All 73 messages in `SharedResource.fr.resx` were written without a native speaker. Wording, tone and Quebec/Canadian terms (courriel, jeton, forfait, établissement) may need correction before French users see them.

## Evidence
- `src/RestaurantManagement.Shared/Localization/SharedResource.fr.resx` — 73 messages, none reviewed by a Canadian French native speaker

## Acceptance criteria
- [ ] A Canadian French native speaker has reviewed all 73 messages and the agreed corrections are applied
- [ ] The tests that name French texts still pass after the corrections

## Related
None
