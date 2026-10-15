# Session redesign: access pass revocation

**Found during:** docs/specs/identity/2026-10-15-refresh-tokens-own-table.md
**Date:** 2026-10-15

## Description
- Access tokens can't be revoked: password or role changes take effect only when the token expires.

## Why it matters
A deactivated user or one whose role was removed keeps access until the access token expires.

## Notes
Already done by docs/specs/identity/2026-10-15-refresh-tokens-own-table.md: separate refresh token table, logout, reuse detection with family revocation, several devices per user, direct lookup by fingerprint.
Remaining idea: `TokenVersion` on `User`, added to the JWT; increase it on password change, deactivation or role change; reject JWTs with an old version.
