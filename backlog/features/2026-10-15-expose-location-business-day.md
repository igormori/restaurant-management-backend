# Expose a location's business day to other modules

**Found during:** docs/plans/organization/2026-10-15-organization-and-location-fields/plan.md
**Date:** 2026-10-15

## Description
The business day calculator added for the organization and location fields spec lives inside the Organization module and takes the time zone and cutoff as inputs. Modules that record sales or close cash (Phase 1) cannot reach it or read a location's time zone and the organization's cutoff, because modules never reference each other. A small Shared contract (for example, "business day for location X at moment Y") implemented by the Organization module and registered by the Web host is needed.

## Why it matters
Cash closing must group sales by the right business day in each country. Without a shared contract, each module would copy the rule or read Organization tables directly, which the architecture forbids. Adding a Shared contract is a high-risk change and should be its own slice before the first consumer.
