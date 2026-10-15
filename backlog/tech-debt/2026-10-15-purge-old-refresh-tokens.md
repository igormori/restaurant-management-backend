# Purge expired and revoked refresh tokens

**Found during:** docs/plans/identity/2026-10-15-refresh-tokens-own-table/plan.md
**Date:** 2026-10-15

## Description
Every sign-in and every refresh adds a row to `refresh_tokens`. Nothing ever deletes expired or revoked rows, except when the user is deleted.

## Why it matters
The table grows without limit, which slows lookups and keeps device and IP (Internet Protocol) address data longer than needed. A scheduled cleanup should remove families once they have been fully expired or revoked for a retention period.
