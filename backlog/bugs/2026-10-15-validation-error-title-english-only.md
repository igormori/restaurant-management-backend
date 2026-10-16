# Validation error response title is always English

**Found during:** docs/plans/shared/2026-10-15-localize-client-messages/plan.md
**Date:** 2026-10-15

## Description
Invalid requests return the platform's default validation response, whose title is always "One or more validation errors occurred." regardless of the requested language. The catalog already has an unused `ValidationFailed` message in en and pt that was likely meant for this.

## Why it matters
Brazilian users still see one English sentence in every validation error, even after the field errors are translated. Changing the response title is a response-contract change for all endpoints, so it needs its own decision.
