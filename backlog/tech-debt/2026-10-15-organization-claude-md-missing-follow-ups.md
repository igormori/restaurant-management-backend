# Organization module CLAUDE.md points to a missing follow-ups file

**Found during:** docs/plans/organization/2026-10-15-organization-and-location-fields/plan.md
**Date:** 2026-10-15

## Description
`src/RestaurantManagement.Modules.Organization/CLAUDE.md` says accepted gaps are tracked in `docs/follow-ups.md`, but that file does not exist. Out-of-scope findings now go to `backlog/`. The module file also still says "no spec exists yet for this module", which is no longer true.

## Why it matters
Agents read this file before every Organization change. A dead reference sends them looking for a list that isn't there, and gaps may be recorded in the wrong place.
