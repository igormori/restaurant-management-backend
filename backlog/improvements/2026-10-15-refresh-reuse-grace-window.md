# Grace window for parallel refreshes on one device

**Found during:** docs/plans/identity/2026-10-15-refresh-tokens-own-table/plan.md
**Date:** 2026-10-15

## Description
With rotation and reuse detection, two browser tabs on one device that refresh at the same moment send the same token. The second request counts as reuse, so the whole family is revoked and the user is signed out.

## Why it matters
Staff on shared tablets may get unexpected sign-outs. Options: a short grace period where the just-rotated token returns the same replacement, or coordinating refresh on the client.
