# User.SelectedOrganizationId is shared across devices

**Found during:** docs/plans/identity/2026-10-15-refresh-tokens-own-table/plan.md
**Date:** 2026-10-15

## Description
After refresh tokens move to their own table, each token carries the organization chosen on that device, and refresh uses it. `User.SelectedOrganizationId` is still written by login and select-organization but no longer drives refresh, and it holds one value for all devices.

## Why it matters
A column that looks authoritative but isn't invites future code to read it and give a device the wrong organization. Remove it, or document what it's for.
